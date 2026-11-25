using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using IconGrid.Helpers;

namespace IconGrid.ViewModels.Launcher
{
    /// <summary>
    /// Small, UI-independent state machine for the hotspot warning shown in the gaming overlay.
    /// It deliberately owns no timer of its own: the existing <see cref="SystemMonitor"/> feed is
    /// the single source of truth, and a short demo override lets the settings page test the look.
    /// </summary>
    public sealed class GpuHotspotAlertState : INotifyPropertyChanged
    {
        private const int HotSamplesRequired = 2;
        private const int CoolSamplesRequired = 3;
        private const double DeltaWarningCelsius = 15.0;
        private const double CriticalDeltaCelsius = 20.0;
        private const double CriticalOffsetCelsius = 5.0;
        private const double HysteresisCelsius = 2.0;

        private readonly SystemMonitor _systemMonitor;
        private bool _alarmEnabled;
        private int _thresholdCelsius = 100;
        private GpuHotspotAlertSeverity _severity;
        private double? _hotspotCelsius;
        private double? _gpuCelsius;
        private double? _deltaCelsius;
        private int _hotSamples;
        private int _coolSamples;
        private bool _isDemo;
        private DateTime _demoUntilUtc;

        public GpuHotspotAlertState(SystemMonitor systemMonitor)
        {
            _systemMonitor = systemMonitor;
            _systemMonitor.PropertyChanged += SystemMonitor_PropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public GpuHotspotAlertSeverity Severity => _severity;
        public bool IsVisible => _severity != GpuHotspotAlertSeverity.None;
        public bool IsCritical => _severity == GpuHotspotAlertSeverity.Critical;
        public double? HotspotCelsius => _hotspotCelsius;
        public double? GpuCelsius => _gpuCelsius;
        public double? DeltaCelsius => _deltaCelsius;
        public int ThresholdCelsius => _thresholdCelsius;

        public void UpdateSettings(bool alarmEnabled, int thresholdCelsius)
        {
            var changed = _alarmEnabled != alarmEnabled || _thresholdCelsius != thresholdCelsius;
            _alarmEnabled = alarmEnabled;
            _thresholdCelsius = thresholdCelsius;

            if (changed)
            {
                Refresh();
            }
        }

        /// <summary>
        /// Shows a realistic critical warning for a few seconds without changing the real alarm
        /// settings. Used by the "Test hotspot alert" button on the Gaming overlay settings page.
        /// </summary>
        public void StartDemo(TimeSpan duration)
        {
            _isDemo = true;
            _demoUntilUtc = DateTime.UtcNow.Add(duration);
            Refresh();
        }

        public void Refresh()
        {
            if (_isDemo)
            {
                if (DateTime.UtcNow < _demoUntilUtc)
                {
                    _hotSamples = 0;
                    _coolSamples = 0;
                    SetValues(97, 78);
                    SetSeverity(GpuHotspotAlertSeverity.Critical);
                    return;
                }

                _isDemo = false;
            }

            if (!_alarmEnabled)
            {
                Reset();
                return;
            }

            var hotspot = ParseCelsius(_systemMonitor.GpuHotspot);
            var gpu = ParseCelsius(_systemMonitor.GpuTemp);
            SetValues(hotspot, gpu);

            if (!hotspot.HasValue)
            {
                _hotSamples = 0;
                _coolSamples = 0;
                SetSeverity(GpuHotspotAlertSeverity.None);
                return;
            }

            var overThreshold = hotspot.Value >= _thresholdCelsius;
            var overDelta = _deltaCelsius.HasValue && _deltaCelsius.Value >= DeltaWarningCelsius;
            var critical = hotspot.Value >= _thresholdCelsius + CriticalOffsetCelsius ||
                           (_deltaCelsius.HasValue && _deltaCelsius.Value >= CriticalDeltaCelsius);

            if (overThreshold || overDelta)
            {
                _hotSamples++;
                _coolSamples = 0;

                if (_hotSamples >= HotSamplesRequired)
                {
                    SetSeverity(critical
                        ? GpuHotspotAlertSeverity.Critical
                        : GpuHotspotAlertSeverity.Warning);
                }

                return;
            }

            _hotSamples = 0;
            _coolSamples++;

            if (_coolSamples >= CoolSamplesRequired && hotspot.Value < _thresholdCelsius - HysteresisCelsius)
            {
                SetSeverity(GpuHotspotAlertSeverity.None);
            }
        }
        private void Reset()
        {
            _hotSamples = 0;
            _coolSamples = 0;
            SetValues(null, null);
            SetSeverity(GpuHotspotAlertSeverity.None);
        }

        private void SetValues(double? hotspotCelsius, double? gpuCelsius)
        {
            var delta = hotspotCelsius.HasValue && gpuCelsius.HasValue
                ? Math.Max(0, hotspotCelsius.Value - gpuCelsius.Value)
                : (double?)null;

            var changed = _hotspotCelsius != hotspotCelsius ||
                          _gpuCelsius != gpuCelsius ||
                          _deltaCelsius != delta;

            _hotspotCelsius = hotspotCelsius;
            _gpuCelsius = gpuCelsius;
            _deltaCelsius = delta;

            if (changed)
            {
                OnPropertyChanged(nameof(HotspotCelsius));
                OnPropertyChanged(nameof(GpuCelsius));
                OnPropertyChanged(nameof(DeltaCelsius));
            }
        }

        private void SetSeverity(GpuHotspotAlertSeverity severity)
        {
            if (_severity == severity)
            {
                return;
            }

            _severity = severity;
            OnPropertyChanged(nameof(Severity));
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(IsCritical));
        }

        private void SystemMonitor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(SystemMonitor.GpuHotspot), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(SystemMonitor.GpuTemp), StringComparison.Ordinal))
            {
                Refresh();
            }
        }

        private static double? ParseCelsius(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                if (char.IsDigit(character) || character == '.' || character == ',')
                {
                    builder.Append(character == ',' ? '.' : character);
                }
            }

            return double.TryParse(
                builder.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : null;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum GpuHotspotAlertSeverity
    {
        None,
        Warning,
        Critical
    }
}

