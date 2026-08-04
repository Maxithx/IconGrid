using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace IconGrid.ViewModels.Launcher
{
    /// <summary>
    /// Owns all launcher layout-measurement state and calculations:
    /// header height, icon row spacing, last-row padding, icon-panel expansion,
    /// content-area sizing, content-host height and desired window dimensions.
    /// Extracted from MainViewModel so the shell view model stays focused.
    /// </summary>
    public class LauncherLayoutMeasurements : INotifyPropertyChanged
    {
        // ---------- Constants ----------

        public const double TileSlotWidth = 172;             // approximate width per icon tile including margin
        public const double BaseTileSlotHeight = 152;        // nominal row slot (icon + label) before spacing
        public const double MinimumTileSlotHeight = 96;      // hard floor = actual compact tile (56 icon + 40 label, no padding/margin)
        public const double ContentVerticalPaddingTop = 17;  // matches XAML top padding
        public const double ContentVerticalPaddingBottom = 17; // matches XAML bottom padding (symmetric with top)
        public const double ExtraBottomPaddingPerRow = 0;    // no extra bottom padding per row
        public const double ContentHorizontalPadding = 60;   // content border padding left+right
        public const double CarouselHorizontalPadding = 40;  // carousel border padding left+right (20+20, matching grid)
        public const double WindowHorizontalPadding = 0;     // remove outer shell padding to keep full-mode window tight to content
        public const double SettingsMinWindowHeight = 620;
        public const double FixedSettingsHeight = 680;

        public const string GridViewMode = "Grid";
        public const string CarouselViewMode = "Carousel";

        // ---------- Measurement state ----------

        private double _headerHeight = 140;                   // measured height for top chrome + tabs
        private double _iconRowSpacing = 0;                   // adjustable extra spacing between rows (0 = no gap)
        private double _lastRowPaddingAdjust = 0;             // fine-tune bottom space under the last visible row
        private bool _isIconPanelExpanded = true;
        private string _iconViewMode = GridViewMode;

        // ---------- Public measurement state ----------

        public bool IsIconPanelExpanded
        {
            get => _isIconPanelExpanded;
            set
            {
                if (SetField(ref _isIconPanelExpanded, value))
                {
                    NotifyContentHeightChanged();
                }
            }
        }

        public double IconRowSpacing => _iconRowSpacing;

        public double LastRowPaddingAdjust => _lastRowPaddingAdjust;

        /// <summary>
        /// Current view mode for the shortcut icons: "Grid" or "Carousel".
        /// </summary>
        public string IconViewMode
        {
            get => _iconViewMode;
            set
            {
                if (SetIconViewMode(value))
                {
                    NotifyContentHeightChanged();
                }
            }
        }

        /// <summary>
        /// Sets row spacing; returns true when the value changed.
        /// </summary>
        public bool SetIconRowSpacing(double value)
        {
            if (EqualityComparer<double>.Default.Equals(_iconRowSpacing, value))
                return false;

            _iconRowSpacing = value;
            OnPropertyChanged(nameof(IconRowSpacing));
            NotifyContentHeightChanged();
            return true;
        }

        /// <summary>
        /// Sets last-row padding adjust; returns true when the value changed.
        /// </summary>
        public bool SetLastRowPaddingAdjust(double value)
        {
            if (EqualityComparer<double>.Default.Equals(_lastRowPaddingAdjust, value))
                return false;

            _lastRowPaddingAdjust = value;
            OnPropertyChanged(nameof(LastRowPaddingAdjust));
            NotifyContentHeightChanged();
            return true;
        }

        /// <summary>
        /// Sets the icon view mode ("Grid" or "Carousel"); returns true when the value changed.
        /// Invalid values are normalized to "Grid".
        /// </summary>
        public bool SetIconViewMode(string value)
        {
            var normalized = string.Equals(value, CarouselViewMode, StringComparison.OrdinalIgnoreCase)
                ? CarouselViewMode
                : GridViewMode;

            if (string.Equals(_iconViewMode, normalized, StringComparison.Ordinal))
                return false;

            _iconViewMode = normalized;
            OnPropertyChanged(nameof(IconViewMode));
            NotifyContentHeightChanged();
            return true;
        }

        /// <summary>
        /// Margin applied to each icon tile (horizontal fixed, vertical derived from row spacing).
        /// The vertical margin is clamped to 0 so a negative icon-row spacing can NEVER
        /// lift tiles up or clip them — it only makes rows sit tighter together.
        /// </summary>
        public Thickness IconMargin =>
            new Thickness(14, Math.Max(0, IconRowSpacing / 2), 14, Math.Max(0, IconRowSpacing / 2));

        // ---------- Measurement calculations ----------

        public double ContentAreaMaxHeight => Math.Max(200, SystemParameters.WorkArea.Height - _headerHeight - 60);

        private double SettingsContentHeight => Math.Max(200, FixedSettingsHeight - _headerHeight);

public double ContentHostHeight(bool isOverlayOpen, int itemCount, int iconsPerRow, double effectiveIconScale, bool enableContentScroll)
            => isOverlayOpen ? SettingsContentHeight : CalculateContentAreaHeight(itemCount, iconsPerRow, effectiveIconScale, enableContentScroll);

        public double ContentMinWidth(int iconsPerRow)
        {
            var slots = Math.Max(1, iconsPerRow);
            var baseWidth = (slots * TileSlotWidth) + ContentHorizontalPadding;
            return baseWidth;
        }

        public double ContentWidth(int iconsPerRow) => ContentMinWidth(iconsPerRow);

        /// <summary>
        /// Width of each carousel cell so the visible viewport shows exactly
        /// <paramref name="iconsPerRow"/> icons — the same column width the grid's
        /// UniformGrid produces, so icon spacing is identical in both modes.
        /// </summary>
        public double CarouselCellWidth(int iconsPerRow)
        {
            var slots = Math.Max(1, iconsPerRow);
            var innerWidth = ContentWidth(iconsPerRow) - CarouselHorizontalPadding;
            return Math.Max(1, innerWidth / slots);
        }

        public double ContentMaxWidth =>
            Math.Max(0, SystemParameters.WorkArea.Width - WindowHorizontalPadding - 24);

        /// <summary>
        /// Desired window dimensions so chrome tracks content size.
        /// </summary>
        public double WindowDesiredWidth(double contentWidth, double uiScale)
            => (contentWidth + WindowHorizontalPadding) * uiScale;

        public double WindowDesiredHeight(bool isOverlayOpen, double contentHostHeight, double uiScale)
            => isOverlayOpen ? (FixedSettingsHeight * uiScale) : ((contentHostHeight + _headerHeight) * uiScale);

public double CalculateContentAreaHeight(int itemCount, int iconsPerRow, double effectiveIconScale, bool enableContentScroll)
        {
            if (!IsIconPanelExpanded) return 0;

            if (IsCarouselMode)
            {
                // Carousel: single row, so inter-row spacing must NOT inflate the
                // height. Carousel uses its own slightly more generous padding:
                // Padding="23,20,23,20" gives 20 px above and below the row. The
                // "Bottom padding (last row)" slider then adjusts only extra space;
                // the floor at 0 means the icons are never overlapped by the
                // horizontal scrollbar (which only appears inside an overflowing
                // viewport).
                const double CarouselBaseTileSlotHeight = 96; // tight single-row slot = compact icon tile
                const double CarouselPaddingTop = 20;         // matches carousel Border padding top
                const double CarouselPaddingBottom = 20;      // matches carousel Border padding bottom
                const double CarouselScrollBarReserve = 12;   // horizontal scrollbar height when enabled
                var carouselRow = CarouselBaseTileSlotHeight * effectiveIconScale;
                var underIcons = Math.Max(0, _lastRowPaddingAdjust);
                var scrollBarReserve = enableContentScroll ? CarouselScrollBarReserve : 0;
                return CarouselPaddingTop + carouselRow + CarouselPaddingBottom + underIcons + scrollBarReserve;
            }

            var columns = Math.Max(1, iconsPerRow);

            var rows = Math.Max(1, Math.Ceiling(itemCount / (double)columns));

            // Rows sit flush against each other: the row slot equals the actual icon
            // tile height plus any POSITIVE row spacing. Negative or zero spacing
            // yields zero gap between rows, so there is no padding below the top row
            // and no top padding on the next row — and icons are never lifted/clipped.
            var singleRowHeight = (MinimumTileSlotHeight * effectiveIconScale)
                                  + ExtraBottomPaddingPerRow
                                  + Math.Max(0, _iconRowSpacing);

            var totalContentHeight = (rows * singleRowHeight)
                                     + ContentVerticalPaddingTop
                                     + ContentVerticalPaddingBottom;

            const int MaxRowsWithoutScroll = 3;
            if (rows > MaxRowsWithoutScroll)
            {
                // Viewport height capped at ~3 rows so a 4th row has full scroll range.
                var viewportHeight = (MaxRowsWithoutScroll * singleRowHeight)
                                     + ContentVerticalPaddingTop
                                     + ContentVerticalPaddingBottom;
                // Force additional overflow so the 4th row can scroll fully into view and leave a small buffer underneath.
                const double ScrollBuffer = 24; // extra breathing room below the last row when scrolling
                var forcedOverflowHeight = viewportHeight - (singleRowHeight * 0.8) - ScrollBuffer;

                var maxContentHeight = Math.Max(200, SystemParameters.WorkArea.Height - _headerHeight - 48);
                var minHeight = singleRowHeight + ContentVerticalPaddingTop + ContentVerticalPaddingBottom;
                forcedOverflowHeight += _lastRowPaddingAdjust;
                return Math.Max(minHeight, Math.Min(forcedOverflowHeight, maxContentHeight));
            }

            // The Border in LauncherGrid already provides symmetric padding above
            // and below the icon area (17 px). The "Bottom padding (last row)"
            // slider (-20..+20 px) only adds or removes EXTRA space below the
            // last row; the floor at 0 means the icons are never lifted/clipped.
            var underIconsSpace = Math.Max(0, _lastRowPaddingAdjust);
            return totalContentHeight + underIconsSpace;
        }

        private bool IsCarouselMode => string.Equals(_iconViewMode, CarouselViewMode, StringComparison.Ordinal);

        // ---------- State mutation ----------

        /// <summary>
        /// Update measured header height (top bar + tabs) based on actual visuals.
        /// </summary>
        public void SetHeaderHeight(double value)
        {
            var clamped = Math.Max(0, value);
            if (SetField(ref _headerHeight, clamped))
            {
                RefreshLayoutMeasurements();
            }
        }

        /// <summary>
        /// Apply persisted measurement state without raising change notifications
        /// (used when config is loaded or defaults are restored).
        /// </summary>
        public void ApplyMeasurementState(double iconRowSpacing, double lastRowPaddingAdjust, string iconViewMode)
        {
            _iconRowSpacing = iconRowSpacing;
            _lastRowPaddingAdjust = lastRowPaddingAdjust;
            _iconViewMode = string.Equals(iconViewMode, CarouselViewMode, StringComparison.OrdinalIgnoreCase)
                ? CarouselViewMode
                : GridViewMode;
        }

        public void NotifyWorkAreaChanged()
        {
            OnPropertyChanged(nameof(ContentMaxWidth));
        }

        public void RefreshLayoutMeasurements()
        {
            NotifyContentHeightChanged(includeMaxHeight: true);
            OnPropertyChanged(nameof(ContentMinWidth));
            OnPropertyChanged(nameof(ContentWidth));
            OnPropertyChanged(nameof(CarouselCellWidth));
            OnPropertyChanged(nameof(ContentMaxWidth));
            OnPropertyChanged(nameof(WindowDesiredWidth));
            OnPropertyChanged(nameof(IconMargin));
        }

        // ---------- Notifications ----------

        public void NotifyContentHeightChanged(bool includeMaxHeight = false)
        {
            // These property names are the thin bound properties that MainViewModel
            // exposes and forwards; they are raised here by name so MainViewModel's
            // PropertyChanged subscription can re-raise them on the shell view model.
            OnPropertyChanged("ContentAreaHeight");
            if (includeMaxHeight)
                OnPropertyChanged(nameof(ContentAreaMaxHeight));
            OnPropertyChanged("ContentHostHeight");
            OnPropertyChanged("WindowDesiredHeight");
            OnPropertyChanged("WindowDesiredHeightEffective");
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}