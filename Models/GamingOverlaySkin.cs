using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using WMedia = System.Windows.Media;

namespace IconGrid.Models
{
    /// <summary>
    /// Visual tokens ("skin") for the gaming overlay.
    ///
    /// A skin only PAINTS the existing overlay slots. It can never move, hide, reorder or resize
    /// anything: layout is owned by the monitor row settings (which items show, their order,
    /// spacing, overlay scale and bar height). This separation keeps a skin safe to apply while a
    /// game is running and lets the Skin Studio page edit colours/fonts without touching the
    /// overlay layout.
    /// </summary>
    public sealed class GamingOverlaySkin : INotifyPropertyChanged
    {
        public const string DefaultName = "Night Ops";

        /// <summary>Fonts offered in the skin editor (installed with Windows; nothing is bundled).</summary>
        public static readonly IReadOnlyList<string> AvailableFonts = new[]
        {
            "Segoe UI",
            "Bahnschrift",
            "Bahnschrift SemiCondensed",
            "Consolas",
            "Cascadia Mono",
            "Impact",
            "Arial Narrow",
            "Tahoma"
        };

        /// <summary>Monospace fonts used for the FPS / frame-time digits.</summary>
        public static readonly IReadOnlyList<string> AvailableMonoFonts = new[]
        {
            "Consolas",
            "Cascadia Mono",
            "Cascadia Code",
            "Courier New"
        };

        private string _name = DefaultName;
        private string _barBackgroundColor = "#202020";
        private string _popupBackgroundColor = "#202020";
        private string _textColor = "#FFFFFF";
        private string _accentColor = "#7CFF6B";
        private string _cpuBarColor = "#22C55E";
        private string _gpuBarColor = "#A855F7";
        private double _dividerOpacity = 0.8;
        private string _dividerColor = "#FFFFFF";
        private double _dividerWidth = 1;
        private double _dividerHeight = 12;
        private string _fontFamily = "Segoe UI";
        private string _monoFontFamily = "Consolas";
        private double _cornerRadius = 8;

        // ---------- Layered design tokens (chips / buttons / background art / logo) ----------
        private bool _showChips;
        private string _chipBackgroundColor = "#A6000000";
        private string _chipBorderColor = "#66FFFFFF";
        private double _chipCornerRadius = 4;
        private double _chipPadding = 0;
        private string _buttonBackgroundColor = "#33000000";
        private string _buttonBorderColor = "#66FFFFFF";
        private double _buttonCornerRadius = 4;
        private string _fontWeight = "Normal";
        private string _backgroundImagePath = string.Empty;
        private double _backgroundImageOpacity = 1.0;
        private string _backgroundTintColor = "#000000";
        private double _backgroundTintOpacity = 0.35;
        private string _logoImagePath = string.Empty;
        private double _logoWidth = 64;

        // ---------- Procedural effects (patterns / gradients / glow) ----------
        private string _patternKind = IconGrid.Helpers.Skin.SkinPatternFactory.PatternNone;
        private string _patternColorA = "#4A4E3B";
        private string _patternColorB = "#8B7D6B";
        private string _patternColorC = "#1F231A";
        private string _patternLineColor = "#33FFFFFF";
        private double _patternOpacity = 0.55;
        private bool _meterGradient = true;
        private string _meterGradientStartColor = "#059669";
        private string _meterGradientEndColor = "#00FF66";
        private bool _meterSegments = true;
        private string _meterSegmentColor = "#66000000";
        private bool _glowEnabled = true;
        private string _glowColor = "#00FF66";
        private double _glowRadius = 8;
        private bool _buttonGradient;
        private string _buttonGradientTopColor = "#B45309";
        private string _buttonGradientBottomColor = "#18181B";

