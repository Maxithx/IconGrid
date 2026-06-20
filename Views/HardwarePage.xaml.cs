using System.ComponentModel;
using System.Threading.Tasks;
using IconGrid.Helpers;

namespace IconGrid.Views;

public partial class HardwarePage : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    private HardwareOverview _overview = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public HardwareOverview Overview
{
    get => _overview;
    private set
    {
        _overview = value;
        OnPropertyChanged(nameof(Overview));
        OnPropertyChanged(nameof(IsAsusMotherboard));
        OnPropertyChanged(nameof(IsAmdCpu));
        OnPropertyChanged(nameof(CpuBadgeBackground));
        OnPropertyChanged(nameof(GpuBadgeBackground));
        // Tilføj disse tre linjer:
        OnPropertyChanged(nameof(IsNvidiaGpu));
        OnPropertyChanged(nameof(IsNvidiaRtx));
        OnPropertyChanged(nameof(IsNvidiaGtx));
    }
}

    public bool IsAsusMotherboard =>
        Overview.Board.Manufacturer.Contains("ASUS", System.StringComparison.OrdinalIgnoreCase) ||
        Overview.Board.Manufacturer.Contains("ASUSTeK", System.StringComparison.OrdinalIgnoreCase);

    public bool IsAmdCpu =>
        string.Equals(Overview.Cpu.Vendor, "AMD", System.StringComparison.OrdinalIgnoreCase);
    public bool IsNvidiaGpu =>
    Overview.Gpu.Vendor.Contains("NVIDIA", System.StringComparison.OrdinalIgnoreCase);

public bool IsNvidiaRtx =>
    IsNvidiaGpu && Overview.Gpu.Name.Contains("RTX", System.StringComparison.OrdinalIgnoreCase);

public bool IsNvidiaGtx =>
    IsNvidiaGpu && Overview.Gpu.Name.Contains("GTX", System.StringComparison.OrdinalIgnoreCase);

    public System.Windows.Media.Brush CpuBadgeBackground => CreateVendorBrush(Overview.Cpu.Vendor);
    public System.Windows.Media.Brush GpuBadgeBackground => CreateVendorBrush(Overview.Gpu.Vendor);

    public HardwarePage()
    {
        InitializeComponent();
        Loaded += HardwarePage_Loaded;
    }

    private async void HardwarePage_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Loaded -= HardwarePage_Loaded;
        Overview = await Task.Run(HardwareInfoProvider.LoadOverview);
    }

    private static System.Windows.Media.Brush CreateVendorBrush(string? vendor)
    {
        if (string.Equals(vendor, "AMD", System.StringComparison.OrdinalIgnoreCase))
        {
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 88, 12));
        }

        if (string.Equals(vendor, "Intel", System.StringComparison.OrdinalIgnoreCase))
        {
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
        }

        if (string.Equals(vendor, "NVIDIA", System.StringComparison.OrdinalIgnoreCase))
        {
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
        }

        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
