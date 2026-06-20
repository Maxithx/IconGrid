using System.Reflection;
using System.Windows;

namespace IconGrid.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            DataContext = System.Windows.Application.Current?.MainWindow?.DataContext;
        }
    }
}
