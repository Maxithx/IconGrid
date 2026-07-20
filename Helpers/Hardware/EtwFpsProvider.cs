using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Measures FPS via ETW (Event Tracing for Windows).
/// Same approach as FPS Overlay project.
/// Uses its own ETW session name, aligned with the FPS Overlay approach.
/// </summary>
internal sealed class EtwFpsProvider : IDisposable
{
    private const string SessionName = "FPSOverlay_ETW_IconGrid";
    private const string ProbeSessionNamePrefix = "FPSOverlay_ETW_IconGrid_UIProbe_";
    private const uint EventTraceControlStop = 1;
    private const uint ErrorAlreadyExists = 183;
    private const ulong DxgKrnlKeywordPresent = 0x8000000;
    private const ulong DxgKrnlKeywordBase = 0x1;
    private const ushort DxgiPresentStartEventId = 42;
    private const ushort D3D9PresentStartEventId = 1;
    private const ushort DxgKrnlPresentInfoEventId = 0x00B8;
    private const ushort DxgKrnlFlipInfoEventId = 0x00A8;
    private const ushort DxgKrnlBlitInfoEventId = 0x00A6;
    private const uint ProcessTraceModeRealTime = 0x00000100;
    private const uint ProcessTraceModeEventRecord = 0x10000000;
    private const uint EventControlCodeEnableProvider = 1;
    private const byte TraceLevelInformation = 4;
    private const uint EventTraceRealTimeMode = 0x00000100;
    private const uint WnodeFlagTracedGuid = 0x00020000;
    private const uint InvalidProcessId = 0;
    private const ulong InvalidTraceHandle = ulong.MaxValue;
    private const int PollIntervalMs = 1000;

    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3D9Provider = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");
    private static readonly Guid DxgKrnlProvider = new("802EC45A-1E99-4B83-9920-87C98277BA9D");

