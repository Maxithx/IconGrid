using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IconGrid.Helpers.UsbCopy;
using IconGrid.ViewModels.Settings;

namespace IconGrid.Views
{
    /// <summary>
    /// Fast Copy settings page. Owns a UsbCopyViewModel so all copy/benchmark
    /// feature logic stays out of MainViewModel (see ARCHITECTURE_RULES.md).
    /// Localized texts bind to the MainViewModel DataContext; feature bindings
    /// resolve through RelativeSource AncestorType={x:Type UsbCopyPage} because
    /// the page content lives inside the TemplatePage UserControl (a separate
    /// namescope) where ElementName bindings cannot resolve.
    /// </summary>
    public partial class UsbCopyPage : System.Windows.Controls.UserControl
    {
        private bool _suppressDriveSelection;
        private ViewModels.Settings.UsbCopyViewModel? _progressViewModel;
        private IconGrid.Views.FileCopyProgressWindow? _progressWindow;

        public UsbCopyPage()
        {
            InitializeComponent();
            // Existing destinations during a copy prompt the user with the
            // Windows-style Overwrite? dialog (Yes / Yes to all / No / No to all).
            ViewModel = new UsbCopyViewModel(new OverwritePromptResolver());
            // Populate early so the ComboBox bindings (FileBrowserPane.DriveRoots,
            // State.Devices) have data as soon as the page binds; Loaded below
            // re-runs the same refresh to pick up any drives that changed.
            ViewModel.RefreshDevices();
            ViewModel.State.PropertyChanged += OnCopyStatePropertyChanged;
            ViewModel.NewFolderRequested += OnNewFolderRequested;
            ViewModel.DeleteRequested += OnDeleteRequested;
            ViewModel.RunSyntheticBenchmarkRequested += OnRunSyntheticBenchmarkRequested;
            Loaded += (_, _) => ViewModel.RefreshDevices();
        }

        public UsbCopyViewModel ViewModel { get; }

        /// <summary>
        /// Shows the Explorer-style "New folder" name dialog for the active pane.
        /// Invalid names and existing folder names are surfaced inline; the pane
        /// is refreshed after a successful create.
        /// </summary>
        private void OnNewFolderRequested(string? paneTag)
        {
            var pane = ResolvePane(paneTag);
            var dialog = new NewFolderDialogWindow();
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return;
            }

            if (!pane.CreateDirectory(dialog.FolderName))
            {
                // Invalid name or an existing folder with the same name.
                ViewModel.State.PipelineStatus = string.Empty;
            }
        }

