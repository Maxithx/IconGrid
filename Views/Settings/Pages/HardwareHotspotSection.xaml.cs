using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using IconGrid.Helpers;
using IconGrid.Helpers.Hardware;
using IconGrid.Helpers.Settings;
using IconGrid.ViewModels;
using Newtonsoft.Json;

namespace IconGrid.Views
{
    public partial class HardwareHotspotSection : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        private string _gpuHotspotStatusText = string.Empty;
        private string _gpuHotspotWarningText = string.Empty;
        private string _gpuHotspotRecordText = string.Empty;
        private string _gpuHotspotHistoryNoDataText = string.Empty;
        private System.Windows.Media.PointCollection _hotspotTrendPoints = new();
        private System.Windows.Media.PointCollection _gpuTempTrendPoints = new();
        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer =
            new() { Interval = TimeSpan.FromSeconds(1) };

        public event PropertyChangedEventHandler? PropertyChanged;

        public HardwareHotspotSection()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (_, _) =>
            {
                RefreshFromState();
                _refreshTimer.Start();
            };
            Unloaded += (_, _) => _refreshTimer.Stop();
            _refreshTimer.Tick += (_, _) => RefreshFromState();
        }

        public ObservableCollection<HotspotHistoryRow> HotspotHistoryRows { get; } = new();

        public sealed record HotspotHistoryRow(
            string TimeText,
            string HotspotText,
            string GpuText,
            string GpuClockText,
            string MemoryClockText,
            string VramText,
            string PowerText,
            string GameText);

        public string GpuHotspotStatusText
        {
            get => _gpuHotspotStatusText;
            private set => SetField(ref _gpuHotspotStatusText, value);
        }

        public string GpuHotspotWarningText
        {
            get => _gpuHotspotWarningText;
            private set => SetField(ref _gpuHotspotWarningText, value);
        }

        public string GpuHotspotRecordText
        {
            get => _gpuHotspotRecordText;
            private set => SetField(ref _gpuHotspotRecordText, value);
        }

        public string GpuHotspotHistoryNoDataText
        {
            get => _gpuHotspotHistoryNoDataText;
            private set => SetField(ref _gpuHotspotHistoryNoDataText, value);
        }

        public System.Windows.Media.PointCollection HotspotTrendPoints
        {
            get => _hotspotTrendPoints;
            private set => SetField(ref _hotspotTrendPoints, value);
        }

        public System.Windows.Media.PointCollection GpuTempTrendPoints
        {
            get => _gpuTempTrendPoints;
            private set => SetField(ref _gpuTempTrendPoints, value);
        }

        private void RefreshFromState()
        {
            var viewModel = ResolveViewModel();
            if (viewModel == null)
            {
                return;
            }

            try
            {
                var path = Path.Combine(
                    new ConfigManager().BaseDirectory,
                    "monitor-state.json");

                if (!File.Exists(path))
                {
                    ShowNoData(viewModel);
                    return;
                }

                var snapshot = JsonConvert.DeserializeObject<HardwareMonitorSnapshot>(
                    File.ReadAllText(path));
                Update(viewModel, snapshot);
            }
            catch
            {
                // A failed monitor-state read must never break the settings page.
            }
        }

        public void ShowNoData(MainViewModel viewModel)
        {
            GpuHotspotStatusText = viewModel.GpuHotspotNoData;
            GpuHotspotWarningText = string.Empty;
            HotspotHistoryRows.Clear();
            GpuHotspotHistoryNoDataText = viewModel.GpuHotspotHistoryNoData;
            GpuHotspotRecordText = viewModel.GpuHotspotRecordNoData;
            HotspotTrendPoints = new System.Windows.Media.PointCollection();
            GpuTempTrendPoints = new System.Windows.Media.PointCollection();
        }

        public void Update(MainViewModel viewModel, HardwareMonitorSnapshot? snapshot)
        {
            var entries = snapshot?.HotspotHistory ?? new List<HotspotHistoryEntry>();

            GpuHotspotRecordText = entries.Count > 0
                ? FormatHotspotHistoryEntry(viewModel, entries[0])
                : viewModel.GpuHotspotRecordNoData;

            var history = entries
                .Select(entry => new HotspotHistoryRow(
                    entry.CapturedAtUtc.ToLocalTime().ToString("dd-MM HH:mm"),
                    $"{entry.HotspotC:F0} °C",
                    $"{entry.EdgeC:F0} °C",
                    entry.GpuClock ?? "—",
                    entry.GpuMemoryClock ?? "—",
                    entry.GpuMemoryUsage ?? "—",
                    entry.GpuPower ?? "—",
                    string.IsNullOrWhiteSpace(entry.GameName)
                        ? viewModel.GpuHotspotHistoryNoGame
                        : entry.GameName))
                .ToList();

            if (!HotspotHistoryRows.SequenceEqual(history))
            {
                HotspotHistoryRows.Clear();
                foreach (var row in history)
                {
                    HotspotHistoryRows.Add(row);
                }
            }

            GpuHotspotHistoryNoDataText = entries.Count == 0
                ? viewModel.GpuHotspotHistoryNoData
                : string.Empty;

            RefreshHotspotTrend(snapshot);

            if (snapshot?.GpuHotspot == null)
            {
                GpuHotspotStatusText = viewModel.GpuHotspotNoData;
                GpuHotspotWarningText = string.Empty;
                return;
            }

            var delta = snapshot.GpuHotspotDeltaC ?? 0;
            GpuHotspotStatusText = string.Format(
                viewModel.GpuHotspotStatusFormat,
                snapshot.GpuHotspot,
                snapshot.GpuTemp ?? "–",
                delta.ToString("F1"));

            var hotspot = ParseTemperature(snapshot.GpuHotspot);
            var topEntry = snapshot.HotspotHistory?.FirstOrDefault();
            var peakHotspot = topEntry?.HotspotC;
            var currentOverLimit =
                hotspot.HasValue && hotspot.Value >= viewModel.GpuHotspotAlarmThresholdC;
            var peakOverLimit = peakHotspot.HasValue &&
                                peakHotspot.Value >= viewModel.GpuHotspotAlarmThresholdC;
            var deltaOverLimit = delta >= 15;

            if (viewModel.GpuHotspotAlarmEnabled && (currentOverLimit || deltaOverLimit))
            {
                GpuHotspotWarningText = string.Format(
                    viewModel.GpuHotspotWarning,
                    snapshot.GpuHotspot,
                    delta.ToString("F1"));
            }
            else if (viewModel.GpuHotspotAlarmEnabled && peakOverLimit)
            {
                GpuHotspotWarningText = string.Format(
                    viewModel.GpuHotspotPeakWarning,
                    topEntry!.HotspotC,
                    topEntry.EdgeC,
                    topEntry.DeltaC,
                    topEntry.CapturedAtUtc.ToLocalTime(),
                    string.IsNullOrWhiteSpace(topEntry.GameName)
                        ? viewModel.GpuHotspotHistoryNoGame
                        : topEntry.GameName,
                    viewModel.GpuHotspotAlarmThresholdC);
            }
            else
            {
                GpuHotspotWarningText = string.Empty;
            }
        }

        private void RefreshHotspotTrend(HardwareMonitorSnapshot? snapshot)
        {
            var trend = snapshot?.HotspotTrend;
            if (trend == null || trend.Count < 2)
            {
                HotspotTrendPoints = new System.Windows.Media.PointCollection();
                GpuTempTrendPoints = new System.Windows.Media.PointCollection();
                return;
            }

            const double width = 560;
            const double height = 120;
            const double padding = 8;
            const double maxTemp = 110;

            var hotspot = new System.Windows.Media.PointCollection();
            var edge = new System.Windows.Media.PointCollection();
            var step = (width - (2 * padding)) / (trend.Count - 1);

            for (var index = 0; index < trend.Count; index++)
            {
                var x = padding + (index * step);
                var hotspotY = height - padding - (Math.Clamp(trend[index].HotspotC, 0, maxTemp) / maxTemp) * (height - (2 * padding));
                var edgeY = height - padding - (Math.Clamp(trend[index].EdgeC, 0, maxTemp) / maxTemp) * (height - (2 * padding));
                hotspot.Add(new System.Windows.Point(x, hotspotY));
                edge.Add(new System.Windows.Point(x, edgeY));
            }

            HotspotTrendPoints = hotspot;
            GpuTempTrendPoints = edge;
        }

        private static string FormatHotspotHistoryEntry(MainViewModel viewModel, HotspotHistoryEntry entry)
            => string.Format(
                viewModel.GpuHotspotHistoryFormat,
                entry.HotspotC,
                entry.EdgeC,
                entry.DeltaC,
                entry.CapturedAtUtc.ToLocalTime(),
                string.IsNullOrWhiteSpace(entry.GameName)
                    ? viewModel.GpuHotspotHistoryNoGame
                    : entry.GameName);

        private void HotspotTestButton_Click(object sender, RoutedEventArgs e)
        {
            // Runs an 8-second visual demo on the live overlay and in the preview above.
            // It never changes the user's real hotspot alarm settings.
            ResolveViewModel()?.StartGamingOverlayHotspotAlertDemo();
        }

        private async void ClearHotspotHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var viewModel = ResolveViewModel();
                if (viewModel == null)
                {
                    return;
                }

                var owner = Window.GetWindow(this);
                var result = owner != null
                    ? System.Windows.MessageBox.Show(
                        owner,
                        viewModel.GpuHotspotHistoryClearConfirmFormat,
                        viewModel.GpuHotspotHistoryClearConfirmTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No)
                    : System.Windows.MessageBox.Show(
                        viewModel.GpuHotspotHistoryClearConfirmFormat,
                        viewModel.GpuHotspotHistoryClearConfirmTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                var requestPath = Path.Combine(
                    new ConfigManager().BaseDirectory,
                    "hotspot-history-clear.request");
                File.WriteAllText(requestPath, "clear");

                HotspotHistoryRows.Clear();
                GpuHotspotHistoryNoDataText = viewModel.GpuHotspotHistoryNoData;
            }
            catch (Exception ex)
            {
                GpuHotspotStatusText = ex.Message;
            }
        }

        private MainViewModel? ResolveViewModel()
            => Window.GetWindow(this)?.DataContext as MainViewModel;

        private static double? ParseTemperature(string? text)
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
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
                ? value
                : null;
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
