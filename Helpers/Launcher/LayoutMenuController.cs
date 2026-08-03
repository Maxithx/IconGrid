using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IconGrid.Models;
using IconGrid.ViewModels;
using IconGrid.Views;
using Microsoft.VisualBasic;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the launcher layout-menu behavior that previously lived in MainWindow.xaml.cs:
    /// layout menu population and checks, save/rename/delete layout flows, layout-card
    /// selection highlighting, and layout-slot/link button handling.
    /// Keeps the launcher shell focused on window lifetime and UI composition.
    /// </summary>
    public sealed class LayoutMenuController
    {
        private readonly Window _window;
        private readonly MainViewModel _viewModel;
        private readonly Func<IntPtr> _getWindowHandle;
        private readonly Action<string> _log;
        private readonly Action<GamingOverlayLayout> _showGamingOverlay;
        private readonly Action _refreshLayoutCards;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        public LayoutMenuController(
            Window window,
            MainViewModel viewModel,
            Func<IntPtr> getWindowHandle,
            Action<string> log,
            Action<GamingOverlayLayout> showGamingOverlay,
            Action refreshLayoutCards)
        {
            _window = window;
            _viewModel = viewModel;
            _getWindowHandle = getWindowHandle;
            _log = log;
            _showGamingOverlay = showGamingOverlay;
            _refreshLayoutCards = refreshLayoutCards;
        }

        public void PopulateLayoutMenu(ItemCollection? menuItems)
        {
            if (menuItems == null) return;

            menuItems.Clear();

            var gamingOverlayMenu = new System.Windows.Controls.MenuItem
            {
                Header = "Gaming overlay"
            };

            var gamingHorizontal = new System.Windows.Controls.MenuItem
            {
                Header = "Open horizontal"
            };
            gamingHorizontal.Click += (_, _) => _showGamingOverlay(GamingOverlayLayout.Horizontal);

            var gamingVertical = new System.Windows.Controls.MenuItem
            {
                Header = "Open vertical"
            };
            gamingVertical.Click += (_, _) => _showGamingOverlay(GamingOverlayLayout.Vertical);

            gamingOverlayMenu.Items.Add(gamingHorizontal);
            gamingOverlayMenu.Items.Add(gamingVertical);
            menuItems.Add(gamingOverlayMenu);
            menuItems.Add(new Separator());

            // Add the static "Auto" option
            var autoItem = new System.Windows.Controls.MenuItem
            {
                Header = "Auto",
                Tag = "Auto",
                IsCheckable = true
            };
            autoItem.Click += LayoutPresetMenuItem_Click;
            menuItems.Add(autoItem);

            if (_viewModel.SavedLayoutNames.Any())
            {
                menuItems.Add(new Separator());
            }

            // Add each saved layout with its own context menu
            foreach (var name in _viewModel.SavedLayoutNames)
            {
                var containerItem = new System.Windows.Controls.MenuItem
                {
                    Header = name,
                    Tag = name, // Tag for UpdateLayoutMenuChecks to find and potentially style the container
                };

                var selectItem = new System.Windows.Controls.MenuItem
                {
                    Header = _viewModel.SelectLabel,
                    Tag = name, // Tag for the click handler
                    IsCheckable = true
                };
                selectItem.Click += LayoutPresetMenuItem_Click;
                containerItem.Items.Add(selectItem);

                containerItem.Items.Add(new Separator());

                var renameItem = new System.Windows.Controls.MenuItem { Header = _viewModel.RenameLabel, Tag = name };
                renameItem.Click += RenameLayoutMenuItem_Click;
                containerItem.Items.Add(renameItem);

                var deleteItem = new System.Windows.Controls.MenuItem { Header = _viewModel.RemoveLabel, Tag = name };
                deleteItem.Click += DeleteLayoutMenuItem_Click;
                containerItem.Items.Add(deleteItem);

                menuItems.Add(containerItem);
            }

            if (_viewModel.SavedLayoutNames.Any())
            {
                menuItems.Add(new Separator());
            }

            var saveItem = new System.Windows.Controls.MenuItem { Header = _viewModel.LayoutSaveAsText };
            saveItem.Click += SaveLayoutAsMenuItem_Click;
            menuItems.Add(saveItem);
        }

        public void UpdateLayoutMenuChecks(ItemCollection? menuItems)
        {
            if (menuItems == null) return;

            void UpdateChecks(ItemCollection items)
            {
                foreach (var item in items.OfType<System.Windows.Controls.MenuItem>())
                {
                    if (item.Tag is string tag && item.IsCheckable)
                    {
                        item.IsChecked = string.Equals(tag, _viewModel.LayoutPreset, StringComparison.OrdinalIgnoreCase);
                    }

                    if (item.Tag is string containerTag && !item.IsCheckable && item.HasItems)
                    {
                        item.FontWeight = string.Equals(containerTag, _viewModel.LayoutPreset, StringComparison.OrdinalIgnoreCase)
                            ? FontWeights.Bold
                            : FontWeights.Normal;
                    }

                    if (item.HasItems)
                    {
                        UpdateChecks(item.Items);
                    }
                }
            }

            UpdateChecks(menuItems);
            _refreshLayoutCards();
        }

        public void LayoutPresetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem mi || mi.Tag is not string preset)
                return;

            _viewModel.LayoutPreset = preset;
            if (_window.FindName("LayoutPresetButton") is System.Windows.Controls.Button layoutPresetButton)
            {
                if (_window.FindName("LayoutPresetButton") is System.Windows.Controls.Button btn && btn.ContextMenu != null)
                {
                    // Delegate arrange via the refresh callback; MainWindow listens to LayoutPreset.
                }
            }
            ArrangeWindowsFromPreset(preset);
            UpdateLayoutMenuChecks(GetLayoutPresetMenuItems());
            _refreshLayoutCards();
        }

        public void LayoutSlotButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn || btn.Tag is null)
                return;

            if (!_viewModel.LayoutReserveIconGridSlot)
                return;

            if (!int.TryParse(btn.Tag.ToString(), out var slot))
                return;

            _viewModel.LayoutIconGridSlot = slot;
            _refreshLayoutCards();
            UpdateLayoutMenuChecks(GetLayoutPresetMenuItems());
        }

        public void LayoutLinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag)
                return;

            var parts = tag.Split('|');
            if (parts.Length != 4)
                return;

            var preset = parts[0];
            if (!int.TryParse(parts[2], out var a) || !int.TryParse(parts[3], out var b))
                return;

            if (_viewModel.LayoutLinks.TryGetValue(preset, out var existing) && existing.Length == 2 && existing[0] == a && existing[1] == b)
            {
                _viewModel.SetLayoutLink(preset, Array.Empty<int>());
            }
            else
            {
                _viewModel.SetLayoutLink(preset, new[] { a, b });
            }

            _refreshLayoutCards();
        }

        public void SaveLayoutAsButton_Click(object sender, RoutedEventArgs e) => PromptAndSaveLayout();

        public void SaveLayoutAsMenuItem_Click(object sender, RoutedEventArgs e) => PromptAndSaveLayout();

        public void PromptAndSaveLayout()
        {
            var suggested = string.Equals(_viewModel.LayoutPreset, "Auto", StringComparison.OrdinalIgnoreCase)
                ? "Mit layout"
                : _viewModel.LayoutPreset;

            var name = Interaction.InputBox("Navngiv layoutet", "Gem layout som", suggested ?? "Mit layout").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (string.Equals(name, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show("Navnet kan ikke være 'Auto'. Vælg et andet navn.", "Ugyldigt navn", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (TrySaveLayout(name))
            {
                _viewModel.LayoutPreset = name;
                UpdateLayoutMenuChecks(GetLayoutPresetMenuItems());
                _refreshLayoutCards();
            }
        }

        public void RenameLayoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string current)
                return;

            if (string.IsNullOrWhiteSpace(current) || string.Equals(current, "Auto", StringComparison.OrdinalIgnoreCase))
                return;

            var newName = Interaction.InputBox("Omdøb layoutet", "Omdøb layout", current).Trim();
            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, "Auto", StringComparison.OrdinalIgnoreCase))
                return;

            if (!_viewModel.RenameLayout(current, newName))
            {
                System.Windows.MessageBox.Show("Kunne ikke omdøbe layoutet. Navnet kan være i brug eller ugyldigt.", "Omdøb layout", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _viewModel.LayoutPreset = newName;
            PopulateLayoutMenu(GetLayoutPresetMenuItems());
            UpdateLayoutMenuChecks(GetLayoutPresetMenuItems());
            _refreshLayoutCards();
        }

        public void DeleteLayoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string current)
                return;

            if (string.IsNullOrWhiteSpace(current) || string.Equals(current, "Auto", StringComparison.OrdinalIgnoreCase))
                return;

            var confirm = System.Windows.MessageBox.Show($"Slet layoutet '{current}'?", "Slet layout", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            if (_viewModel.DeleteLayout(current))
            {
                PopulateLayoutMenu(GetLayoutPresetMenuItems());
                UpdateLayoutMenuChecks(GetLayoutPresetMenuItems());
                _refreshLayoutCards();
            }
        }

        public void RefreshLayoutCardSelection()
        {
            try
            {
                var accent = _viewModel.AccentBrush;
                var normal = new SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55));
                var dim = new SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));

                if (_window.FindName("LayoutCardsHost") is not System.Windows.Controls.Panel layoutCardsHost)
                    return;

                foreach (var child in layoutCardsHost.Children)
                {
                    if (child is not Border card)
                        continue;

                    foreach (var button in FindVisualChildren<System.Windows.Controls.Button>(card))
                    {
                        if (button.Tag is not string tag)
                            continue;

                        if (tag.Contains("Link"))
                        {
                            var linkParts = tag.Split('|');
                            if (linkParts.Length == 4 && int.TryParse(linkParts[2], out var la) && int.TryParse(linkParts[3], out var lb))
                            {
                                var active = _viewModel.LayoutLinks.TryGetValue(_viewModel.LayoutPreset, out var link) && link.Length == 2 && link[0] == la && link[1] == lb;
                                button.Background = active ? accent : new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
                                button.BorderBrush = active ? accent : new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
                                if (button.Content is TextBlock tb)
                                {
                                    tb.Foreground = active ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
                                }
                            }
                        }
                        else
                        {
                            if (!int.TryParse(tag, out var slot))
                                continue;

                            var slotEnabled = _viewModel.LayoutReserveIconGridSlot;
                            button.IsEnabled = slotEnabled;
                            button.Opacity = slotEnabled ? 1 : 0.6;

                            var isMatch = slotEnabled && slot == _viewModel.LayoutIconGridSlot;

                            var selectedBg = _viewModel.AccentBrush as SolidColorBrush
                                            ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
                            var unselectedBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55));
                            var unselectedBorder = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 32, 48));

                            button.Background = isMatch ? selectedBg : unselectedBg;
                            button.BorderBrush = isMatch ? selectedBg : unselectedBorder;
                            button.BorderThickness = isMatch ? new Thickness(2) : new Thickness(0);
                            button.Foreground = System.Windows.Media.Brushes.White;

                            if (button.Content is TextBlock tb)
                            {
                                var showIg = slotEnabled && isMatch;
                                tb.Text = showIg ? "IG" : $"{slot + 1}";
                            }
                        }
                    }
                }
            }
            catch
            {
                // best effort
            }
        }

        public void ArrangeWindowsFromPreset(string presetName)
        {
            WindowLayoutEngine.ArrangeWindowsFromPreset(
                presetName,
                _viewModel,
                _getWindowHandle(),
                _log,
                _refreshLayoutCards);
        }

        private bool TrySaveLayout(string layoutName)
        {
            layoutName = layoutName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(layoutName))
                return false;

            try
            {
                var handle = _getWindowHandle();
                var targetMonitor = _viewModel.LayoutCurrentMonitorOnly && handle != IntPtr.Zero
                    ? LauncherWindowInterop.MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST)
                    : IntPtr.Zero;

                var workArea = WindowLayoutEngine.GetWorkArea(targetMonitor);
                var includeIconGridWindow = _viewModel.LayoutReserveIconGridSlot || _viewModel.IsFullWindowVisible;
                var windows = WindowLayoutEngine.CollectCandidateWindows(targetMonitor, includeIconGridWindow, handle, _viewModel, _log);
                if (windows.Count == 0)
                {
                    _log($"Layout '{layoutName}' not saved: no candidate windows were found.");
                    return false;
                }

                var iconHandle = handle;
                var orderedWindows = windows
                    .OrderBy(w => w.Rect.Top)
                    .ThenBy(w => w.Rect.Left)
                    .Take(4)
                    .ToList();

                var normalized = new List<CustomLayoutSlot>();
                var iconSlotIndex = -1;

                for (int i = 0; i < orderedWindows.Count; i++)
                {
                    var slot = WindowLayoutEngine.NormalizeRectToWorkArea(orderedWindows[i].Rect, workArea);
                    if (slot.Width <= 0 || slot.Height <= 0)
                    {
                        continue;
                    }

                    if (iconHandle != IntPtr.Zero && orderedWindows[i].Hwnd == iconHandle)
                    {
                        iconSlotIndex = normalized.Count;
                    }

                    normalized.Add(slot);
                }

                var distinct = new List<CustomLayoutSlot>();
                foreach (var slot in normalized)
                {
                    if (!distinct.Any(existing => WindowLayoutEngine.SlotsClose(existing, slot, 0.02)))
                    {
                        distinct.Add(slot);
                    }
                }

                normalized = distinct.Take(4).ToList();

                if (normalized.Count == 0)
                {
                    _log($"Layout '{layoutName}' not saved: normalization produced no usable slots.");
                    return false;
                }

                _viewModel.SaveLayout(layoutName, normalized);
                _log($"Layout '{layoutName}' distinct slots saved: {string.Join(", ", normalized.Select(s => $"({s.X:F3},{s.Y:F3},{s.Width:F3},{s.Height:F3})"))}");
                if (iconSlotIndex >= 0)
                {
                    _viewModel.LayoutIconGridSlot = iconSlotIndex;
                }

                _log($"Saved layout '{layoutName}' with {normalized.Count} slots.");
                return true;
            }
            catch (Exception ex)
            {
                _log("SaveLayout failed: " + ex);
                return false;
            }
        }

        private ItemCollection? GetLayoutPresetMenuItems()
        {
            if (_window.FindName("LayoutPresetButton") is System.Windows.Controls.Button layoutPresetButton && layoutPresetButton.ContextMenu != null)
            {
                return layoutPresetButton.ContextMenu.Items;
            }
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }
    }
}