using IconGrid.Models;

namespace IconGrid.Helpers.Skin
{
    /// <summary>
    /// Starting XAML for the "Custom template" editor. The Skin Studio loads the markup that matches
    /// the selected module (meter style, temp, clock, ping, stats, divider), so the user immediately
    /// sees which data the module exposes instead of an empty box.
    ///
    /// The snippets avoid converters and StaticResource lookups on purpose: they are parsed before
    /// the template is attached to the visual tree, so anything referring to the bar's resources
    /// would fail to load. Skin colours are reached with ordinary bindings instead.
    /// </summary>
    public static class OverlayTemplateCatalog
    {
        private const string Rel = "RelativeSource={RelativeSource AncestorType=ItemsControl}";

        /// <summary>XAML that shows the selected module's own data, ready to be edited.</summary>
        public static string GetStarter(OverlayBlock? block)
        {
            if (block == null)
            {
                return OverlaySnippetTemplateFactory.StarterExample;
            }

            var isGpu = block.IsGpu;
            var temp = Monitor(isGpu ? "GpuTemp" : "CpuTemp");
            var usage = Monitor(isGpu ? "GpuUsagePercent" : "CpuUsagePercent");
            var meter = Skin(isGpu ? "GpuMeterBrush" : "CpuMeterBrush");
            var text = Skin("TextBrush");
            var chip =
                $"<Border Background=\"{Skin("ChipBackgroundBrush")}\" BorderBrush=\"{Skin("ChipBorderBrush")}\"\n" +
                "        BorderThickness=\"1\" CornerRadius=\"2\" Padding=\"6,2\" Margin=\"0,0,9,0\">\n" +
                "    <StackPanel Orientation=\"Horizontal\" VerticalAlignment=\"Center\">\n" +
                $"        <TextBlock Text=\"{{Binding DisplayLabel, StringFormat={{}}{{0}}:}}\" Margin=\"0,0,6,0\" Foreground=\"{text}\" />\n";

            return block.Block switch
            {
                "clock" => $"<TextBlock Margin=\"0,0,9,0\" Foreground=\"{text}\" Text=\"{Monitor("ClockText")}\" />",

                "ping" =>
                    "<StackPanel Orientation=\"Horizontal\" Margin=\"0,0,9,0\">\n" +
                    "    <Ellipse Width=\"10\" Height=\"10\" Fill=\"#7CFF6B\" VerticalAlignment=\"Center\" Margin=\"0,0,6,0\" />\n" +
                    $"    <TextBlock Foreground=\"{text}\" Text=\"{{Binding DisplayLabel}}\" />\n" +
                    $"    <TextBlock Foreground=\"{text}\" Text=\"{Monitor("NetworkStatus")}\" />\n" +
                    "</StackPanel>",

                "divider" =>
                    $"<Border Width=\"1\" Height=\"14\" Margin=\"0,0,12,0\" VerticalAlignment=\"Center\" Background=\"{Skin("DividerBrush")}\" />",

                "temp" => chip +
                    $"        <TextBlock Text=\"{temp}\" Foreground=\"{text}\" />\n" +
                    "    </StackPanel>\n" +
                    "</Border>",

                "gauge" or "usage" when block.MeterStyle == "text" => chip +
                    $"        <TextBlock Text=\"{usage}\" FontWeight=\"Bold\" Foreground=\"{meter}\" />\n" +
                    "    </StackPanel>\n" +
                    "</Border>",

                "gauge" or "usage" => chip +
                    $"        <ProgressBar Width=\"60\" Height=\"10\" Minimum=\"0\" Maximum=\"100\" Value=\"{usage}\"\n" +
                    $"                     Foreground=\"{meter}\" Background=\"#22000000\" BorderThickness=\"0\" />\n" +
                    "    </StackPanel>\n" +
                    "</Border>",

                "stat" when block.DataKey == "fps" =>
                    "<StackPanel Orientation=\"Horizontal\" Margin=\"0,0,10,0\">\n" +
                    $"    <TextBlock Text=\"FPS: \" Foreground=\"{text}\" />\n" +
                    $"    <TextBlock Text=\"{Monitor("FpsStatus")}\" Foreground=\"{Skin("AccentBrush")}\" />\n" +
                    "</StackPanel>",

                "stat" when block.DataKey == "frameTime" =>
                    "<StackPanel Orientation=\"Horizontal\" Margin=\"0,0,9,0\">\n" +
                    $"    <TextBlock Text=\"FT: \" Foreground=\"{text}\" />\n" +
                    $"    <TextBlock Text=\"{Monitor("FrameTimeDisplay")}\" Foreground=\"{text}\" />\n" +
                    "</StackPanel>",

                "stat" =>
                    $"<TextBlock Width=\"58\" Margin=\"0,0,9,0\" TextAlignment=\"Right\" Foreground=\"{text}\"\n" +
                    $"           Text=\"{Monitor("GpuClock")}\" />",

                _ => OverlaySnippetTemplateFactory.StarterExample
            };
        }

        private static string Skin(string property) =>
            $"{{Binding DataContext.GamingOverlaySkin.{property}, {Rel}}}";

        private static string Monitor(string property) =>
            $"{{Binding DataContext.SystemMonitor.{property}, {Rel}}}";
    }
}
