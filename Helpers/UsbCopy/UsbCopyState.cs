using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// INotifyPropertyChanged state container for the Fast USB Copy feature.
    /// Owned by UsbCopyViewModel; keeps feature state out of MainViewModel.
    /// </summary>
    public sealed class UsbCopyState : INotifyPropertyChanged
    {
        private readonly ObservableDeviceCollection _devices = new();
        private UsbDeviceInfo? _selectedDevice;
        private int _bufferSize = 1024 * 1024;
        private int _workerCount = 8;
        private bool _isCopying;
        private bool _isBenchmarking;
        private double _liveMiBS;
        private double _averageMiBS;
        private double _peakMiBS;
        private string _etaText = string.Empty;
        private double _progress;
        private string _pipelineStatus = string.Empty;
        private string _logContent = string.Empty;
        private string _lastError = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Observable device list: WPF ComboBox cannot refresh a plain IReadOnlyList
        /// unless the whole reference is swapped every time. The observable collection
        /// notifies the UI as devices are added/cleared, keeping the Drives dropdown
        /// in sync after RefreshDevices().
        /// </summary>
        public ObservableDeviceCollection Devices => _devices;

        public UsbDeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set => SetField(ref _selectedDevice, value);
        }

        public int BufferSize
        {
            get => _bufferSize;
            set => SetField(ref _bufferSize, value);
        }

        public int WorkerCount
        {
            get => _workerCount;
            set => SetField(ref _workerCount, value);
        }

        public bool IsCopying
        {
            get => _isCopying;
            set => SetField(ref _isCopying, value);
        }

        public bool IsBenchmarking
        {
            get => _isBenchmarking;
            set => SetField(ref _isBenchmarking, value);
        }

        public double LiveMiBS
        {
            get => _liveMiBS;
            set => SetField(ref _liveMiBS, value);
        }

        public double AverageMiBS
        {
            get => _averageMiBS;
            set => SetField(ref _averageMiBS, value);
        }

        public double PeakMiBS
        {
            get => _peakMiBS;
            set => SetField(ref _peakMiBS, value);
        }

        public string EtaText
        {
            get => _etaText;
            set => SetField(ref _etaText, value);
        }

        public double Progress
        {
            get => _progress;
            set => SetField(ref _progress, value);
        }

        public string PipelineStatus
        {
            get => _pipelineStatus;
            set => SetField(ref _pipelineStatus, value);
        }

        public string LogContent
        {
            get => _logContent;
            set => SetField(ref _logContent, value);
        }

        public string LastError
        {
            get => _lastError;
            set => SetField(ref _lastError, value);
        }

        public ObservableBenchmarkResults BenchmarkResults { get; } = new();

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Observable collection wrapper for benchmark results so the UI can bind
    /// without depending on System.Collections.ObjectModel in the state file.
    /// </summary>
    public sealed class ObservableBenchmarkResults : System.Collections.ObjectModel.ObservableCollection<UsbBenchmarkResult>
    {
    }

    /// <summary>
    /// Observable collection of drives so the WPF Drives dropdown updates when
    /// RefreshDevices() adds/clears entries.
    /// </summary>
    public sealed class ObservableDeviceCollection : System.Collections.ObjectModel.ObservableCollection<UsbDeviceInfo>
    {
    }
}