        // ---------- MW4-style layout tokens (callout tag / brand / sizes / chamfer) ----------
        private double _fontSize = 13;
        private double _monoFontSize = 13;
        private bool _showBrand;
        private string _brandText = "MW4";
        private string _brandColor = "#00FF66";
        private bool _showCalloutTag;
        private string _calloutTopText = "X-01";
        private string _calloutBottomText = "BLACK SITE";
        private string _calloutAccentColor = "#C5A059";
        private string _cpuMeterBorderColor = "#80FFFFFF";
        private string _gpuMeterBorderColor = "#80FFFFFF";
        private bool _showGpuAccentLine;
        private string _gpuAccentLineColor = "#A855F7";
        private double _chamferSize;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, string.IsNullOrWhiteSpace(value) ? DefaultName : value);
        }

        public string BarBackgroundColor
        {
            get => _barBackgroundColor;
            set => SetColor(ref _barBackgroundColor, value, nameof(BarBackgroundColor), nameof(BarBackgroundBrush));
        }

        public string PopupBackgroundColor
        {
            get => _popupBackgroundColor;
            set => SetColor(ref _popupBackgroundColor, value, nameof(PopupBackgroundColor), nameof(PopupBackgroundBrush));
        }

        public string TextColor
        {
            get => _textColor;
            set => SetColor(ref _textColor, value, nameof(TextColor), nameof(TextBrush));
        }

        /// <summary>
        /// Colour of the "|" dividers between the monitor slots. Kept separate from the text colour
        /// so a skin can always keep the dividers clearly visible against dark backgrounds.
        /// </summary>
        public string DividerColor
        {
            get => _dividerColor;
            set => SetColor(ref _dividerColor, value, nameof(DividerColor), nameof(DividerBrush));
        }

        /// <summary>Accent colour used for the FPS value (and the scale slider).</summary>
        public string AccentColor
        {
            get => _accentColor;
            set => SetColor(ref _accentColor, value, nameof(AccentColor), nameof(AccentBrush));
        }

        public string CpuBarColor
        {
            get => _cpuBarColor;
            set => SetColor(ref _cpuBarColor, value, nameof(CpuBarColor), nameof(CpuBarBrush));
        }

        public string GpuBarColor
        {
            get => _gpuBarColor;
            set => SetColor(ref _gpuBarColor, value, nameof(GpuBarColor), nameof(GpuBarBrush));
        }

        public double DividerOpacity
        {
            get => _dividerOpacity;
            set
            {
                // Floor at 0.6: on dark/camo backgrounds dividers below this are invisible,
                // which made the monitor slots blend together.
                var clamped = Math.Max(0.6, Math.Min(1.0, value));
                if (SetField(ref _dividerOpacity, clamped))
                {
                    OnPropertyChanged(nameof(DividerBrush));
                }
            }
        }

        /// <summary>Thickness of the drawn divider line in px (1 = hairline).</summary>
        public double DividerWidth
        {
            get => _dividerWidth;
            set
            {
                var clamped = Math.Max(1.0, Math.Min(6.0, value));
                if (SetField(ref _dividerWidth, clamped))
                {
                    OnPropertyChanged(nameof(DividerWidthValue));
                }
            }
        }

        /// <summary>
        /// Height of the drawn divider line in px. Drawn lines are used instead of the "|" glyph so
        /// the dividers stay optically centered with the numbers regardless of font/baseline.
        /// </summary>
        public double DividerHeight
        {
            get => _dividerHeight;
            set
            {
                var clamped = Math.Max(4.0, Math.Min(40.0, value));
                if (SetField(ref _dividerHeight, clamped))
                {
                    OnPropertyChanged(nameof(DividerHeightValue));
                }
            }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set => SetField(ref _fontFamily, string.IsNullOrWhiteSpace(value) ? "Segoe UI" : value);
        }

        /// <summary>Normal / SemiBold / Bold — typography weight for all overlay text.</summary>
        public string FontWeight
        {
            get => _fontWeight;
            set
            {
                if (SetField(ref _fontWeight, string.IsNullOrWhiteSpace(value) ? "Normal" : value))
                {
                    OnPropertyChanged(nameof(FontWeightValue));
                }
            }
        }

        /// <summary>Draws a rounded "chip" behind each value group (CPU, GPU, FT, FPS).</summary>
        public bool ShowChips
        {
            get => _showChips;
            set
            {
                if (SetField(ref _showChips, value))
                {
                    OnPropertyChanged(nameof(ChipPaddingThickness));
                    OnPropertyChanged(nameof(ChipBackgroundBrush));
                    OnPropertyChanged(nameof(ChipBorderThickness));
                }
            }
        }

        public string ChipBackgroundColor
        {
            get => _chipBackgroundColor;
            set => SetColor(ref _chipBackgroundColor, value, nameof(ChipBackgroundColor), nameof(ChipBackgroundBrush));
        }

        public string ChipBorderColor
        {
            get => _chipBorderColor;
            set => SetColor(ref _chipBorderColor, value, nameof(ChipBorderColor), nameof(ChipBorderBrush));
        }

        public double ChipCornerRadius
        {
            get => _chipCornerRadius;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(24.0, value));
                if (SetField(ref _chipCornerRadius, clamped))
                {
                    OnPropertyChanged(nameof(ChipCornerRadiusValue));
                }
            }
        }

        public double ChipPadding
        {
            get => _chipPadding;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(24.0, value));
                if (SetField(ref _chipPadding, clamped))
                {
                    OnPropertyChanged(nameof(ChipPaddingThickness));
                }
            }
        }

        public string ButtonBackgroundColor
        {
            get => _buttonBackgroundColor;
            set => SetColor(ref _buttonBackgroundColor, value, nameof(ButtonBackgroundColor), nameof(ButtonBackgroundBrush));
        }

        public string ButtonBorderColor
        {
            get => _buttonBorderColor;
            set => SetColor(ref _buttonBorderColor, value, nameof(ButtonBorderColor), nameof(ButtonBorderBrush));
        }

        public double ButtonCornerRadius
        {
            get => _buttonCornerRadius;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(24.0, value));
                if (SetField(ref _buttonCornerRadius, clamped))
                {
                    OnPropertyChanged(nameof(ButtonCornerRadiusValue));
                }
            }
        }

        /// <summary>Optional background art (camo/hex/league texture) supplied by the user.</summary>
        public string BackgroundImagePath
        {
            get => _backgroundImagePath;
            set
            {
                if (SetField(ref _backgroundImagePath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(BackgroundImageBrush));
                    OnPropertyChanged(nameof(HasBackgroundImage));
                }
            }
        }

        public double BackgroundImageOpacity
        {
            get => _backgroundImageOpacity;
            set => SetField(ref _backgroundImageOpacity, Math.Max(0.0, Math.Min(1.0, value)));
        }

        public string BackgroundTintColor
        {
            get => _backgroundTintColor;
            set => SetColor(ref _backgroundTintColor, value, nameof(BackgroundTintColor), nameof(BackgroundTintBrush));
        }

        public double BackgroundTintOpacity
        {
            get => _backgroundTintOpacity;
            set => SetField(ref _backgroundTintOpacity, Math.Max(0.0, Math.Min(1.0, value)));
        }

        /// <summary>Optional brand/logo image shown on the far left of the bar.</summary>
        public string LogoImagePath
        {
            get => _logoImagePath;
            set
            {
                if (SetField(ref _logoImagePath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(LogoImageSource));
                    OnPropertyChanged(nameof(HasLogo));
                }
            }
        }

        public double LogoWidth
        {
            get => _logoWidth;
            set
            {
                var clamped = Math.Max(16.0, Math.Min(240.0, value));
                if (SetField(ref _logoWidth, clamped))
                {
                    OnPropertyChanged(nameof(LogoWidthValue));
                }
            }
        }

        // ---------- Procedural effect tokens ----------

        /// <summary>None / Camo / Hex / Scanlines — drawn procedurally, no image asset needed.</summary>
        public string PatternKind
        {
            get => _patternKind;
            set
            {
                if (SetField(ref _patternKind, string.IsNullOrWhiteSpace(value) ? IconGrid.Helpers.Skin.SkinPatternFactory.PatternNone : value))
                {
                    OnPropertyChanged(nameof(PatternBrush));
                }
            }
        }

        public string PatternColorA
        {
            get => _patternColorA;
            set => SetColor(ref _patternColorA, value, nameof(PatternColorA), nameof(PatternBrush));
        }

        public string PatternColorB
        {
            get => _patternColorB;
            set => SetColor(ref _patternColorB, value, nameof(PatternColorB), nameof(PatternBrush));
        }

        public string PatternColorC
        {
            get => _patternColorC;
            set => SetColor(ref _patternColorC, value, nameof(PatternColorC), nameof(PatternBrush));
        }

        public string PatternLineColor
        {
            get => _patternLineColor;
            set => SetColor(ref _patternLineColor, value, nameof(PatternLineColor), nameof(PatternBrush));
        }

        public double PatternOpacity
        {
            get => _patternOpacity;
            set => SetField(ref _patternOpacity, Math.Max(0.0, Math.Min(1.0, value)));
        }

        public bool MeterGradient
        {
            get => _meterGradient;
            set
            {
                if (SetField(ref _meterGradient, value))
                {
                    OnPropertyChanged(nameof(CpuMeterBrush));
                    OnPropertyChanged(nameof(GpuMeterBrush));
                }
            }
        }

        public string MeterGradientStartColor
        {
            get => _meterGradientStartColor;
            set => SetColor(ref _meterGradientStartColor, value, nameof(MeterGradientStartColor), nameof(CpuMeterBrush), nameof(GpuMeterBrush));
        }

        public string MeterGradientEndColor
        {
            get => _meterGradientEndColor;
            set => SetColor(ref _meterGradientEndColor, value, nameof(MeterGradientEndColor), nameof(CpuMeterBrush), nameof(GpuMeterBrush));
        }

        public bool MeterSegments
        {
            get => _meterSegments;
            set
            {
                if (SetField(ref _meterSegments, value))
                {
                    OnPropertyChanged(nameof(MeterSegmentsBrush));
                }
            }
        }

        public string MeterSegmentColor
        {
            get => _meterSegmentColor;
            set => SetColor(ref _meterSegmentColor, value, nameof(MeterSegmentColor), nameof(MeterSegmentsBrush));
        }

        public bool GlowEnabled
        {
            get => _glowEnabled;
            set
            {
                if (SetField(ref _glowEnabled, value))
                {
                    OnPropertyChanged(nameof(AccentGlowEffect));
                }
            }
        }

        public string GlowColor
        {
            get => _glowColor;
            set => SetColor(ref _glowColor, value, nameof(GlowColor), nameof(AccentGlowEffect));
        }

        public double GlowRadius
        {
            get => _glowRadius;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(24.0, value));
                if (SetField(ref _glowRadius, clamped))
                {
                    OnPropertyChanged(nameof(AccentGlowEffect));
                }
            }
        }

        /// <summary>Turns the action buttons into metal-style vertical gradients.</summary>
        public bool ButtonGradient
        {
            get => _buttonGradient;
            set
            {
                if (SetField(ref _buttonGradient, value))
                {
                    OnPropertyChanged(nameof(ButtonBackgroundBrush));
                }
            }
        }

        public string ButtonGradientTopColor
        {
            get => _buttonGradientTopColor;
            set => SetColor(ref _buttonGradientTopColor, value, nameof(ButtonGradientTopColor), nameof(ButtonBackgroundBrush));
        }

        public string ButtonGradientBottomColor
        {
            get => _buttonGradientBottomColor;
            set => SetColor(ref _buttonGradientBottomColor, value, nameof(ButtonGradientBottomColor), nameof(ButtonBackgroundBrush));
        }

        // ---------- MW4-style layout tokens ----------

        public double FontSize
        {
            get => _fontSize;
            set
            {
                var clamped = Math.Max(9.0, Math.Min(28.0, value));
                if (SetField(ref _fontSize, clamped))
                {
                    OnPropertyChanged(nameof(FontSizeValue));
                    OnPropertyChanged(nameof(BrandFontSizeValue));
                }
            }
        }

        public double MonoFontSize
        {
            get => _monoFontSize;
            set
            {
                var clamped = Math.Max(9.0, Math.Min(28.0, value));
                if (SetField(ref _monoFontSize, clamped))
                {
                    OnPropertyChanged(nameof(MonoFontSizeValue));
                }
            }
        }

        /// <summary>Shows a brand block (e.g. "MW4") on the left with the accent glow.</summary>
        public bool ShowBrand
        {
            get => _showBrand;
            set
            {
                if (SetField(ref _showBrand, value))
                {
                    OnPropertyChanged(nameof(HasBrand));
                }
            }
        }

        public string BrandText
        {
            get => _brandText;
            set => SetField(ref _brandText, string.IsNullOrWhiteSpace(value) ? "MW4" : value);
        }

        public string BrandColor
        {
            get => _brandColor;
            set => SetColor(ref _brandColor, value, nameof(BrandColor), nameof(BrandBrush));
        }

        /// <summary>Shows the military callout tag ("X-01 / BLACK SITE") at the far left.</summary>
        public bool ShowCalloutTag
        {
            get => _showCalloutTag;
            set
            {
                if (SetField(ref _showCalloutTag, value))
                {
                    OnPropertyChanged(nameof(HasCalloutTag));
                }
            }
        }

        public string CalloutTopText
        {
            get => _calloutTopText;
            set => SetField(ref _calloutTopText, value ?? string.Empty);
        }

        public string CalloutBottomText
        {
            get => _calloutBottomText;
            set => SetField(ref _calloutBottomText, value ?? string.Empty);
        }

        public string CalloutAccentColor
        {
            get => _calloutAccentColor;
            set => SetColor(ref _calloutAccentColor, value, nameof(CalloutAccentColor), nameof(CalloutAccentBrush));
        }

        public string CpuMeterBorderColor
        {
            get => _cpuMeterBorderColor;
            set => SetColor(ref _cpuMeterBorderColor, value, nameof(CpuMeterBorderColor), nameof(CpuMeterBorderBrush));
        }

        public string GpuMeterBorderColor
        {
            get => _gpuMeterBorderColor;
            set => SetColor(ref _gpuMeterBorderColor, value, nameof(GpuMeterBorderColor), nameof(GpuMeterBorderBrush));
        }

        /// <summary>Thin accent stripe after the GPU chip (the purple line in the MW4 mockup).</summary>
        public bool ShowGpuAccentLine
        {
            get => _showGpuAccentLine;
            set
            {
                if (SetField(ref _showGpuAccentLine, value))
                {
                    OnPropertyChanged(nameof(HasGpuAccentLine));
                }
            }
        }

        public string GpuAccentLineColor
        {
            get => _gpuAccentLineColor;
            set => SetColor(ref _gpuAccentLineColor, value, nameof(GpuAccentLineColor), nameof(GpuAccentLineBrush));
        }

        /// <summary>Cut corner size in px for the bar (clipped polygon, like CSS clip-path).</summary>
        public double ChamferSize
        {
            get => _chamferSize;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(40.0, value));
                if (SetField(ref _chamferSize, clamped))
                {
                    OnPropertyChanged(nameof(ChamferSizeValue));
                }
            }
        }

        public string MonoFontFamily
        {
            get => _monoFontFamily;
            set => SetField(ref _monoFontFamily, string.IsNullOrWhiteSpace(value) ? "Consolas" : value);
        }

        public double CornerRadius
        {
            get => _cornerRadius;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(24.0, value));
                if (SetField(ref _cornerRadius, clamped))
                {
                    OnPropertyChanged(nameof(CornerRadiusValue));
                }
            }
        }
        // ---------- Derived brushes for XAML ----------

        [JsonIgnore]
        public WMedia.Brush BarBackgroundBrush => CreateBrush(_barBackgroundColor);

        [JsonIgnore]
        public WMedia.Brush PopupBackgroundBrush => CreateBrush(_popupBackgroundColor);

        [JsonIgnore]
        public WMedia.Brush TextBrush => CreateBrush(_textColor);

        [JsonIgnore]
        public WMedia.Brush AccentBrush => CreateBrush(_accentColor);

        [JsonIgnore]
        public WMedia.Brush CpuBarBrush => CreateBrush(_cpuBarColor);

        [JsonIgnore]
        public WMedia.Brush GpuBarBrush => CreateBrush(_gpuBarColor);

        [JsonIgnore]
        public double DividerWidthValue => _dividerWidth;

        [JsonIgnore]
        public double DividerHeightValue => _dividerHeight;

        [JsonIgnore]
        public WMedia.Brush DividerBrush
        {
            get
            {
                try
                {
                    var color = (WMedia.Color)WMedia.ColorConverter.ConvertFromString(_dividerColor);

                    // Opacity must be applied BEFORE freezing: mutating a frozen brush throws,
                    // which silently broke the divider colour binding (dividers fell back to the
                    // inherited, dark foreground).
                    var brush = new WMedia.SolidColorBrush(color) { Opacity = _dividerOpacity };
                    brush.Freeze();
                    return brush;
                }
                catch
                {
                    return WMedia.Brushes.White;
                }
            }
        }

        [JsonIgnore]
        public System.Windows.CornerRadius CornerRadiusValue => new(_cornerRadius);

        // ---------- Derived values for the layered design tokens ----------

        [JsonIgnore]
        public System.Windows.Thickness ChipPaddingThickness =>
            _showChips ? new System.Windows.Thickness(_chipPadding, 0, _chipPadding, 0) : new System.Windows.Thickness(0);

        [JsonIgnore]
        public WMedia.Brush ChipBackgroundBrush =>
            _showChips ? CreateBrush(_chipBackgroundColor) : WMedia.Brushes.Transparent;

        [JsonIgnore]
        public WMedia.Brush ChipBorderBrush => CreateBrush(_chipBorderColor);

        [JsonIgnore]
        public System.Windows.Thickness ChipBorderThickness =>
            _showChips ? new System.Windows.Thickness(1) : new System.Windows.Thickness(0);

        [JsonIgnore]
        public System.Windows.CornerRadius ChipCornerRadiusValue => new(_chipCornerRadius);

        [JsonIgnore]
        public WMedia.Brush ButtonBackgroundBrush =>
            _buttonGradient ? CreateVerticalGradient(_buttonGradientTopColor, _buttonGradientBottomColor) : CreateBrush(_buttonBackgroundColor);

        // ---------- Procedural effect values ----------

        [JsonIgnore]
        public WMedia.Brush? PatternBrush =>
            IconGrid.Helpers.Skin.SkinPatternFactory.Create(_patternKind, _patternColorA, _patternColorB, _patternColorC, _patternLineColor);

        [JsonIgnore]
        public bool HasPattern => !string.IsNullOrWhiteSpace(_patternKind) &&
                                  !string.Equals(_patternKind, IconGrid.Helpers.Skin.SkinPatternFactory.PatternNone, StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public WMedia.Brush CpuMeterBrush => CreateMeterBrush();

        [JsonIgnore]
        public WMedia.Brush GpuMeterBrush => CreateMeterBrush();

        [JsonIgnore]
        public WMedia.Brush? MeterSegmentsBrush =>
            _meterSegments ? IconGrid.Helpers.Skin.SkinPatternFactory.CreateMeterSegmentsBrush(_meterSegmentColor) : null;

        [JsonIgnore]
        public WMedia.Effects.Effect? AccentGlowEffect => CreateGlowEffect();

        // ---------- MW4-style derived values ----------

        [JsonIgnore]
        public double FontSizeValue => _fontSize;

        [JsonIgnore]
        public double MonoFontSizeValue => _monoFontSize;

        [JsonIgnore]
        public bool HasBrand => _showBrand;

        [JsonIgnore]
        public WMedia.Brush BrandBrush => CreateBrush(_brandColor);

        [JsonIgnore]
        public bool HasCalloutTag => _showCalloutTag;

        [JsonIgnore]
        public WMedia.Brush CalloutAccentBrush => CreateBrush(_calloutAccentColor);

        [JsonIgnore]
        public WMedia.Brush CpuMeterBorderBrush => CreateBrush(_cpuMeterBorderColor);

        [JsonIgnore]
        public WMedia.Brush GpuMeterBorderBrush => CreateBrush(_gpuMeterBorderColor);

        [JsonIgnore]
        public bool HasGpuAccentLine => _showGpuAccentLine;

        [JsonIgnore]
        public WMedia.Brush GpuAccentLineBrush => CreateBrush(_gpuAccentLineColor);

        [JsonIgnore]
        public double BrandFontSizeValue => Math.Round(_fontSize * 1.7, 0);

        [JsonIgnore]
        public double ChamferSizeValue => _chamferSize;

        [JsonIgnore]
        public WMedia.Brush ButtonBorderBrush => CreateBrush(_buttonBorderColor);

        [JsonIgnore]
        public System.Windows.CornerRadius ButtonCornerRadiusValue => new(_buttonCornerRadius);

        [JsonIgnore]
        public System.Windows.FontWeight FontWeightValue => _fontWeight switch
        {
            "Bold" => System.Windows.FontWeights.Bold,
            "SemiBold" => System.Windows.FontWeights.SemiBold,
            _ => System.Windows.FontWeights.Normal
        };

        [JsonIgnore]
        public WMedia.Brush BackgroundTintBrush => CreateBrush(_backgroundTintColor);

        [JsonIgnore]
        public WMedia.ImageBrush? BackgroundImageBrush => CreateImageBrush(_backgroundImagePath);

        [JsonIgnore]
        public bool HasBackgroundImage => !string.IsNullOrWhiteSpace(_backgroundImagePath);

        [JsonIgnore]
        public WMedia.Imaging.BitmapImage? LogoImageSource => CreateBitmap(_logoImagePath);

        [JsonIgnore]
        public bool HasLogo => !string.IsNullOrWhiteSpace(_logoImagePath);

        [JsonIgnore]
        public double LogoWidthValue => _logoWidth;

        // ---------- Serialization / cloning ----------

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static GamingOverlaySkin FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateDefault();
            }

            try
            {
                var skin = JsonConvert.DeserializeObject<GamingOverlaySkin>(json);
                if (skin == null)
                {
                    return CreateDefault();
                }

                // 18 px was the first default for the drawn divider line and looked too tall next
                // to the 13-14 px monitor text; migrate it to the smaller 12 px default. Any other
                // explicit value is left untouched.
                if (Math.Abs(skin.DividerHeight - 18) < 0.01)
                {
                    skin.DividerHeight = 12;
                }

                return skin;
            }
            catch
            {
                return CreateDefault();
            }
        }

        public GamingOverlaySkin Clone() => FromJson(ToJson());

        public void CopyFrom(GamingOverlaySkin other)
        {
            if (other == null)
            {
                return;
            }

            Name = other.Name;
            BarBackgroundColor = other.BarBackgroundColor;
            PopupBackgroundColor = other.PopupBackgroundColor;
            TextColor = other.TextColor;
            AccentColor = other.AccentColor;
            CpuBarColor = other.CpuBarColor;
            GpuBarColor = other.GpuBarColor;
            DividerOpacity = other.DividerOpacity;
            DividerColor = other.DividerColor;
            DividerWidth = other.DividerWidth;
            DividerHeight = other.DividerHeight;
            FontFamily = other.FontFamily;
            MonoFontFamily = other.MonoFontFamily;
            CornerRadius = other.CornerRadius;
            FontWeight = other.FontWeight;
            ShowChips = other.ShowChips;
            ChipBackgroundColor = other.ChipBackgroundColor;
            ChipBorderColor = other.ChipBorderColor;
            ChipCornerRadius = other.ChipCornerRadius;
            ChipPadding = other.ChipPadding;
            ButtonBackgroundColor = other.ButtonBackgroundColor;
            ButtonBorderColor = other.ButtonBorderColor;
            ButtonCornerRadius = other.ButtonCornerRadius;
            BackgroundImagePath = other.BackgroundImagePath;
            BackgroundImageOpacity = other.BackgroundImageOpacity;
            BackgroundTintColor = other.BackgroundTintColor;
            BackgroundTintOpacity = other.BackgroundTintOpacity;
            LogoImagePath = other.LogoImagePath;
            LogoWidth = other.LogoWidth;
            PatternKind = other.PatternKind;
            PatternColorA = other.PatternColorA;
            PatternColorB = other.PatternColorB;
            PatternColorC = other.PatternColorC;
            PatternLineColor = other.PatternLineColor;
            PatternOpacity = other.PatternOpacity;
            MeterGradient = other.MeterGradient;
            MeterGradientStartColor = other.MeterGradientStartColor;
            MeterGradientEndColor = other.MeterGradientEndColor;
            MeterSegments = other.MeterSegments;
            MeterSegmentColor = other.MeterSegmentColor;
            GlowEnabled = other.GlowEnabled;
            GlowColor = other.GlowColor;
            GlowRadius = other.GlowRadius;
            ButtonGradient = other.ButtonGradient;
            ButtonGradientTopColor = other.ButtonGradientTopColor;
            ButtonGradientBottomColor = other.ButtonGradientBottomColor;
            FontSize = other.FontSize;
            MonoFontSize = other.MonoFontSize;
            ShowBrand = other.ShowBrand;
            BrandText = other.BrandText;
            BrandColor = other.BrandColor;
            ShowCalloutTag = other.ShowCalloutTag;
            CalloutTopText = other.CalloutTopText;
            CalloutBottomText = other.CalloutBottomText;
            CalloutAccentColor = other.CalloutAccentColor;
            CpuMeterBorderColor = other.CpuMeterBorderColor;
            GpuMeterBorderColor = other.GpuMeterBorderColor;
            ShowGpuAccentLine = other.ShowGpuAccentLine;
            GpuAccentLineColor = other.GpuAccentLineColor;
            ChamferSize = other.ChamferSize;
        }

        // ---------- Defaults / presets ----------

        public static GamingOverlaySkin CreateDefault() => new();

        /// <summary>
        /// Built-in presets. Deliberately named after their look (not after a game) so the public
        /// repository never ships third-party branding; user-created skins stay local.
        /// </summary>
        public static IReadOnlyList<GamingOverlaySkin> CreatePresets()
        {
            // The "MW4" preset recreates the tactical HUD reference 1:1 (callout tag, brand block,
            // segmented meters, gold beveled buttons, camo plate). Fonts fall back to Windows fonts
            // when the Google Fonts (Chakra Petch / Share Tech Mono) are not installed.
            var mw4 = new GamingOverlaySkin
            {
                Name = "MW4",
                BarBackgroundColor = "#0B0D0F",
                PopupBackgroundColor = "#11151A",
                TextColor = "#E8EAED",
                AccentColor = "#00FF66",
                CpuBarColor = "#00FF66",
                GpuBarColor = "#A855F7",
                DividerColor = "#FFFFFF",
                DividerOpacity = 0.6,
                FontFamily = "Chakra Petch, Bahnschrift, Segoe UI",
                MonoFontFamily = "Share Tech Mono, Consolas",
                FontWeight = "Bold",
                FontSize = 14,
                MonoFontSize = 14,
                CornerRadius = 6,
                ShowChips = true,
                ChipBackgroundColor = "#E6000000",
                ChipBorderColor = "#FF3F3F46",
                ChipCornerRadius = 3,
                ChipPadding = 7,
                ButtonGradient = true,
                ButtonGradientTopColor = "#B45309",
                ButtonGradientBottomColor = "#18181B",
                ButtonBorderColor = "#C5A059",
                ButtonCornerRadius = 3,
                ShowBrand = true,
                BrandText = "MW4",
                BrandColor = "#00FF66",
                ShowCalloutTag = true,
                CalloutTopText = "X-01",
                CalloutBottomText = "BLACK SITE",
                CalloutAccentColor = "#C5A059",
                PatternKind = IconGrid.Helpers.Skin.SkinPatternFactory.PatternCamo,
                PatternColorA = "#2B2F26",
                PatternColorB = "#4A4E3B",
                PatternColorC = "#15170F",
                PatternLineColor = "#33FFFFFF",
                PatternOpacity = 0.7,
                BackgroundTintColor = "#05070A",
                BackgroundTintOpacity = 0.45,
                MeterGradient = true,
                MeterGradientStartColor = "#059669",
                MeterGradientEndColor = "#00FF66",
                MeterSegments = true,
                MeterSegmentColor = "#99000000",
                CpuMeterBorderColor = "#CC00FF66",
                GpuMeterBorderColor = "#CCA855F7",
                ShowGpuAccentLine = true,
                GpuAccentLineColor = "#A855F7",
                GlowEnabled = true,
                GlowColor = "#00FF66",
                GlowRadius = 10,
                ChamferSize = 14,
                DividerHeight = 12
            };

            var olive = new GamingOverlaySkin
            {
                Name = "Tactical Olive",
                BarBackgroundColor = "#15170F",
                PopupBackgroundColor = "#1B1E14",
                TextColor = "#E8E4D9",
                AccentColor = "#A3B565",
                CpuBarColor = "#8BA84A",
                GpuBarColor = "#B7A45E",
                DividerColor = "#FFFFFF",
                DividerOpacity = 0.85,
                FontFamily = "Bahnschrift",
                MonoFontFamily = "Consolas",
                CornerRadius = 2,
                // Layered look: value chips with a bright outline, bold condensed type and
                // framed action buttons — the "tactical HUD" feel.
                ShowChips = true,
                ChipBackgroundColor = "#B314180D",
                ChipBorderColor = "#FF8BA84A",
                ChipCornerRadius = 3,
                ChipPadding = 6,
                ButtonBackgroundColor = "#33000000",
                ButtonBorderColor = "#66E8E4D9",
                ButtonCornerRadius = 3,
                FontWeight = "Bold",
                BackgroundTintColor = "#0A0C07",
                BackgroundTintOpacity = 0.35,
                // Procedural effects (no image assets): camo pattern, glowing gradient meters,
                // segmented gauge overlay and metal-gradient action buttons.
                PatternKind = IconGrid.Helpers.Skin.SkinPatternFactory.PatternCamo,
                PatternColorA = "#4A4E3B",
                PatternColorB = "#8B7D6B",
                PatternColorC = "#1F231A",
                PatternLineColor = "#33FFFFFF",
                PatternOpacity = 0.5,
                MeterGradient = true,
                MeterGradientStartColor = "#059669",
                MeterGradientEndColor = "#00FF66",
                MeterSegments = true,
                MeterSegmentColor = "#66000000",
                GlowEnabled = true,
                GlowColor = "#00FF66",
                GlowRadius = 8,
                ButtonGradient = true,
                ButtonGradientTopColor = "#B45309",
                ButtonGradientBottomColor = "#18181B"
            };

            var cyan = new GamingOverlaySkin
            {
                Name = "Cyber Cyan",
                BarBackgroundColor = "#0B1220",
                PopupBackgroundColor = "#111C2E",
                TextColor = "#E6F7FF",
                AccentColor = "#22D3EE",
                CpuBarColor = "#22C55E",
                GpuBarColor = "#A855F7",
                DividerColor = "#FFFFFF",
                DividerOpacity = 0.75,
                FontFamily = "Bahnschrift SemiCondensed",
                MonoFontFamily = "Cascadia Mono",
                CornerRadius = 6
            };

            var ember = new GamingOverlaySkin
            {
                Name = "Ember",
                BarBackgroundColor = "#1A0F0C",
                PopupBackgroundColor = "#24140F",
                TextColor = "#FFE9DD",
                AccentColor = "#FF7A45",
                CpuBarColor = "#FFB020",
                GpuBarColor = "#FF5470",
                DividerColor = "#FFFFFF",
                DividerOpacity = 0.75,
                FontFamily = "Segoe UI",
                MonoFontFamily = "Consolas",
                CornerRadius = 8
            };

            var arctic = new GamingOverlaySkin
            {
                Name = "Arctic",
                BarBackgroundColor = "#0F1620",
                PopupBackgroundColor = "#162130",
                TextColor = "#EAF2FF",
                AccentColor = "#7DD3FC",
                CpuBarColor = "#38BDF8",
                GpuBarColor = "#A78BFA",
                DividerColor = "#FFFFFF",
                DividerOpacity = 0.75,
                FontFamily = "Segoe UI",
                MonoFontFamily = "Cascadia Code",
                CornerRadius = 6
            };

            return new List<GamingOverlaySkin> { CreateDefault(), mw4, olive, cyan, ember, arctic };
        }

        // ---------- Helpers ----------

        private static WMedia.Brush CreateBrush(string hex)
        {
            try
            {
                var color = (WMedia.Color)WMedia.ColorConverter.ConvertFromString(hex);
                var brush = new WMedia.SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return WMedia.Brushes.White;
            }
        }

        private WMedia.Brush CreateMeterBrush()
        {
            if (!_meterGradient)
            {
                return CreateBrush(_meterGradientEndColor);
            }

            var gradient = new WMedia.LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0.5),
                EndPoint = new System.Windows.Point(1, 0.5)
            };

            gradient.GradientStops.Add(new WMedia.GradientStop(ParseColor(_meterGradientStartColor, WMedia.Colors.SeaGreen), 0));
            gradient.GradientStops.Add(new WMedia.GradientStop(ParseColor(_meterGradientEndColor, WMedia.Colors.LimeGreen), 1));
            gradient.Freeze();
            return gradient;
        }

        private static WMedia.Brush CreateVerticalGradient(string topHex, string bottomHex)
        {
            var gradient = new WMedia.LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0.5, 0),
                EndPoint = new System.Windows.Point(0.5, 1)
            };

            gradient.GradientStops.Add(new WMedia.GradientStop(ParseColor(topHex, WMedia.Colors.DimGray), 0));
            gradient.GradientStops.Add(new WMedia.GradientStop(ParseColor(bottomHex, WMedia.Colors.Black), 1));
            gradient.Freeze();
            return gradient;
        }

        private WMedia.Effects.Effect? CreateGlowEffect()
        {
            if (!_glowEnabled || _glowRadius <= 0)
            {
                return null;
            }

            var effect = new WMedia.Effects.DropShadowEffect
            {
                ShadowDepth = 0,
                BlurRadius = _glowRadius,
                Opacity = 0.85,
                Color = ParseColor(_glowColor, WMedia.Colors.LimeGreen)
            };

            effect.Freeze();
            return effect;
        }

        private static WMedia.Color ParseColor(string hex, WMedia.Color fallback)
        {
            try
            {
                return (WMedia.Color)WMedia.ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return fallback;
            }
        }

        private static WMedia.Imaging.BitmapImage? CreateBitmap(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return null;
            }

            try
            {
                var bitmap = new WMedia.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = WMedia.Imaging.BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = WMedia.Imaging.BitmapCreateOptions.IgnoreImageCache;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static WMedia.ImageBrush? CreateImageBrush(string path)
        {
            var bitmap = CreateBitmap(path);
            if (bitmap == null)
            {
                return null;
            }

            var brush = new WMedia.ImageBrush(bitmap)
            {
                Stretch = WMedia.Stretch.UniformToFill,
                AlignmentX = WMedia.AlignmentX.Center,
                AlignmentY = WMedia.AlignmentY.Center
            };

            brush.Freeze();
            return brush;
        }

        private void SetColor(ref string field, string value, params string[] changedProperties)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? field : value;
            if (string.Equals(field, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            field = normalized;

            foreach (var propertyName in changedProperties)
            {
                OnPropertyChanged(propertyName);
            }
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
