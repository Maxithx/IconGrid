using System.Collections.Generic;
using IconGrid.Models;

namespace IconGrid.Helpers.Skin
{
    /// <summary>
    /// Factory for built-in overlay blueprints. The MW4 blueprint mirrors the tactical HUD
    /// reference (callout tag, brand, clock, ping, CPU/GPU gauges, GPU clock, frame time, FPS and
    /// the gold control group) as pure data.
    /// </summary>
    public static class OverlayBlueprintDefaults
    {
        /// <summary>
        /// Classic blueprint: exactly the module order the overlay used before the block system
        /// (clock | ping | CPU | GPU | GPU clock | FT | FPS) so the data-driven renderer can be
        /// verified as pixel-identical against the hand-built XAML.
        /// </summary>
        public static OverlaySkinBlueprint CreateClassic()
        {
            return new OverlaySkinBlueprint
            {
                SkinId = "icongrid_classic_v1",
                Name = "IconGrid Classic",
                Chrome = new List<OverlayBlock>(),
                Theme = new OverlaySkinTheme
                {
                    PrimaryColor = "#7CFF6B",
                    AccentColor = "#FFFFFF"
                },
                Layers = new List<OverlayLayer>
                {
                    new() { Type = "solid", Color = "#202020", Opacity = 1.0 }
                },
                Layout = new List<OverlayBlock>
                {
                    Block("clock"),
                    Block("divider"),
                    Block("ping", Props(("label", "Net"), ("showDot", "true"))),
                    Block("divider"),
                    Block("gauge", Props(("id", "cpu"), ("label", "CPU"), ("hasGrid", "true"))),
                    Block("divider"),
                    Block("gauge", Props(("id", "gpu"), ("label", "GPU"), ("hasGrid", "true"))),
                    Block("stat", Props(("data", "gpuClock"))),
                    Block("divider"),
                    Block("stat", Props(("data", "frameTime"), ("label", "FT"))),
                    Block("stat", Props(("data", "fps"), ("label", "FPS"), ("highlight", "true")))
                }
            };
        }

        public static OverlaySkinBlueprint CreateMw4()
        {
            return new OverlaySkinBlueprint
            {
                SkinId = "cod_mw4_tactical_v1",
                Name = "Modern Warfare 4 Gold Edition",
                Chrome = new List<OverlayBlock>(),
                Theme = new OverlaySkinTheme
                {
                    PrimaryColor = "#00FF66",
                    AccentColor = "#C5A059",
                    FontFamily = "Chakra Petch, Bahnschrift, Segoe UI"
                },
                Layers = new List<OverlayLayer>
                {
                    new() { Type = "solid", Color = "#0B0D0F", Opacity = 1.0 },
                    new() { Type = "camo", PatternKind = "camo", Opacity = 0.85, BlendMode = "overlay" },
                    new() { Type = "scanlines", Opacity = 0.35 }
                },
                Layout = new List<OverlayBlock>
                {
                    Block("tag", Props(("code", "X-01"), ("sub", "BLACK SITE"))),
                    Block("logo", Props(("text", "MW4"), ("effect", "matrix-glow"))),
                    Block("clock", Props(("format", "mm:ss"))),
                    Block("divider"),
                    Block("ping", Props(("label", "Net"), ("showDot", "true"))),
                    Block("divider"),
                    Block("gauge", Props(("id", "cpu"), ("label", "CPU"), ("color", "#00FF66"), ("hasGrid", "true"))),
                    Block("divider"),
                    Block("gauge", Props(("id", "gpu"), ("label", "GPU"), ("color", "#00FF66"), ("hasGrid", "true"), ("vramIndicator", "#A855F7"))),
                    Block("divider"),
                    Block("stat", Props(("data", "gpuClock"))),
                    Block("divider"),
                    Block("stat", Props(("data", "frameTime"), ("label", "FT"))),
                    Block("divider"),
                    Block("stat", Props(("data", "fps"), ("label", "FPS"), ("highlight", "true"))),
                    Block("controls", Props(("style", "gold-octagon")))
                }
            };
        }

        private static OverlayBlock Block(string type, Dictionary<string, string>? props = null) =>
            new() { Block = type, Props = props ?? new Dictionary<string, string>() };

        private static Dictionary<string, string> Props(params (string Key, string Value)[] entries)
        {
            var props = new Dictionary<string, string>();
            foreach (var (key, value) in entries)
            {
                props[key] = value;
            }

            return props;
        }
    }
}
