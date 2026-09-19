using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Threading;
using IconGrid.Helpers.Hardware;
using IconGrid.Helpers.Settings;

namespace IconGrid.Helpers
{
    public enum PingSeverity
    {
        Unknown,
        Good,
        Warning,
        Critical
    }

    /// <summary>
    /// User-selectable ping target for the launcher monitor row.
    /// Stored as string in config for forward-compat (new targets can be added without breaking older configs).
    /// </summary>
    public enum PingTargetMode
    {
        Auto,        // internet-first: Cloudflare -> Google -> router (last resort)
        Gateway,     // user's local router/gateway only
        Cloudflare,  // 1.1.1.1
        Google,      // 8.8.8.8
        Custom       // user-defined IP/hostname from config
    }

    public class SystemMonitor : INotifyPropertyChanged, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan HardwareSnapshotMaxAge = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FpsStateMaxAge = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PingStaleAfter = TimeSpan.FromSeconds(10);
        private const int PingTimeoutMs = 200;
        // EMA smoothing factor (0 = no new info, 1 = no smoothing). 0.3 dampens single spikes well.
        private const double PingEmaAlpha = 0.3;
        // Numeric floor for the ping value. Windows' ICMP timing resolution is 1 ms, so a
        // sub-millisecond round-trip — typically the local router on a LAN — is reported as 0 ms.
        // The display shows that as "<1ms" (see CaptureNetworkSnapshot); this floor only keeps the
        // numeric EMA from decaying to 0.
        private const double MinPingMs = 1.0;

        // Hardcoded fallback targets after the auto-detected gateway.
        private static readonly IPAddress[] FallbackPingTargets =
        {
            IPAddress.Parse("1.1.1.1"),  // Cloudflare
            IPAddress.Parse("8.8.8.8"),  // Google
        };

        private readonly Dispatcher _dispatcher;
        private readonly string _monitorStatePath;
        private readonly string _fpsStatePath;
        private readonly DispatcherTimer _fpsTimer;
        private int _fpsUiPollIntervalMs = 1;

        // Reusable Ping instance. Not thread-safe — guarded by _pingLock when calling Send/SendAsync.
        private readonly Ping _sharedPing = new();
        private readonly object _pingLock = new();

        private string _networkStatus = "--";
        private string _cpuTemp = "--";
        private string _gpuTemp = "--";
        private string _cpuUsage = "--%";
        private double _cpuUsagePercent;
        private string _cpuClock = "--";
        private string _cpuVoltage = "--";
        private string _gpuClock = "--";
        private string _gpuUsage = "--%";
        private double _gpuUsagePercent;
        private string _gpuName = "";
        private string _downloadStatus = "--";
        private string _uploadStatus = "--";
        private string _fpsStatus = "--";
        private string _frameTimeStatus = "--";
        private double? _targetFpsValue;
        private double? _displayedFpsValue;
        private int? _displayedFpsInteger;
        private bool _isHighPing;
        private PingSeverity _pingSeverity = PingSeverity.Unknown;
        private long _lastDownloadBytes, _lastUploadBytes;
        private DateTime _lastUpdateTime = DateTime.UtcNow;
        private bool _isPawnIoAvailable;
        private double _fpsDisplayResponsiveness = 1.0;
        private bool _inGame;

        // EMA-smoothed ping state (independent from raw sample so we always have a sensible display value)
        private double? _pingEmaMs;
        private DateTime _lastSuccessfulPingUtc = DateTime.MinValue;
        private string _activePingTargetLabel = "—";
        private bool _isPingStale;

        // User-configurable ping target. Defaults to Auto (Cloudflare -> Google -> router).
        private PingTargetMode _pingTargetMode = PingTargetMode.Auto;
        private string _customPingTarget = "";

        // True when the most recent successful raw sample was sub-millisecond. Windows' ICMP
        // timing resolution is 1 ms, so a LAN round-trip to the local router reports 0 ms —
        // used to display "<1ms" instead of a misleading, never-changing "1ms".
        private bool _lastSampleSubMillisecond;
        private string _clockText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Local wall-clock time shown in the gaming overlay, formatted with the
        /// user's own Windows short-time pattern (e.g. "03:12" in Denmark,
        /// "3:12 AM" in the US) so no format is hardcoded.
        /// </summary>
        public string ClockText { get => _clockText; private set { _clockText = value; OnPropertyChanged(); } }