        /// <summary>
        /// Confirms deletion of the selected entries in the active pane before
        /// anything is deleted (Explorer-style "Delete '{0}'?").
        /// </summary>
        private void OnDeleteRequested(string? paneTag)
        {
            var pane = ResolvePane(paneTag);
            var selected = pane.Entries.Where(e => e.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var names = string.Join(", ", selected.Take(3).Select(e => e.Name));
            if (selected.Count > 3)
            {
                names += ", …";
            }

            var mainVm = System.Windows.Application.Current?.MainWindow?.DataContext as ViewModels.MainViewModel;
            var template = mainVm?.FastCopyDeleteConfirmMessage ?? "Delete '{0}'?";
            var message = template.Replace("{0}", names);

            var confirm = new DeleteConfirmWindow(message);
            if (confirm.ShowDialog() != true)
            {
                return;
            }

            pane.DeleteSelectedEntries();
        }

        private FileBrowserPane ResolvePane(string? tag)
        {
            if (string.Equals(tag, "Left", StringComparison.Ordinal))
            {
                return ViewModel.LeftPane;
            }

            if (string.Equals(tag, "Right", StringComparison.Ordinal))
            {
                return ViewModel.RightPane;
            }

            return ViewModel.ActivePane;
        }

        /// <summary>
        /// Show the File Copy progress dialog while a copy is running and close
        /// it when the copy finishes/cancels. The dialog is non-modal + topmost
        /// so it floats freely over the whole desktop like Windows' file copy.
        /// </summary>
        private void OnCopyStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewModel.State.IsCopying))
            {
                return;
            }

            if (!ViewModel.State.IsCopying)
            {
                // Close is safe: just detach + close on the dispatcher.
                var window = _progressWindow;
                _progressWindow = null;
                _progressViewModel = null;
                window?.Dispatcher.BeginInvoke(new Action(() => window.Close()));
                return;
            }

            if (_progressWindow != null)
            {
                return;
            }

            // Show the dialog AFTER the current call stack completes so a window
            // creation/focus hiccup can never abort the copy (which starts in the
            // very next statement after IsCopying=true). Any dialog failure is
            // caught and logged here instead of breaking the copy.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // The copy may already have finished between IsCopying=true
                    // and this queued delegate — don't show a stale dialog.
                    if (!ViewModel.State.IsCopying || _progressWindow != null)
                    {
                        return;
                    }

                    _progressViewModel = ViewModel;
                    var window = new IconGrid.Views.FileCopyProgressWindow(
                        ViewModel.State,
                        () => ViewModel.CancelCopyCommand?.Execute(null));
                    _progressWindow = window;
                    window.Show();
                }
                catch (Exception ex)
                {
                    _progressWindow = null;
                    _progressViewModel = null;
                    System.Diagnostics.Debug.WriteLine($"FileCopyProgressWindow error: {ex}");
                }
            }));
        }

        /// <summary>
        /// Double-click a row: enter a directory, or toggle selection for a file.
        /// The EventSetter is attached to the ListBoxItem, so sender is the item
        /// container - resolve the owning ListBox to route to the correct pane.
        /// </summary>
        /// <summary>
        /// Explorer-style single/range selection on the pane rows:
        /// - plain click clears everything and selects only that entry (new anchor)
        /// - Ctrl+click toggles that entry without moving the anchor
        /// - Shift+click selects the range between the anchor and the clicked entry
        /// Clicks on the row's CheckBox are left untouched so the checkbox works
        /// independently.
        /// </summary>
        private void BrowserList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.ListBox listBox || listBox.Tag is not string tag)
            {
                return;
            }

            // Let checkbox clicks toggle on their own (no double-handling).
            if (FindAncestor<System.Windows.Controls.CheckBox>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            var container = listBox.ContainerFromElement(e.OriginalSource as DependencyObject) as System.Windows.Controls.ListBoxItem;
            if (container?.DataContext is not FileBrowserEntry entry)
            {
                return;
            }

            var pane = string.Equals(tag, "Right", StringComparison.Ordinal) ? ViewModel.RightPane : ViewModel.LeftPane;
            var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (ctrl)
            {
                entry.IsSelected = !entry.IsSelected;
                pane.RefreshSelectionStatus();
            }
            else if (shift)
            {
                if (pane.AnchorEntry != null)
                {
                    pane.SelectRange(pane.AnchorEntry, entry);
                }
                else
                {
                    entry.IsSelected = true;
                    pane.SetAnchor(entry);
                    pane.RefreshSelectionStatus();
                }
            }
            else
            {
                pane.ClearSelection();
                entry.IsSelected = true;
                pane.SetAnchor(entry);
                pane.RefreshSelectionStatus();
            }
        }

        /// <summary>
        /// Runs BenchmarkRunner.exe (the synthetic copy benchmark) in a live
        /// progress window so the user can follow 100/300/500/1000 MB tests.
        /// </summary>
        private static void OnRunSyntheticBenchmarkRequested(int sizeMb)
        {
            try
            {
                var exe = Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "tools",
                    "benchmark-runner",
                    "bin",
                    "Debug",
                    "net10.0-windows10.0.22621.0",
                    "BenchmarkRunner.exe");

                if (!File.Exists(exe))
                {
                    System.Windows.MessageBox.Show(
                        $"BenchmarkRunner.exe blev ikke fundet:\n{exe}\nByg den først: dotnet build tools/benchmark-runner/BenchmarkRunner.csproj",
                        "Benchmark",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var window = new IconGrid.Views.BenchmarkRunWindow(exe, new[] { "--size", sizeMb.ToString() });
                window.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Kunne ikke starte benchmark: {ex.Message}", "Benchmark");
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void BrowserList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.ListBoxItem listBoxItem)
            {
                return;
            }

            var listBox = System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(listBoxItem) as System.Windows.Controls.ListBox;
            if (listBox == null)
            {
                return;
            }

            if (listBox.SelectedItem is not FileBrowserEntry entry)
            {
                return;
            }

            var tag = listBox.Tag as string;
            var pane = string.Equals(tag, "Right", StringComparison.Ordinal) ? ViewModel.RightPane : ViewModel.LeftPane;

            if (entry.IsDirectory)
            {
                if (ReferenceEquals(pane, ViewModel.ActivePane))
                {
                    ViewModel.EnterDirectoryCommand?.Execute(entry);
                }
                else
                {
                    pane.NavigateTo(entry.FullPath);
                }
            }
            else
            {
                pane.ToggleEntrySelection(entry);
            }
        }

        /// <summary>
        /// When the user picks a drive in the From/To dropdown, navigate the
        /// corresponding pane to that drive. The guard flag prevents re-entrancy
        /// when the pane raises its own selection notifications.
        /// </summary>
        private void DriveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressDriveSelection)
            {
                return;
            }

            if (sender is not System.Windows.Controls.ComboBox comboBox)
            {
                return;
            }

            var tag = comboBox.Tag as string;
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            string? drive = null;
            switch (comboBox.SelectedItem)
            {
                case DriveRootItem driveItem:
                    drive = driveItem.Path;
                    break;
                case string path:
                    drive = path;
                    break;
            }

            if (string.IsNullOrEmpty(drive) || !Directory.Exists(drive))
            {
                return;
            }

            _suppressDriveSelection = true;
            try
            {
                if (string.Equals(tag, "Left", StringComparison.Ordinal))
                {
                    ViewModel.LeftPane.NavigateTo(drive);
                }
                else if (string.Equals(tag, "Right", StringComparison.Ordinal))
                {
                    ViewModel.RightPane.NavigateTo(drive);
                }
            }
            finally
            {
                _suppressDriveSelection = false;
            }
        }
    }
}