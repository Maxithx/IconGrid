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
            }

            _viewModel = e.NewValue as MainViewModel;

            if (_viewModel != null)
            {
                _viewModel.GamingOverlaySkin.PropertyChanged += Skin_PropertyChanged;
            }

            ApplyChamfer();
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
            if (SettingsMenuButton != null)
            {
                SettingsMenuButton.Visibility = _isPreview ? Visibility.Collapsed : Visibility.Visible;
            }

            if (CloseButton != null)
            {
                CloseButton.Visibility = _isPreview ? Visibility.Collapsed : Visibility.Visible;
            }

            if (PopupPanelsRow != null && _isPreview)
            {
                PopupPanelsRow.Visibility = Visibility.Collapsed;
            }

            // The preview host controls the visual scale (Viewbox), so the control must not
            // apply the window's LayoutTransform on top of it.
            if (_isPreview && OverlayRoot != null)
            {
                OverlayRoot.LayoutTransform = Transform.Identity;
            }
        }
    }
}