        public string NetworkStatus { get => _networkStatus; private set { _networkStatus = value; OnPropertyChanged(); } }
        public string PingTargetLabel { get => _activePingTargetLabel; private set { _activePingTargetLabel = value; OnPropertyChanged(); } }
        public bool IsPingStale { get => _isPingStale; private set { if (_isPingStale != value) { _isPingStale = value; OnPropertyChanged(); } } }
        public string CpuTemp { get => _cpuTemp; private set { _cpuTemp = value; OnPropertyChanged(); } }
        public string GpuTemp { get => _gpuTemp; private set { _gpuTemp = value; OnPropertyChanged(); } }
        public string CpuUsage { get => _cpuUsage; private set { _cpuUsage = value; OnPropertyChanged(); } }
        public double CpuUsagePercent { get => _cpuUsagePercent; private set { _cpuUsagePercent = value; OnPropertyChanged(); } }
        public string CpuClock { get => _cpuClock; private set { _cpuClock = value; OnPropertyChanged(); } }
        public string CpuVoltage { get => _cpuVoltage; private set { _cpuVoltage = value; OnPropertyChanged(); } }
        public string GpuClock { get => _gpuClock; private set { _gpuClock = value; OnPropertyChanged(); } }
        public string GpuUsage { get => _gpuUsage; private set { _gpuUsage = value; OnPropertyChanged(); } }
        public double GpuUsagePercent { get => _gpuUsagePercent; private set { _gpuUsagePercent = value; OnPropertyChanged(); } }
        public string GpuName { get => _gpuName; private set { _gpuName = value; OnPropertyChanged(); } }
        public string DownloadStatus { get => _downloadStatus; private set { _downloadStatus = value; OnPropertyChanged(); NotifyDownloadPartsChanged(); } }
        public string UploadStatus { get => _uploadStatus; private set { _uploadStatus = value; OnPropertyChanged(); NotifyUploadPartsChanged(); } }

        public string DownloadValue => SplitSpeed(_downloadStatus).value;
        public string DownloadUnit => SplitSpeed(_downloadStatus).unit;
        public string UploadValue => SplitSpeed(_uploadStatus).value;
        public string UploadUnit => SplitSpeed(_uploadStatus).unit;

        private void NotifyDownloadPartsChanged()
        {
            OnPropertyChanged(nameof(DownloadValue));
            OnPropertyChanged(nameof(DownloadUnit));
        }

        private void NotifyUploadPartsChanged()
        {
            OnPropertyChanged(nameof(UploadValue));
            OnPropertyChanged(nameof(UploadUnit));
        }

