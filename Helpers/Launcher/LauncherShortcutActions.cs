using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using IconGrid.Models;
using IconGrid.ViewModels;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the launcher shortcut-administration behavior that previously lived in MainWindow.xaml.cs:
    /// item context-menu actions, add-shortcut flows, icon helpers, and Windows-shortcut loading.
    /// Keeps the launcher shell focused on window lifetime and UI composition.
    /// </summary>
    public sealed class LauncherShortcutActions
    {
        private readonly Window _window;
        private readonly MainViewModel _viewModel;
        private readonly Func<string, string, string, string> _showInputBox;
        private readonly string _powerShellPath;

        private static readonly (string Danish, string English)[] WindowShortcutNames =
        {
            ("Billeder", "Pictures"),
            ("Dokumenter", "Documents"),
            ("Galleri", "Gallery"),
            ("Kontrolpanel", "Control Panel"),
            ("Musik", "Music"),
            ("OneDrive", "OneDrive"),
            ("Overførsler", "Downloads"),
            ("Papirkurv", "Recycle Bin"),
            ("Skrivebord", "Desktop"),
            ("Stifinder", "File Explorer"),
            ("Taskmanager", "Task Manager"),
            ("Denne computer", "This PC"),
            ("Videoer", "Videos"),
            ("Windows PowerShell", "Windows PowerShell"),
            ("Windows-sikkerhed", "Windows Security"),
            ("Windows Update", "Windows Update")
        };

        public record WindowsShortcutTemplate(string DisplayName, string FullPath);

        public IReadOnlyList<WindowsShortcutTemplate> WindowsShortcuts { get; private set; } = new List<WindowsShortcutTemplate>();

        public LauncherShortcutActions(
            Window window,
            MainViewModel viewModel,
            string powerShellPath,
            Func<string, string, string, string> showInputBox)
        {
            _window = window;
            _viewModel = viewModel;
            _powerShellPath = powerShellPath;
            _showInputBox = showInputBox;
        }

        public void RemoveItem(LauncherItem item) => _viewModel.RemoveItem(item);

        public void LaunchItem(LauncherItem item) => _viewModel.LaunchItem(item);

        public void OpenItem(LauncherItem item) => _viewModel.LaunchItem(item);

        public void RunAsAdmin(LauncherItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return;

            try
            {
                var psi = new ProcessStartInfo(item.Path)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };

                var workingDirectory = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not run as administrator:\n{ex.Message}", "Run as admin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void OpenLocation(LauncherItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return;

            try
            {
                if (File.Exists(item.Path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{item.Path}\"",
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(item.Path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{item.Path}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not open file location:\n{ex.Message}", "Open location", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CopyPath(LauncherItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return;

            try
            {
                System.Windows.Clipboard.SetText(item.Path);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not copy path:\n{ex.Message}", "Copy path", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RenameItem(LauncherItem item)
        {
            if (item == null)
                return;

            var newName = _showInputBox("Enter a new display name:", "Rename Launcher", item.DisplayName);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                _viewModel.RenameItem(item, newName);
            }
        }

        public void ChangeIcon(LauncherItem item, string? taggedPath = null)
        {
            if (item == null)
                return;

            var initialPath = ResolveIconPickerPath(taggedPath, item);
            var sb = new System.Text.StringBuilder(512);
            sb.Append(initialPath);
            var iconIndex = item.IconIndex;
            var hwnd = new WindowInteropHelper(_window).Handle;

            if (LauncherWindowInterop.PickIconDlg(hwnd, sb, sb.Capacity, ref iconIndex))
            {
                var raw = sb.ToString();
                var cleanPath = raw.Split('\0')[0].Trim();
                if (string.IsNullOrWhiteSpace(cleanPath))
                {
                    return;
                }

                var selectedPath = NormalizeToSystemRootToken(cleanPath);
                var sanitizedIndex = Math.Max(0, iconIndex);
                _viewModel.UpdateItemIcon(item, selectedPath, sanitizedIndex);
            }
        }

        public void ResetIcon(LauncherItem item)
        {
            if (item == null)
                return;

            if (string.IsNullOrWhiteSpace(item.Path))
            {
                System.Windows.MessageBox.Show("Cannot reset icon because the target path is empty.", "Reset icon", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _viewModel.UpdateItemIcon(item, item.Path, 0);
        }

        public void AddShortcut()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose file or shortcut",
                Filter = "Applications and shortcuts (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(_window) == true)
            {
                var name = _showInputBox("Enter display name", "New shortcut", Path.GetFileNameWithoutExtension(dialog.FileName));
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                var item = _viewModel.CreateCustomShortcut(name, dialog.FileName, _viewModel.SelectedTab);
                RefreshNewShortcutIcon(item);
            }
        }

        public void AddPowerShellCustom()
        {
            var command = _showInputBox("Enter PowerShell command (e.g. Start-Process ms-settings:windowsupdate)", "New PowerShell shortcut", "Start-Process ms-settings:windowsupdate");
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            var name = _showInputBox("Enter display name", "New PowerShell shortcut", "PowerShell");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var item = _viewModel.CreateCustomShortcut(name, _powerShellPath, _viewModel.SelectedTab, $"-NoExit -Command \"{command}\"");
            ApplyDefaultIconFromPack(item, "WindowsPowerShell.png", _powerShellPath, 0);
        }

        public void AddWindowsShortcut(WindowsShortcutTemplate template)
        {
            if (template == null)
                return;

            var targetCategory = _viewModel.Tabs.Contains("Windows") ? "Windows" : _viewModel.SelectedTab;
            AddWindowsShortcut(template, targetCategory);
        }

        public void AddPowerShellPreset(string args, string label)
        {
            var item = _viewModel.CreateCustomShortcut(label, _powerShellPath, _viewModel.SelectedTab, args);

            // Prefer executable icon (index 0); fall back to icon pack if available.
            _viewModel.SetIcon(item, _powerShellPath, 0);
            ApplyDefaultIconFromPack(item, "WindowsPowerShell.png", _powerShellPath, 0);
        }

        public void AddAllWindowsShortcuts()
        {
            var targetCategory = _viewModel.Tabs.Contains("Windows") ? "Windows" : _viewModel.SelectedTab;
            foreach (var shortcut in WindowsShortcuts)
            {
                AddWindowsShortcut(shortcut, targetCategory);
            }
        }

        public void LoadWindowsShortcuts()
        {
            try
            {
                var list = new List<WindowsShortcutTemplate>();

                var potentialRoots = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Assets", "WindowsShortcuts"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "WindowsShortcuts"), // dev path
                    Path.Combine(Environment.CurrentDirectory, "Assets", "WindowsShortcuts"),
                    Path.Combine(_viewModel.IconPackFolder, "..", "WindowsShortcuts")
                };

                foreach (var dir in potentialRoots.Distinct())
                {
                    var full = Path.GetFullPath(dir);
                    if (!Directory.Exists(full))
                        continue;

                    foreach (var file in Directory.GetFiles(full, "*.lnk", SearchOption.TopDirectoryOnly))
                    {
                        var rawName = Path.GetFileNameWithoutExtension(file);
                        var displayName = TranslateShortcutName(rawName);
                        if (list.All(l => !string.Equals(l.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)))
                        {
                            list.Add(new WindowsShortcutTemplate(displayName, file));
                        }
                    }
                }

                WindowsShortcuts = list.OrderBy(s => s.DisplayName).ToList();
            }
            catch
            {
                WindowsShortcuts = new List<WindowsShortcutTemplate>();
            }
        }

        private static string ResolveIconPickerPath(string? taggedPath, LauncherItem item)
        {
            var candidate = !string.IsNullOrWhiteSpace(taggedPath)
                ? taggedPath
                : item.IconPath;

            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = @"%SystemRoot%\System32\shell32.dll";
            }

            return Environment.ExpandEnvironmentVariables(candidate);
        }

        private void RefreshNewShortcutIcon(LauncherItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
            {
                return;
            }

            _viewModel.UpdateItemIcon(item, item.Path, 0);
        }

        private void ApplyDefaultIconFromPack(LauncherItem item, string fileName, string fallbackIconPath, int fallbackIndex = 0)
        {
            var candidate = Path.Combine(_viewModel.IconPackFolder, fileName);
            if (File.Exists(candidate))
            {
                _viewModel.SetIcon(item, candidate, 0);
                return;
            }

            _viewModel.SetIcon(item, fallbackIconPath, fallbackIndex);
        }

        private string TranslateShortcutName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var useDanish = string.Equals(_viewModel.Language, "da", StringComparison.OrdinalIgnoreCase);

            foreach (var pair in WindowShortcutNames)
            {
                if (name.Equals(pair.Danish, StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(pair.English, StringComparison.OrdinalIgnoreCase))
                {
                    return useDanish ? pair.Danish : pair.English;
                }
            }

            // Custom names are left untouched.
            return name;
        }

        /// <summary>
        /// Renames Windows-category shortcuts that still use a known built-in name so they
        /// follow the active IconGrid language. Custom user names are not modified.
        /// </summary>
        public void ApplyWindowsShortcutLanguage()
        {
            var changed = false;

            foreach (var item in _viewModel.Items)
            {
                if (!string.Equals(item.Category, "Windows", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(item.Path) ||
                    !item.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var localizedName = TranslateShortcutName(item.DisplayName);
                if (string.Equals(item.DisplayName, localizedName, StringComparison.Ordinal))
                {
                    continue;
                }

                item.DisplayName = localizedName;
                changed = true;
            }

            if (changed)
            {
                _viewModel.SaveItemsToFile();
            }
        }

        private void AddWindowsShortcut(WindowsShortcutTemplate template, string targetCategory)
        {
            var exists = _viewModel.Items.Any(i =>
                string.Equals(i.Path, template.FullPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.Category, targetCategory, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                return;
            }

            var item = _viewModel.CreateCustomShortcut(template.DisplayName, template.FullPath, targetCategory, arguments: null, iconPath: template.FullPath, iconIndex: 0);
            _viewModel.UpdateItemIcon(item, template.FullPath, 0);
        }

        private static string NormalizeToSystemRootToken(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            if (!string.IsNullOrWhiteSpace(systemRoot) &&
                path.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = path.Length > systemRoot.Length
                    ? path[systemRoot.Length..].TrimStart('\\')
                    : string.Empty;

                return string.IsNullOrEmpty(remainder)
                    ? "%SystemRoot%"
                    : $"%SystemRoot%\\{remainder}";
            }

            return path;
        }
    }
}