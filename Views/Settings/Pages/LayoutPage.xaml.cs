using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using IconGrid.ViewModels;
using IconGrid.ViewModels.Launcher;

namespace IconGrid.Views
{
public partial class LayoutPage : System.Windows.Controls.UserControl
    {
        private bool _isUpdatingScrollBar;

        public LayoutPage()
        {
            InitializeComponent();
        }

        private MainWindow? GetMainWindow() => System.Windows.Application.Current?.MainWindow as MainWindow;

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void RunLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            var preset = ViewModel?.LayoutPreset;
            if (string.IsNullOrWhiteSpace(preset))
                return;

            GetMainWindow()?.ArrangeWindowsFromPreset(preset);
        }

        private void SaveLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.PromptAndSaveLayout();
        }

        private void LayoutSlotButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null || !ViewModel.LayoutReserveIconGridSlot)
                return;

            if (sender is WpfButton button && button.Tag is string text && int.TryParse(text, out var slot))
            {
                ViewModel.LayoutIconGridSlot = slot;
            }
        }

        private void LayoutScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (LayoutScrollBar == null)
                return;

            _isUpdatingScrollBar = true;
            LayoutScrollBar.Value = e.VerticalOffset;
            LayoutScrollBar.Visibility = e.ExtentHeight > e.ViewportHeight ? Visibility.Visible : Visibility.Collapsed;
            _isUpdatingScrollBar = false;
        }

        private void LayoutScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingScrollBar || LayoutScrollViewer == null)
                return;

            LayoutScrollViewer.ScrollToVerticalOffset(e.NewValue);
        }
    }
}
