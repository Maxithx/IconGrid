using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IconGrid.Helpers;
using IconGrid.Helpers.Maintenance;
using IconGrid.Helpers.Settings;

namespace IconGrid.ViewModels.Settings
{
    public sealed class CleanupFileRow : INotifyPropertyChanged
    {
        private bool _isSelected;

        public CleanupFileRow(CleanupFileCandidate candidate, string categoryText)
        {
            Candidate = candidate;
            CategoryText = categoryText;
        }

        public CleanupFileCandidate Candidate { get; }
        public string FileName => Path.GetFileName(Candidate.Path);
        public string FullPath => Candidate.Path;
        public string CategoryText { get; }
        public string SizeText => CleanupViewModel.FormatBytes(Candidate.SizeBytes);
        public string ModifiedText => Candidate.LastWriteTimeUtc.ToLocalTime().ToString("dd-MM HH:mm");

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed class CleanupViewModel : INotifyPropertyChanged
    {
        private readonly MainViewModel _mainViewModel;
        private readonly List<CleanupFileRow> _rows = new();
        private bool _isBusy;
        private string _statusText = string.Empty;
        private string _lastErrorText = string.Empty;

        public CleanupViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            ScanCommand = new RelayCommand(async _ => await ScanAsync(), _ => !IsBusy);
            SelectAllCommand = new RelayCommand(_ => SetAllSelected(true), _ => Files.Count > 0 && !IsBusy);
            SelectNoneCommand = new RelayCommand(_ => SetAllSelected(false), _ => Files.Count > 0 && !IsBusy);
            CleanSelectedCommand = new RelayCommand(async _ => await CleanSelectedAsync(), _ => SelectedCount > 0 && !IsBusy);
            _statusText = L("CleanupScanPrompt");
            _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
        }

        public ObservableCollection<CleanupFileRow> Files { get; } = new();

        public ICommand ScanCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand CleanSelectedCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string LastErrorText
        {
            get => _lastErrorText;
            private set
            {
                if (_lastErrorText == value) return;
                _lastErrorText = value;
                OnPropertyChanged(nameof(LastErrorText));
            }
        }

        public int SelectedCount => _rows.Count(row => row.IsSelected);
        public long SelectedBytes => _rows.Where(row => row.IsSelected).Sum(row => row.Candidate.SizeBytes);

        public string SummaryText => SelectedCount == 0
            ? L("CleanupSummaryNone")
            : string.Format(L("CleanupSummaryFormat"), SelectedCount, FormatBytes(SelectedBytes));

        public string PageTitle => L("CleanupPageTitle");
        public string PageIntro => L("CleanupPageIntro");
        public string SafetyText => L("CleanupSafetyText");
        public string ScanButtonText => L("CleanupScanButton");
        public string SelectAllButtonText => L("CleanupSelectAll");
        public string SelectNoneButtonText => L("CleanupSelectNone");
        public string CleanButtonText => L("CleanupDeleteButton");
        public string FileColumnText => L("CleanupFileColumn");
        public string CategoryColumnText => L("CleanupCategoryColumn");
        public string SizeColumnText => L("CleanupSizeColumn");
        public string ModifiedColumnText => L("CleanupModifiedColumn");
        public string EmptyText => L("CleanupNoFiles");

