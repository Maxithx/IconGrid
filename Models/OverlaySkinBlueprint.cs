using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using IconGrid.Helpers.Skin;
using Newtonsoft.Json;

namespace IconGrid.Models
{
    /// <summary>
    /// Data-driven blueprint for the gaming overlay: layers (painters) plus a layout list of
    /// modular blocks. This is the "skin JSON" concept: the overlay row is composed from data, so
    /// blocks can be reordered, added or restyled without touching XAML.
    ///
    /// A blueprint never owns layout geometry that the monitor settings control (overlay scale,
    /// bar height) — it describes ORDER, VISIBILITY and STYLING of blocks.
    /// </summary>
    public sealed class OverlaySkinBlueprint : INotifyPropertyChanged
    {
        public const string DefaultSkinId = "cod_mw4_tactical_v1";

        private string _skinId = DefaultSkinId;
        private string _name = "Modern Warfare 4 Gold Edition";
        private OverlaySkinTheme _theme = new();
        private List<OverlayLayer> _layers = new();
        private List<OverlayBlock> _layout = new();
        private List<OverlayLayerGroup> _groups = new();
        private List<OverlayBlock> _chrome = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string SkinId
        {
            get => _skinId;
            set => SetField(ref _skinId, string.IsNullOrWhiteSpace(value) ? DefaultSkinId : value);
        }

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value ?? string.Empty);
        }

        public OverlaySkinTheme Theme
        {
            get => _theme;
            set => SetField(ref _theme, value ?? new OverlaySkinTheme());
        }

        public List<OverlayLayer> Layers
        {
            get => _layers;
            set => SetField(ref _layers, value ?? new List<OverlayLayer>());
        }

        public List<OverlayBlock> Layout
        {
            get => _layout;
            set
            {
                if (SetField(ref _layout, value ?? new List<OverlayBlock>()))
                {
                    OnPropertyChanged(nameof(LayoutView));
                }
            }
        }

        /// <summary>
        /// Fixed overlay chrome (the settings and close buttons). They are styled like modules —
        /// icon, size, colour, opacity — but they are never part of the scrollable module row, so
        /// they stay pinned to the right edge of the overlay.
        /// </summary>
        public List<OverlayBlock> Chrome
        {
            get => _chrome;
            set => SetField(ref _chrome, EnsureChrome(value));
        }

        private static List<OverlayBlock> EnsureChrome(List<OverlayBlock>? chrome)
        {
            var result = chrome ?? new List<OverlayBlock>();

            if (result.All(block => block.Block != "background"))
            {
                result.Insert(0, new OverlayBlock { Block = "background" });
            }

            if (result.All(block => block.Block != "settings"))
            {
                result.Add(new OverlayBlock { Block = "settings" });
            }

            if (result.All(block => block.Block != "close"))
            {
                result.Add(new OverlayBlock { Block = "close" });
            }

            return result;
        }

        /// <summary>
        /// Folders for the layer panel. Always has at least one group so every module has a home.
        /// </summary>
        public List<OverlayLayerGroup> Groups
        {
            get => _groups;
            set => SetField(ref _groups, EnsureGroups(value));
        }

        private static List<OverlayLayerGroup> EnsureGroups(List<OverlayLayerGroup>? groups)
        {
            var result = groups ?? new List<OverlayLayerGroup>();
            if (result.Count == 0)
            {
                result.Add(new OverlayLayerGroup());
            }

            // The first group is the shipped base group: it is always protected and keeps the
            // standard name. Blueprints saved by earlier builds carry neither the flag nor the
            // new name, so mark and migrate them here (never rename a name the user chose).
            var systemGroup = result[0];
            systemGroup.IsSystem = true;

            if (string.IsNullOrWhiteSpace(systemGroup.Name) ||
                string.Equals(systemGroup.Name, OverlayLayerGroup.LegacyDefaultName, StringComparison.Ordinal))
            {
                systemGroup.Name = OverlayLayerGroup.DefaultName;
            }

            return result;
        }

        /// <summary>Blocks that should actually be rendered (visible + known type).</summary>
        [JsonIgnore]
        public IReadOnlyList<OverlayBlock> LayoutView =>
            _layout.Where(block => block.IsVisible).ToList();

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static OverlaySkinBlueprint FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return OverlayBlueprintDefaults.CreateClassic();
            }

            try
            {
                var blueprint = JsonConvert.DeserializeObject<OverlaySkinBlueprint>(json);
                if (blueprint == null)
                {
                    return OverlayBlueprintDefaults.CreateClassic();
                }

                // Migration: the first showcase blueprint (MW4) used tag/logo/controls blocks that
                // have no render templates yet. Fall back to the classic module order instead of
                // showing raw block objects in the overlay.
                if (string.Equals(blueprint.SkinId, "cod_mw4_tactical_v1", StringComparison.OrdinalIgnoreCase))
                {
                    return OverlayBlueprintDefaults.CreateClassic();
                }

                // Older blueprints have no "chrome" section: the settings/close buttons were
                // hardcoded. Materialise the two default chrome blocks so they show up as layers.
                blueprint.Chrome = EnsureChrome(blueprint.Chrome);

                // Older blueprints have no layer groups: give them the default "Gaming overlay"
                // group so every module belongs to a folder in the panel.
                blueprint.Groups = EnsureGroups(blueprint.Groups);

                return blueprint;
            }
            catch
            {
                return OverlayBlueprintDefaults.CreateClassic();
            }
        }

        public OverlaySkinBlueprint Clone() => FromJson(ToJson());

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private void OnPropertyChanged(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Colour/font theme of a blueprint (maps to the skin tokens when applied).</summary>
    public sealed class OverlaySkinTheme
    {
        public string PrimaryColor { get; set; } = "#00FF66";
        public string AccentColor { get; set; } = "#C5A059";
        public string CamoImage { get; set; } = string.Empty;
        public string FontFamily { get; set; } = "Bahnschrift";
    }

    /// <summary>
    /// One paint layer of the overlay background (Z-order: first = bottom).
    /// Types: solid, gradient, camo, pattern, image, scanlines.
    /// </summary>
    public sealed class OverlayLayer
    {
        public string Type { get; set; } = "solid";
        public bool Enabled { get; set; } = true;
        public double Opacity { get; set; } = 1.0;
        public string BlendMode { get; set; } = "normal";
        public string Color { get; set; } = "#000000";
        public string Color2 { get; set; } = "#000000";
        public string Source { get; set; } = string.Empty;
        public string PatternKind { get; set; } = "camo";
    }

    /// <summary>
    /// A folder in the layer panel. Pure organisation: it groups modules so the layer list stays
    /// readable (e.g. everything from the shipped overlay in one "Gaming overlay" group).
    /// </summary>
    public sealed class OverlayLayerGroup
    {
        /// <summary>Name of the shipped base group (the one every module starts in).</summary>
        public const string DefaultName = "Standard Gaming Overlay";

        /// <summary>Name earlier builds wrote to settings; migrated on load.</summary>
        public const string LegacyDefaultName = "Gaming overlay";

        public string Id { get; set; } = "main";
        public string Name { get; set; } = DefaultName;

        /// <summary>
        /// The shipped base group that holds the core overlay modules. It is always the first
        /// group and can never be deleted, so the user cannot lose the standard telemetry layout.
        /// </summary>
        public bool IsSystem { get; set; }

        /// <summary>Eye on the folder: hides every module in the group without deleting it.</summary>
        public bool IsHidden { get; set; }

        /// <summary>Locked folders cannot be deleted from the layer panel.</summary>
        public bool IsLocked { get; set; }
    }

    /// <summary>
    /// One modular overlay block. <see cref="Block"/> selects the WPF data template;
    /// <see cref="Props"/> carries block-specific settings exactly like the reference blueprint.
    /// </summary>
    public sealed class OverlayBlock
    {
        public string Block { get; set; } = "stat";
        public Dictionary<string, string> Props { get; set; } = new();

        /// <summary>
        /// Last code snippet applied to this module (a JSON property patch). Kept so the Skin Studio
        /// can show and re-edit it. The properties themselves live in <see cref="Props"/>.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Snippet { get; set; }

        /// <summary>
        /// Optional custom XAML DataTemplate (niveau B snippet) that replaces the built-in template
        /// for this module. Parsed by <c>OverlaySnippetTemplateFactory</c>; invalid markup falls back
        /// to the standard template.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Template { get; set; }

        // ---------- Convenience accessors used by the WPF templates ----------

        [JsonIgnore] public string Id => Get("id");
        [JsonIgnore] public string Label => Get("label");
        [JsonIgnore] public string Text => Get("text");
        [JsonIgnore] public string Sub => Get("sub");
        [JsonIgnore] public string Code => Get("code");
        [JsonIgnore] public string ColorHex => Get("color");
        [JsonIgnore] public string VramColor => Get("vramIndicator");
        [JsonIgnore] public string Source => Get("src");
        [JsonIgnore] public string DataKey => Get("data");
        [JsonIgnore] public bool Highlight => GetBool("highlight");
        [JsonIgnore] public bool ShowDot => GetBool("showDot", true);
        [JsonIgnore] public bool HasGrid => GetBool("hasGrid", true);
        [JsonIgnore] public bool IsVisible => !GetBool("hidden");
        [JsonIgnore] public double Width => GetDouble("width");

        /// <summary>Per-module opacity (props["opacity"], default 1.0).</summary>
        [JsonIgnore]
        public double OpacityValue
        {
            get
            {
                var raw = Get("opacity");
                return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                    ? System.Math.Max(0.0, System.Math.Min(1.0, parsed))
                    : 1.0;
            }
        }

        /// <summary>Eye state for the layer list (hidden modules are dimmed).</summary>
        [JsonIgnore]
        public bool IsHidden => !IsVisible;

        /// <summary>
        /// Panel-only name for a gauge: shows what the module actually contains (usage meter and,
        /// when enabled, temperature). Kept out of <see cref="DisplayLabel"/>, so the overlay chip
        /// still reads just "CPU" / "GPU".
        /// </summary>
        [JsonIgnore]
        public string GaugeDisplayName =>
            $"{CpuGpuLabel}{(ShowTemp ? " (Usage + Temp)" : " (Usage)")}";

        /// <summary>"CPU" or "GPU" for the gauge/usage/temp family.</summary>
        [JsonIgnore]
        public string CpuGpuLabel => IsGpu ? "GPU" : "CPU";

        // ---------- Gauge module appearance (CPU / GPU usage) ----------

        /// <summary>Meter rendering style for gauge blocks: bar | ring | segments | text.</summary>
        [JsonIgnore]
        public string MeterStyle
        {
            get
            {
                var raw = Get("meter").Trim().ToLowerInvariant();
                return raw is "ring" or "segments" or "text" ? raw : "bar";
            }
        }

        /// <summary>True for the GPU gauge/temp/usage module so one template can serve CPU and GPU.</summary>
        [JsonIgnore]
        public bool IsGpu =>
            (string.Equals(Block, "gauge", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(Block, "usage", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(Block, "temp", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(Id, "gpu", StringComparison.OrdinalIgnoreCase);

        /// <summary>True for modules that render a usage meter (gauge = combined, usage = meter only).</summary>
        [JsonIgnore]
        public bool IsMeterModule =>
            string.Equals(Block, "gauge", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Block, "usage", StringComparison.OrdinalIgnoreCase);

        /// <summary>Label shown in the overlay chip; falls back to CPU/GPU when the label is empty.</summary>
        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                var label = Label;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }

                return Block switch
                {
                    "gauge" or "usage" or "temp" => CpuGpuLabel,
                    "ping" => "Net",
                    _ => string.Empty
                };
            }
        }

        /// <summary>
        /// Show the temperature next to the meter. A "usage" module never shows it (that lives in
        /// its own "temp" module), a "temp" module always does.
        /// </summary>
        [JsonIgnore]
        public bool ShowTemp => Block switch
        {
            "usage" => false,
            "temp" => true,
            _ => GetBool("showTemp", true)
        };

        /// <summary>Show the usage percentage as text (gauge blocks).</summary>
        [JsonIgnore]
        public bool ShowPercent => GetBool("showPercent");

        /// <summary>Meter width in DIPs (props["meterWidth"]); default depends on the meter style.</summary>
        [JsonIgnore]
        public double MeterWidthValue =>
            GetPositive("meterWidth", MeterStyle switch { "segments" => 56, "ring" => 16, _ => 12 });

        /// <summary>Meter height in DIPs (props["meterHeight"]); default depends on the meter style.</summary>
        [JsonIgnore]
        public double MeterHeightValue =>
            GetPositive("meterHeight", MeterStyle switch { "segments" => 10, "ring" => 16, _ => 14 });

        /// <summary>Per-module meter colour override (props["meterColor"] or legacy props["color"]).</summary>
        [JsonIgnore]
        public string MeterColorHex
        {
            get
            {
                var explicitColor = Get("meterColor");
                return string.IsNullOrWhiteSpace(explicitColor) ? ColorHex : explicitColor;
            }
        }

        /// <summary>Parsed meter colour, or null when the module inherits the skin meter brush.</summary>
        [JsonIgnore]
        public System.Windows.Media.Brush? MeterBrush =>
            IconGrid.Helpers.Skin.OverlayBlockBrushes.FromHex(MeterColorHex);

        /// <summary>True when a valid per-module meter colour was set (templates then skip the skin brush).</summary>
        [JsonIgnore]
        public bool HasMeterColor => MeterBrush != null;

        // ---------- Overlay chrome (settings / close buttons) ----------

        /// <summary>True for the fixed overlay chrome entries (background + buttons).</summary>
        [JsonIgnore]
        public bool IsChromeButton =>
            string.Equals(Block, "settings", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Block, "close", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Block, "background", StringComparison.OrdinalIgnoreCase);

        /// <summary>True for the layer-panel entry that represents the overlay background.</summary>
        [JsonIgnore]
        public bool IsBackgroundLayer =>
            string.Equals(Block, "background", StringComparison.OrdinalIgnoreCase);

        /// <summary>True for a layer-panel group header (a folder, not a module).</summary>
        [JsonIgnore]
        public bool IsGroupLayer => string.Equals(Block, "group", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when a group header row is the protected standard group.</summary>
        [JsonIgnore]
        public bool IsSystemGroup => IsGroupLayer && GetBool("system");

        /// <summary>Group this module belongs to (props["group"]); empty means the first group.</summary>
        [JsonIgnore]
        public string GroupId => Get("group");

        /// <summary>Display name of a group header row.</summary>
        [JsonIgnore]
        public string GroupName => Get("name");

        /// <summary>True when a group folder is collapsed in the layer panel.</summary>
        [JsonIgnore]
        public bool IsCollapsed => GetBool("collapsed");

        /// <summary>Locked rows cannot be moved, regrouped or deleted from the layer panel.</summary>
        [JsonIgnore]
        public bool IsLocked => GetBool("locked");

        /// <summary>
        /// Panel-only: the owning group is hidden, so the row is dimmed even though this module is
        /// not hidden itself. Never serialized — it is recomputed when the layer list is built.
        /// </summary>
        [JsonIgnore]
        public bool IsGroupHidden { get; set; }

        /// <summary>
        /// Panel-only: the row belongs to the protected standard group, so it can never be
        /// deleted or moved out of it (the standard telemetry layout must stay intact).
        /// Never serialized — recomputed when the layer list is built.
        /// </summary>
        [JsonIgnore]
        public bool IsSystemGroupMember { get; set; }

        /// <summary>True when the layer panel must refuse to delete this row.</summary>
        [JsonIgnore]
        public bool IsDeleteBlocked => IsGroupLayer ? IsSystemGroup : IsSystemGroupMember;

        /// <summary>True for the two action buttons (settings + close).</summary>
        [JsonIgnore]
        public bool IsButtonLayer =>
            string.Equals(Block, "settings", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Block, "close", StringComparison.OrdinalIgnoreCase);

        /// <summary>Segoe MDL2 glyph for a chrome button (props["icon"], else the built-in glyph).</summary>
        [JsonIgnore]
        public string ButtonGlyph
        {
            get
            {
                var icon = Get("icon");
                if (!string.IsNullOrWhiteSpace(icon))
                {
                    return icon;
                }

                return string.Equals(Block, "settings", StringComparison.OrdinalIgnoreCase)
                    ? "\uE713"
                    : "\uE8BB";
            }
        }

        /// <summary>Button width in DIPs (props["buttonWidth"]); both overlay buttons default to 36.</summary>
        [JsonIgnore]
        public double ButtonWidthValue => GetPositive("buttonWidth", 36);

        /// <summary>Button height in DIPs (props["buttonHeight"], default 32).</summary>
        [JsonIgnore]
        public double ButtonHeightValue => GetPositive("buttonHeight", 32);

        /// <summary>
        /// Border thickness in DIPs (props["borderThickness"], default 1). Set it to 0 to remove the
        /// frame around the button; the skin's button border colour is used unless
        /// props["borderColor"] overrides it.
        /// </summary>
        [JsonIgnore]
        public double ButtonBorderThicknessValue => GetDoubleOr("borderThickness", 1);

        /// <summary>Border thickness as a WPF <see cref="System.Windows.Thickness"/> for binding.</summary>
        [JsonIgnore]
        public System.Windows.Thickness ButtonBorderThickness =>
            new(Math.Max(0, ButtonBorderThicknessValue));

        /// <summary>Per-button border colour override (props["borderColor"]).</summary>
        [JsonIgnore]
        public System.Windows.Media.Brush? BorderColorBrush =>
            IconGrid.Helpers.Skin.OverlayBlockBrushes.FromHex(Get("borderColor"));

        /// <summary>True when the button defines its own border colour.</summary>
        [JsonIgnore]
        public bool HasBorderColor => BorderColorBrush != null;

        /// <summary>
        /// Background override (props["background"]): a hex colour or "transparent". Null means the
        /// skin's button background brush is used.
        /// </summary>
        [JsonIgnore]
        public System.Windows.Media.Brush? ButtonBackgroundBrush
        {
            get
            {
                var raw = Get("background").Trim();
                if (raw.Length == 0)
                {
                    return null;
                }

                if (string.Equals(raw, "transparent", StringComparison.OrdinalIgnoreCase))
                {
                    return System.Windows.Media.Brushes.Transparent;
                }

                return IconGrid.Helpers.Skin.OverlayBlockBrushes.FromHex(raw);
            }
        }

        /// <summary>True when the button sets its own background (including "transparent").</summary>
        [JsonIgnore]
        public bool HasButtonBackground => ButtonBackgroundBrush != null;

        /// <summary>Parsed glyph colour override, or null when the skin text button brush is used.</summary>
        [JsonIgnore]
        public System.Windows.Media.Brush? ColorBrush =>
            IconGrid.Helpers.Skin.OverlayBlockBrushes.FromHex(ColorHex);

        /// <summary>True when the module sets its own colour.</summary>
        [JsonIgnore]
        public bool HasColor => ColorBrush != null;

        /// <summary>
        /// Human readable module name for the Skin Studio module list. This is panel-only text:
        /// the overlay itself renders <see cref="DisplayLabel"/>, so suffixes like "(Temp)" and
        /// "(Ping)" never leak into the gaming overlay.
        /// </summary>
        [JsonIgnore]
        public string DisplayName => Block switch
        {
            "clock" => "Clock",
            "divider" => "Divider",
            "ping" => string.IsNullOrWhiteSpace(Label) ? "Net (Ping)" : $"{Label} (Ping)",
            "gauge" => GaugeDisplayName,
            "usage" => $"{CpuGpuLabel} (Usage)",
            "temp" => $"{CpuGpuLabel} (Temp)",
            "settings" => "Settings button",
            "close" => "Close button",
            "background" => "Overlay background",
            "group" => GroupName,
            "stat" => DataKey switch
            {
                "gpuClock" => "GPU clock",
                "frameTime" => string.IsNullOrWhiteSpace(Label) ? "FT" : Label,
                "fps" => string.IsNullOrWhiteSpace(Label) ? "FPS" : Label,
                _ => "Stat"
            },
            _ => Block
        };

        private string Get(string key) => Props != null && Props.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

        private bool GetBool(string key, bool fallback = false)
        {
            if (Props == null || !Props.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            return bool.TryParse(raw, out var parsed) ? parsed : fallback;
        }

        private double GetDouble(string key)
        {
            if (Props == null || !Props.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            return double.TryParse(raw, out var parsed) ? parsed : 0;
        }

        private double GetPositive(string key, double fallback)
        {
            var parsed = GetDouble(key);
            return parsed > 0 ? parsed : fallback;
        }

        private double GetDoubleOr(string key, double fallback)
        {
            if (Props == null || !Props.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
        }
    }
}
