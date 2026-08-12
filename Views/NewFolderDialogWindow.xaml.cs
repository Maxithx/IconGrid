using System;
using System.Windows;
using System.Windows.Input;

namespace IconGrid.Views
{
    /// <summary>
    /// Small modal input dialog for creating a new folder in a Fast Copy pane
    /// (Explorer-style "New folder"). The user enters the folder name; the
    /// result is exposed through FolderName.
    /// </summary>
    public partial class NewFolderDialogWindow : Window
    {
        public NewFolderDialogWindow()
        {
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;
            DataContext = System.Windows.Application.Current?.MainWindow?.DataContext;
            Loaded += (_, _) =>
            {
                FolderNameBox.Focus();
                FolderNameBox.SelectAll();
            };
        }

        /// <summary>
        /// The folder name entered by the user, or null/empty when cancelled.
        /// </summary>
        public string? FolderName { get; private set; }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            FolderName = FolderNameBox.Text.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void FolderNameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Ok_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        }
    }
}