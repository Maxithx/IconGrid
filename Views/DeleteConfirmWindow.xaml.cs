using System.Windows;

namespace IconGrid.Views
{
    /// <summary>
    /// Modal delete confirmation dialog for Fast Copy pane entries
    /// (Explorer-style). The localized "Delete '{0}'?" message is passed in
    /// via Message; Confirmed is true only when the user clicks Delete.
    /// </summary>
    public partial class DeleteConfirmWindow : Window
    {
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(DeleteConfirmWindow),
                new PropertyMetadata(string.Empty));

        public DeleteConfirmWindow(string message)
        {
            InitializeComponent();
            Message = message;
            Owner = System.Windows.Application.Current?.MainWindow;
            DataContext = System.Windows.Application.Current?.MainWindow?.DataContext;
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public bool Confirmed { get; private set; }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}