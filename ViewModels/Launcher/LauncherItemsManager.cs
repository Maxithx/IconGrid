using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IconGrid.Models;

namespace IconGrid.ViewModels.Launcher
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

        /// <summary>
        /// Moves a launcher item to a different category, repositioning it at the end
        /// of the target category in the flat item list.
        /// </summary>
        public bool MoveItemToCategory(LauncherItem? item, string newCategory)
        {
            if (item == null || string.IsNullOrWhiteSpace(newCategory))
                return false;

            if (string.Equals(item.Category, newCategory, StringComparison.OrdinalIgnoreCase))
                return false;

            var sourceIndex = _items.IndexOf(item);
            if (sourceIndex < 0)
                return false;

            item.Category = newCategory;

            // Find the index just after the last item already in the target category.
            // Walk backwards from the end of the list so we can handle the case where
            // the source item has already been removed conceptually (its Category is
            // now the target category).
            var insertIndex = _items.Count;
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_items[i].Category, newCategory, StringComparison.OrdinalIgnoreCase)
                    && !ReferenceEquals(_items[i], item))
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            _items.Move(sourceIndex, insertIndex > sourceIndex ? insertIndex - 1 : insertIndex);
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
