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
            };

            OverlayRoot.SizeChanged += (_, _) => ApplyChamfer();
            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// Makes the divider lines exactly N device pixels wide (N = DividerWidth token).
        ///
        /// A fixed DIP width is NOT pixel exact at 125%/150% scaling: 1 DIP becomes 1.25/1.5
        /// device pixels, so identical dividers can render 1 px or 2 px depending on where they
        /// land. Converting device pixels to DIPs through the current DPI removes that variance.
        /// </summary>
        private void ApplyDividerThickness()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var scale = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
            var pixels = Math.Max(1.0, Math.Round(_viewModel?.GamingOverlaySkin.DividerWidthValue ?? 1.0));
            var dipWidth = pixels / scale;

            foreach (var divider in EnumerateDividers(this))
            {
                if (Math.Abs(divider.Width - dipWidth) > 0.001)
                {
                    divider.Width = dipWidth;
                }
            }
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
            var width = OverlayRoot.ActualWidth;
            var height = OverlayRoot.ActualHeight;

            if (size <= 0 || width <= 0 || height <= 0)
            {
                OverlayRoot.Clip = null;
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
            ApplyPreviewState();
        }

        /// <summary>Keeps the preview at the same overlay scale as the live window.</summary>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayUiScale), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayBackgroundHeight), StringComparison.Ordinal))
            {
                ApplyPreviewState();
            }
        }

        private void Skin_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(GamingOverlaySkin.ChamferSizeValue), StringComparison.Ordinal))
            {
                ApplyChamfer();
            }

            if (string.Equals(e.PropertyName, nameof(GamingOverlaySkin.DividerWidthValue), StringComparison.Ordinal))
            {
                ApplyDividerThickness();
            }
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
                SettingsMenuButton.Visibility = Visibility.Visible;
                SettingsMenuButton.IsHitTestVisible = !_isPreview;
                SettingsMenuButton.Focusable = !_isPreview;

                if (_isPreview)
                {
                    SettingsMenuButton.Height = buttonHeight;
                }
            }

            if (CloseButton != null)
            {
                CloseButton.Visibility = Visibility.Visible;
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
