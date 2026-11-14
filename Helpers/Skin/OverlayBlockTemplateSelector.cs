using System.Windows;
using System.Windows.Controls;
using IconGrid.Models;

namespace IconGrid.Helpers.Skin
{
    /// <summary>
    /// Chooses the WPF data template for an <see cref="OverlayBlock"/>. This is the "container
    /// renderer" from the blueprint architecture: the overlay row is data, and each block type maps
    /// to a template (clock, ping, CPU/GPU gauge, stats, divider...).
    /// </summary>
    public sealed class OverlayBlockTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? Clock { get; set; }
        public DataTemplate? Ping { get; set; }
        public DataTemplate? GaugeCpu { get; set; }
        public DataTemplate? GaugeGpu { get; set; }
        public DataTemplate? StatGpuClock { get; set; }
        public DataTemplate? StatFrameTime { get; set; }
        public DataTemplate? StatFps { get; set; }
        public DataTemplate? Divider { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is not OverlayBlock block)
            {
                return base.SelectTemplate(item, container);
            }

            return block.Block switch
            {
                "clock" => Clock,
                "ping" => Ping,
                "divider" => Divider,
                "gauge" => string.Equals(block.Id, "gpu", System.StringComparison.OrdinalIgnoreCase) ? GaugeGpu : GaugeCpu,
                "stat" => block.DataKey switch
                {
                    "gpuClock" => StatGpuClock,
                    "frameTime" => StatFrameTime,
                    "fps" => StatFps,
                    _ => null
                },
                _ => null
            };
        }
    }
}
