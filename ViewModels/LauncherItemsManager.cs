using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IconGrid.Models;

namespace IconGrid.ViewModels
{
    public class LauncherItemsManager
    {
        private readonly ObservableCollection<LauncherItem> _items;
        private readonly Func<string?> _selectedTabAccessor;

        public LauncherItemsManager(ObservableCollection<LauncherItem> items, Func<string?> selectedTabAccessor)
        {
            _items = items;
            _selectedTabAccessor = selectedTabAccessor;
        }

        public bool ClearCurrentCategory()
        {
            var selectedTab = _selectedTabAccessor();
            if (string.IsNullOrWhiteSpace(selectedTab))
                return false;

            var itemsToRemove = GetItemsForTabSnapshot(selectedTab);
            if (itemsToRemove.Count == 0)
                return false;

            foreach (var item in itemsToRemove)
            {
                _items.Remove(item);
            }

            return true;
        }

        public bool RemoveItem(LauncherItem? item)
        {
            if (item == null)
                return false;

            return _items.Remove(item);
        }

        public bool RenameItem(LauncherItem? item, string newName)
        {
            if (item == null || string.IsNullOrWhiteSpace(newName))
                return false;

            item.DisplayName = newName;
            return true;
        }

        public bool MoveItemWithinCategory(LauncherItem? source, LauncherItem? target, bool insertAfter)
        {
            if (source == null || target == null)
                return false;

            if (!string.Equals(source.Category, target.Category, StringComparison.OrdinalIgnoreCase))
                return false;

            var sourceIndex = _items.IndexOf(source);
            var targetIndex = _items.IndexOf(target);

            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
                return false;

            var destinationIndex = insertAfter
                ? (sourceIndex < targetIndex ? targetIndex : targetIndex + 1)
                : targetIndex;

            if (destinationIndex < 0)
                destinationIndex = 0;
            if (destinationIndex >= _items.Count)
                destinationIndex = _items.Count - 1;

            _items.Move(sourceIndex, destinationIndex);
            return true;
        }

        private List<LauncherItem> GetItemsForTabSnapshot(string? tabName)
        {
            if (string.IsNullOrWhiteSpace(tabName))
                return new List<LauncherItem>();

            return _items
                .Where(i => string.Equals(i.Category, tabName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
