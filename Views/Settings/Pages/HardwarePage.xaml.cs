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

    private readonly System.Windows.Threading.DispatcherTimer _gpuUndervoltTimer =
        new() { Interval = System.TimeSpan.FromSeconds(1) };

    private string _gpuUndervoltStatusText = string.Empty;

    private double _undervoltTargetClockMhz = 1800;

    private string _benchmarkLabel = "Map test";
    private int _benchmarkSeconds = 60;
    private string _gpuBenchmarkStatusText = string.Empty;
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
    private DateTime _benchmarkResultsStampUtc = DateTime.MinValue;

    /// <summary>Duration options offered by the benchmark drop-down (seconds).</summary>
    public int[] BenchmarkDurationChoices { get; } = { 30, 60, 120 };

    public System.Collections.ObjectModel.ObservableCollection<string> BenchmarkResults { get; } = new();

    public string BenchmarkLabel
    {
        get => _benchmarkLabel;
        set
        {
            if (_benchmarkLabel == value)
            {
                return;
            }

            _benchmarkLabel = value;
            OnPropertyChanged(nameof(BenchmarkLabel));
        }
    }

    public int BenchmarkSeconds
    {
        get => _benchmarkSeconds;
        set
        {
            if (_benchmarkSeconds == value)
            {
                return;
            }

            _benchmarkSeconds = value;
            OnPropertyChanged(nameof(BenchmarkSeconds));
        }
    }

    public string GpuBenchmarkStatusText
    {
        get => _gpuBenchmarkStatusText;
        private set
        {
            if (_gpuBenchmarkStatusText == value)
            {
                return;
            }

            _gpuBenchmarkStatusText = value;
            OnPropertyChanged(nameof(GpuBenchmarkStatusText));
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
    private string _gpuCurveChartNoDataText = string.Empty;
    private string _gpuCurveChartActiveProfileText = string.Empty;
    private string _gpuCurveChartTargetText = string.Empty;

    /// <summary>Target clock used when IconGrid generates an undervolt curve.</summary>
    public double UndervoltTargetClockMhz
    {
        get => _undervoltTargetClockMhz;
        set
        {
            var clamped = System.Math.Clamp(value, 1500, 1950);
            if (System.Math.Abs(_undervoltTargetClockMhz - clamped) < 0.01)
            {
                return;
            }

            _undervoltTargetClockMhz = clamped;
            OnPropertyChanged(nameof(UndervoltTargetClockMhz));
            RefreshTargetCurveLine();
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

    /// <summary>
    /// Afterburner profiles offered by the two drop-downs. The first entry (number
    /// 0) means "do not switch", so the user's own active profile is left alone.
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<ProfileChoice> AfterburnerProfileChoices { get; } = new();

    public sealed record ProfileChoice(int Number, string Label);

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
        Loaded += HardwarePage_Loaded;
        Unloaded += HardwarePage_Unloaded;
        _gpuUndervoltTimer.Tick += (_, _) => RefreshGpuUndervoltStatus();
        _gpuUndervoltTimer.Tick += (_, _) => RefreshCurveChart();
    }

    private async void HardwarePage_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Loaded -= HardwarePage_Loaded;
        Overview = await Task.Run(HardwareInfoProvider.LoadOverview);

        RefreshGpuUndervoltStatus();
        RefreshAfterburnerProfiles();
        RefreshCurveChart();
        _gpuUndervoltTimer.Start();
    }

    private void HardwarePage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _gpuUndervoltTimer.Stop();
    }

    private void GpuBenchmarkRunSingle_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        StartBenchmark(new System.Collections.Generic.List<int>());
    }

    private void GpuBenchmarkRunAb_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Profile 1 = the user's stock/mild profile, profile 9 = the IconGrid curve.
        StartBenchmark(new System.Collections.Generic.List<int> { 1, 9 });
    }

    private void StartBenchmark(System.Collections.Generic.List<int> profiles)
    {
        try
        {
            var requestPath = System.IO.Path.Combine(
                new ConfigManager().BaseDirectory,
                "gpu-benchmark-request.json");

            var payload = JsonConvert.SerializeObject(new
            {
                label = string.IsNullOrWhiteSpace(BenchmarkLabel) ? "Benchmark" : BenchmarkLabel,
                seconds = BenchmarkSeconds,
                profiles
            });

            System.IO.File.WriteAllText(requestPath, payload);
            GpuBenchmarkStatusText = "Test request sent to the hardware monitor agent…";
        }
        catch (Exception ex)
        {
            GpuBenchmarkStatusText = ex.Message;
        }
    }

    /// <summary>
    /// Mirrors the agent's live benchmark status and the last saved results into the
    /// page. Called from the same one-second timer as the undervolt status.
    /// </summary>
    private void RefreshBenchmarkStatus(GpuPowerSavingState state)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(state.BenchmarkStatus))
        {
            parts.Add(state.BenchmarkStatus!);
        }

        if (!string.IsNullOrWhiteSpace(state.BenchmarkProgress))
        {
            parts.Add(state.BenchmarkProgress!);
        }

        GpuBenchmarkStatusText = string.Join(" · ", parts);

        try
        {
            var path = System.IO.Path.Combine(
                new ConfigManager().BaseDirectory,
                "gpu-benchmark-results.json");

            if (!System.IO.File.Exists(path))
            {
                return;
            }

            var stamp = System.IO.File.GetLastWriteTimeUtc(path);
            if (stamp == _benchmarkResultsStampUtc)
            {
                return;
            }

            _benchmarkResultsStampUtc = stamp;

            var summaries = JsonConvert.DeserializeObject<System.Collections.Generic.List<BenchmarkSummary>>(
                System.IO.File.ReadAllText(path));

            BenchmarkResults.Clear();

            foreach (var summary in (summaries ?? new System.Collections.Generic.List<BenchmarkSummary>()).AsEnumerable().Reverse())
            {
                BenchmarkResults.Add(
                    $"{summary.Label} — {summary.CompletedAtUtc.ToLocalTime():dd-MM HH:mm}");

                foreach (var pass in summary.Passes)
                {
                    BenchmarkResults.Add(
                        $"   P{pass.Profile}: {pass.AvgFps:F1} fps · p99 {pass.P99FrameMs:F1} ms · " +
                        $"{pass.Over20MsPercent:F1}% > 20 ms · max {pass.MaxTempC} °C · " +
                        $"{pass.AvgPowerW:F0} W · {pass.AvgClockMhz:F0} MHz");
                }
            }
        }
        catch
        {
            // Results are informative only; never break the page over them.
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
                return;
            }

            var snapshot = JsonConvert.DeserializeObject<HardwareMonitorSnapshot>(
                System.IO.File.ReadAllText(path));

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
            var shouldWarn = viewModel.GpuHotspotAlarmEnabled &&
                             ((hotspot.HasValue && hotspot.Value >= viewModel.GpuHotspotAlarmThresholdC) || delta >= 15);

            GpuHotspotWarningText = shouldWarn
                ? string.Format(viewModel.GpuHotspotWarning, snapshot.GpuHotspot, delta.ToString("F1"))
                : string.Empty;
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

    private void GpuUndervoltGenerateCurve_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var targetClock = (int)System.Math.Round(UndervoltTargetClockMhz);

            // Profile 9 is reserved for curves generated by IconGrid, so the user's
            // own profiles (1-5) are never overwritten.
            const int generatedProfile = 9;

            var requestPath = System.IO.Path.Combine(
                new ConfigManager().BaseDirectory,
                "gpu-undervolt-request.json");

            var payload = JsonConvert.SerializeObject(new
            {
                targetClockMhz = targetClock,
                profile = generatedProfile
            });

            System.IO.File.WriteAllText(requestPath, payload);

            if (DataContext is IconGrid.ViewModels.MainViewModel viewModel)
            {
                viewModel.GpuUndervoltGameProfile = generatedProfile;
                viewModel.GpuUndervoltEnabled = true;
                GpuUndervoltStatusText = viewModel.GpuUndervoltCurveRequested;
            }

            RefreshAfterburnerProfiles();
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
        var viewModel = DataContext as IconGrid.ViewModels.MainViewModel;

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

        var viewModel = DataContext as IconGrid.ViewModels.MainViewModel;
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
        var noProfileLabel = (DataContext as IconGrid.ViewModels.MainViewModel)?.GpuUndervoltNoProfile
                             ?? "—";

        AfterburnerProfileChoices.Clear();
        AfterburnerProfileChoices.Add(new ProfileChoice(0, noProfileLabel));

        try
        {
            foreach (var profile in new AfterburnerProfileController().ReadProfiles())
            {
                if (profile.IsEmpty)
                {
                    continue;
                }

                AfterburnerProfileChoices.Add(new ProfileChoice(profile.Number, profile.DisplayName));
            }
        }
        catch
        {
            // A missing or unreadable Afterburner install simply leaves the list empty.
        }

        OnPropertyChanged(nameof(AfterburnerProfileChoices));
        RefreshCurveChart();
    }

    private void RefreshGpuUndervoltStatus()
    {
        if (DataContext is not IconGrid.ViewModels.MainViewModel viewModel)
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
            RefreshBenchmarkStatus(state);
            RefreshHotspot(viewModel);
        }
        catch
        {
            // Best-effort status display; a failed read must never break the page.
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

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
