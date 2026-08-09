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

            // Try saved user defaults first, fall back to hardcoded factory values.
            if (vm.TryApplySavedMonitorLayoutDefaults())
                return;

            vm.MonitorPingToNetGap = 2;
            vm.MonitorNetToDownloadGap = 2;
            vm.MonitorDownloadToUploadGap = 2;
            vm.MonitorUploadToCpuGap = 2;
            vm.MonitorCpuToGpuGap = 2;
            vm.MonitorDivider0Visible = true;
            vm.MonitorDivider1Visible = true;
            vm.MonitorDivider2Visible = true;
            vm.MonitorDivider3Visible = true;
            vm.MonitorDividerGap = 0;
            vm.MonitorCpuBarGap = 0;
            vm.MonitorGpuBarGap = 0;
            vm.MonitorDownloadLabelToValueGap = 0;
            vm.MonitorUploadLabelToValueGap = 0;
            vm.MonitorDownloadValueToUnitGap = 0;
            vm.MonitorUploadValueToUnitGap = 0;
            vm.MonitorDownloadValueWidth = 0;
            vm.MonitorUploadValueWidth = 0;
        }

        private void SaveAsDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            vm.SaveMonitorLayoutDefaults();
        }
    }
}