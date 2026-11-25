using System.Collections.Generic;
using System.Linq;
using IconGrid.Helpers.Hardware;

namespace IconGrid.Views
{
    public partial class HardwarePage
    {
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
    }
}
