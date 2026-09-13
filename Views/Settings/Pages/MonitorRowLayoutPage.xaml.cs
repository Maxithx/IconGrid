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

            // "Reset to default" ALWAYS restores the factory defaults —
            // it cannot be changed by the user's saved default.
            vm.MonitorPingToNetGap = 4;
            vm.MonitorNetToDownloadGap = 4;
            vm.MonitorDownloadToUploadGap = 4;
            vm.MonitorUploadToCpuGap = 4;
            vm.MonitorCpuToGpuGap = 4;
            vm.MonitorDivider0Visible = true;
            vm.MonitorDivider1Visible = true;
            vm.MonitorDivider2Visible = true;
            vm.MonitorDivider3Visible = true;
            vm.MonitorDividerGap = 16;
            vm.MonitorCpuBarGap = 8;
            vm.MonitorGpuBarGap = 8;
            vm.MonitorDownloadLabelToValueGap = 4;
            vm.MonitorUploadLabelToValueGap = 4;
            vm.MonitorDownloadValueToUnitGap = 4;
            vm.MonitorUploadValueToUnitGap = 4;
            vm.MonitorDownloadValueWidth = 20;
            vm.MonitorUploadValueWidth = 20;
            vm.GamingOverlayBackgroundHeight = MainViewModel.GamingOverlayDefaultBackgroundHeight;
        }

        private void ResetToMyDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            // "Reset to my default" restores the user's saved default.
            // If none is saved, it falls back to the factory values.
            if (vm.TryApplySavedMonitorLayoutDefaults())
                return;

            ResetDefaultsButton_Click(sender, e);
        }

        private void SaveAsDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            vm.SaveMonitorLayoutDefaults();
        }
    }
}