using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// A single file or folder row shown in a Fast Copy browser pane.
    /// Selection is tracked per entry so the pane can show "X files · Y MB".
    /// </summary>
    public sealed class FileBrowserEntry : INotifyPropertyChanged
    {
        private bool _isSelected;

        public required string Name { get; init; }

        public required string FullPath { get; init; }

        public bool IsDirectory { get; init; }

        public long SizeBytes { get; init; }

        public DateTime ModifiedTime { get; init; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public string SizeDisplay => IsDirectory ? string.Empty : FormatBytes(SizeBytes);

        public string ModifiedDisplay => ModifiedTime.ToString("yyyy-MM-dd HH:mm");

        public event PropertyChangedEventHandler? PropertyChanged;

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return $"{value:0.##} {units[unitIndex]}";
        }
    }
}