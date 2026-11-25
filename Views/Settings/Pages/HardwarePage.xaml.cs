using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using IconGrid.Helpers;
using IconGrid.Helpers.Hardware;
using IconGrid.Helpers.Settings;
using Newtonsoft.Json;

namespace IconGrid.Views;

public partial class HardwarePage : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    private HardwareOverview _overview = new();
    private bool _overviewLoaded;

    private readonly System.Windows.Threading.DispatcherTimer _gpuUndervoltTimer =
        new() { Interval = System.TimeSpan.FromSeconds(1) };

    private string _gpuUndervoltStatusText = string.Empty;

    private double _undervoltTargetClockMhz = 1800;

    private string _gpuHotspotStatusText = string.Empty;
    private string _gpuHotspotWarningText = string.Empty;

    public string GpuHotspotStatusText
    {
        get => _gpuHotspotStatusText;
        private set
        {
            if (_gpuHotspotStatusText == value)
            {
                return;
            }

            _gpuHotspotStatusText = value;
            OnPropertyChanged(nameof(GpuHotspotStatusText));
        }
    }

    public string GpuHotspotWarningText
    {
        get => _gpuHotspotWarningText;
        private set
        {
            if (_gpuHotspotWarningText == value)
            {
                return;
            }

            _gpuHotspotWarningText = value;
            OnPropertyChanged(nameof(GpuHotspotWarningText));
        }
    }
    private const double CurveChartWidth = 560;
    private const double CurveChartHeight = 180;
    private const double CurveChartPadding = 10;
    private const float CurveMinVoltageMv = 600;
    private const float CurveMaxVoltageMv = 1700;
    private const float CurveMinClockMhz = 300;
    private const float CurveMaxClockMhz = 2100;

    private System.Windows.Media.PointCollection _stockCurvePoints = new();
    private System.Windows.Media.PointCollection _activeCurvePoints = new();
    private System.Windows.Media.PointCollection _targetCurvePoints = new();
    private System.Windows.Media.PointCollection _hotspotTrendPoints = new();
    private System.Windows.Media.PointCollection _gpuTempTrendPoints = new();
    private string _gpuCurveChartNoDataText = string.Empty;
    private string _gpuCurveChartActiveProfileText = string.Empty;
    private string _gpuCurveChartTargetText = string.Empty;
    private string _gpuCurveStockVoltageText = string.Empty;
    private string _gpuCurveProfileVoltageText = string.Empty;
    private string _gpuStockBaselineText = string.Empty;
    private string _gpuUndervoltLiveVoltageText = string.Empty;
    private string _gpuUndervoltVoltageSelectionText = string.Empty;
    private string _gpuHotspotHistoryNoDataText = string.Empty;
    private string _gpuHotspotRecordText = string.Empty;

    public string GpuHotspotRecordText
    {
        get => _gpuHotspotRecordText;
        private set
        {
            if (_gpuHotspotRecordText == value)
            {
                return;
            }

            _gpuHotspotRecordText = value;
            OnPropertyChanged(nameof(GpuHotspotRecordText));
        }
    }
    private double _undervoltMinClockMhz = 1500;
    private double _undervoltMaxClockMhz = 1950;

    public double UndervoltMinClockMhz
    {
        get => _undervoltMinClockMhz;
        private set
        {
            if (System.Math.Abs(_undervoltMinClockMhz - value) < 0.01)
            {
                return;
            }

            _undervoltMinClockMhz = value;
            OnPropertyChanged(nameof(UndervoltMinClockMhz));
        }
    }

    public double UndervoltMaxClockMhz
    {
        get => _undervoltMaxClockMhz;
        private set
        {
            if (System.Math.Abs(_undervoltMaxClockMhz - value) < 0.01)
            {
                return;
            }

            _undervoltMaxClockMhz = value;
            OnPropertyChanged(nameof(UndervoltMaxClockMhz));
        }
    }

    public string GpuStockBaselineText
    {
        get => _gpuStockBaselineText;
        private set
        {
            if (_gpuStockBaselineText == value)
            {
                return;
            }

            _gpuStockBaselineText = value;
            OnPropertyChanged(nameof(GpuStockBaselineText));
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<HotspotHistoryRow> HotspotHistoryRows { get; } = new();

    public sealed record HotspotHistoryRow(
        string TimeText,
        string HotspotText,
        string GpuText,
        string GpuClockText,
        string MemoryClockText,
        string VramText,
        string PowerText,
        string GameText);

    public string GpuHotspotHistoryNoDataText
    {
        get => _gpuHotspotHistoryNoDataText;
        private set
        {
            if (_gpuHotspotHistoryNoDataText == value)
            {
                return;
            }

            _gpuHotspotHistoryNoDataText = value;
            OnPropertyChanged(nameof(GpuHotspotHistoryNoDataText));
        }
    }

    private double _undervoltTargetVoltageMv = 925;
    private double _undervoltTargetPowerLimitPercent = 100;

    private bool _isAfterburnerInstalled;
    private string _afterburnerInstallPath = string.Empty;

    public bool IsAfterburnerInstalled
    {
        get => _isAfterburnerInstalled;
        private set
        {
            if (_isAfterburnerInstalled == value)
            {
                return;
            }

            _isAfterburnerInstalled = value;
            OnPropertyChanged(nameof(IsAfterburnerInstalled));
        }
    }

    public string AfterburnerInstallPath
    {
        get => _afterburnerInstallPath;
        private set
        {
            if (_afterburnerInstallPath == value)
            {
                return;
            }

            _afterburnerInstallPath = value;
            OnPropertyChanged(nameof(AfterburnerInstallPath));
        }
    }

    private readonly System.Collections.Generic.Dictionary<int, IReadOnlyList<AfterburnerCurvePoint>> _profileCurves = new();
    private readonly System.Collections.Generic.Dictionary<int, AfterburnerProfileInfo> _profilesByNumber = new();
    private int _targetProfileSlot = 9;

    public System.Collections.ObjectModel.ObservableCollection<ProfileRow> ProfileRows { get; } = new();

    public sealed class ProfileRow
    {
        public int Number { get; init; }
        public string SlotText { get; init; } = string.Empty;
        public string Contents { get; init; } = string.Empty;
        public string RoleText { get; init; } = string.Empty;
        public string SaveLabel { get; init; } = string.Empty;
        public string ActivityLabel { get; init; } = string.Empty;
        public bool IsOccupied { get; init; }
        public bool IsGame { get; init; }
        public bool IsIdle { get; init; }
    }

    public System.Collections.ObjectModel.ObservableCollection<ProfileSlotChoice> TargetProfileChoices { get; } = new();

    public System.Collections.ObjectModel.ObservableCollection<ProfileSlotChoice> DeleteProfileChoices { get; } = new();

    private int _deleteProfileSlot;

    public int DeleteProfileSlot
    {
        get => _deleteProfileSlot;
        set
        {
            if (_deleteProfileSlot == value)
            {
                return;
            }

            _deleteProfileSlot = value;
            OnPropertyChanged(nameof(DeleteProfileSlot));
        }
    }

    public sealed record ProfileSlotChoice(int Number, string Label);

    public int TargetProfileSlot
    {
        get => _targetProfileSlot;
        set
        {
            if (_targetProfileSlot == value)
            {
                return;
            }

            _targetProfileSlot = value;
            OnPropertyChanged(nameof(TargetProfileSlot));
        }
    }

    /// <summary>Target clock used when IconGrid generates an undervolt curve.</summary>
    public double UndervoltTargetClockMhz
    {
        get => _undervoltTargetClockMhz;
        set
        {
            var minClock = System.Math.Min(UndervoltMinClockMhz, UndervoltMaxClockMhz);
            var maxClock = System.Math.Max(UndervoltMinClockMhz, UndervoltMaxClockMhz);
            var clamped = System.Math.Clamp(value, minClock, maxClock);
            if (System.Math.Abs(_undervoltTargetClockMhz - clamped) < 0.01)
            {
                return;
            }

            _undervoltTargetClockMhz = clamped;
            OnPropertyChanged(nameof(UndervoltTargetClockMhz));

            // Keep the configured hard clock ceiling in sync while the user drags
            // the slider, so the agent caps at the same clock the chart shows.
            var viewModel = ResolveViewModel();
            if (viewModel != null)
            {
                viewModel.GpuUndervoltTargetClockMhz = (int)System.Math.Round(clamped);
            }

            RefreshTargetCurveLine();
            UpdateProfileChoiceLabels();
            RefreshCurveChart();
        }
    }

    public System.Windows.Media.PointCollection StockCurvePoints
    {
        get => _stockCurvePoints;
        private set
        {
            _stockCurvePoints = value;
            OnPropertyChanged(nameof(StockCurvePoints));
        }
    }

    public System.Windows.Media.PointCollection ActiveCurvePoints
    {
        get => _activeCurvePoints;
        private set
        {
            _activeCurvePoints = value;
            OnPropertyChanged(nameof(ActiveCurvePoints));
        }
    }

    public System.Windows.Media.PointCollection TargetCurvePoints
    {
        get => _targetCurvePoints;
        private set
        {
            _targetCurvePoints = value;
            OnPropertyChanged(nameof(TargetCurvePoints));
        }
    }

    public System.Windows.Media.PointCollection HotspotTrendPoints
    {
        get => _hotspotTrendPoints;
        private set
        {
            _hotspotTrendPoints = value;
            OnPropertyChanged(nameof(HotspotTrendPoints));
        }
    }

    public System.Windows.Media.PointCollection GpuTempTrendPoints
    {
        get => _gpuTempTrendPoints;
        private set
        {
            _gpuTempTrendPoints = value;
            OnPropertyChanged(nameof(GpuTempTrendPoints));
        }
    }

    public string GpuCurveChartNoDataText
    {
        get => _gpuCurveChartNoDataText;
        private set
        {
            _gpuCurveChartNoDataText = value;
            OnPropertyChanged(nameof(GpuCurveChartNoDataText));
        }
    }

    public string GpuCurveChartActiveProfileText
    {
        get => _gpuCurveChartActiveProfileText;
        private set
        {
            _gpuCurveChartActiveProfileText = value;
            OnPropertyChanged(nameof(GpuCurveChartActiveProfileText));
        }
    }

    public string GpuCurveChartTargetText
    {
        get => _gpuCurveChartTargetText;
        private set
        {
            _gpuCurveChartTargetText = value;
            OnPropertyChanged(nameof(GpuCurveChartTargetText));
        }
    }

    public string GpuCurveStockVoltageText
    {
        get => _gpuCurveStockVoltageText;
        private set
        {
            _gpuCurveStockVoltageText = value;
            OnPropertyChanged(nameof(GpuCurveStockVoltageText));
        }
    }

    public string GpuCurveProfileVoltageText
    {
        get => _gpuCurveProfileVoltageText;
        private set
        {
            _gpuCurveProfileVoltageText = value;
            OnPropertyChanged(nameof(GpuCurveProfileVoltageText));
        }
    }

    public string GpuUndervoltLiveVoltageText
    {
        get => _gpuUndervoltLiveVoltageText;
        private set
        {
            _gpuUndervoltLiveVoltageText = value;
            OnPropertyChanged(nameof(GpuUndervoltLiveVoltageText));
        }
    }

    public string GpuUndervoltVoltageSelectionText
    {
        get => _gpuUndervoltVoltageSelectionText;
        private set
        {
            _gpuUndervoltVoltageSelectionText = value;
            OnPropertyChanged(nameof(GpuUndervoltVoltageSelectionText));
        }
    }

    /// <summary>
    /// Voltage where the generated curve's flat segment starts. The range is kept
    /// deliberately narrow (825-1050 mV); going lower than the silicon can handle
    /// causes a driver reset, not a higher score.
    /// </summary>
    public double UndervoltTargetVoltageMv
    {
        get => _undervoltTargetVoltageMv;
        set
        {
            var clamped = System.Math.Clamp(System.Math.Round(value / 5.0) * 5.0, 825, 1050);
            if (System.Math.Abs(_undervoltTargetVoltageMv - clamped) < 0.01)
            {
                return;
            }

            _undervoltTargetVoltageMv = clamped;
            OnPropertyChanged(nameof(UndervoltTargetVoltageMv));

            var viewModel = ResolveViewModel();
            if (viewModel != null)
            {
                viewModel.GpuUndervoltTargetVoltageMv = (int)System.Math.Round(clamped);
            }

            RefreshCurveChart();
        }
    }

    /// <summary>
    /// Afterburner power limit (percent) written into the selected profile when the
    /// curve is saved. 70-100 % keeps it inside the range the driver accepts.
    /// </summary>
    public double UndervoltTargetPowerLimitPercent
    {
        get => _undervoltTargetPowerLimitPercent;
        set
        {
            var clamped = System.Math.Clamp(System.Math.Round(value), 70, 100);
            if (System.Math.Abs(_undervoltTargetPowerLimitPercent - clamped) < 0.01)
            {
                return;
            }

            _undervoltTargetPowerLimitPercent = clamped;
            OnPropertyChanged(nameof(UndervoltTargetPowerLimitPercent));

            var viewModel = ResolveViewModel();
            if (viewModel != null)
            {
                viewModel.GpuUndervoltTargetPowerLimitPercent = (int)System.Math.Round(clamped);
            }
        }
    }

    /// <summary>
    /// Afterburner profiles offered by the two drop-downs. The first entry (number
    /// 0) means "do not switch", so the user's own active profile is left alone.
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<ProfileChoice> AfterburnerProfileChoices { get; } = new();

    public sealed class ProfileChoice : System.ComponentModel.INotifyPropertyChanged
    {
        private string _label;

        public ProfileChoice(int number, string baseLabel, string? label = null)
        {
            Number = number;
            BaseLabel = baseLabel;
            _label = label ?? baseLabel;
        }

        public int Number { get; }

        public string BaseLabel { get; }

        public string Label
        {
            get => _label;
            set
            {
                if (_label == value)
                {
                    return;
                }

                _label = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Label)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Live "measured" line for the GPU undervolt card. Reads the state file the
    /// elevated hardware monitor agent writes, because only that process has NVML
    /// access (the settings UI itself is not elevated).
    /// </summary>
    public string GpuUndervoltStatusText
    {
        get => _gpuUndervoltStatusText;
        private set
        {
            if (_gpuUndervoltStatusText == value)
            {
                return;
            }

            _gpuUndervoltStatusText = value;
            OnPropertyChanged(nameof(GpuUndervoltStatusText));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public HardwareOverview Overview
{
    get => _overview;
    private set
    {
        _overview = value;
        OnPropertyChanged(nameof(Overview));
        OnPropertyChanged(nameof(IsAsusMotherboard));
        OnPropertyChanged(nameof(IsAmdCpu));
        OnPropertyChanged(nameof(IsIntelCpu));
        OnPropertyChanged(nameof(CpuBadgeBackground));
        OnPropertyChanged(nameof(GpuBadgeBackground));
        OnPropertyChanged(nameof(IsNvidiaGpu));
        OnPropertyChanged(nameof(IsNvidiaRtx));
        OnPropertyChanged(nameof(IsNvidiaGtx));
        OnPropertyChanged(nameof(IsIntelGpu));
        OnPropertyChanged(nameof(GpuVendorDisplay));
    }
}

    public bool IsAsusMotherboard =>
        Overview.Board.Manufacturer.Contains("ASUS", System.StringComparison.OrdinalIgnoreCase) ||
        Overview.Board.Manufacturer.Contains("ASUSTeK", System.StringComparison.OrdinalIgnoreCase);

    public bool IsAmdCpu =>
        string.Equals(Overview.Cpu.Vendor, "AMD", System.StringComparison.OrdinalIgnoreCase);
    public bool IsIntelCpu =>
        Overview.Cpu.Vendor.Contains("Intel", System.StringComparison.OrdinalIgnoreCase);
    public bool IsNvidiaGpu =>
        Overview.Gpu.Vendor.Contains("NVIDIA", System.StringComparison.OrdinalIgnoreCase);

    public bool IsIntelGpu =>
        Overview.Gpu.Vendor.Contains("Intel", System.StringComparison.OrdinalIgnoreCase) ||
        Overview.Gpu.Name.Contains("Intel", System.StringComparison.OrdinalIgnoreCase);

public bool IsNvidiaRtx =>
    IsNvidiaGpu && Overview.Gpu.Name.Contains("RTX", System.StringComparison.OrdinalIgnoreCase);

public bool IsNvidiaGtx =>
    IsNvidiaGpu && Overview.Gpu.Name.Contains("GTX", System.StringComparison.OrdinalIgnoreCase);

    public string GpuVendorDisplay =>
        Overview.Gpu.Name.Contains("Intel", System.StringComparison.OrdinalIgnoreCase)
            ? "Intel"
            : Overview.Gpu.Vendor;

    public System.Windows.Media.Brush CpuBadgeBackground => CreateVendorBrush(Overview.Cpu.Vendor);
    public System.Windows.Media.Brush GpuBadgeBackground => CreateVendorBrush(Overview.Gpu.Vendor);

    public HardwarePage()
    {
        InitializeComponent();

        // Reuse the overview from a previous visit (or the startup preload) so
        // the vendor logos can be visible during the very first layout pass
        // instead of waiting for WMI/LibreHardwareMonitor again.
        if (HardwareOverviewCache.Current is { } cachedOverview)
        {
            _overviewLoaded = true;
            Overview = cachedOverview;
        }

        Loaded += HardwarePage_Loaded;
        Unloaded += HardwarePage_Unloaded;
        DataContextChanged += (_, _) =>
        {
            RefreshAfterburnerProfiles();
            RefreshCurveChart();
        };
        _gpuUndervoltTimer.Tick += (_, _) => RefreshGpuUndervoltStatus();
        _gpuUndervoltTimer.Tick += (_, _) => RefreshCurveChart();
    }

    private async void HardwarePage_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Keep this handler subscribed: the settings window reuses the page when
        // the user navigates away and back. Unsubscribing here meant the profile
        // drop-downs were never refreshed on the second visit (they appeared empty
        // after a trip into the game).
        if (!_overviewLoaded)
        {
            _overviewLoaded = true;
            var overview = await HardwareOverviewCache.GetOrLoadAsync();
            if (overview != null)
            {
                Overview = overview;
            }
        }

        RefreshGpuUndervoltStatus();
        RefreshAfterburnerProfiles();
        RefreshCurveChart();
        _gpuUndervoltTimer.Start();
    }

    private void HardwarePage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _gpuUndervoltTimer.Stop();
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
            var hotspotY = height - padding - (System.Math.Clamp(trend[index].HotspotC, 0, maxTemp) / maxTemp) * (height - (2 * padding));
            var edgeY = height - padding - (System.Math.Clamp(trend[index].EdgeC, 0, maxTemp) / maxTemp) * (height - (2 * padding));
            hotspot.Add(new System.Windows.Point(x, hotspotY));
            edge.Add(new System.Windows.Point(x, edgeY));
        }

        HotspotTrendPoints = hotspot;
        GpuTempTrendPoints = edge;
    }

    private static string FormatHotspotHistoryEntry(
        IconGrid.ViewModels.MainViewModel viewModel,
        HotspotHistoryEntry entry)
        => string.Format(
            viewModel.GpuHotspotHistoryFormat,
            entry.HotspotC,
            entry.EdgeC,
            entry.DeltaC,
            entry.CapturedAtUtc.ToLocalTime(),
            string.IsNullOrWhiteSpace(entry.GameName)
                ? viewModel.GpuHotspotHistoryNoGame
                : entry.GameName);

    private async void ClearHotspotHistory_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var viewModel = ResolveViewModel();
            if (viewModel == null)
            {
                return;
            }

            var owner = System.Windows.Window.GetWindow(this);
            var result = owner != null
                ? System.Windows.MessageBox.Show(
                    owner,
                    viewModel.GpuHotspotHistoryClearConfirmFormat,
                    viewModel.GpuHotspotHistoryClearConfirmTitle,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question,
                    System.Windows.MessageBoxResult.No)
                : System.Windows.MessageBox.Show(
                    viewModel.GpuHotspotHistoryClearConfirmFormat,
                    viewModel.GpuHotspotHistoryClearConfirmTitle,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question,
                    System.Windows.MessageBoxResult.No);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            var requestPath = System.IO.Path.Combine(
                new ConfigManager().BaseDirectory,
                "hotspot-history-clear.request");
            System.IO.File.WriteAllText(requestPath, "clear");

            HotspotHistoryRows.Clear();
            GpuHotspotHistoryNoDataText = viewModel.GpuHotspotHistoryNoData;

            await System.Threading.Tasks.Task.Delay(1500);
            if (IsLoaded)
            {
                RefreshGpuUndervoltStatus();
            }
        }
        catch (Exception ex)
        {
            GpuHotspotStatusText = ex.Message;
        }
    }

    /// <summary>
    /// Shows the GPU hot spot (the sensor the driver actually throttles on) and, when
    /// the opt-in alarm is enabled, a friendly warning that points at cooler contact.
    /// </summary>
    private void RefreshHotspot(IconGrid.ViewModels.MainViewModel viewModel)
    {
        try
        {
            var path = System.IO.Path.Combine(new ConfigManager().BaseDirectory, "monitor-state.json");
            if (!System.IO.File.Exists(path))
            {
                GpuHotspotStatusText = viewModel.GpuHotspotNoData;
                GpuHotspotWarningText = string.Empty;
                GpuUndervoltLiveVoltageText = viewModel.GpuUndervoltLiveVoltageNoData;
                HotspotHistoryRows.Clear();
                GpuHotspotHistoryNoDataText = viewModel.GpuHotspotHistoryNoData;
                GpuHotspotRecordText = viewModel.GpuHotspotRecordNoData;
                return;
            }

            var snapshot = JsonConvert.DeserializeObject<HardwareMonitorSnapshot>(
                System.IO.File.ReadAllText(path));

            var entries = snapshot?.HotspotHistory ?? new System.Collections.Generic.List<HotspotHistoryEntry>();

            if (entries.Count > 0)
            {
                GpuHotspotRecordText = FormatHotspotHistoryEntry(viewModel, entries[0]);
            }
            else
            {
                GpuHotspotRecordText = viewModel.GpuHotspotRecordNoData;
            }

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

            GpuUndervoltLiveVoltageText = !string.IsNullOrWhiteSpace(snapshot?.GpuVoltage)
                ? string.Format(viewModel.GpuUndervoltLiveVoltageFormat, snapshot!.GpuVoltage)
                : viewModel.GpuUndervoltLiveVoltageNoData;

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
        catch
        {
            // The hot spot line is informational; never break the page over it.
        }
    }

    private static double? ParseTemperature(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var builder = new System.Text.StringBuilder(text.Length);
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

    private bool ConfirmProfileOverwrite(int profileNumber)
    {
        if (!_profilesByNumber.TryGetValue(profileNumber, out var existing) || existing.IsEmpty)
        {
            return true;
        }

        var viewModel = ResolveViewModel();
        var detail = viewModel?.GpuUndervoltOverwriteExisting ?? "an existing profile";
        if (_profileCurves.TryGetValue(profileNumber, out var curve) && curve.Count > 0)
        {
            var maxClock = (int)System.Math.Round(curve.Max(point => point.ClockMhz));
            var voltageMv = FindCurveVoltageAtClock(curve, maxClock, preferLowest: true);
            if (voltageMv.HasValue)
            {
                detail = $"{maxClock} MHz @ {FormatCurveVoltage(voltageMv.Value)}";
            }
        }

        var message = string.Format(
            viewModel?.GpuUndervoltOverwriteWarningFormat ??
            "Profile {0} already contains {1}. It will be overwritten. Continue?",
            profileNumber,
            detail);

        var title = viewModel?.GpuUndervoltOverwriteWarningTitle ?? "Overwrite profile?";
        var owner = System.Windows.Window.GetWindow(this);
        var result = owner != null
            ? System.Windows.MessageBox.Show(
                owner,
                message,
                title,
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No)
            : System.Windows.MessageBox.Show(
                message,
                title,
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

        return result == System.Windows.MessageBoxResult.Yes;
    }

    private async void GpuUndervoltGenerateCurve_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var targetClock = (int)System.Math.Round(UndervoltTargetClockMhz);
            var generatedProfile = TargetProfileSlot is >= 1 and <= 9 ? TargetProfileSlot : 9;

            if (!ConfirmProfileOverwrite(generatedProfile))
            {
                return;
            }

            var requestPath = System.IO.Path.Combine(
                new ConfigManager().BaseDirectory,
                "gpu-undervolt-request.json");

            var targetVoltage = (int)System.Math.Round(UndervoltTargetVoltageMv);
            var targetPowerLimit = (int)System.Math.Round(UndervoltTargetPowerLimitPercent);

            var payload = JsonConvert.SerializeObject(new
            {
                targetClockMhz = targetClock,
                voltageMv = targetVoltage,
                powerLimitPercent = targetPowerLimit,
                profile = generatedProfile
            });

            System.IO.File.WriteAllText(requestPath, payload);

            var viewModel = ResolveViewModel();
            if (viewModel != null)
            {
                viewModel.GpuUndervoltGameProfile = generatedProfile;
                viewModel.GpuUndervoltTargetClockMhz = targetClock;
                viewModel.GpuUndervoltTargetVoltageMv = targetVoltage;
                viewModel.GpuUndervoltTargetPowerLimitPercent = targetPowerLimit;
                viewModel.GpuUndervoltEnabled = true;
                GpuUndervoltStatusText = viewModel.GpuUndervoltCurveRequested;
            }

            // The elevated agent writes and applies the selected profile
            // asynchronously. Do not rebuild the profile lists before that file
            // write has completed: that used to leave the ComboBoxes blank.
            await System.Threading.Tasks.Task.Delay(3000);
            if (!IsLoaded)
            {
                return;
            }

            RefreshAfterburnerProfiles();
            RefreshGpuUndervoltStatus();
            RefreshCurveChart();

            // One retry in case Afterburner/agent held the profile file a little
            // longer than the first delay.
            if (AfterburnerProfileChoices.All(choice => choice.Number != generatedProfile))
            {
                await System.Threading.Tasks.Task.Delay(3000);
                if (IsLoaded)
                {
                    RefreshAfterburnerProfiles();
                }
            }
        }
        catch (Exception ex)
        {
            GpuUndervoltStatusText = ex.Message;
        }
    }

    private async void GpuUndervoltDeleteProfile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var slot = DeleteProfileSlot;
            var viewModel = ResolveViewModel();

            if (slot <= 0 ||
                !_profilesByNumber.TryGetValue(slot, out var existing) ||
                existing.IsEmpty)
            {
                if (viewModel != null)
                {
                    GpuUndervoltStatusText = viewModel.GpuUndervoltDeleteNoProfile;
                }

                return;
            }

            var detail = viewModel?.GpuUndervoltOverwriteExisting ?? "an existing profile";
            if (_profileCurves.TryGetValue(slot, out var curve) && curve.Count > 0)
            {
                var maxClock = (int)System.Math.Round(curve.Max(point => point.ClockMhz));
                var voltageMv = FindCurveVoltageAtClock(curve, maxClock, preferLowest: true);
                if (voltageMv.HasValue)
                {
                    detail = $"{maxClock} MHz @ {FormatCurveVoltage(voltageMv.Value)}";
                }
            }

            var message = string.Format(
                viewModel?.GpuUndervoltDeleteWarningFormat ??
                "Profile {0} ({1}) will be deleted permanently. Continue?",
                slot,
                detail);
            var title = viewModel?.GpuUndervoltDeleteWarningTitle ?? "Delete profile?";
            var owner = System.Windows.Window.GetWindow(this);
            var result = owner != null
                ? System.Windows.MessageBox.Show(
                    owner,
                    message,
                    title,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No)
                : System.Windows.MessageBox.Show(
                    message,
                    title,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            var requestPath = System.IO.Path.Combine(
                new ConfigManager().BaseDirectory,
                "gpu-undervolt-request.json");

            System.IO.File.WriteAllText(
                requestPath,
                JsonConvert.SerializeObject(new { action = "delete", profile = slot }));

            if (viewModel != null)
            {
                if (viewModel.GpuUndervoltGameProfile == slot)
                {
                    viewModel.GpuUndervoltGameProfile = 0;
                }

                if (viewModel.GpuUndervoltIdleProfile == slot)
                {
                    viewModel.GpuUndervoltIdleProfile = 0;
                }

                GpuUndervoltStatusText = viewModel.GpuUndervoltDeleteRequested;
            }

            await System.Threading.Tasks.Task.Delay(3000);
            if (!IsLoaded)
            {
                return;
            }

            RefreshAfterburnerProfiles();
            RefreshGpuUndervoltStatus();
            RefreshCurveChart();
        }
        catch (Exception ex)
        {
            GpuUndervoltStatusText = ex.Message;
        }
    }

    private void GpuUndervoltRefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RefreshAfterburnerProfiles();
        RefreshGpuUndervoltStatus();
        RefreshCurveChart();
    }

    /// <summary>
    /// Draws the stock V/F curve, the selected profile's curve and the clock target
    /// on a fixed-size canvas. Reading the profile file needs no elevation.
    /// </summary>
    private void RefreshCurveChart()
    {
        var viewModel = ResolveViewModel();

        try
        {
            var controller = new AfterburnerProfileController();
            var stock = controller.ReadStockCurve();
            var activeProfile = (viewModel?.GpuUndervoltGameProfile ?? 0) > 0
                ? viewModel!.GpuUndervoltGameProfile
                : 9;
            var active = controller.ReadCurve(activeProfile);

            StockCurvePoints = BuildCurvePoints(stock);
            ActiveCurvePoints = BuildCurvePoints(active);
            RefreshTargetCurveLine();

            var stockMaxClock = stock.Count > 0
                ? (int)System.Math.Round(stock.Max(point => point.ClockMhz))
                : 1950;
            var stockMaxVoltageMv = FindCurveVoltageAtClock(stock, stockMaxClock, preferLowest: true);

            UndervoltMinClockMhz = 1500;
            UndervoltMaxClockMhz = System.Math.Clamp(
                System.Math.Max(1800, stockMaxClock),
                1500,
                2200);
            if (UndervoltTargetClockMhz > UndervoltMaxClockMhz)
            {
                UndervoltTargetClockMhz = UndervoltMaxClockMhz;
            }

            GpuStockBaselineText = stockMaxVoltageMv.HasValue
                ? string.Format(
                    viewModel?.GpuStockBaselineFormat ?? "Stock baseline: max {0} MHz @ {1}",
                    stockMaxClock,
                    FormatCurveVoltage(stockMaxVoltageMv.Value))
                : string.Empty;

            var targetClock = (int)System.Math.Round(UndervoltTargetClockMhz);
            var stockVoltageMv = FindCurveVoltageAtClock(stock, targetClock, preferLowest: true);
            var profileClock = active.Count > 0
                ? (int)System.Math.Round(active.Max(point => point.ClockMhz))
                : targetClock;
            var profileVoltageMv = FindCurveVoltageAtClock(active, profileClock, preferLowest: true);

            GpuCurveStockVoltageText = stockVoltageMv.HasValue
                ? string.Format(
                    viewModel?.GpuUndervoltStockVoltageFormat ?? "Stock {0} MHz: {1}",
                    targetClock,
                    FormatCurveVoltage(stockVoltageMv.Value))
                : string.Empty;

            GpuCurveProfileVoltageText = profileVoltageMv.HasValue
                ? string.Format(
                    viewModel?.GpuUndervoltProfileVoltageFormat ?? "Profile {0} at {1} MHz: {2}",
                    activeProfile,
                    profileClock,
                    FormatCurveVoltage(profileVoltageMv.Value))
                : string.Empty;

            var selectedVoltageMv = (int)System.Math.Round(_undervoltTargetVoltageMv);
            var gameProfileNumber = viewModel?.GpuUndervoltGameProfile ?? 0;

            if (gameProfileNumber > 0 && profileVoltageMv.HasValue)
            {
                var comparisonClock = System.Math.Min(targetClock, profileClock);
                var comparisonVoltageMv =
                    FindCurveVoltageAtClock(active, comparisonClock, preferLowest: true) ??
                    profileVoltageMv.Value;

                GpuUndervoltVoltageSelectionText = string.Format(
                    viewModel?.GpuUndervoltProfileVoltageCompareFormat ??
                    "Profile {0} at {1} MHz: {2:F0} mV · selected: {3} mV ({4:+#;-#;0} mV)",
                    gameProfileNumber,
                    comparisonClock,
                    comparisonVoltageMv,
                    selectedVoltageMv,
                    selectedVoltageMv - (int)System.Math.Round(comparisonVoltageMv));
            }
            else if (stockVoltageMv.HasValue)
            {
                GpuUndervoltVoltageSelectionText = string.Format(
                    viewModel?.GpuUndervoltVoltageSelectionFormat ??
                    "Stock {0} MHz: {1:F0} mV · selected: {2} mV ({3:+#;-#;0} mV)",
                    targetClock,
                    stockVoltageMv.Value,
                    selectedVoltageMv,
                    selectedVoltageMv - (int)System.Math.Round(stockVoltageMv.Value));
            }
            else
            {
                GpuUndervoltVoltageSelectionText = string.Empty;
            }

            GpuCurveChartNoDataText = stock.Count == 0 && active.Count == 0
                ? viewModel?.GpuCurveChartNoData ?? "No V/F curve found."
                : string.Empty;

            GpuCurveChartActiveProfileText = viewModel == null
                ? $"Profile {activeProfile}"
                : string.Format(viewModel.GpuCurveChartProfileFormat, activeProfile);
        }
        catch
        {
            GpuCurveChartNoDataText = viewModel?.GpuCurveChartNoData ?? "No V/F curve found.";
        }
    }

    private void RefreshTargetCurveLine()
    {
        var y = ClockToY(UndervoltTargetClockMhz);
        TargetCurvePoints = new System.Windows.Media.PointCollection
        {
            new System.Windows.Point(CurveChartPadding, y),
            new System.Windows.Point(CurveChartWidth - CurveChartPadding, y)
        };

        var viewModel = ResolveViewModel();
        GpuCurveChartTargetText = viewModel == null
            ? $"{UndervoltTargetClockMhz:F0} MHz target"
            : string.Format(viewModel.GpuCurveChartTargetFormat, (int)System.Math.Round(UndervoltTargetClockMhz));
    }

    private static System.Windows.Media.PointCollection BuildCurvePoints(
        IReadOnlyList<AfterburnerCurvePoint> curve)
    {
        var points = new System.Windows.Media.PointCollection();
        foreach (var point in curve)
        {
            points.Add(new System.Windows.Point(VoltageToX(point.VoltageMv), ClockToY(point.ClockMhz)));
        }

        return points;
    }

    /// <summary>
    /// Finds the curve voltage for a clock. A flattened undervolt curve contains
    /// many points at the target clock; the lowest voltage is the operating point
    /// the card actually uses, so that is what the profile readout shows.
    /// </summary>
    private static float? FindCurveVoltageAtClock(
        IReadOnlyList<AfterburnerCurvePoint> curve,
        double targetClockMhz,
        bool preferLowest)
    {
        if (curve.Count == 0)
        {
            return null;
        }

        var exact = curve
            .Where(point => System.Math.Abs(point.ClockMhz - targetClockMhz) < 0.5)
            .ToList();

        if (exact.Count > 0)
        {
            return preferLowest
                ? exact.Min(point => point.VoltageMv)
                : exact.Average(point => point.VoltageMv);
        }

        var ordered = curve.OrderBy(point => point.ClockMhz).ToList();
        var lower = ordered.LastOrDefault(point => point.ClockMhz < targetClockMhz);
        var upper = ordered.FirstOrDefault(point => point.ClockMhz > targetClockMhz);

        if (lower != null && upper != null && upper.ClockMhz > lower.ClockMhz)
        {
            var ratio = (targetClockMhz - lower.ClockMhz) / (upper.ClockMhz - lower.ClockMhz);
            return (float)(lower.VoltageMv + (ratio * (upper.VoltageMv - lower.VoltageMv)));
        }

        return (lower ?? upper)?.VoltageMv;
    }

    private static string FormatCurveVoltage(float voltageMv)
        => $"{(voltageMv / 1000f).ToString("F3", System.Globalization.CultureInfo.CurrentCulture)} V";

    private static double VoltageToX(float voltageMv)
        => CurveChartPadding +
           ((System.Math.Clamp(voltageMv, CurveMinVoltageMv, CurveMaxVoltageMv) - CurveMinVoltageMv) /
            (CurveMaxVoltageMv - CurveMinVoltageMv)) *
           (CurveChartWidth - (2 * CurveChartPadding));

    private static double ClockToY(double clockMhz)
        => CurveChartHeight - CurveChartPadding -
           ((System.Math.Clamp(clockMhz, CurveMinClockMhz, CurveMaxClockMhz) - CurveMinClockMhz) /
            (CurveMaxClockMhz - CurveMinClockMhz)) *
           (CurveChartHeight - (2 * CurveChartPadding));

    private void GpuUndervoltProfile_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => RefreshCurveChart();

    /// <summary>
    /// Reads the Afterburner profiles so the drop-downs can list them. Reading the
    /// profile file needs no elevation; applying a profile does, which is why the
    /// elevated agent performs the actual switch.
    /// </summary>
    private void RefreshAfterburnerProfiles()
    {
        var viewModel = ResolveViewModel();
        var noProfileLabel = viewModel?.GpuUndervoltNoProfile ?? "—";

        // Remember the user's choices: clearing ItemsSource makes WPF write the
        // current selection back through the two-way bindings before the new list
        // has been populated.
        var selectedGame = viewModel?.GpuUndervoltGameProfile ?? 0;
        var selectedIdle = viewModel?.GpuUndervoltIdleProfile ?? 0;

        var choices = new System.Collections.Generic.List<ProfileChoice>
        {
            new(0, noProfileLabel)
        };

        var controller = new AfterburnerProfileController();
        IsAfterburnerInstalled = controller.IsAvailable;
        AfterburnerInstallPath = controller.InstallPath ?? string.Empty;

        // Existing installations created the curve before the clock ceiling was
        // stored in the config. Profile 9 is reserved for IconGrid curves, so its
        // highest clock is the target the agent should lock to.
        if (viewModel != null &&
            selectedGame == 9 &&
            viewModel.GpuUndervoltTargetClockMhz <= 0)
        {
            var generatedCurve = controller.ReadCurve(9);
            if (generatedCurve.Count > 0)
            {
                viewModel.GpuUndervoltTargetClockMhz =
                    (int)System.Math.Round(generatedCurve.Max(point => point.ClockMhz));
            }
        }

        if (viewModel != null && viewModel.GpuUndervoltTargetClockMhz > 0)
        {
            UndervoltTargetClockMhz = viewModel.GpuUndervoltTargetClockMhz;
        }

        var targetClock = (int)System.Math.Round(UndervoltTargetClockMhz);
        _profileCurves.Clear();
        _profilesByNumber.Clear();

        try
        {
            var profiles = controller.ReadProfiles();
            foreach (var profile in profiles)
            {
                _profilesByNumber[profile.Number] = profile;
                if (profile.IsEmpty)
                {
                    continue;
                }

                var curve = controller.ReadCurve(profile.Number);
                _profileCurves[profile.Number] = curve;
                choices.Add(new ProfileChoice(
                    profile.Number,
                    profile.DisplayName,
                    BuildProfileLabel(profile.DisplayName, profile.Number, targetClock)));
            }
        }
        catch
        {
            // A missing or unreadable Afterburner install simply leaves the list empty.
        }

        RefreshTargetProfileChoices(viewModel);
        RefreshProfileRows(viewModel);

        // Seed the voltage slider from the config, or from the selected profile's
        // own curve the first time the page is opened after this feature was added.
        if (viewModel != null)
        {
            if (viewModel.GpuUndervoltTargetVoltageMv > 0)
            {
                UndervoltTargetVoltageMv = viewModel.GpuUndervoltTargetVoltageMv;
            }
            else if (selectedGame > 0 &&
                     _profileCurves.TryGetValue(selectedGame, out var selectedCurve) &&
                     selectedCurve.Count > 0)
            {
                var maxClock = (int)System.Math.Round(selectedCurve.Max(point => point.ClockMhz));
                var displayClock = System.Math.Min((int)System.Math.Round(UndervoltTargetClockMhz), maxClock);
                var curveVoltageMv = FindCurveVoltageAtClock(selectedCurve, displayClock, preferLowest: true);
                if (curveVoltageMv.HasValue)
                {
                    viewModel.GpuUndervoltTargetVoltageMv =
                        (int)System.Math.Round(curveVoltageMv.Value);
                    UndervoltTargetVoltageMv = curveVoltageMv.Value;
                }
            }

            UndervoltTargetPowerLimitPercent = viewModel.GpuUndervoltTargetPowerLimitPercent;
        }

        // Keep a selected profile visible even if the file was momentarily locked
        // while the agent wrote profile 9.
        AddProfilePlaceholder(choices, selectedGame);
        AddProfilePlaceholder(choices, selectedIdle);

        AfterburnerProfileChoices.Clear();
        foreach (var choice in choices)
        {
            AfterburnerProfileChoices.Add(choice);
        }

        // Restore the choices after the list exists again.
        if (viewModel != null)
        {
            if (selectedGame > 0)
            {
                viewModel.GpuUndervoltGameProfile = selectedGame;
            }

            if (selectedIdle > 0)
            {
                viewModel.GpuUndervoltIdleProfile = selectedIdle;
            }

            // The ComboBoxes bound SelectedValue before this method populated
            // their ItemsSource. Re-notify so WPF selects the item again now that
            // the profile entries exist.
            viewModel.NotifyGpuUndervoltProfileSelections();
        }

        OnPropertyChanged(nameof(AfterburnerProfileChoices));
        RefreshCurveChart();
    }

    private string BuildProfileLabel(string baseLabel, int profileNumber, int targetClockMhz)
    {
        if (profileNumber <= 0 ||
            !_profileCurves.TryGetValue(profileNumber, out var curve) ||
            curve.Count == 0)
        {
            return baseLabel;
        }

        var maxClock = (int)System.Math.Round(curve.Max(point => point.ClockMhz));
        if (maxClock <= 0)
        {
            return baseLabel;
        }

        // Show the voltage at the selected target when the profile can reach it.
        // A lower-clocked profile (for example profile 9 at 1700) is shown with its
        // own maximum so the list never promises a clock the curve cannot deliver.
        var displayClock = System.Math.Min(targetClockMhz, maxClock);
        var voltageMv = FindCurveVoltageAtClock(curve, displayClock, preferLowest: true);
        if (!voltageMv.HasValue)
        {
            return baseLabel;
        }

        var suffix = maxClock < targetClockMhz
            ? $"{displayClock} MHz (max) @ {FormatCurveVoltage(voltageMv.Value)}"
            : $"{displayClock} MHz @ {FormatCurveVoltage(voltageMv.Value)}";

        return $"{baseLabel} — {suffix}";
    }

    /// <summary>
    /// Refreshes the voltage suffix on every profile entry in place. Rebuilding the
    /// list would clear the ComboBox selections while the user drags the slider.
    /// </summary>
    private void UpdateProfileChoiceLabels()
    {
        var targetClock = (int)System.Math.Round(UndervoltTargetClockMhz);
        foreach (var choice in AfterburnerProfileChoices)
        {
            if (choice.Number <= 0)
            {
                continue;
            }

            choice.Label = BuildProfileLabel(choice.BaseLabel, choice.Number, targetClock);
        }
    }

    private void RefreshTargetProfileChoices(IconGrid.ViewModels.MainViewModel? viewModel)
    {
        var selectedSlot = TargetProfileSlot;
        var choices = new System.Collections.Generic.List<ProfileSlotChoice>();

        for (var slot = 1; slot <= 9; slot++)
        {
            var occupied = _profilesByNumber.TryGetValue(slot, out var profile) && !profile.IsEmpty;
            string label;

            if (!occupied)
            {
                label = string.Format(
                    viewModel?.GpuUndervoltProfileSlotFreeFormat ?? "Profile {0} — free",
                    slot);
            }
            else
            {
                var detail = string.Empty;
                if (_profileCurves.TryGetValue(slot, out var curve) && curve.Count > 0)
                {
                    var maxClock = (int)System.Math.Round(curve.Max(point => point.ClockMhz));
                    var voltageMv = FindCurveVoltageAtClock(curve, maxClock, preferLowest: true);
                    detail = voltageMv.HasValue
                        ? $"{maxClock} MHz @ {FormatCurveVoltage(voltageMv.Value)}"
                        : $"{maxClock} MHz";
                }

                label = string.Format(
                    viewModel?.GpuUndervoltProfileSlotOccupiedFormat ?? "Profile {0} — in use ({1})",
                    slot,
                    detail);
            }

            choices.Add(new ProfileSlotChoice(slot, label));
        }

        TargetProfileChoices.Clear();
        foreach (var choice in choices)
        {
            TargetProfileChoices.Add(choice);
        }

        TargetProfileSlot = selectedSlot is >= 1 and <= 9 ? selectedSlot : 9;

        // Only occupied profiles can be deleted; offer them in their own list.
        var deleteSlot = DeleteProfileSlot;
        var deletable = choices
            .Where(choice => _profilesByNumber.TryGetValue(choice.Number, out var info) && !info.IsEmpty)
            .ToList();

        DeleteProfileChoices.Clear();
        foreach (var choice in deletable)
        {
            DeleteProfileChoices.Add(choice);
        }

        if (deleteSlot > 0 && deletable.Any(choice => choice.Number == deleteSlot))
        {
            DeleteProfileSlot = deleteSlot;
        }
        else
        {
            // Prefer the IconGrid test slot (9) as the default delete target; it is
            // safer than defaulting to the user's known-good fallback profile.
            DeleteProfileSlot = deletable.FirstOrDefault(choice => choice.Number == 9)?.Number
                                ?? deletable.FirstOrDefault()?.Number
                                ?? 0;
        }

        OnPropertyChanged(nameof(DeleteProfileChoices));
        OnPropertyChanged(nameof(TargetProfileChoices));
    }

    private void RefreshProfileRows(IconGrid.ViewModels.MainViewModel? viewModel)
    {
        var rows = new System.Collections.Generic.List<ProfileRow>();
        var gameProfile = viewModel?.GpuUndervoltGameProfile ?? 0;
        var idleProfile = viewModel?.GpuUndervoltIdleProfile ?? 0;

        for (var slot = 1; slot <= 9; slot++)
        {
            var occupied = _profilesByNumber.TryGetValue(slot, out var info) && !info.IsEmpty;
            var contents = viewModel?.GpuUndervoltProfileFree ?? "Free";

            if (occupied)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (_profileCurves.TryGetValue(slot, out var curve) && curve.Count > 0)
                {
                    var maxClock = (int)System.Math.Round(curve.Max(point => point.ClockMhz));
                    var voltageMv = FindCurveVoltageAtClock(curve, maxClock, preferLowest: true);
                    parts.Add(voltageMv.HasValue
                        ? $"{maxClock} MHz @ {FormatCurveVoltage(voltageMv.Value)}"
                        : $"{maxClock} MHz");
                }
                else
                {
                    parts.Add(viewModel?.GpuUndervoltProfileNoCurve ?? "No curve");
                }

                if (info!.PowerLimitPercent > 0)
                {
                    parts.Add($"{info.PowerLimitPercent}% power");
                }

                if (info.FanPercent > 0)
                {
                    parts.Add($"fan {info.FanPercent}%");
                }

                contents = string.Join(" · ", parts);
            }

            var isGame = occupied && gameProfile == slot;
            var isIdle = occupied && idleProfile == slot;
            var role = isGame && isIdle
                ? viewModel?.GpuUndervoltRoleBoth ?? "Game + idle"
                : isGame
                    ? viewModel?.GpuUndervoltRoleGame ?? "Game"
                    : isIdle
                        ? viewModel?.GpuUndervoltRoleIdle ?? "Idle"
                        : viewModel?.GpuUndervoltRoleNone ?? "—";

            rows.Add(new ProfileRow
            {
                Number = slot,
                SlotText = $"Profil {slot}",
                Contents = contents,
                RoleText = role,
                SaveLabel = occupied
                    ? viewModel?.GpuUndervoltProfileOverwrite ?? "Overwrite"
                    : viewModel?.GpuUndervoltProfileSave ?? "Save",
                ActivityLabel = isGame
                    ? viewModel?.GpuUndervoltProfileActive ?? "Active"
                    : viewModel?.GpuUndervoltProfileInactive ?? "Inactive",
                IsOccupied = occupied,
                IsGame = isGame,
                IsIdle = isIdle
            });
        }

        ProfileRows.Clear();
        foreach (var row in rows)
        {
            ProfileRows.Add(row);
        }

        OnPropertyChanged(nameof(ProfileRows));
    }

    private void ProfileRowGame_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as System.Windows.FrameworkElement)?.DataContext is not ProfileRow row)
        {
            return;
        }

        var viewModel = ResolveViewModel();
        if (viewModel == null)
        {
            return;
        }

        viewModel.GpuUndervoltGameProfile =
            viewModel.GpuUndervoltGameProfile == row.Number ? 0 : row.Number;
        viewModel.GpuUndervoltEnabled = true;
        RefreshAfterburnerProfiles();
        RefreshCurveChart();
    }

    private void ProfileRowIdle_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as System.Windows.FrameworkElement)?.DataContext is not ProfileRow row)
        {
            return;
        }

        var viewModel = ResolveViewModel();
        if (viewModel == null)
        {
            return;
        }

        viewModel.GpuUndervoltIdleProfile =
            viewModel.GpuUndervoltIdleProfile == row.Number ? 0 : row.Number;
        RefreshAfterburnerProfiles();
        RefreshCurveChart();
    }

    private void ProfileRowSave_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as System.Windows.FrameworkElement)?.DataContext is not ProfileRow row)
        {
            return;
        }

        TargetProfileSlot = row.Number;
        GpuUndervoltGenerateCurve_Click(sender, new System.Windows.RoutedEventArgs());
    }

    private void ProfileRowDelete_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as System.Windows.FrameworkElement)?.DataContext is not ProfileRow row)
        {
            return;
        }

        DeleteProfileSlot = row.Number;
        GpuUndervoltDeleteProfile_Click(sender, new System.Windows.RoutedEventArgs());
    }

    private static void AddProfilePlaceholder(
        System.Collections.Generic.List<ProfileChoice> choices,
        int profileNumber)
    {
        if (profileNumber <= 0 ||
            choices.Any(choice => choice.Number == profileNumber))
        {
            return;
        }

        choices.Add(new ProfileChoice(profileNumber, $"Profile {profileNumber}"));
    }

    private void RefreshGpuUndervoltStatus()
    {
        var viewModel = ResolveViewModel();
        if (viewModel == null)
        {
            return;
        }

        try
        {
            var statePath = System.IO.Path.Combine(
                new ConfigManager().BaseDirectory,
                "gpu-power-saving-state.json");

            if (!System.IO.File.Exists(statePath))
            {
                GpuUndervoltStatusText = viewModel.GpuUndervoltStatusInactive;
                return;
            }

            var state = JsonConvert.DeserializeObject<GpuPowerSavingState>(
                System.IO.File.ReadAllText(statePath));

            if (state == null)
            {
                GpuUndervoltStatusText = viewModel.GpuUndervoltStatusInactive;
                return;
            }

            if (!state.Available)
            {
                GpuUndervoltStatusText = viewModel.GpuUndervoltStatusUnavailable;
                return;
            }

            var header = state.UndervoltActive
                ? viewModel.GpuUndervoltStatusActive
                : viewModel.GpuUndervoltStatusInactive;

            var profile = state.AfterburnerProfile > 0 ? state.AfterburnerProfile.ToString() : "–";
            var details = string.Format(
                viewModel.GpuUndervoltStatusFormat,
                profile,
                state.SmClockMhz.ToString(),
                state.CurrentTempC,
                System.Math.Round(state.PowerDrawMw / 1000.0),
                state.FanPercent,
                state.TempTargetC);

            GpuUndervoltStatusText = header + System.Environment.NewLine + details;
            RefreshHotspot(viewModel);
        }
        catch
        {
            // Best-effort status display; a failed read must never break the page.
        }
    }

    private void OpenAfterburnerDownload_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.msi.com/Landing/afterburner",
                UseShellExecute = true
            });
        }
        catch
        {
            // Opening the vendor page is best-effort; never break the hardware page.
        }
    }

    private void OpenAmdChipsetDrivers_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.amd.com/en/support/download/drivers.html",
                UseShellExecute = true
            });
        }
        catch
        {
            // Opening the vendor page is best-effort; never break the hardware page.
        }
    }

    private void SearchBoardButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var manufacturer = Overview.Board.Manufacturer;
        var model = Overview.Board.Model;

        if (string.IsNullOrWhiteSpace(manufacturer) || manufacturer == "--")
        {
            manufacturer = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(model) || model == "--")
        {
            model = "motherboard";
        }

        var searchQuery = Uri.EscapeDataString($"{manufacturer} {model}");
        var url = $"https://www.google.com/search?q={searchQuery}";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HardwarePage failed to open search URL: {ex}");
        }
    }

    private static System.Windows.Media.Brush CreateVendorBrush(string? vendor)
    {
        if (string.Equals(vendor, "AMD", System.StringComparison.OrdinalIgnoreCase))
        {
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 88, 12));
        }

        if (string.Equals(vendor, "Intel", System.StringComparison.OrdinalIgnoreCase))
        {
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
        }

        if (string.Equals(vendor, "NVIDIA", System.StringComparison.OrdinalIgnoreCase))
        {
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
        }

        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
    }

    /// <summary>
    /// The page normally receives the MainViewModel as DataContext, but during
    /// the first load/navigation it can briefly be null. The settings window
    /// always has the view model, so fall back to its DataContext.
    /// </summary>
    private IconGrid.ViewModels.MainViewModel? ResolveViewModel()
        => DataContext as IconGrid.ViewModels.MainViewModel
           ?? System.Windows.Window.GetWindow(this)?.DataContext as IconGrid.ViewModels.MainViewModel;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
