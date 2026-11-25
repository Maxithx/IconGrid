using System.Collections.Generic;
using System.Linq;

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

    }
}
