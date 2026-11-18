using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IconGrid.Models;
using IconGrid.ViewModels;

namespace IconGrid.Views
{
    /// <summary>
    /// Visual part of the gaming overlay: the monitor row plus the settings/scale popups.
    /// The control owns no interaction logic itself — <see cref="GamingOverlayWindow"/> wires
    /// drag, the gear toggle, close and the scale slider in code. That keeps the visuals in a
    /// single place so the Gaming overlay settings page can host the exact same control as a
    /// read-only preview instead of a stale screenshot.
    /// </summary>
    public partial class GamingOverlayBar : System.Windows.Controls.UserControl
    {
        private bool _isPreview;
        private MainViewModel? _viewModel;

        public GamingOverlayBar()
        {
            InitializeComponent();

            // IsPreview may be set from XAML before the child elements exist; re-apply once
            // the control is loaded so the preview state always wins.
            Loaded += (_, _) =>
            {
                ApplyPreviewState();
                ApplyChamfer();
                ApplyDividerThickness();

                // Items are generated after the first layout pass, so the dividers only exist here.
                // Running the pixel-exact thickness afterwards keeps every line the same weight.
                Dispatcher.BeginInvoke(
                    new Action(ApplyDividerThickness),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            };

            OverlayRoot.SizeChanged += (_, _) =>
            {
                ApplyChamfer();
                ApplyDividerThickness();
            };
            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// Makes the divider lines exactly N device pixels *thick* (N = DividerWidth token).
        ///
        /// A fixed DIP width is NOT pixel exact at 125%/150% scaling: 1 DIP becomes 1.25/1.5
        /// device pixels, so identical dividers can render 1 px or 2 px depending on where they
        /// land. Converting device pixels to DIPs through the current DPI removes that variance.
        /// A horizontal bar draws vertical lines (thickness = Width), a vertical bar draws wide
        /// horizontal lines (thickness = Height), so the axis matters.
        /// </summary>
        private void ApplyDividerThickness()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var scale = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
            var pixels = Math.Max(1.0, Math.Round(_viewModel?.GamingOverlaySkin.DividerWidthValue ?? 1.0));
            var dipWidth = pixels / scale;

            foreach (var divider in EnumerateDividers(this))
            {
                if (IsInsideVerticalBar(divider))
                {
                    // Wide horizontal line: the line stretches, only its height is a thickness.
                    if (!double.IsNaN(divider.Width))
                    {
                        divider.Width = double.NaN;
                    }

                    if (Math.Abs(divider.Height - dipWidth) > 0.001)
                    {
                        divider.Height = dipWidth;
                    }

                    continue;
                }

                if (Math.Abs(divider.Width - dipWidth) > 0.001)
                {
                    divider.Width = dipWidth;
                }
            }
        }

        /// <summary>True when a divider lives in a bar that stacks its modules vertically.</summary>
        private static bool IsInsideVerticalBar(DependencyObject element)
        {
            var current = element;

            while (current != null)
            {
                if (current is System.Windows.Controls.ItemsControl items)
                {
                    return items.Tag is bool vertical && vertical;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private IEnumerable<Border> EnumerateDividers(DependencyObject root)
        {
            var dividerStyle = TryFindResource("OverlayDividerStyle") as Style;
            var childCount = VisualTreeHelper.GetChildrenCount(root);

            for (var index = 0; index < childCount; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);

                if (child is Border border && dividerStyle != null && ReferenceEquals(border.Style, dividerStyle))
                {
                    yield return border;
                }

                foreach (var nested in EnumerateDividers(child))
                {
                    yield return nested;
                }
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            ApplyDividerThickness();
        }

        /// <summary>
        /// Cuts the top-right and bottom-left corners of the bar (the CSS clip-path look from the
        /// tactical HUD reference). WPF cannot express percentage clip-paths in XAML, so the
        /// geometry is rebuilt whenever the bar size or the ChamferSize token changes.
        /// </summary>
        private void ApplyChamfer()
        {
            if (OverlayRoot == null)
            {
                return;
            }

            var size = _viewModel?.GamingOverlaySkin.ChamferSizeValue ?? 0;
            var radius = _viewModel?.GamingOverlaySkin.CornerRadius ?? 0;
            var width = OverlayRoot.ActualWidth;
            var height = OverlayRoot.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                OverlayRoot.Clip = null;
                return;
            }

            // WPF's Border does not clip its children to its CornerRadius, so the background/tint/
            // pattern layers inside the bar kept square corners behind the rounded shape. Clip the
            // whole bar to the rounded rectangle when no chamfer is set.
            if (size <= 0)
            {
                if (radius <= 0)
                {
                    OverlayRoot.Clip = null;
                    return;
                }

                var rounded = new RectangleGeometry(
                    new System.Windows.Rect(0, 0, width, height), radius, radius);
                rounded.Freeze();
                OverlayRoot.Clip = rounded;
                return;
            }

            var cut = Math.Min(size, Math.Min(width, height) / 2);
            var geometry = new StreamGeometry();

            using (var context = geometry.Open())
            {
                context.BeginFigure(new System.Windows.Point(0, 0), true, true);
                context.LineTo(new System.Windows.Point(width - cut, 0), true, false);
                context.LineTo(new System.Windows.Point(width, cut), true, false);
                context.LineTo(new System.Windows.Point(width, height), true, false);
                context.LineTo(new System.Windows.Point(cut, height), true, false);
                context.LineTo(new System.Windows.Point(0, height - cut), true, false);
            }

            geometry.Freeze();
            OverlayRoot.Clip = geometry;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.GamingOverlaySkin.PropertyChanged -= Skin_PropertyChanged;
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = e.NewValue as MainViewModel;

            if (_viewModel != null)
            {
                _viewModel.GamingOverlaySkin.PropertyChanged += Skin_PropertyChanged;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            ApplyChamfer();
            RecomputeOrientation();
            ApplyPreviewState();
        }

        /// <summary>Keeps the preview at the same overlay scale as the live window.</summary>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayUiScale), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayBackgroundHeight), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayActiveGroupIsVertical), StringComparison.Ordinal))
            {
                RecomputeOrientation();
                ApplyPreviewState();
                return;
            }

            // Eye toggles on the chrome layers must reach the buttons immediately.
            if (string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlaySettingsButton), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayCloseButton), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayLayerItems), StringComparison.Ordinal))
            {
                ApplyPreviewState();
            }
        }

