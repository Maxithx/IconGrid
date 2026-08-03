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
        public const double BaseTileSlotHeight = 152;        // base row height (icon + label) before spacing
        public const double ContentVerticalPaddingTop = 36;  // upper padding portion (matches XAML padding)
        public const double ContentVerticalPaddingBottom = 0; // remove bottom padding to eliminate extra space
        public const double ExtraBottomPaddingPerRow = 0;    // no extra bottom padding per row
        public const double ContentHorizontalPadding = 60;   // content border padding left+right
        public const double WindowHorizontalPadding = 0;     // remove outer shell padding to keep full-mode window tight to content
        public const double SettingsMinWindowHeight = 620;
        public const double FixedSettingsHeight = 680;

        // ---------- Measurement state ----------

        private double _headerHeight = 140;                   // measured height for top chrome + tabs
        private double _iconRowSpacing = -20;                 // adjustable extra spacing between rows (default tightened)
        private double _lastRowPaddingAdjust = 0;             // fine-tune bottom space under the last visible row
        private bool _isIconPanelExpanded = true;

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
        /// Margin applied to each icon tile (horizontal fixed, vertical derived from row spacing).
        /// </summary>
        public Thickness IconMargin => new Thickness(14, IconRowSpacing / 2, 14, IconRowSpacing / 2);

        // ---------- Measurement calculations ----------

        public double ContentAreaMaxHeight => Math.Max(200, SystemParameters.WorkArea.Height - _headerHeight - 60);

        private double SettingsContentHeight => Math.Max(200, FixedSettingsHeight - _headerHeight);

        public double ContentHostHeight(bool isOverlayOpen, int itemCount, int iconsPerRow, double effectiveIconScale)
            => isOverlayOpen ? SettingsContentHeight : CalculateContentAreaHeight(itemCount, iconsPerRow, effectiveIconScale);

        public double ContentMinWidth(int iconsPerRow)
        {
            var slots = Math.Max(1, iconsPerRow);
            var baseWidth = (slots * TileSlotWidth) + ContentHorizontalPadding;
            return baseWidth;
        }

        public double ContentWidth(int iconsPerRow) => ContentMinWidth(iconsPerRow);

        public double ContentMaxWidth =>
            Math.Max(0, SystemParameters.WorkArea.Width - WindowHorizontalPadding - 24);

        /// <summary>
        /// Desired window dimensions so chrome tracks content size.
        /// </summary>
        public double WindowDesiredWidth(double contentWidth, double uiScale)
            => (contentWidth + WindowHorizontalPadding) * uiScale;

        public double WindowDesiredHeight(bool isOverlayOpen, double contentHostHeight, double uiScale)
            => isOverlayOpen ? (FixedSettingsHeight * uiScale) : ((contentHostHeight + _headerHeight) * uiScale);

        public double CalculateContentAreaHeight(int itemCount, int iconsPerRow, double effectiveIconScale)
        {
            if (!IsIconPanelExpanded) return 0;

            var scaledTileHeight = BaseTileSlotHeight * effectiveIconScale;
            var columns = Math.Max(1, iconsPerRow);
            var rows = Math.Max(1, Math.Ceiling(itemCount / (double)columns));

            // For 1-3 rows we respect any negative row spacing to keep height snug.
            // For 4+ rows we clamp spacing to 0 so scrolling range is consistent.
            var useRowSpacing = (rows > 3) ? Math.Max(0, _iconRowSpacing) : _iconRowSpacing;
            var singleRowHeight = scaledTileHeight + ExtraBottomPaddingPerRow + useRowSpacing;

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

            const double NoScrollBottomBuffer = 32; // slightly more buffer under last row when no scrollbar
            var adjustedHeight = totalContentHeight + NoScrollBottomBuffer + _lastRowPaddingAdjust;
            var minimum = singleRowHeight + ContentVerticalPaddingTop + Math.Min(0, _lastRowPaddingAdjust);
            return Math.Max(minimum, adjustedHeight);
        }

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
        public void ApplyMeasurementState(double iconRowSpacing, double lastRowPaddingAdjust)
        {
            _iconRowSpacing = iconRowSpacing;
            _lastRowPaddingAdjust = lastRowPaddingAdjust;
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