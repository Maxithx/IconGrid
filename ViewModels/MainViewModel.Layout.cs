using System;
using System.Collections.Generic;
using IconGrid.Models;

namespace IconGrid.ViewModels
{
    public partial class MainViewModel
    {
        public string LayoutPreset
        {
            get => _layoutState.LayoutPreset;
            set
            {
                if (!_layoutState.SetLayoutPreset(value, out var slotForPreset))
                    return;

                _layoutState.LayoutIconGridSlot = slotForPreset;
                NotifyLayoutPresetChanged();
                OnPropertyChanged(nameof(LayoutIconGridSlot));
                SaveSettingsToConfig();
            }
        }

        public string LayoutPresetToolTip
        {
            get
            {
                if (string.Equals(_layoutState.LayoutPreset, "Auto", StringComparison.OrdinalIgnoreCase))
                    return "Kør auto layout";

                if (TryGetSavedLayout(_layoutState.LayoutPreset, out _, out var canonical) || string.Equals(_layoutState.LayoutPreset, "Favorite", StringComparison.OrdinalIgnoreCase))
                    return $"Kør {canonical ?? _layoutState.LayoutPreset} layout";

                return "Kør standard layout";
            }
        }

        public bool LayoutSkipMinimized
        {
            get => _layoutState.LayoutSkipMinimized;
            set
            {
                if (!_layoutState.SetLayoutSkipMinimized(value))
                    return;

                PersistLayoutPropertyChange(nameof(LayoutSkipMinimized));
            }
        }

        public bool LayoutCurrentMonitorOnly
        {
            get => _layoutState.LayoutCurrentMonitorOnly;
            set
            {
                if (!_layoutState.SetLayoutCurrentMonitorOnly(value))
                    return;

                PersistLayoutPropertyChange(nameof(LayoutCurrentMonitorOnly));
            }
        }

        public bool LayoutReserveIconGridSlot
        {
            get => _layoutState.LayoutReserveIconGridSlot;
            set
            {
                if (!_layoutState.SetLayoutReserveIconGridSlot(value))
                    return;

                PersistLayoutPropertyChange(nameof(LayoutReserveIconGridSlot));
            }
        }

        public int LayoutIconGridSlot
        {
            get => _layoutState.LayoutIconGridSlot;
            set
            {
                if (!_layoutState.SetLayoutIconGridSlot(value))
                    return;

                PersistLayoutPropertyChange(nameof(LayoutIconGridSlot));
            }
        }

        public double LayoutIconGridSlotOffsetX
        {
            get => _layoutState.LayoutIconGridSlotOffsetX;
            set
            {
                if (!_layoutState.SetLayoutIconGridSlotOffsetX(value))
                    return;

                PersistLayoutPropertyChange(nameof(LayoutIconGridSlotOffsetX));
            }
        }

        public double LayoutIconGridSlotOffsetY
        {
            get => _layoutState.LayoutIconGridSlotOffsetY;
            set
            {
                if (!_layoutState.SetLayoutIconGridSlotOffsetY(value))
                    return;

                PersistLayoutPropertyChange(nameof(LayoutIconGridSlotOffsetY));
            }
        }

        public IReadOnlyDictionary<string, int[]> LayoutLinks => _layoutState.LayoutLinks;

        public IReadOnlyDictionary<string, List<CustomLayoutSlot>> SavedLayouts => _layoutState.SavedLayouts;

        public IEnumerable<string> SavedLayoutNames => _layoutState.SavedLayoutNames;

        public IEnumerable<string> LayoutPresetChoices => _layoutState.LayoutPresetChoices;

        public IReadOnlyList<CustomLayoutSlot> FavoriteLayoutSlots => _layoutState.FavoriteLayoutSlots;

        public void SetLayoutLink(string preset, int[]? slots)
        {
            _layoutState.SetLayoutLink(preset, slots);
            SaveSettingsToConfig();
        }

        public IReadOnlyList<CustomLayoutSlot> GetSavedLayoutSlots(string layoutName)
        {
            return _layoutState.GetSavedLayoutSlots(layoutName);
        }

        public bool TryGetSavedLayout(string layoutName, out List<CustomLayoutSlot> slots, out string? canonicalName)
        {
            return _layoutState.TryGetSavedLayout(layoutName, out slots, out canonicalName);
        }

        public void SaveLayout(string layoutName, IEnumerable<CustomLayoutSlot> slots)
        {
            _layoutState.LayoutIconGridSlot = LayoutIconGridSlot;
            _layoutState.SaveLayout(layoutName, slots);
            PersistLayoutCollectionChange(notifyPreset: false);
        }

        public bool RenameLayout(string oldName, string newName)
        {
            if (!_layoutState.RenameLayout(oldName, newName))
                return false;

            PersistLayoutCollectionChange(notifyPreset: true);
            return true;
        }

        public bool DeleteLayout(string layoutName)
        {
            var removed = _layoutState.DeleteLayout(layoutName);
            if (!removed)
                return false;

            PersistLayoutCollectionChange(notifyPreset: true);
            return true;
        }

        public void SetFavoriteLayoutSlots(IEnumerable<CustomLayoutSlot> slots)
        {
            _layoutState.SetFavoriteLayoutSlots(slots);
            SaveSettingsToConfig();
            OnPropertyChanged(nameof(FavoriteLayoutSlots));
        }

        private void NotifyLayoutPresetChanged()
        {
            OnPropertyChanged(nameof(LayoutPreset));
            OnPropertyChanged(nameof(LayoutPresetToolTip));
        }

        private void PersistLayoutPropertyChange(string propertyName)
        {
            OnPropertyChanged(propertyName);
            SaveSettingsToConfig();
        }

        private void NotifyLayoutCollectionsChanged()
        {
            OnPropertyChanged(nameof(FavoriteLayoutSlots));
            OnPropertyChanged(nameof(SavedLayouts));
            OnPropertyChanged(nameof(SavedLayoutNames));
            OnPropertyChanged(nameof(LayoutPresetChoices));
        }

        private void PersistLayoutCollectionChange(bool notifyPreset)
        {
            if (notifyPreset)
                NotifyLayoutPresetChanged();

            NotifyLayoutCollectionsChanged();
            SaveSettingsToConfig();
        }

        private void NotifyAllLayoutPropertiesChanged()
        {
            NotifyLayoutPresetChanged();
            OnPropertyChanged(nameof(LayoutSkipMinimized));
            OnPropertyChanged(nameof(LayoutCurrentMonitorOnly));
            OnPropertyChanged(nameof(LayoutReserveIconGridSlot));
            OnPropertyChanged(nameof(LayoutIconGridSlot));
            NotifyLayoutCollectionsChanged();
        }
    }
}