        private static (string value, string unit) SplitSpeed(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return ("--", "");

            var lastSpace = status.LastIndexOf(' ');
            if (lastSpace < 0)
                return (status, "");

            return (status[..lastSpace], status[(lastSpace + 1)..]);
        }
        public string FpsStatus
        {
            get => _fpsStatus;
            private set
            {
                _fpsStatus = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// True when a game process is actively tracked by the native FPS agent
        /// (TargetPid > 0 in shared memory). Deliberately does NOT use FPS data:
        /// the FPS pipeline is independent of whether a game process is running,
        /// and the "hold last FPS" behavior could keep a stale value that made the
        /// overlay stay transparent in Windows after the game closed. Transparency
        /// follows the tracked game process only, so the background returns to solid
        /// as soon as the game exits.
        /// </summary>
        public bool IsInGame => _inGame;
        public string FrameTimeStatus { get => _frameTimeStatus; private set { _frameTimeStatus = value; OnPropertyChanged(); } }
        public double FpsDisplayResponsiveness
        {
            get => _fpsDisplayResponsiveness;
            set => _fpsDisplayResponsiveness = Math.Max(0.15, Math.Min(1.0, value));
        }

        public bool IsHighPing
        {
            get => _isHighPing;
            private set
            {
                if (_isHighPing == value)
                {
                    return;
                }

                _isHighPing = value;
                OnPropertyChanged();
            }
        }

        public PingSeverity PingSeverityLevel
        {
            get => _pingSeverity;
            private set
            {
                if (_pingSeverity == value)
                {
                    return;
                }

                _pingSeverity = value;
                OnPropertyChanged();
            }
        }

        public bool IsPawnIoAvailable
        {
            get => _isPawnIoAvailable;
            private set
            {
                if (_isPawnIoAvailable == value)
                {
                    return;
                }

                _isPawnIoAvailable = value;
                OnPropertyChanged();
            }
        }

        public SystemMonitor()
        {
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            var configManager = new ConfigManager();
            _monitorStatePath = Path.Combine(configManager.BaseDirectory, "monitor-state.json");
            _fpsStatePath = Path.Combine(configManager.BaseDirectory, "fps-state.json");
            InitializeNetworkStats();

            _fpsTimer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(_fpsUiPollIntervalMs)
            };
            _fpsTimer.Tick += FpsTimer_Tick;
            _fpsTimer.Start();
        }

        public int FpsUiPollIntervalMs
        {
            get => _fpsUiPollIntervalMs;
            set
            {
                var clamped = Math.Max(1, Math.Min(8, value));
                if (_fpsUiPollIntervalMs == clamped)
                {
                    return;
                }

                _fpsUiPollIntervalMs = clamped;
                _fpsTimer.Interval = TimeSpan.FromMilliseconds(_fpsUiPollIntervalMs);
            }
        }

        public void Update()
        {
            var network = CaptureNetworkSnapshot();
            var hardware = ReadHardwareSnapshot();

            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (hardware != null)
                {
                    if (!string.IsNullOrWhiteSpace(hardware.CpuTemp))
                    {
                        CpuTemp = hardware.CpuTemp;
                    }

                    if (!string.IsNullOrWhiteSpace(hardware.GpuTemp))
                    {
                        GpuTemp = hardware.GpuTemp;
                    }

                    if (!string.IsNullOrWhiteSpace(hardware.CpuUsage) && hardware.CpuUsagePercent.HasValue)
                    {
                        CpuUsage = hardware.CpuUsage;
                        CpuUsagePercent = hardware.CpuUsagePercent.Value;
                    }

                    if (!string.IsNullOrWhiteSpace(hardware.CpuClock))
                    {
                        CpuClock = hardware.CpuClock;
                    }

                    if (!string.IsNullOrWhiteSpace(hardware.CpuVoltage))
                    {
                        CpuVoltage = hardware.CpuVoltage;
                    }

                    if (!string.IsNullOrWhiteSpace(hardware.GpuClock))
                    {
                        GpuClock = hardware.GpuClock;
                    }

                    if (!string.IsNullOrWhiteSpace(hardware.GpuUsage) && hardware.GpuUsagePercent.HasValue)
                    {
                        GpuUsage = hardware.GpuUsage;
                        GpuUsagePercent = hardware.GpuUsagePercent.Value;
                    }

                    if (!string.IsNullOrWhiteSpace(hardware.GpuName))
                    {
                        GpuName = hardware.GpuName;
                    }

                    IsPawnIoAvailable = hardware.IsPawnIoAvailable;
                }

                NetworkStatus = network.NetworkStatus;
                PingTargetLabel = network.PingTargetLabel;
                IsPingStale = network.IsPingStale;
                DownloadStatus = network.DownloadStatus;
                UploadStatus = network.UploadStatus;
                PingSeverityLevel = network.Severity;
                IsHighPing = network.IsHighPing;
                _lastDownloadBytes = network.NewDownload;
                _lastUploadBytes = network.NewUpload;
                _lastUpdateTime = network.NewUpdateTime;
            }));
        }

        /// <summary>
        /// Apply user-configurable ping target. Safe to call multiple times; takes effect on the next Update() tick.
        /// </summary>
        public void ConfigurePingTarget(PingTargetMode mode, string? customTarget)
        {
            _pingTargetMode = mode;
            _customPingTarget = customTarget ?? "";
            // Reset EMA so a target switch doesn't carry stale smoothed value from the old target.
            _pingEmaMs = null;
        }

        /// <summary>
        /// Returns a localized, human-readable label for a ping target, used in tooltips.
        /// </summary>
        private static string LabelForTarget(IPAddress? addr, string? hostname)
        {
            if (addr == null && string.IsNullOrWhiteSpace(hostname))
            {
                return "—";
            }

            var text = hostname ?? addr!.ToString();
            // Strip zone IDs (e.g. fe80::1%12) and trailing dots
            var pct = text.IndexOf('%');
            if (pct >= 0) text = text.Substring(0, pct);
            text = text.TrimEnd('.');
            return text;
        }

        private HardwareMonitorSnapshot? ReadHardwareSnapshot()
        {
            try
            {
                if (!File.Exists(_monitorStatePath))
                {
                    return null;
                }

                var info = new FileInfo(_monitorStatePath);
                if (DateTime.UtcNow - info.LastWriteTimeUtc > HardwareSnapshotMaxAge)
                {
                    return null;
                }

                var json = ReadSharedTextFile(_monitorStatePath);
                var snapshot = JsonSerializer.Deserialize<HardwareMonitorSnapshot>(json, JsonOptions);
                if (snapshot == null || DateTime.UtcNow - snapshot.CapturedAtUtc > HardwareSnapshotMaxAge)
                {
                    return null;
                }

                return snapshot;
            }
            catch
            {
                return null;
            }
        }

        private NetworkSnapshot CaptureNetworkSnapshot()
        {
            string networkStatus = "--";
            string downloadStatus = "--";
            string uploadStatus = "--";
            var severity = _pingSeverity;
            var highPing = _isHighPing;
            long newDownload = _lastDownloadBytes;
            long newUpload = _lastUploadBytes;
            var newUpdateTime = _lastUpdateTime;

            // Ping: try targets in priority order. First success wins.
            string targetLabel = "—";
            bool gotSample = false;
            long rawMs = -1;
            try
            {
                foreach (var target in ResolvePingTargets())
                {
                    targetLabel = LabelForTarget(target.Address, target.Hostname);
                    try
                    {
                        PingReply reply;
                        lock (_pingLock)
                        {
                            // Ping.Send accepts an IPAddress OR a hostname string.
                            if (target.Hostname != null)
                            {
                                reply = _sharedPing.Send(target.Hostname, PingTimeoutMs);
                            }
                            else
                            {
                                reply = _sharedPing.Send(target.Address, PingTimeoutMs);
                            }
                        }
                        if (reply != null && reply.Status == IPStatus.Success)
                        {
                            rawMs = reply.RoundtripTime;
                            gotSample = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Try next target on this one's failure (e.g. unknown host, permission denied).
                    }
                }
            }
            catch
            {
                // Fall through — leave networkStatus at last good value or "--".
            }

            var nowUtc = DateTime.UtcNow;
            if (gotSample)
            {
                // Windows' ICMP timing resolution is 1 ms, so a sub-millisecond round-trip
                // (typically the local router) reports 0. Remember that so the display can
                // show "<1ms" instead of a constant "1ms".
                _lastSampleSubMillisecond = rawMs <= 0;

                // Clamp raw sample to MinPingMs so we never EMA-smooth toward 0 (Windows can report
                // 0ms because its clock-tick resolution is ~15.6ms — sub-ms round-trips round down).
                var clampedRaw = Math.Max(MinPingMs, (double)rawMs);
                if (!_pingEmaMs.HasValue)
                {
                    // First sample — initialize EMA directly to avoid warm-up skew.
                    _pingEmaMs = clampedRaw;
                }
                else
                {
                    var ema = (PingEmaAlpha * clampedRaw) + ((1.0 - PingEmaAlpha) * _pingEmaMs.Value);
                    _pingEmaMs = Math.Max(MinPingMs, ema);
                }
                _lastSuccessfulPingUtc = nowUtc;
            }

            // Decide what to display: prefer EMA-smoothed value, fall back to last good value
            // if all targets currently fail but we have a recent successful sample (within PingStaleAfter).
            var displayMs = (double?)null;
            var isStale = false;
            if (_pingEmaMs.HasValue)
            {
                var ageSinceSuccess = nowUtc - _lastSuccessfulPingUtc;
                if (gotSample || ageSinceSuccess <= PingStaleAfter)
                {
                    displayMs = Math.Max(MinPingMs, _pingEmaMs.Value);
                    isStale = !gotSample;
                }
                // else: completely stale — drop to "--" so user sees something is wrong.
            }

            if (displayMs.HasValue)
            {
                // A sub-millisecond LAN round-trip (local router) reports 0 ms because of
                // Windows' 1 ms ICMP timing resolution. Show "<1ms" rather than a constant,
                // never-changing "1ms" that would look like a broken measurement.
                var subMillisecond = _lastSampleSubMillisecond && displayMs.Value <= MinPingMs;
                var valueText = subMillisecond ? "<1" : $"{displayMs.Value:F0}";

                networkStatus = isStale
                    ? $"--ms ({valueText})"
                    : $"{valueText}ms";
                severity = DeterminePingSeverity(subMillisecond ? 1 : (long)Math.Round(displayMs.Value));
                highPing = severity == PingSeverity.Critical;
            }
            else
            {
                networkStatus = "--ms";
                severity = PingSeverity.Critical;
                highPing = false;
            }

            try
            {
                var stats = GetNetAdapterStatistics();
                var now = DateTime.UtcNow;
                var diff = (now - _lastUpdateTime).TotalSeconds;

                if (diff > 0)
                {
                    var dlSpeed = (long)((stats.r - _lastDownloadBytes) / diff);
                    var ulSpeed = (long)((stats.s - _lastUploadBytes) / diff);
                    downloadStatus = FormatSpeed(dlSpeed);
                    uploadStatus = FormatSpeed(ulSpeed);
                    newDownload = stats.r;
                    newUpload = stats.s;
                    newUpdateTime = now;
                }
                else
                {
                    newDownload = stats.r;
                    newUpload = stats.s;
                    newUpdateTime = now;
                }
            }
            catch
            {
                // Keep last good values rather than nuking the display.
            }

            return new NetworkSnapshot(networkStatus, downloadStatus, uploadStatus, severity, highPing, newDownload, newUpload, newUpdateTime, targetLabel, isStale);
        }

        /// <summary>
        /// Resolves the list of ping targets to try this tick, in priority order.
        /// Honors user-selected mode (Auto / Gateway / Cloudflare / Google / Custom).
        /// </summary>
        private IEnumerable<(IPAddress Address, string? Hostname)> ResolvePingTargets()
        {
            switch (_pingTargetMode)
            {
                case PingTargetMode.Gateway:
                    {
                        var gw = GetActiveGatewayAddress();
                        if (gw != null) yield return (gw, null);
                        yield break;
                    }
                case PingTargetMode.Cloudflare:
                    yield return (IPAddress.Parse("1.1.1.1"), null);
                    yield break;
                case PingTargetMode.Google:
                    yield return (IPAddress.Parse("8.8.8.8"), null);
                    yield break;
                case PingTargetMode.Custom:
                    {
                        if (!string.IsNullOrWhiteSpace(_customPingTarget))
                        {
                            // Ping.Send accepts IPAddress OR hostname string. Use hostname string so
                            // DNS-based custom hostnames (e.g. "speedtest.example.com") work too.
                            yield return (IPAddress.Loopback, _customPingTarget.Trim());
                        }
                        yield break;
                    }
                case PingTargetMode.Auto:
                default:
                    {
                        // Internet-first: the launcher's "Net" value must reflect the real
                        // internet latency so it moves when the line degrades. The local router
                        // answers in under 1 ms (always "1ms"), so it is only used as a last
                        // resort when no internet target responds at all.
                        foreach (var fb in FallbackPingTargets)
                        {
                            yield return (fb, null);
                        }

                        var gw = GetActiveGatewayAddress();
                        if (gw != null) yield return (gw, null);
                        yield break;
                    }
            }
        }

        /// <summary>
        /// Returns the IPv4 gateway address of the first active, non-loopback, gateway-bearing adapter.
        /// Returns null when no usable gateway is found (e.g. disconnected).
        /// </summary>
        private static IPAddress? GetActiveGatewayAddress()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(x => x.NetworkInterfaceType != NetworkInterfaceType.Loopback
                             && x.OperationalStatus == OperationalStatus.Up
                             && x.GetIPProperties().GatewayAddresses.Count > 0
                             && x.GetIPProperties().GatewayAddresses.Any(g => g.Address.ToString() != "0.0.0.0"));

                // Prefer IPv4 (typical home/cable routers).
                foreach (var adapter in interfaces)
                {
                    foreach (var gw in adapter.GetIPProperties().GatewayAddresses)
                    {
                        var addr = gw.Address;
                        if (addr == null) continue;
                        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            var text = addr.ToString();
                            if (text != "0.0.0.0")
                            {
                                return addr;
                            }
                        }
                    }
                }

                // Fallback: accept IPv6 gateway if no IPv4 found.
                foreach (var adapter in interfaces)
                {
                    foreach (var gw in adapter.GetIPProperties().GatewayAddresses)
                    {
                        var addr = gw.Address;
                        if (addr == null) continue;
                        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        {
                            var text = addr.ToString();
                            var pct = text.IndexOf('%');
                            if (pct >= 0) text = text.Substring(0, pct);
                            if (text != "::")
                            {
                                return addr;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Swallow — we'll just have no gateway target this tick.
            }

            return null;
        }

        private (long r, long s) GetNetAdapterStatistics()
        {
            long r = 0;
            long s = 0;
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && x.OperationalStatus == OperationalStatus.Up
                         && x.GetIPProperties().GatewayAddresses.Count > 0
                         && x.GetIPProperties().GatewayAddresses.Any(g => g.Address.ToString() != "0.0.0.0"));
            foreach (var adapter in interfaces)
            {
                try
                {
                    var stats = adapter.GetIPv4Statistics();
                    r += stats.BytesReceived;
                    s += stats.BytesSent;
                }
                catch
                {
                }
            }

            return (r, s);
        }

        private static PingSeverity DeterminePingSeverity(long? roundtripMs)
        {
            if (!roundtripMs.HasValue || roundtripMs.Value < 0)
            {
                return PingSeverity.Unknown;
            }

            if (roundtripMs.Value <= 30)
            {
                return PingSeverity.Good;
            }

            if (roundtripMs.Value <= 100)
            {
                return PingSeverity.Warning;
            }

            return PingSeverity.Critical;
        }

        private static string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond >= 1048576)
            {
                return string.Format("{0:F1} MB/s", (double)bytesPerSecond / 1048576);
            }

            return string.Format("{0:F0} KB/s", (double)bytesPerSecond / 1024);
        }

        private FpsState? ReadFpsState()
        {
            try
            {
                if (!File.Exists(_fpsStatePath))
                {
                    return null;
                }

                var info = new FileInfo(_fpsStatePath);
                if (DateTime.UtcNow - info.LastWriteTimeUtc > FpsStateMaxAge)
                {
                    return null;
                }

                var json = ReadSharedTextFile(_fpsStatePath);
                var state = JsonSerializer.Deserialize<FpsState>(json, JsonOptions);
                if (state == null || DateTime.UtcNow - state.CapturedAtUtc > FpsStateMaxAge)
                {
                    return null;
                }

                return state;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Refreshes the overlay clock. Called from the FPS timer, but only raises a
        /// property change when the rendered text actually changes, so the overlay is
        /// not invalidated on every poll.
        /// </summary>
        private void UpdateClock()
        {
            var pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
            var text = DateTime.Now.ToString(pattern, CultureInfo.CurrentCulture);
            if (!string.Equals(text, _clockText, StringComparison.Ordinal))
            {
                ClockText = text;
            }
        }

        private void FpsTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();

            var nativeFpsState = NativeFpsSharedMemory.TryRead();
            var fpsState = ReadFpsState();
            var nativeFpsValue = nativeFpsState?.FpsValue;
            var hasNativeFps = nativeFpsValue.HasValue && nativeFpsValue.Value > 0 &&
                               DateTime.UtcNow - nativeFpsState!.Value.CapturedAtUtc <= FpsStateMaxAge;
            var correctedFpsValue = fpsState?.LiveFpsValue;
            var hasCorrectedFps = correctedFpsValue.HasValue && correctedFpsValue.Value > 0;

            // In-game is driven ONLY by the native agent's tracked PID. When the game
            // exits, TargetPid drops to 0 immediately, so the overlay background returns
            // right away instead of waiting for the FPS feed to decay.
            //
            // IMPORTANT: We deliberately do NOT use FPS data to decide IsInGame. The FPS
            // pipeline is independent of whether a game process is actually running — the
            // "hold last FPS" behavior can keep a stale value, which made the overlay stay
            // transparent in Windows after the game closed. Transparency must follow the
            // tracked game process only: TargetPid greater than 0 means a game is running,
            // anything else (0 or unavailable shared memory) means no game is tracked.
            var trackedGamePid = nativeFpsState?.TargetPid ?? 0;

            // The native agent only publishes a target PID for a confirmed game, so
            // this preserves existing behavior — but the overlay now also follows
            // the VRAM rule, so a game that holds game-like dedicated VRAM shows up
            // even when the native agent had no application-level present evidence
            // (emulators / older titles). A shell process never qualifies: it holds
            // a few MB, far below the game floor.
            var nativeGameEvidence = trackedGamePid > 0
                ? GameVramEvidence.Evaluate(trackedGamePid, null, DateTime.UtcNow, out _)
                : GameEvidenceVerdict.Unknown;
            var isGameTarget = nativeFpsState?.GameConfirmed == true ||
                               nativeGameEvidence == GameEvidenceVerdict.Game;
            SetInGame(trackedGamePid > 0 && isGameTarget);
            if (!hasNativeFps && fpsState == null)
            {
                _targetFpsValue = null;
                _displayedFpsValue = null;
                _displayedFpsInteger = null;
                FpsStatus = "--";
                FrameTimeStatus = "--";
                return;
            }

            if (hasCorrectedFps)
            {
                _targetFpsValue = correctedFpsValue!.Value;
            }
            else if (hasNativeFps)
            {
                _targetFpsValue = nativeFpsValue!.Value;
            }
            else if (int.TryParse(fpsState?.LiveFpsStatus, out var parsedLiveStatus) && parsedLiveStatus > 0)
            {
                _targetFpsValue = parsedLiveStatus;
            }
            else if (int.TryParse(fpsState?.FpsStatus, out var parsedStatus) && parsedStatus > 0)
            {
                _targetFpsValue = parsedStatus;
            }
            else
            {
                _targetFpsValue = null;
            }

            if (!_targetFpsValue.HasValue)
            {
                _displayedFpsValue = null;
                _displayedFpsInteger = null;
                FpsStatus = "--";
                FrameTimeStatus = "--";
                return;
            }

            if (!_displayedFpsValue.HasValue)
            {
                _displayedFpsValue = _targetFpsValue.Value;
            }
            else
            {
                var alpha = _fpsDisplayResponsiveness;
                _displayedFpsValue = (_targetFpsValue.Value * alpha) + (_displayedFpsValue.Value * (1.0 - alpha));
            }

            var targetDisplayValue = (int)Math.Round(_displayedFpsValue.Value);
            if (!_displayedFpsInteger.HasValue)
            {
                _displayedFpsInteger = targetDisplayValue;
            }
            else if (_displayedFpsInteger.Value < targetDisplayValue)
            {
                _displayedFpsInteger++;
            }
            else if (_displayedFpsInteger.Value > targetDisplayValue)
            {
                _displayedFpsInteger--;
            }

            FpsStatus = (_displayedFpsInteger ?? targetDisplayValue).ToString("F0");
            FrameTimeStatus = _targetFpsValue.Value > 0
                ? $"{Math.Round(1000.0 / _targetFpsValue.Value):F0}"
                : "--";
        }

        private static string ReadSharedTextFile(string path)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private record NetworkSnapshot(
            string NetworkStatus,
            string DownloadStatus,
            string UploadStatus,
            PingSeverity Severity,
            bool IsHighPing,
            long NewDownload,
            long NewUpload,
            DateTime NewUpdateTime,
            string PingTargetLabel,
            bool IsPingStale);

        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void SetInGame(bool value)
        {
            if (_inGame == value)
            {
                return;
            }

            _inGame = value;
            OnPropertyChanged(nameof(IsInGame));
        }

        private void InitializeNetworkStats()
        {
            try
            {
                var stats = GetNetAdapterStatistics();
                _lastDownloadBytes = stats.r;
                _lastUploadBytes = stats.s;
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            _fpsTimer.Stop();
            _fpsTimer.Tick -= FpsTimer_Tick;
            try
            {
                _sharedPing.Dispose();
            }
            catch
            {
                // Ignore — best-effort cleanup on shutdown.
            }
        }
    }
}
