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
    /// One modular overlay block. <see cref="Block"/> selects the WPF data template;
    /// <see cref="Props"/> carries block-specific settings exactly like the reference blueprint.
    /// </summary>
    public sealed class OverlayBlock
    {
        public string Block { get; set; } = "stat";
        public Dictionary<string, string> Props { get; set; } = new();

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

        /// <summary>Human readable module name for the Skin Studio module list.</summary>
        [JsonIgnore]
        public string DisplayName => Block switch
        {
            "clock" => "Clock",
            "divider" => "Divider",
            "ping" => string.IsNullOrEmpty(Label) ? "Net" : Label,
            "gauge" => string.Equals(Id, "gpu", System.StringComparison.OrdinalIgnoreCase) ? "GPU" : "CPU",
            "stat" => DataKey switch
            {
                "gpuClock" => "GPU clock",
                "frameTime" => string.IsNullOrEmpty(Label) ? "FT" : Label,
                "fps" => string.IsNullOrEmpty(Label) ? "FPS" : Label,
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
    }
}
