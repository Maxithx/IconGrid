using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        public GamingOverlayBar()
        {
            InitializeComponent();

            // IsPreview may be set from XAML before the child elements exist; re-apply once
            // the control is loaded so the preview state always wins.
            Loaded += (_, _) => ApplyPreviewState();
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