    // Known game process names (lowercase, no .exe)
    private static readonly HashSet<string> KnownGameNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pathofexile", "pathofexile_x64", "pathofexile_x64steam",
        "pathofexilesteam", "pathofexile_x64egs",
        "yuzu", "yuzu early access", "ryujinx", "rpcs3",
    };

    private readonly object _sync = new();
    private readonly System.Threading.Timer _targetPollTimer;
    private readonly EventRecordCallback _eventRecordCallback;
    private readonly double _qpcFrequency;
    private readonly Action<string>? _log;
    private readonly List<string> _trackedExeNames;
    private readonly int _currentProcessId = Environment.ProcessId;

    private bool _disposed;
    private bool _isRunning;
    private ulong _sessionHandle;
    private ulong _traceHandle;
    private Thread? _processTraceThread;
    private uint _targetProcessId;
    private string _currentFpsStatus = "--";
    private string _currentProcessName = "";
    private uint _lastPid;
    private double _windowStartSeconds;
    private int _dxgiCount;
    private int _d3d9Count;
    private int _dxgKrnlCount;
    private DateTime _lastNoDataLogUtc = DateTime.MinValue;
    private DateTime _lastProviderEventLogUtc = DateTime.MinValue;
    private int _rawDxgiEvents;
    private int _rawD3d9Events;
    private int _rawDxgKrnlEvents;

    // Secondary: GPU Performance Counter fallback
    private bool _gpuCounterAvailable;
    private PerformanceCounter? _gpu3DCounter;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void EventRecordCallback([In] ref EventRecord eventRecord);

    [DllImport("advapi32.dll", CharSet = CharSet.Ansi)]
    private static extern uint StartTrace(out ulong sessionHandle, string sessionName, IntPtr properties);

    [DllImport("advapi32.dll", EntryPoint = "ControlTraceA", CharSet = CharSet.Ansi)]
    private static extern uint ControlTrace(ulong sessionHandle, string? sessionName, IntPtr properties, uint controlCode);

    [DllImport("advapi32.dll")]
    private static extern uint EnableTraceEx2(
        ulong traceHandle, [In] ref Guid providerId, uint controlCode, byte level,
        ulong matchAnyKeyword, ulong matchAllKeyword, uint timeout, IntPtr enableParameters);

    [DllImport("advapi32.dll", CharSet = CharSet.Ansi)]
    private static extern ulong OpenTrace([In, Out] ref EventTraceLogfile logFile);

    [DllImport("advapi32.dll")]
    private static extern uint ProcessTrace([In] ulong[] handleArray, uint handleCount, IntPtr startTime, IntPtr endTime);

    [DllImport("advapi32.dll")]
    private static extern uint CloseTrace(ulong traceHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out SafeFileHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(SafeHandle tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

    public EtwFpsProvider(IEnumerable<string>? additionalExeNames = null, Action<string>? log = null)
    {
        _qpcFrequency = Stopwatch.Frequency;
        _eventRecordCallback = HandleEventRecord;
        _log = log;

        _trackedExeNames = new List<string>(KnownGameNames);
        if (additionalExeNames != null)
        {
            foreach (var name in additionalExeNames)
            {
                var clean = name.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
                if (!_trackedExeNames.Contains(clean))
                    _trackedExeNames.Add(clean);
            }
        }

        _log?.Invoke($"[EtwFpsProvider] Tracking {_trackedExeNames.Count} names: {string.Join(",", _trackedExeNames)}");

        // Try GPU Performance Counter as quick fallback
        try
        {
            if (PerformanceCounterCategory.Exists("GPU Engine"))
            {
                var cat = new PerformanceCounterCategory("GPU Engine");
                foreach (var inst in cat.GetInstanceNames())
                {
                    if (inst.Contains("3D", StringComparison.OrdinalIgnoreCase) &&
                        (inst.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                         !inst.Contains("Intel", StringComparison.OrdinalIgnoreCase)))
                    {
                        var counters = cat.GetCounters(inst);
                        foreach (var c in counters)
                        {
                            if (c.CounterName.Contains("Utilization", StringComparison.OrdinalIgnoreCase))
                            {
                                _gpu3DCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst);
                                _gpuCounterAvailable = true;
                                _log?.Invoke($"[EtwFpsProvider] GPU counter: {inst}\\{c.CounterName}");
                                break;
                            }
                        }
                        if (_gpuCounterAvailable) break;
                    }
                }
            }
        }
        catch { }

        StartSession();
        _targetPollTimer = new System.Threading.Timer(PollTargetProcess, null, PollIntervalMs, PollIntervalMs);
    }

    public static string RunStartupProbe(Action<string>? log = null)
    {
        var lines = new List<string>();
        var sessionName = ProbeSessionNamePrefix + Environment.ProcessId;

        void Write(string message)
        {
            lines.Add(message);
            log?.Invoke(message);
        }

        Write($"[EtwProbe] PID={Environment.ProcessId} Elevated={IsCurrentProcessElevated()} Integrity={GetCurrentIntegrityLevel()} SessionName={sessionName}");

        var size = Marshal.SizeOf<EventTraceProperties>() + 256;
        var buffer = Marshal.AllocHGlobal(size);
        ulong sessionHandle = 0;
        ulong traceHandle = InvalidTraceHandle;

        try
        {
            InitializeSessionProperties(buffer, size, sessionName);
            try
            {
                var stopHr = ControlTrace(0, sessionName, buffer, EventTraceControlStop);
                Write($"[EtwProbe] ControlTrace(stop-existing) => {stopHr}");
            }
            catch (Exception ex)
            {
                Write($"[EtwProbe] ControlTrace(stop-existing) threw: {ex.Message}");
            }

            InitializeSessionProperties(buffer, size, sessionName);
            var startHr = StartTrace(out sessionHandle, sessionName, buffer);
            Write($"[EtwProbe] StartTrace => {startHr}");
            if (startHr != 0)
            {
                return string.Join(Environment.NewLine, lines);
            }

            var dxgKrnlProvider = DxgKrnlProvider;
            var dxgKrnlHr = EnableTraceEx2(sessionHandle, ref dxgKrnlProvider, EventControlCodeEnableProvider, TraceLevelInformation,
                DxgKrnlKeywordPresent | DxgKrnlKeywordBase, 0, 0, IntPtr.Zero);
            Write($"[EtwProbe] EnableTraceEx2(DxgKrnl) => {dxgKrnlHr}");

            var dxgiProvider = DxgiProvider;
            var dxgiHr = EnableTraceEx2(sessionHandle, ref dxgiProvider, EventControlCodeEnableProvider, TraceLevelInformation, 0, 0, 0, IntPtr.Zero);
            Write($"[EtwProbe] EnableTraceEx2(DXGI) => {dxgiHr}");

            var d3d9Provider = D3D9Provider;
            var d3d9Hr = EnableTraceEx2(sessionHandle, ref d3d9Provider, EventControlCodeEnableProvider, TraceLevelInformation, 0, 0, 0, IntPtr.Zero);
            Write($"[EtwProbe] EnableTraceEx2(D3D9) => {d3d9Hr}");

            var logFile = new EventTraceLogfile
            {
                LoggerName = sessionName,
                ProcessTraceMode = ProcessTraceModeRealTime | ProcessTraceModeEventRecord
            };

            traceHandle = OpenTrace(ref logFile);
            Write($"[EtwProbe] OpenTrace => {(traceHandle == InvalidTraceHandle ? "INVALID" : traceHandle.ToString())}");

            return string.Join(Environment.NewLine, lines);
        }
        finally
        {
            if (traceHandle != 0 && traceHandle != InvalidTraceHandle)
            {
                CloseTrace(traceHandle);
            }

            if (sessionHandle != 0)
            {
                InitializeSessionProperties(buffer, size, sessionName);
                try
                {
                    var stopHr = ControlTrace(sessionHandle, sessionName, buffer, EventTraceControlStop);
                    Write($"[EtwProbe] ControlTrace(stop-own) => {stopHr}");
                }
                catch (Exception ex)
                {
                    Write($"[EtwProbe] ControlTrace(stop-own) threw: {ex.Message}");
                }
            }

            Marshal.FreeHGlobal(buffer);
        }
    }

    public string GetCurrentFpsStatus()
    {
        lock (_sync) return _currentFpsStatus;
    }

    public string GetCurrentProcessName()
    {
        lock (_sync) return _currentProcessName;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _targetPollTimer.Dispose();
        StopSession();
        _gpu3DCounter?.Dispose();
    }

    private void PollTargetProcess(object? state)
    {
        if (_disposed) return;

        try
        {
            var hwnd = GetForegroundWindow();
            uint currentPid = InvalidProcessId;
            if (hwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(hwnd, out currentPid);
            }

            string processName = "";
            if (currentPid != InvalidProcessId && currentPid != _currentProcessId)
            {
                try
                {
                    using var process = Process.GetProcessById((int)currentPid);
                    if (!process.HasExited)
                    {
                        processName = process.ProcessName;
                    }
                }
                catch
                {
                    currentPid = InvalidProcessId;
                }
            }

            lock (_sync)
            {
                if (currentPid == _targetProcessId)
                    return;

                _targetProcessId = currentPid;
                _currentProcessName = processName;
                _lastPid = 0;
                _dxgiCount = _d3d9Count = _dxgKrnlCount = 0;
                _rawDxgiEvents = _rawD3d9Events = _rawDxgKrnlEvents = 0;
                _windowStartSeconds = 0;
                _currentFpsStatus = "--";
                _lastNoDataLogUtc = DateTime.MinValue;
                _lastProviderEventLogUtc = DateTime.MinValue;
            }

            if (currentPid != InvalidProcessId)
            {
                _log?.Invoke($"[EtwFpsProvider] NOW TRACKING FG PID={currentPid} Name={processName}");
            }
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private void StartSession()
    {
        var size = Marshal.SizeOf<EventTraceProperties>() + 256;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            InitializeSessionProperties(buffer, size);

            try { ControlTrace(0, SessionName, buffer, EventTraceControlStop); } catch { }

            var hr = StartTrace(out _sessionHandle, SessionName, buffer);
            if (hr == ErrorAlreadyExists)
            {
                _log?.Invoke("[EtwFpsProvider] Session already exists, forcing stop and retry.");
                try { ControlTrace(0, SessionName, buffer, EventTraceControlStop); } catch { }
                InitializeSessionProperties(buffer, size);
                hr = StartTrace(out _sessionHandle, SessionName, buffer);
            }

            if (hr != 0)
            {
                _log?.Invoke($"[EtwFpsProvider] StartTrace failed: {hr}");
                return;
            }

            var d = DxgiProvider;
            var dxgi = EnableTraceEx2(_sessionHandle, ref d, EventControlCodeEnableProvider, TraceLevelInformation, 0, 0, 0, IntPtr.Zero);
            if (dxgi != 0) _log?.Invoke($"[EtwFpsProvider] DXGI enable failed: {dxgi}");
            else _log?.Invoke("[EtwFpsProvider] DXGI enable OK");

            var d3 = D3D9Provider;
            var d3d9 = EnableTraceEx2(_sessionHandle, ref d3, EventControlCodeEnableProvider, TraceLevelInformation, 0, 0, 0, IntPtr.Zero);
            if (d3d9 != 0) _log?.Invoke($"[EtwFpsProvider] D3D9 enable failed: {d3d9}");
            else _log?.Invoke("[EtwFpsProvider] D3D9 enable OK");

            var dx = DxgKrnlProvider;
            var krnl = EnableTraceEx2(_sessionHandle, ref dx, EventControlCodeEnableProvider, TraceLevelInformation,
                DxgKrnlKeywordPresent | DxgKrnlKeywordBase, 0, 0, IntPtr.Zero);
            if (krnl != 0) _log?.Invoke($"[EtwFpsProvider] DxgKrnl enable failed: {krnl}");
            else _log?.Invoke("[EtwFpsProvider] DxgKrnl enable OK");

            var logFile = new EventTraceLogfile
            {
                LoggerName = SessionName,
                ProcessTraceMode = ProcessTraceModeRealTime | ProcessTraceModeEventRecord,
                EventRecordCallback = _eventRecordCallback
            };

            _traceHandle = OpenTrace(ref logFile);
            if (_traceHandle == InvalidTraceHandle)
            {
                _log?.Invoke("[EtwFpsProvider] OpenTrace failed");
                return;
            }

            _isRunning = true;
            _processTraceThread = new Thread(() =>
            {
                try { ProcessTrace(new[] { _traceHandle }, 1, IntPtr.Zero, IntPtr.Zero); }
                catch { }
            })
            {
                IsBackground = true,
                Name = "IconGrid ETW"
            };
            _processTraceThread.Start();
            _log?.Invoke("[EtwFpsProvider] ETW session OK");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void InitializeSessionProperties(IntPtr buffer, int size)
    {
        InitializeSessionProperties(buffer, size, SessionName);
    }

    private static void InitializeSessionProperties(IntPtr buffer, int size, string sessionName)
    {
        var zero = new byte[size];
        Marshal.Copy(zero, 0, buffer, size);

        var props = new EventTraceProperties
        {
            Wnode = new WnodeHeader
            {
                BufferSize = (uint)size,
                Flags = WnodeFlagTracedGuid,
                ClientContext = 1
            },
            LogFileMode = EventTraceRealTimeMode,
            LoggerNameOffset = (uint)Marshal.SizeOf<EventTraceProperties>()
        };

        Marshal.StructureToPtr(props, buffer, false);
        var namePtr = IntPtr.Add(buffer, (int)props.LoggerNameOffset);
        Marshal.Copy(Encoding.ASCII.GetBytes(sessionName + "\0"), 0, namePtr, sessionName.Length + 1);
    }

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            return GetCurrentIntegrityLevel() is "High" or "System";
        }
        catch
        {
            return false;
        }
    }

    private static string GetCurrentIntegrityLevel()
    {
        const uint tokenQuery = 0x0008;
        const int tokenIntegrityLevel = 25;

        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, tokenQuery, out var tokenHandle))
        {
            return $"Unknown(OpenProcessToken:{Marshal.GetLastWin32Error()})";
        }

        using (tokenHandle)
        {
            GetTokenInformation(tokenHandle, tokenIntegrityLevel, IntPtr.Zero, 0, out var size);
            if (size <= 0)
            {
                return "Unknown";
            }

            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(tokenHandle, tokenIntegrityLevel, buffer, size, out _))
                {
                    return $"Unknown(GetTokenInformation:{Marshal.GetLastWin32Error()})";
                }

                var tokenLabel = Marshal.PtrToStructure<TOKEN_MANDATORY_LABEL>(buffer);
                if (tokenLabel.Label.Sid == IntPtr.Zero)
                {
                    return "Unknown";
                }

                var sid = tokenLabel.Label.Sid;
                var sidCount = Marshal.ReadByte(sid, 1);
                if (sidCount <= 0)
                {
                    return "Unknown";
                }

                var subAuthority = Marshal.ReadInt32(sid, 8 + ((sidCount - 1) * 4));

                return subAuthority switch
                {
                    >= 0x00004000 => "System",
                    >= 0x00003000 => "High",
                    >= 0x00002000 => "Medium",
                    >= 0x00001000 => "Low",
                    _ => $"RID:{subAuthority}"
                };
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private void StopSession()
    {
        _isRunning = false;
        if (_traceHandle != 0 && _traceHandle != InvalidTraceHandle)
            CloseTrace(_traceHandle);
        if (_processTraceThread?.IsAlive == true)
            _processTraceThread.Join(500);

        if (_sessionHandle != 0)
        {
            var size = Marshal.SizeOf<EventTraceProperties>() + 256;
            var buf = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(new EventTraceProperties
            {
                Wnode = new WnodeHeader { BufferSize = (uint)size },
                LoggerNameOffset = (uint)Marshal.SizeOf<EventTraceProperties>()
            }, buf, false);
            var np = IntPtr.Add(buf, Marshal.SizeOf<EventTraceProperties>());
            Marshal.Copy(Encoding.ASCII.GetBytes(SessionName + "\0"), 0, np, SessionName.Length + 1);
            try { ControlTrace(_sessionHandle, SessionName, buf, EventTraceControlStop); } catch { }
            Marshal.FreeHGlobal(buf);
            _sessionHandle = 0;
        }
    }

    private void HandleEventRecord(ref EventRecord eventRecord)
    {
        if (!_isRunning) return;

        lock (_sync)
        {
            if (_targetProcessId == InvalidProcessId || eventRecord.EventHeader.ProcessId != _targetProcessId)
                return;

            var pid = eventRecord.EventHeader.ProcessId;
            var providerId = eventRecord.EventHeader.ProviderId;
            var eventId = eventRecord.EventHeader.EventDescriptor.Id;

            var isDxgi = providerId == DxgiProvider && eventId == DxgiPresentStartEventId;
            var isD3D9 = providerId == D3D9Provider && eventId == D3D9PresentStartEventId;
            var isDxgKrnl = providerId == DxgKrnlProvider &&
                (eventId == DxgKrnlPresentInfoEventId || eventId == DxgKrnlFlipInfoEventId || eventId == DxgKrnlBlitInfoEventId);

            if (!isDxgi && !isD3D9 && !isDxgKrnl) return;

            if (isDxgi) _rawDxgiEvents++;
            if (isD3D9) _rawD3d9Events++;
            if (isDxgKrnl) _rawDxgKrnlEvents++;

            var ts = eventRecord.EventHeader.TimeStamp / _qpcFrequency;

            if (_lastPid != pid)
            {
                _lastPid = pid;
                _dxgiCount = _d3d9Count = _dxgKrnlCount = 0;
                _windowStartSeconds = ts;
                return;
            }

            if (isDxgi) _dxgiCount++;
            if (isD3D9) _d3d9Count++;
            if (isDxgKrnl) _dxgKrnlCount++;

            var providerLogNow = DateTime.UtcNow;
            if ((providerLogNow - _lastProviderEventLogUtc).TotalSeconds >= 5)
            {
                _log?.Invoke($"[EtwFpsProvider] Event flow PID={pid} DXGI(raw={_rawDxgiEvents},win={_dxgiCount}) D3D9(raw={_rawD3d9Events},win={_d3d9Count}) DxgKrnl(raw={_rawDxgKrnlEvents},win={_dxgKrnlCount}) eventId={eventId}");
                _lastProviderEventLogUtc = providerLogNow;
            }

            var elapsed = ts - _windowStartSeconds;
            if (elapsed < 1.0) return;

            int frames = 0;
            if (_d3d9Count > 0) frames = _d3d9Count;
            else if (_dxgiCount > 0) frames = _dxgiCount;
            else if (_dxgKrnlCount > 0 && _dxgKrnlCount / elapsed >= 20) frames = _dxgKrnlCount;

            if (frames > 0)
            {
                _currentFpsStatus = Math.Round(frames / elapsed).ToString("F0");
            }
            else
            {
                _currentFpsStatus = "--";
                var nowUtc = DateTime.UtcNow;
                if ((nowUtc - _lastNoDataLogUtc).TotalSeconds >= 5)
                {
                    _log?.Invoke($"[EtwFpsProvider] No FPS events for PID={pid}. DXGI={_dxgiCount} D3D9={_d3d9Count} DxgKrnl={_dxgKrnlCount} elapsed={elapsed:F2}s");
                    _lastNoDataLogUtc = nowUtc;
                }
            }

            _dxgiCount = _d3d9Count = _dxgKrnlCount = 0;
            _windowStartSeconds = ts;

            // Also measure GPU utilization as sanity check
            if (_gpuCounterAvailable && _gpu3DCounter != null)
            {
                try { _gpu3DCounter.NextValue(); } catch { }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WnodeHeader { public uint BufferSize, ProviderId; public ulong HistoricalContext; public long TimeStamp; public Guid Guid; public uint ClientContext, Flags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTraceProperties { public WnodeHeader Wnode; public uint BufferSize, MinimumBuffers, MaximumBuffers, MaximumFileSize, LogFileMode, FlushTimer, EnableFlags; public int AgeLimit; public uint NumberOfBuffers, FreeBuffers, EventsLost, BuffersWritten, LogBuffersLost, RealTimeBuffersLost; public IntPtr LoggerThreadId; public uint LogFileNameOffset, LoggerNameOffset; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct EventTraceLogfile { public string? LogFileName, LoggerName; public long CurrentTime; public uint BuffersRead, ProcessTraceMode; public EventTrace CurrentEvent; public TraceLogfileHeader LogfileHeader; public IntPtr BufferCallback, BufferSize2, Filled, EventsLost2; public EventRecordCallback? EventRecordCallback; public uint IsKernelTrace; public IntPtr Context; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTrace { public EventTraceHeader Header; public uint InstanceId, ParentInstanceId; public Guid ParentGuid; public IntPtr MofData; public uint MofLength, ClientContext; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTraceHeader { public ushort Size, FieldTypeFlags; public uint VersionThreadId, ProcessId; public long TimeStamp; public Guid Guid; public uint KernelTime, UserTime; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TraceLogfileHeader { public uint BufferSize, Version, ProviderVersion, NumberOfProcessors; public long EndTime; public uint TimerResolution, MaximumFileSize, LogFileMode, BuffersWritten; public Guid LogInstanceGuid; public string? LoggerName, LogFileName; public TimeZoneInformation TimeZone; public long BootTime, PerfFreq, StartTime; public uint ReservedFlags, BuffersLost; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TimeZoneInformation { public int Bias; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string StandardName; public System.Runtime.InteropServices.ComTypes.FILETIME StandardDate; public int StandardBias; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DaylightName; public System.Runtime.InteropServices.ComTypes.FILETIME DaylightDate; public int DaylightBias; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventRecord { public EventHeader EventHeader; public EtwBufferContext BufferContext; public ushort ExtendedDataCount, UserDataLength; public IntPtr ExtendedData, UserData, UserContext; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHeader { public ushort Size, HeaderType, Flags, EventProperty; public uint ThreadId, ProcessId; public long TimeStamp; public Guid ProviderId; public EventDescriptor EventDescriptor; public ulong ProcessorTime; public Guid ActivityId; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventDescriptor { public ushort Id; public byte Version, Channel, Level, Opcode; public ushort Task; public ulong Keyword; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EtwBufferContext { public ushort ProcessorIndex, LoggerId; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_MANDATORY_LABEL
    {
        public SID_AND_ATTRIBUTES Label;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public int Attributes;
    }
}
