using System;
using System.IO;
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

        public UsbCopyPage()
        {
            InitializeComponent();
            ViewModel = new UsbCopyViewModel();
            // Populate early so the ComboBox bindings (FileBrowserPane.DriveRoots,
            // State.Devices) have data as soon as the page binds; Loaded below
            // re-runs the same refresh to pick up any drives that changed.
            ViewModel.RefreshDevices();
            Loaded += (_, _) => ViewModel.RefreshDevices();
        }

        public UsbCopyViewModel ViewModel { get; }

        /// <summary>
        /// Double-click a row: enter a directory, or toggle selection for a file.
        /// The EventSetter is attached to the ListBoxItem, so sender is the item
        /// container - resolve the owning ListBox to route to the correct pane.
        /// </summary>
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