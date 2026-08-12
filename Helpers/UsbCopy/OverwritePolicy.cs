using System.Threading.Tasks;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// User decision for a copy destination that already exists, or a "to all"
    /// decision that applies for the remainder of the current copy session.
    /// </summary>
    public enum OverwriteDecision
    {
        /// <summary>Overwrite this file only.</summary>
        Overwrite,

        /// <summary>Overwrite this file and do not ask again (remaining conflicts overwrite silently).</summary>
        OverwriteAll,

        /// <summary>Skip this file only.</summary>
        Skip,

        /// <summary>Skip this file and do not ask again (remaining conflicts are skipped silently).</summary>
        SkipAll
    }

    /// <summary>
    /// Resolves what to do when the copy pipeline is about to write to a
    /// destination that already exists. Implementations are called from worker
    /// threads while a copy is running and may marshal to the UI thread to ask
    /// the user (Windows file-copy style Yes/Yes all/No/No all).
    /// </summary>
    public interface IOverwriteConflictResolver
    {
        Task<OverwriteDecision> ResolveAsync(string sourcePath, string destinationPath);
    }

    /// <summary>
    /// Fallback resolver used when no user interaction is available: always
    /// overwrite, preserving the pre-policy behavior of the copy pipeline.
    /// </summary>
    public sealed class AlwaysOverwriteResolver : IOverwriteConflictResolver
    {
        public static readonly AlwaysOverwriteResolver Instance = new();

        public Task<OverwriteDecision> ResolveAsync(string sourcePath, string destinationPath)
            => Task.FromResult(OverwriteDecision.Overwrite);
    }
}