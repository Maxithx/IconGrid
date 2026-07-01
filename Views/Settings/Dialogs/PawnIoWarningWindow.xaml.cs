using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace IconGrid.Views;

public partial class PawnIoWarningWindow : Window
{
    private const string PawnIoUrl = "https://pawnio.eu";
    private static readonly Uri PawnIoDownloadUri = new("https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe");
    private static readonly HttpClient HttpClient = new();
    private bool _isDownloading;

    public PawnIoWarningWindow(string message, string downloadText)
    {
        InitializeComponent();
        MessageTextBlock.Text = message;
        DownloadButton.Content = downloadText;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
        {
            return;
        }

        await DownloadPawnIoInstallerAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task DownloadPawnIoInstallerAsync()
    {
        _isDownloading = true;
        DownloadButton.IsEnabled = false;
        DownloadStatusTextBlock.Text = "Downloading PawnIO…";

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"PawnIO_setup_{Guid.NewGuid():N}.exe");
            using var response = await HttpClient.GetAsync(PawnIoDownloadUri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream);
            }

            DownloadStatusTextBlock.Text = "Launching PawnIO installer…";
            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            _ = Dispatcher.InvokeAsync(Close);
        }
        catch (Exception ex)
        {
            DownloadStatusTextBlock.Text = "Download failed.";
            System.Windows.MessageBox.Show($"Failed to download PawnIO: {ex.Message}", "PawnIO", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isDownloading = false;
            DownloadButton.IsEnabled = true;
        }
    }

    private void LicenseLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        OpenUrl(e.Uri?.AbsoluteUri ?? PawnIoUrl);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PawnIoWarningWindow failed to open URL: {ex}");
        }
    }
}
