using System;
using System.IO;
using System.Windows;
using IconGrid.Helpers.UsbCopy;

namespace IconGrid.Views
{
    /// <summary>
    /// Modal "Overwrite?" prompt shown while a Fast Copy copy encounters a
    /// destination file that already exists. Offers the Windows file-copy
    /// choices: Yes / Yes to all / No / No to all, localized via the app's
    /// MainViewModel DataContext.
    /// </summary>
    public partial class OverwritePromptWindow : Window
    {
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register(
                nameof(FileName),
                typeof(string),
                typeof(OverwritePromptWindow),
                new PropertyMetadata(string.Empty));

        public OverwritePromptWindow(string sourcePath, string destinationPath)
        {
            InitializeComponent();

            // File name shown in the prompt body; the full destination path is
            // kept for future "open location" style actions if needed.
            FileName = SafeFileName(destinationPath);
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;

            Owner = System.Windows.Application.Current?.MainWindow;
            DataContext = System.Windows.Application.Current?.MainWindow?.DataContext;
        }

        private readonly string _sourcePath;
        private readonly string _destinationPath;

        /// <summary>
        /// The destination file name displayed in the prompt body.
        /// </summary>
        public string FileName
        {
            get => (string)GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        /// <summary>
        /// The decision the user picked. Opens a fresh dialog per conflict when
        /// the user does not choose "to all", so each ResolveAsync call creates
        /// and shows its own window through Dispatcher.Invoke (blocking).
        /// </summary>
        public OverwriteDecision Result { get; private set; } = OverwriteDecision.Skip;

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = OverwriteDecision.Overwrite;
            DialogResult = true;
        }

        private void YesAll_Click(object sender, RoutedEventArgs e)
        {
            Result = OverwriteDecision.OverwriteAll;
            DialogResult = true;
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            Result = OverwriteDecision.Skip;
            DialogResult = true;
        }

        private void NoAll_Click(object sender, RoutedEventArgs e)
        {
            Result = OverwriteDecision.SkipAll;
            DialogResult = true;
        }

        private static string SafeFileName(string path)
        {
            try
            {
                return Path.GetFileName(path);
            }
            catch (Exception)
            {
                return path;
            }
        }
    }
}