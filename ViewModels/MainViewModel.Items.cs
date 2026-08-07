using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IconGrid.Models;

namespace IconGrid.ViewModels
{
    public partial class MainViewModel
    {
        public void AddTab(string name)
        {
            _tabsState.AddTab(name);
            SaveSettingsToConfig();
        }

        public void ClearCurrentCategory()
        {
            if (!_itemsManager.ClearCurrentCategory())
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        private List<LauncherItem> GetItemsForSelectedTabSnapshot()
        {
            return GetItemsForTabSnapshot(SelectedTab);
        }

        private List<LauncherItem> GetItemsForTabSnapshot(string? tabName)
        {
            if (string.IsNullOrWhiteSpace(tabName))
                return new List<LauncherItem>();

            return Items
                .Where(i => string.Equals(i.Category, tabName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void RenameTab(string oldName, string newName)
        {
            if (!_tabsState.RenameTab(oldName, newName))
                return;

            foreach (var item in Items.Where(i => i.Category == oldName))
            {
                item.Category = newName;
            }

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
            SaveSettingsToConfig();
        }

        public void RemoveTab(string tabName)
        {
            if (!_tabsState.RemoveTab(tabName))
                return;

            // Fjern alle items under det tab
            var toRemove = Items.Where(i => i.Category == tabName).ToList();
            foreach (var item in toRemove)
            {
                Items.Remove(item);
            }

            // Skift til første tab hvis nødvendigt
            if (!Tabs.Contains(SelectedTab) && Tabs.Any())
            {
                SelectedTab = Tabs[0];
            }

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
            SaveSettingsToConfig();
        }

        public void RemoveItem(LauncherItem item)
        {
            if (!_itemsManager.RemoveItem(item))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        public void RenameItem(LauncherItem item, string newName)
        {
            if (!_itemsManager.RenameItem(item, newName))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        /// <summary>
        /// Updates the icon path/index for a launcher item and persists it.
        /// </summary>
        public void UpdateItemIcon(LauncherItem item, string iconPath, int iconIndex)
        {
            if (!_itemIconManager.UpdateItemIcon(item, iconPath, iconIndex))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        /// <summary>
        /// Håndterer filer droppet fra Explorer ind i content-området.
        /// </summary>
        public void MoveItemWithinCategory(LauncherItem source, LauncherItem? target, bool insertAfter)
        {
            if (!_itemsManager.MoveItemWithinCategory(source, target, insertAfter))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        public void HandleFileDrop(string[] files)
        {
            if (!_shortcutManager.HandleFileDrop(files, SelectedTab))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        public void LaunchItem(LauncherItem item)
        {
            _itemLaunchManager.LaunchItem(item);
        }

        private void RememberFpsTarget(LauncherItem item, FpsTargetConfig launchSession)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
            {
                return;
            }

            // Only save the FPS target if the launched process actually started and is still alive.
            if (launchSession != null)
            {
                if (launchSession.RootProcessId.HasValue && launchSession.RootProcessId.Value > 0)
                {
                    if (!ProcessIsAlive(launchSession.RootProcessId.Value))
                    {
                        Debug.WriteLine("FPS target process already exited; not persisting stale target.");
                        return;
                    }
                }
                else
                {
                    // No RootProcessId; try to find the process by executable name.
                    var processName = Path.GetFileNameWithoutExtension(launchSession.ExecutableName ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(processName))
                    {
                        var found = false;
                        try
                        {
                            foreach (var process in Process.GetProcessesByName(processName))
                            {
                                found = true;
                                break;
                            }
                        }
                        catch { }
                        if (!found)
                        {
                            Debug.WriteLine("FPS target process not found; not persisting stale target.");
                            return;
                        }
                    }
                }
            }

            _fpsTarget = launchSession ?? new FpsTargetConfig
            {
                DisplayName = item.DisplayName,
                LauncherPath = item.Path,
                ResolvedExecutablePath = NormalizeExecutablePath(item.Path),
                ExecutableName = Path.GetFileName(item.Path),
                Arguments = item.Arguments,
                WorkingDirectory = Path.GetDirectoryName(item.Path),
                LaunchCapturedFileTimeUtc = DateTime.UtcNow.ToFileTimeUtc()
            };

            SaveSettingsToConfig();

            // Notify listeners (MainWindow) that a game was launched so they can
            // apply the configured auto-behavior (show overlay / hide launcher).
            GameLaunched?.Invoke();
        }

        private static bool ProcessIsAlive(int pid)
        {
            if (pid <= 0)
                return false;
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeExecutablePath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        /// <summary>
        /// Creates and persists a custom shortcut entry.
        /// </summary>
        public LauncherItem CreateCustomShortcut(string displayName, string targetPath, string category, string? arguments = null, string? iconPath = null, int iconIndex = 0)
        {
            var item = _shortcutManager.CreateCustomShortcut(displayName, targetPath, category, arguments, iconPath, iconIndex);
            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
            return item;
        }

        /// <summary>
        /// Applies an icon and persists.
        /// </summary>
        public void SetIcon(LauncherItem item, string iconPath, int iconIndex = 0)
        {
            UpdateItemIcon(item, iconPath, iconIndex);
        }

        // ---------- Persistence (simple JSON) ----------

        public void SaveItemsToFile()
        {
            _itemsPersistence.Save(Items);
        }

        private void LoadItemsFromFile()
        {
            var list = _itemsPersistence.Load();
            if (list.Count == 0)
                return;

            Items.Clear();
            foreach (var it in list)
            {
                it.RefreshIcon();
                Items.Add(it);
            }

            OnPropertyChanged(nameof(CurrentItems));
        }

        private void MaybeMigrateItemsFromLegacy()
        {
            _itemsPersistence.MigrateLegacyIfNeeded();
        }
    }
}