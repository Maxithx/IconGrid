using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// A drive root shown in the From/To dropdowns: raw path ("D:\") plus a
    /// display name ("SSD 220GB (D:\)") derived from the volume label.
    /// </summary>
    public sealed record DriveRootItem(string Path, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// One panel of the Norton-Commander-style dual-pane browser: owns the
    /// current directory, the visible entries, and the selection status.
    /// The owner (UsbCopyViewModel) drives it on the UI thread.
    /// </summary>
    public sealed class FileBrowserPane : INotifyPropertyChanged
    {
        private string _currentDirectory = string.Empty;

        public ObservableCollection<FileBrowserEntry> Entries { get; } = new();

        /// <summary>
        /// Observable drive roots: WPF cannot refresh a ComboBox bound to a plain
        /// IReadOnlyList of T unless the whole reference is swapped. An observable
        /// collection notifies the UI as items change, so From/To dropdowns update.
        /// Items carry both the raw path ("D:\") and a human-friendly display name
        /// ("SSD 220GB (D:\)") so the dropdown shows drive labels where available.
        /// </summary>
        public ObservableCollection<DriveRootItem> DriveRoots { get; } = new();

        public string CurrentDirectory
        {
            get => _currentDirectory;
            private set
            {
                if (_currentDirectory == value)
                {
                    return;
                }

                _currentDirectory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PathDisplay));
                OnPropertyChanged(nameof(CanGoUp));
                OnPropertyChanged(nameof(DriveRootPath));
            }
        }

        public string PathDisplay => string.IsNullOrEmpty(CurrentDirectory) ? string.Empty : CurrentDirectory;

        /// <summary>
        /// The drive root of the current directory ("D:\" when inside "D:\Folder").
        /// Bound to the From/To dropdown SelectedValue so the dropdown always shows
        /// the active drive, even when browsing a sub-folder.
        /// </summary>
        public string DriveRootPath => CurrentDriveRoot();

        public bool CanGoUp
        {
            get
            {
                try
                {
                    return !string.IsNullOrEmpty(CurrentDirectory) &&
                           Directory.GetParent(CurrentDirectory) != null;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public string SelectionStatus
        {
            get
            {
                var files = Entries.Where(e => e.IsSelected && !e.IsDirectory).ToList();
                if (files.Count == 0)
                {
                    return string.Empty;
                }

                var size = files.Sum(f => f.SizeBytes);
                return $"{files.Count} · {FormatBytes(size)}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void LoadDrives()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable)
                .Where(d => Directory.Exists(d.RootDirectory.FullName))
                .OrderBy(d => d.RootDirectory.FullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            DriveRoots.Clear();
            foreach (var drive in drives)
            {
                var path = drive.RootDirectory.FullName;
                string? label = null;
                if (drive.IsReady)
                {
                    try
                    {
                        label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel;
                    }
                    catch (IOException)
                    {
                        // Ignore unreadable volume labels.
                    }
                }

                var display = string.IsNullOrEmpty(label) ? path : $"{label} ({path})";
                DriveRoots.Add(new DriveRootItem(path, display));
            }

            if (string.IsNullOrEmpty(CurrentDirectory) || !Directory.Exists(CurrentDirectory))
            {
                var first = DriveRoots.FirstOrDefault();
                CurrentDirectory = string.IsNullOrEmpty(first?.Path) ? string.Empty : first.Path;
            }

            Refresh();
        }

        public void NavigateTo(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    CurrentDirectory = Path.GetFullPath(path);
                    Refresh();
                }
            }
            catch (Exception)
            {
                // Ignore invalid navigation targets.
            }
        }

        public void GoUp()
        {
            try
            {
                var parent = Directory.GetParent(CurrentDirectory);
                if (parent != null)
                {
                    CurrentDirectory = parent.FullName;
                    Refresh();
                }
            }
            catch (Exception)
            {
                // Ignore navigation failures (e.g. unreadable parent).
            }
        }

        public void Refresh()
        {
            Entries.Clear();
            if (string.IsNullOrEmpty(CurrentDirectory) || !Directory.Exists(CurrentDirectory))
            {
                OnPropertyChanged(nameof(SelectionStatus));
                return;
            }

            var entries = new List<FileBrowserEntry>();
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(CurrentDirectory))
                {
                    var info = new DirectoryInfo(dir);
                    entries.Add(new FileBrowserEntry
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = true,
                        ModifiedTime = SafeLastWrite(info.LastWriteTime)
                    });
                }

                foreach (var file in Directory.EnumerateFiles(CurrentDirectory))
                {
                    var info = new FileInfo(file);
                    entries.Add(new FileBrowserEntry
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = false,
                        SizeBytes = SafeLength(info),
                        ModifiedTime = SafeLastWrite(info.LastWriteTime)
                    });
                }
            }
            catch (Exception)
            {
                // Unreadable directory — show whatever we managed to enumerate.
            }

            foreach (var entry in entries.OrderBy(e => !e.IsDirectory).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Entries.Add(entry);
            }

            OnPropertyChanged(nameof(SelectionStatus));
        }

        public void ToggleEntrySelection(FileBrowserEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.IsSelected = !entry.IsSelected;
            OnPropertyChanged(nameof(SelectionStatus));
        }

        public void SelectAllFiles()
        {
            foreach (var entry in Entries.Where(e => !e.IsDirectory))
            {
                entry.IsSelected = true;
            }

            OnPropertyChanged(nameof(SelectionStatus));
        }

        public void ClearSelection()
        {
            foreach (var entry in Entries)
            {
                entry.IsSelected = false;
            }

            OnPropertyChanged(nameof(SelectionStatus));
        }

        private string CurrentDriveRoot()
        {
            if (string.IsNullOrEmpty(CurrentDirectory))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetPathRoot(CurrentDirectory) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static DateTime SafeLastWrite(DateTime value) =>
            value == default ? DateTime.Now : value;

        private static long SafeLength(FileInfo info)
        {
            try
            {
                return info.Length;
            }
            catch (Exception)
            {
                return 0;
            }
        }

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

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}