        private void Skin_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(GamingOverlaySkin.ChamferSizeValue), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(GamingOverlaySkin.CornerRadius), StringComparison.Ordinal))
            {
                ApplyChamfer();
            }

            if (string.Equals(e.PropertyName, nameof(GamingOverlaySkin.DividerWidthValue), StringComparison.Ordinal))
            {
                ApplyDividerThickness();
            }
        }

        /// <summary>
        /// Optional bar height in DIPs. The Skin Studio canvas uses it so a layer group can have
        /// its own bar height; NaN (the default) keeps the skin's height.
        /// </summary>
        public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
            nameof(BarHeight),
            typeof(double),
            typeof(GamingOverlayBar),
            new PropertyMetadata(double.NaN, (d, _) => ((GamingOverlayBar)d).RecomputeOrientation()));

        /// <summary>Optional module list: when set, the bar renders only these blocks.</summary>
        public static readonly DependencyProperty BlocksSourceProperty = DependencyProperty.Register(
            nameof(BlocksSource),
            typeof(System.Collections.IEnumerable),
            typeof(GamingOverlayBar),
            new PropertyMetadata(null));

        public double BarHeight
        {
            get => (double)GetValue(BarHeightProperty);
            set => SetValue(BarHeightProperty, value);
        }

        public System.Collections.IEnumerable? BlocksSource
        {
            get => (System.Collections.IEnumerable?)GetValue(BlocksSourceProperty);
            set => SetValue(BlocksSourceProperty, value);
        }

        /// <summary>
        /// Optional layer-group id. When set, the settings/close chrome is only drawn if that group
        /// actually owns those chrome layers — a group that owns nothing shows an empty bar, so the
        /// chrome behaves like a module instead of being forced onto every group.
        /// </summary>
        public static readonly DependencyProperty ChromeGroupIdProperty = DependencyProperty.Register(
            nameof(ChromeGroupId),
            typeof(string),
            typeof(GamingOverlayBar),
            new PropertyMetadata(null, (d, _) => ((GamingOverlayBar)d).ApplyPreviewState()));

        public string? ChromeGroupId
        {
            get => (string?)GetValue(ChromeGroupIdProperty);
            set => SetValue(ChromeGroupIdProperty, value);
        }

        /// <summary>
        /// Per-bar orientation override. The Skin Studio canvas sets it for every group bar; the
        /// live overlay leaves it null and follows the active group's orientation from the view model.
        /// </summary>
        public static readonly DependencyProperty IsVerticalOverrideProperty = DependencyProperty.Register(
            nameof(IsVerticalOverride),
            typeof(bool?),
            typeof(GamingOverlayBar),
            new PropertyMetadata(null, (d, _) => ((GamingOverlayBar)d).RecomputeOrientation()));

        /// <summary>Effective orientation used by the templates and triggers.</summary>
        public static readonly DependencyProperty EffectiveIsVerticalProperty = DependencyProperty.Register(
            nameof(EffectiveIsVertical),
            typeof(bool),
            typeof(GamingOverlayBar),
            new PropertyMetadata(false));

        /// <summary>Height of the bar content; NaN lets a vertical bar size to its modules.</summary>
        public static readonly DependencyProperty ContentHeightProperty = DependencyProperty.Register(
            nameof(ContentHeight),
            typeof(double),
            typeof(GamingOverlayBar),
            new PropertyMetadata(double.NaN));

        public bool? IsVerticalOverride
        {
            get => (bool?)GetValue(IsVerticalOverrideProperty);
            set => SetValue(IsVerticalOverrideProperty, value);
        }

        public bool EffectiveIsVertical
        {
            get => (bool)GetValue(EffectiveIsVerticalProperty);
            private set => SetValue(EffectiveIsVerticalProperty, value);
        }

        public double ContentHeight
        {
            get => (double)GetValue(ContentHeightProperty);
            private set => SetValue(ContentHeightProperty, value);
        }

        /// <summary>
        /// Works out the orientation (override first, then the live group) and the content height:
        /// a horizontal bar keeps the skin height, a vertical one grows with its modules unless the
        /// group defines a height.
        /// </summary>
        /// <summary>
        /// Minimum content height of the bar: the skin height for a horizontal bar, 0 for a vertical
        /// one (it grows with its modules) unless the group defines a height. Deliberately a minimum
        /// and not a fixed height, so a column can never clip the modules that do not fit.
        /// </summary>
        private void RecomputeOrientation()
        {
            var vertical = IsVerticalOverride ?? _viewModel?.GamingOverlayActiveGroupIsVertical ?? false;
            EffectiveIsVertical = vertical;

            var skinHeight = _viewModel?.GamingOverlayBackgroundHeight ?? 44.0;
            var groupHeight = BarHeight;
            var hasGroupHeight = !double.IsNaN(groupHeight) && groupHeight > 0;

            ContentHeight = hasGroupHeight
                ? groupHeight
                : (vertical ? 0.0 : skinHeight);

            // The divider thickness lives on a different axis per orientation.
            ApplyDividerThickness();
        }

        /// <summary>
        /// Read-only preview mode: the overlay is shown exactly as it looks in game, but the
        /// action buttons and the scale popup are hidden so the settings page cannot trigger a
        /// second, conflicting set of overlay controls. The host is expected to also set
        /// <c>IsHitTestVisible="False"</c> on the hosting element.
        /// </summary>
        public bool IsPreview
        {
            get => _isPreview;
            set
            {
                if (_isPreview == value)
                {
                    return;
                }

                _isPreview = value;
                ApplyPreviewState();
            }
        }

        /// <summary>
        /// When the bar is pinned to one layer group (the Skin Studio canvas), the chrome only shows
        /// if that group actually owns it — chrome behaves like a module instead of being forced in.
        /// </summary>
        private bool OwnsChrome(OverlayBlock? block)
        {
            if (string.IsNullOrEmpty(ChromeGroupId))
            {
                return true;
            }

            return _viewModel?.GamingOverlayGroupOwns(block, ChromeGroupId) ?? true;
        }

        private void ApplyPreviewState()
        {
            // The chrome buttons are styleable layers now, so the preview shows them exactly as in
            // game — but without hit testing, so the settings page cannot toggle the overlay menu.
            // The live window clamps both buttons to the bar height (a 32 px button would stick out
            // of a 20 px bar); the preview must apply the same clamp or it looks clipped/wrong.
            var barHeight = _viewModel?.GamingOverlayBackgroundHeight ?? 44.0;
            var buttonHeight = Math.Min(32.0, barHeight);

            if (SettingsMenuButton != null)
            {
                SettingsMenuButton.Visibility = _viewModel?.GamingOverlaySettingsButton?.IsVisible == false ||
                                                _viewModel?.GamingOverlayFirstGroupHidden == true ||
                                                !OwnsChrome(_viewModel?.GamingOverlaySettingsButton)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                SettingsMenuButton.IsHitTestVisible = !_isPreview;
                SettingsMenuButton.Focusable = !_isPreview;

                if (_isPreview)
                {
                    SettingsMenuButton.Height = buttonHeight;
                }
            }

            if (CloseButton != null)
            {
                CloseButton.Visibility = _viewModel?.GamingOverlayCloseButton?.IsVisible == false ||
                                         _viewModel?.GamingOverlayFirstGroupHidden == true ||
                                         !OwnsChrome(_viewModel?.GamingOverlayCloseButton)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                CloseButton.IsHitTestVisible = !_isPreview;
                CloseButton.Focusable = !_isPreview;

                if (_isPreview)
                {
                    CloseButton.Height = buttonHeight;
                }
            }

            if (PopupPanelsRow != null && _isPreview)
            {
                PopupPanelsRow.Visibility = Visibility.Collapsed;
            }

            // 1:1 with the live overlay: the window scales the bar with the overlay scale and sizes
            // itself to that scaled content. The preview does the same instead of forcing 1.0,
            // otherwise preview and game show different sizes (and can clip the action buttons).
            if (_isPreview && OverlayRoot != null)
            {
                var scale = _viewModel?.GamingOverlayUiScale ?? 1.0;
                if (scale <= 0)
                {
                    scale = 1.0;
                }

                OverlayRoot.LayoutTransform = new ScaleTransform(scale, scale);
            }
        }
    }
}