        private string L(string key) => LocalizationHelper.Get(_mainViewModel.Language, key);

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):0.0} GB";
            if (bytes >= 1024L * 1024)
                return $"{bytes / (1024.0 * 1024):0.0} MB";
            if (bytes >= 1024L)
                return $"{bytes / 1024.0:0.0} KB";
            return $"{bytes} B";
        }

        private async Task ScanAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            LastErrorText = string.Empty;
            StatusText = L("CleanupScanning");
            ClearRows();

            try
            {
                var targets = CleanupService.GetTargets();
                var candidates = await Task.Run(() => CleanupService.Scan(targets, CancellationToken.None));
                foreach (var candidate in candidates)
                {
                    var row = new CleanupFileRow(candidate, GetCategoryText(candidate.Kind));
                    row.PropertyChanged += Row_PropertyChanged;
                    _rows.Add(row);
                    Files.Add(row);
                }

                StatusText = candidates.Count == 0
                    ? L("CleanupNoFiles")
                    : string.Format(L("CleanupFoundFormat"), candidates.Count, FormatBytes(candidates.Sum(c => c.SizeBytes)));

                NotifySelectionChanged();
            }
            catch (Exception ex)
            {
                StatusText = L("CleanupScanFailed");
                LastErrorText = ex.Message;
            }
            finally
            {
                IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task CleanSelectedAsync()
        {
            if (IsBusy)
                return;

            var selected = _rows.Where(row => row.IsSelected).ToList();
            if (selected.Count == 0)
                return;

            var selectedBytes = selected.Sum(row => row.Candidate.SizeBytes);
            var confirmation = string.Format(
                L("CleanupConfirmMessageFormat"),
                selected.Count,
                FormatBytes(selectedBytes));

            if (System.Windows.MessageBox.Show(
                    confirmation,
                    L("CleanupConfirmTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            LastErrorText = string.Empty;
            StatusText = L("CleanupDeleting");

            try
            {
                var candidates = selected.Select(row => row.Candidate).ToList();
                var result = await Task.Run(() => CleanupService.Delete(candidates, CancellationToken.None));
                var deletedPaths = new HashSet<string>(result.DeletedPaths, StringComparer.OrdinalIgnoreCase);

                foreach (var row in _rows.Where(row => deletedPaths.Contains(row.Candidate.Path)).ToList())
                {
                    row.PropertyChanged -= Row_PropertyChanged;
                    _rows.Remove(row);
                    Files.Remove(row);
                }

                StatusText = string.Format(
                    L("CleanupDoneFormat"),
                    result.DeletedCount,
                    FormatBytes(result.DeletedBytes));
                LastErrorText = result.Errors.Count == 0
                    ? string.Empty
                    : string.Format(L("CleanupErrorsFormat"), result.Errors.Count, string.Join(Environment.NewLine, result.Errors.Take(8)));

                NotifySelectionChanged();
            }
            catch (Exception ex)
            {
                StatusText = L("CleanupDeleteFailed");
                LastErrorText = ex.Message;
            }
            finally
            {
                IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SetAllSelected(bool selected)
        {
            foreach (var row in _rows)
            {
                row.IsSelected = selected;
            }

            NotifySelectionChanged();
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                row.PropertyChanged -= Row_PropertyChanged;
            }

            _rows.Clear();
            Files.Clear();
            NotifySelectionChanged();
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(CleanupFileRow.IsSelected), StringComparison.Ordinal))
            {
                NotifySelectionChanged();
            }
        }

        private void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedBytes));
            OnPropertyChanged(nameof(SummaryText));
            CommandManager.InvalidateRequerySuggested();
        }

        private string GetCategoryText(CleanupTargetKind kind)
        {
            return kind switch
            {
                CleanupTargetKind.NvidiaDxCache => L("CleanupTargetNvidiaDx"),
                CleanupTargetKind.NvidiaGlCache => L("CleanupTargetNvidiaGl"),
                CleanupTargetKind.DirectXShaderCache => L("CleanupTargetDirectX"),
                CleanupTargetKind.IconGridTraceLog => L("CleanupTargetIconGridTrace"),
                CleanupTargetKind.IconGridErrorLog => L("CleanupTargetIconGridError"),
                CleanupTargetKind.IconGridBenchmarks => L("CleanupTargetIconGridBenchmarks"),
                CleanupTargetKind.IconGridAfterburnerBackups => L("CleanupTargetIconGridAfterburner"),
                CleanupTargetKind.IconGridConfigBackups => L("CleanupTargetIconGridConfig"),
                _ => kind.ToString()
            };
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.Language), StringComparison.OrdinalIgnoreCase))
            {
                NotifyLabelsChanged();
            }
        }

        private void NotifyLabelsChanged()
        {
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageIntro));
            OnPropertyChanged(nameof(SafetyText));
            OnPropertyChanged(nameof(ScanButtonText));
            OnPropertyChanged(nameof(SelectAllButtonText));
            OnPropertyChanged(nameof(SelectNoneButtonText));
            OnPropertyChanged(nameof(CleanButtonText));
            OnPropertyChanged(nameof(FileColumnText));
            OnPropertyChanged(nameof(CategoryColumnText));
            OnPropertyChanged(nameof(SizeColumnText));
            OnPropertyChanged(nameof(ModifiedColumnText));
            OnPropertyChanged(nameof(EmptyText));
            OnPropertyChanged(nameof(SummaryText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

