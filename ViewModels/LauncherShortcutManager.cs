using System.Collections.Generic;
using System.Collections.ObjectModel;
using IconGrid.Helpers;
using IconGrid.Models;

namespace IconGrid.ViewModels
{
    public class LauncherShortcutManager
    {
        private readonly ObservableCollection<LauncherItem> _items;
        private readonly LauncherItemIconManager _itemIconManager;

        public LauncherShortcutManager(
            ObservableCollection<LauncherItem> items,
            LauncherItemIconManager itemIconManager)
        {
            _items = items;
            _itemIconManager = itemIconManager;
        }

        public bool HandleFileDrop(IEnumerable<string>? files, string category)
        {
            if (files == null)
                return false;

            var added = false;
            foreach (var file in files)
            {
                if (!ShortcutHelper.IsSupportedLauncherFile(file))
                    continue;

                var launcher = ShortcutHelper.CreateLauncherItemFromFile(file, category);
                if (launcher == null)
                    continue;

                launcher.RefreshIcon();
                _itemIconManager.EnsureItemIconLoaded(launcher);
                _items.Add(launcher);
                added = true;
            }

            return added;
        }

        public LauncherItem CreateCustomShortcut(
            string displayName,
            string targetPath,
            string category,
            string? arguments = null,
            string? iconPath = null,
            int iconIndex = 0)
        {
            var item = new LauncherItem
            {
                DisplayName = displayName,
                Path = targetPath,
                Arguments = arguments,
                Category = category,
                IconPath = iconPath ?? targetPath,
                IconIndex = iconIndex
            };

            _items.Add(item);
            return item;
        }
    }
}
