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
        private int _refreshSerial;

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

            // Re-assert the active drive root after the observable collection was
            // rebuilt (Clear + Add). WPF does NOT re-evaluate the SelectedValue
            // binding when the value is unchanged, so the From/To ComboBox would
            // otherwise stay blank even though the dropdown list has all drives.
            OnPropertyChanged(nameof(DriveRootPath));

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
            ApplyEntries(EnumerateEntries(CurrentDirectory));
        }

        /// <summary>
        /// Creates a new directory in the current directory and refreshes the
        /// pane. Returns false (without deleting anything) when the name is
        /// invalid or the folder already exists.
        /// </summary>
        public bool CreateDirectory(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var invalid = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalid) >= 0 || string.Equals(name, ".", StringComparison.Ordinal) || string.Equals(name, "..", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                var path = Path.Combine(CurrentDirectory, name);
                if (Directory.Exists(path))
                {
                    return false;
                }

                Directory.CreateDirectory(path);
                Refresh();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes the selected files/directories in this pane (recursive for
        /// directories). Returns false when nothing was deleted or deletion
        /// failed at any point.
        /// </summary>
        public bool DeleteSelectedEntries()
        {
            var selected = Entries.Where(e => e.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return false;
            }

            var ok = true;
            try
            {
                foreach (var entry in selected)
                {
                    if (entry.IsDirectory)
                    {
                        Directory.Delete(entry.FullPath, recursive: true);
                    }
                    else
                    {
                        File.Delete(entry.FullPath);
                    }
                }
            }
            catch (Exception)
            {
                ok = false;
            }

            Refresh();
            return ok;
        }

        /// <summary>
        /// Refreshes the entry list without blocking the UI thread: the directory
        /// enumeration + FileInfo calls run on a background thread, then the
        /// resulting list is applied on the caller's (UI) thread. Used while a
        /// copy is writing to the destination pane so the page never freezes.
        /// A serial guard drops stale results when a newer refresh started.
        /// </summary>
        public async Task RefreshAsync()
        {
            var dir = CurrentDirectory;
            var serial = ++_refreshSerial;
            var list = await Task.Run(() => EnumerateEntries(dir));

            if (serial != _refreshSerial || !string.Equals(dir, CurrentDirectory, StringComparison.Ordinal))
            {
                return; // A newer refresh or a navigation happened meanwhile.
            }

            ApplyEntries(list);
        }

        private List<FileBrowserEntry> EnumerateEntries(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return new List<FileBrowserEntry>();
            }

            var entries = new List<FileBrowserEntry>();
            try
            {
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    var info = new DirectoryInfo(d);
                    entries.Add(new FileBrowserEntry
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = true,
                        ModifiedTime = SafeLastWrite(info.LastWriteTime)
                    });
                }

                foreach (var file in Directory.EnumerateFiles(dir))
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

            return entries
                .OrderBy(e => !e.IsDirectory)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private void ApplyEntries(List<FileBrowserEntry> entries)
        {
            Entries.Clear();
            foreach (var entry in entries)
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
            // Explorer-style Ctrl+A: select EVERYTHING (files AND folders).
            foreach (var entry in Entries)
            {
                entry.IsSelected = true;
            }

            OnPropertyChanged(nameof(SelectionStatus));
        }

        /// <summary>
        /// Anchor entry used for Explorer-style Shift+click range selection
        /// (set on ordinary clicks without Shift/Ctrl).
        /// </summary>
        public FileBrowserEntry? AnchorEntry { get; private set; }

        public void SetAnchor(FileBrowserEntry entry)
        {
            AnchorEntry = entry;
        }

        /// <summary>
        /// Re-raises SelectionStatus so the status line updates after manual
        /// selection changes made outside the pane's own commands.
        /// </summary>
        public void RefreshSelectionStatus() => OnPropertyChanged(nameof(SelectionStatus));

        /// <summary>
        /// Selects every entry between the anchor and the clicked entry
        /// (Explorer Shift+click). Existing selections outside the range are kept.
        /// </summary>
        public void SelectRange(FileBrowserEntry from, FileBrowserEntry to)
        {
            var start = Entries.IndexOf(from);
            var end = Entries.IndexOf(to);
            if (start < 0 || end < 0)
            {
                return;
            }

            if (start > end)
            {
                (start, end) = (end, start);
            }

            for (var i = start; i <= end; i++)
            {
                Entries[i].IsSelected = true;
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