using System;
using System.Collections.Generic;
using System.Linq;
using IconGrid.Models;
using IconGrid.ViewModels.Settings;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherLayoutState
    {
        private const int MaxSavedLayouts = 10;
        private static readonly string[] BuiltInLayoutPresets = { "Auto", "Grid2x2", "TwoUp", "ThreePane", "ThreePaneMirror" };

        public string LayoutPreset { get; set; } = "Auto";
        public bool LayoutSkipMinimized { get; set; } = true;
        public bool LayoutCurrentMonitorOnly { get; set; } = true;
        public bool LayoutReserveIconGridSlot { get; set; } = true;
        public int LayoutIconGridSlot { get; set; }
        public double LayoutIconGridSlotOffsetX { get; set; } = 0.5;
        public double LayoutIconGridSlotOffsetY { get; set; } = 0.5;
        public Dictionary<string, int> LayoutIconGridSlots { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int[]> LayoutLinks { get; private set; } = new();
        public Dictionary<string, List<CustomLayoutSlot>> SavedLayouts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<CustomLayoutSlot> FavoriteLayoutSlots { get; private set; } = new();

        public IEnumerable<string> SavedLayoutNames => SavedLayouts.Keys.OrderBy(k => k);

        public IEnumerable<string> LayoutPresetChoices =>
            BuiltInLayoutPresets
                .Concat(SavedLayoutNames)
                .Distinct(StringComparer.OrdinalIgnoreCase);

        public bool SetLayoutPreset(string value, out int slotForPreset)
        {
            slotForPreset = LayoutIconGridSlot;
            if (string.Equals(LayoutPreset, value, StringComparison.Ordinal))
                return false;

            LayoutPreset = value;
            slotForPreset = GetSlotForPreset(LayoutPreset);
            return true;
        }

        public bool SetLayoutSkipMinimized(bool value)
        {
            if (LayoutSkipMinimized == value)
                return false;

            LayoutSkipMinimized = value;
            return true;
        }

        public bool SetLayoutCurrentMonitorOnly(bool value)
        {
            if (LayoutCurrentMonitorOnly == value)
                return false;

            LayoutCurrentMonitorOnly = value;
            return true;
        }

        public bool SetLayoutReserveIconGridSlot(bool value)
        {
            if (LayoutReserveIconGridSlot == value)
                return false;

            LayoutReserveIconGridSlot = value;
            return true;
        }

        public bool SetLayoutIconGridSlot(int value)
        {
            var clamped = Math.Max(0, Math.Min(3, value));
            if (LayoutIconGridSlot == clamped)
                return false;

            LayoutIconGridSlot = clamped;
            SaveSlotForPreset(LayoutPreset, clamped);
            return true;
        }

        public bool SetLayoutIconGridSlotOffsetX(double value)
        {
            var clamped = Math.Max(0, Math.Min(1, value));
            if (Math.Abs(LayoutIconGridSlotOffsetX - clamped) < 0.0001)
                return false;

            LayoutIconGridSlotOffsetX = clamped;
            return true;
        }

        public bool SetLayoutIconGridSlotOffsetY(double value)
        {
            var clamped = Math.Max(0, Math.Min(1, value));
            if (Math.Abs(LayoutIconGridSlotOffsetY - clamped) < 0.0001)
                return false;

            LayoutIconGridSlotOffsetY = clamped;
            return true;
        }

        public string GetLayoutPresetToolTip()
        {
            if (string.Equals(LayoutPreset, "Auto", StringComparison.OrdinalIgnoreCase))
                return "Kør auto layout";

            if (TryGetSavedLayout(LayoutPreset, out _, out var canonicalName) ||
                string.Equals(LayoutPreset, "Favorite", StringComparison.OrdinalIgnoreCase))
            {
                return $"Kør {canonicalName ?? LayoutPreset} layout";
            }

            return "Kør standard layout";
        }

        public int GetSlotForPreset(string preset)
        {
            if (string.IsNullOrWhiteSpace(preset))
                return LayoutIconGridSlot;

            return LayoutIconGridSlots.TryGetValue(preset, out var slot)
                ? slot
                : LayoutIconGridSlot;
        }

        public void SaveSlotForPreset(string preset, int slot)
        {
            if (!string.IsNullOrWhiteSpace(preset))
                LayoutIconGridSlots[preset] = slot;
        }

        public void SetLayoutLink(string preset, int[]? slots)
        {
            if (string.IsNullOrWhiteSpace(preset))
                return;

            if (slots == null || slots.Length == 0)
                LayoutLinks.Remove(preset);
            else
                LayoutLinks[preset] = slots;
        }

        public IReadOnlyList<CustomLayoutSlot> GetSavedLayoutSlots(string layoutName)
        {
            if (TryGetSavedLayout(layoutName, out var slots, out _))
                return slots;

            if (string.Equals(layoutName, "Favorite", StringComparison.OrdinalIgnoreCase) && FavoriteLayoutSlots.Any())
                return FavoriteLayoutSlots;

            return Array.Empty<CustomLayoutSlot>();
        }

        public bool TryGetSavedLayout(string layoutName, out List<CustomLayoutSlot> slots, out string? canonicalName)
        {
            slots = new List<CustomLayoutSlot>();
            canonicalName = null;

            if (string.IsNullOrWhiteSpace(layoutName))
                return false;

            foreach (var kvp in SavedLayouts)
            {
                if (string.Equals(kvp.Key, layoutName, StringComparison.OrdinalIgnoreCase))
                {
                    slots = kvp.Value;
                    canonicalName = kvp.Key;
                    return true;
                }
            }

            return false;
        }

        public void SaveLayout(string layoutName, IEnumerable<CustomLayoutSlot> slots)
        {
            if (string.IsNullOrWhiteSpace(layoutName))
                return;

            SavedLayouts[layoutName] = CloneSlots(slots);
            FavoriteLayoutSlots = SavedLayouts[layoutName];
            SaveSlotForPreset(layoutName, LayoutIconGridSlot);
            TrimSavedLayouts();
        }

        public bool RenameLayout(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                return false;

            if (string.Equals(oldName, "Auto", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newName, "Auto", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!SavedLayouts.ContainsKey(oldName) || SavedLayouts.ContainsKey(newName))
                return false;

            var slots = SavedLayouts[oldName];
            SavedLayouts.Remove(oldName);
            SavedLayouts[newName] = slots;

            if (LayoutLinks.TryGetValue(oldName, out var link))
            {
                LayoutLinks.Remove(oldName);
                LayoutLinks[newName] = link;
            }

            if (string.Equals(LayoutPreset, oldName, StringComparison.OrdinalIgnoreCase))
                LayoutPreset = newName;

            return true;
        }

        public bool DeleteLayout(string layoutName)
        {
            if (string.IsNullOrWhiteSpace(layoutName) || string.Equals(layoutName, "Auto", StringComparison.OrdinalIgnoreCase))
                return false;

            var removed = SavedLayouts.Remove(layoutName);
            LayoutLinks.Remove(layoutName);

            if (removed && string.Equals(LayoutPreset, layoutName, StringComparison.OrdinalIgnoreCase))
                LayoutPreset = SavedLayouts.Keys.FirstOrDefault() ?? "Auto";

            return removed;
        }

        public void SetFavoriteLayoutSlots(IEnumerable<CustomLayoutSlot> slots)
        {
            FavoriteLayoutSlots = CloneSlots(slots);
        }

        public void ResetRuntimeCollections()
        {
            LayoutLinks.Clear();
            FavoriteLayoutSlots.Clear();
        }

        public void ApplyConfig(ConfigModel config)
        {
            LayoutPreset = string.IsNullOrWhiteSpace(config.LayoutPreset) ? "Auto" : config.LayoutPreset;
            LayoutSkipMinimized = config.LayoutSkipMinimized;
            LayoutCurrentMonitorOnly = config.LayoutCurrentMonitorOnly;
            LayoutReserveIconGridSlot = config.LayoutReserveIconGridSlot;
            LayoutIconGridSlotOffsetX = Math.Max(0, Math.Min(1, config.LayoutIconGridSlotOffsetX));
            LayoutIconGridSlotOffsetY = Math.Max(0, Math.Min(1, config.LayoutIconGridSlotOffsetY));

            LayoutIconGridSlots.Clear();
            if (config.LayoutIconGridSlots != null)
            {
                foreach (var kvp in config.LayoutIconGridSlots)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key))
                        continue;
                    LayoutIconGridSlots[kvp.Key] = Math.Max(0, Math.Min(3, kvp.Value));
                }
            }

            LayoutIconGridSlot = GetSlotForPreset(LayoutPreset);
            if (LayoutIconGridSlot == 0 && config.LayoutIconGridSlot > 0)
            {
                LayoutIconGridSlot = Math.Max(0, Math.Min(3, config.LayoutIconGridSlot));
                SaveSlotForPreset(LayoutPreset, LayoutIconGridSlot);
            }

            LayoutLinks = config.LayoutLinks ?? new Dictionary<string, int[]>();
            SavedLayouts.Clear();
            if (config.SavedLayouts != null)
            {
                foreach (var kvp in config.SavedLayouts)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null)
                        continue;

                    SavedLayouts[kvp.Key] = CloneSlots(kvp.Value);
                }
            }

            if (SavedLayouts.Count == 0 && config.FavoriteLayoutSlots != null && config.FavoriteLayoutSlots.Any())
                SavedLayouts["Favorit"] = CloneSlots(config.FavoriteLayoutSlots);

            if (string.Equals(LayoutPreset, "Favorite", StringComparison.OrdinalIgnoreCase) && SavedLayouts.Count > 0)
                LayoutPreset = SavedLayouts.Keys.First();

            var hasSavedMatch = SavedLayouts.Keys.Any(name => string.Equals(name, LayoutPreset, StringComparison.OrdinalIgnoreCase));
            var isBuiltInPreset = BuiltInLayoutPresets.Any(preset =>
                string.Equals(LayoutPreset, preset, StringComparison.OrdinalIgnoreCase));

            if (!hasSavedMatch && !isBuiltInPreset)
                LayoutPreset = SavedLayouts.Keys.FirstOrDefault() ?? "Auto";

            FavoriteLayoutSlots = SavedLayouts.Values.FirstOrDefault() ?? new List<CustomLayoutSlot>();
        }

        public void ApplyToSettingsState(MainViewModelSettingsState state)
        {
            state.LayoutPreset = LayoutPreset;
            state.LayoutSkipMinimized = LayoutSkipMinimized;
            state.LayoutCurrentMonitorOnly = LayoutCurrentMonitorOnly;
            state.LayoutIconGridSlot = LayoutIconGridSlot;
            state.LayoutReserveIconGridSlot = LayoutReserveIconGridSlot;
            state.LayoutIconGridSlotOffsetX = LayoutIconGridSlotOffsetX;
            state.LayoutIconGridSlotOffsetY = LayoutIconGridSlotOffsetY;
            state.LayoutIconGridSlots = new Dictionary<string, int>(LayoutIconGridSlots, StringComparer.OrdinalIgnoreCase);
            state.LayoutLinks = LayoutLinks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            state.SavedLayouts = SavedLayouts.ToDictionary(kvp => kvp.Key, kvp => CloneSlots(kvp.Value));
            state.FavoriteLayoutSlots = CloneSlots(FavoriteLayoutSlots);
        }

        public void ResetToDefaults()
        {
            LayoutPreset = "Auto";
            LayoutSkipMinimized = true;
            LayoutCurrentMonitorOnly = true;
            LayoutReserveIconGridSlot = true;
            LayoutIconGridSlot = 0;
            LayoutIconGridSlotOffsetX = 0.5;
            LayoutIconGridSlotOffsetY = 0.5;
            LayoutIconGridSlots.Clear();
            ResetRuntimeCollections();
        }

        public static List<CustomLayoutSlot> CloneSlots(IEnumerable<CustomLayoutSlot> slots)
        {
            return slots?
                .Where(s => s != null && s.Width > 0 && s.Height > 0)
                .Select(s => new CustomLayoutSlot
                {
                    X = s.X,
                    Y = s.Y,
                    Width = s.Width,
                    Height = s.Height
                })
                .ToList() ?? new List<CustomLayoutSlot>();
        }

        private void TrimSavedLayouts()
        {
            while (SavedLayouts.Count > MaxSavedLayouts)
            {
                var oldest = SavedLayouts.Keys.FirstOrDefault();
                if (oldest == null)
                    break;
                SavedLayouts.Remove(oldest);
            }
        }
    }
}
