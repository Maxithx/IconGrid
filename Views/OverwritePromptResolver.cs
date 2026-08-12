using System.Threading;
using System.Threading.Tasks;
using IconGrid.Helpers.UsbCopy;

namespace IconGrid.Views
{
    /// <summary>
    /// Overwrite conflict resolver that prompts the user with the Windows
    /// file-copy style Overwrite? dialog (Yes / Yes to all / No / No to all)
    /// when the copy pipeline hits an existing destination file.
    /// ResolveAsync may be called from worker threads; the modal dialog is
    /// marshalled to the WPF UI thread via Dispatcher.Invoke and the calling
    /// worker blocks until the user decides.
    /// </summary>
    public sealed class OverwritePromptResolver : IOverwriteConflictResolver
    {
        private static readonly object Sync = new();

        public Task<OverwriteDecision> ResolveAsync(string sourcePath, string destinationPath)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return Task.FromResult(Prompt(sourcePath, destinationPath));
            }

            // Block the calling worker thread until the user answers the modal
            // dialog (shown on the UI thread).
            OverwriteDecision decision = OverwriteDecision.Skip;
            dispatcher.Invoke(() => decision = Prompt(sourcePath, destinationPath));
            return Task.FromResult(decision);
        }

        private static OverwriteDecision Prompt(string sourcePath, string destinationPath)
        {
            // Serialize prompts so only one overwrite dialog is open at a time,
            // even when several copy workers hit conflicts simultaneously.
            lock (Sync)
            {
                var window = new OverwritePromptWindow(sourcePath, destinationPath);
                window.ShowDialog();
                return window.Result;
            }
        }
    }
}