using System;
using System.Windows;
using System.Windows.Input;
using IconGrid.Helpers.UsbCopy;

namespace IconGrid.Views
{
    /// <summary>
    /// Non-modal, topmost File Copy progress dialog shown while a Fast Copy
    /// copy is running. Binds localized labels to the MainViewModel DataContext
    /// and live statistics to the shared UsbCopyState via this window's State
    /// property (RelativeSource bindings in the dialog XAML).
    /// The window is freely movable across the whole desktop like a normal
    /// Windows file-copy dialog (drag the title/header).
    /// </summary>
    public partial class FileCopyProgressWindow : Window
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(
                nameof(State),
                typeof(UsbCopyState),
                typeof(FileCopyProgressWindow),
                new PropertyMetadata(null));

        private readonly Action _cancel;

        public FileCopyProgressWindow(UsbCopyState state, Action cancel)
        {
            InitializeComponent();
            _cancel = cancel;
            State = state;
            Owner = System.Windows.Application.Current?.MainWindow;
            DataContext = System.Windows.Application.Current?.MainWindow?.DataContext;
        }

        /// <summary>
        /// The shared Fast Copy state (owned by the page's UsbCopyViewModel),
        /// exposed as a dependency property so the RelativeSource bindings in
        /// the dialog XAML update live as UsbCopyState raises PropertyChanged.
        /// </summary>
        public UsbCopyState? State
        {
            get => (UsbCopyState?)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        /// <summary>
        /// Let the user drag the dialog anywhere across the desktop
        /// (Windows file-copy style) by holding the title/header.
        /// </summary>
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Cancel the running copy via the owning UsbCopyViewModel.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancel();
        }
    }
}