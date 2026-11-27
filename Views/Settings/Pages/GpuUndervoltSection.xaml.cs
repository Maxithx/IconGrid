using System.Windows;

namespace IconGrid.Views
{
    public partial class GpuUndervoltSection : System.Windows.Controls.UserControl
    {
        public event RoutedEventHandler? OpenAfterburnerDownloadRequested;
        public event RoutedEventHandler? ProfileRowGameRequested;
        public event RoutedEventHandler? ProfileRowIdleRequested;
        public event RoutedEventHandler? ProfileRowSaveRequested;
        public event RoutedEventHandler? ProfileRowDeleteRequested;
        public event RoutedEventHandler? ApplyTuningRequested;

        public GpuUndervoltSection()
        {
            InitializeComponent();
        }

        private void OpenAfterburnerDownload_Click(object sender, RoutedEventArgs e)
            => OpenAfterburnerDownloadRequested?.Invoke(sender, e);

        private void ProfileRowGame_Click(object sender, RoutedEventArgs e)
            => ProfileRowGameRequested?.Invoke(sender, e);

        private void ProfileRowIdle_Click(object sender, RoutedEventArgs e)
            => ProfileRowIdleRequested?.Invoke(sender, e);

        private void ProfileRowSave_Click(object sender, RoutedEventArgs e)
            => ProfileRowSaveRequested?.Invoke(sender, e);

        private void ProfileRowDelete_Click(object sender, RoutedEventArgs e)
            => ProfileRowDeleteRequested?.Invoke(sender, e);

        private void GpuUndervoltGenerateCurve_Click(object sender, RoutedEventArgs e)
            => ApplyTuningRequested?.Invoke(sender, e);
    }
}
