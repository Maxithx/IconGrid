using System.Windows.Controls;
using IconGrid.ViewModels.Settings;

namespace IconGrid.Views
{
    /// <summary>
    /// Fast USB Copy settings page. Owns a UsbCopyViewModel so all copy/benchmark
    /// feature logic stays out of MainViewModel (see ARCHITECTURE_RULES.md).
    /// Localized texts bind to the MainViewModel DataContext; feature bindings use
    /// ElementName="Root" -> ViewModel.State.
    /// </summary>
    public partial class UsbCopyPage : System.Windows.Controls.UserControl
    {
        public UsbCopyPage()
        {
            InitializeComponent();
            ViewModel = new UsbCopyViewModel();
            Loaded += (_, _) => ViewModel.RefreshDevices();
        }

        public UsbCopyViewModel ViewModel { get; }
    }
}