using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

    public class SystemMonitor : INotifyPropertyChanged, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan HardwareSnapshotMaxAge = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FpsStateMaxAge = TimeSpan.FromSeconds(2);
        private readonly Dispatcher _dispatcher;
        private readonly string _monitorStatePath;
        private readonly string _fpsStatePath;
        private readonly DispatcherTimer _fpsTimer;
        private int _fpsUiPollIntervalMs = 1;

        private string _networkStatus = "--";
        private string _cpuTemp = "--";
        private string _gpuTemp = "--";
        private string _cpuUsage = "--%";
        private double _cpuUsagePercent;
        private string _cpuClock = "--";
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

        public event PropertyChangedEventHandler? PropertyChanged;

        public string NetworkStatus { get => _networkStatus; private set { _networkStatus = value; OnPropertyChanged(); } }
        public string CpuTemp { get => _cpuTemp; private set { _cpuTemp = value; OnPropertyChanged(); } }
        public string GpuTemp { get => _gpuTemp; private set { _gpuTemp = value; OnPropertyChanged(); } }
        public string CpuUsage { get => _cpuUsage; private set { _cpuUsage = value; OnPropertyChanged(); } }
        public double CpuUsagePercent { get => _cpuUsagePercent; private set { _cpuUsagePercent = value; OnPropertyChanged(); } }
        public string CpuClock { get => _cpuClock; private set { _cpuClock = value; OnPropertyChanged(); } }
        public string GpuClock { get => _gpuClock; private set { _gpuClock = value; OnPropertyChanged(); } }
        public string GpuUsage { get => _gpuUsage; private set { _gpuUsage = value; OnPropertyChanged(); } }
        public double GpuUsagePercent { get => _gpuUsagePercent; private set { _gpuUsagePercent = value; OnPropertyChanged(); } }
        public string GpuName { get => _gpuName; private set { _gpuName = value; OnPropertyChanged(); } }
        public string DownloadStatus { get => _downloadStatus; private set { _downloadStatus = value; OnPropertyChanged(); } }
        public string UploadStatus { get => _uploadStatus; private set { _uploadStatus = value; OnPropertyChanged(); } }
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
                DownloadStatus = network.DownloadStatus;
                UploadStatus = network.UploadStatus;
                PingSeverityLevel = network.Severity;
                IsHighPing = network.IsHighPing;
                _lastDownloadBytes = network.NewDownload;
                _lastUploadBytes = network.NewUpload;
                _lastUpdateTime = network.NewUpdateTime;
            }));
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

            try
            {
                var reply = new Ping().Send("8.8.8.8", 300);
                networkStatus = (reply?.Status == IPStatus.Success) ? $"{reply.RoundtripTime}ms" : "--ms";
                severity = DeterminePingSeverity(reply?.RoundtripTime);
                highPing = severity == PingSeverity.Critical;

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
                severity = PingSeverity.Critical;
                highPing = false;
            }

            return new NetworkSnapshot(networkStatus, downloadStatus, uploadStatus, severity, highPing, newDownload, newUpload, newUpdateTime);
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

        private void FpsTimer_Tick(object? sender, EventArgs e)
        {
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
            SetInGame(trackedGamePid > 0);
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
            DateTime NewUpdateTime);

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
        }
    }
}
