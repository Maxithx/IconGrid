using System.Windows;
using System.Windows.Controls;
using IconGrid.ViewModels;

namespace IconGrid.Views
{
    public partial class MonitorRowLayoutPage : System.Windows.Controls.UserControl
    {
        public MonitorRowLayoutPage()
        {
            InitializeComponent();
        }

        private void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            vm.MonitorPingToNetGap = 4;
            vm.MonitorNetToDownloadGap = 6;
            vm.MonitorDownloadToUploadGap = 4;
            vm.MonitorUploadToCpuGap = 4;
            vm.MonitorCpuToGpuGap = 4;
            vm.MonitorDivider1Visible = true;
            vm.MonitorDivider2Visible = true;
            vm.MonitorDivider3Visible = true;
            vm.MonitorDividerGap = 4;
        }
    }
}