using System.Windows;
using IconGrid.Models;
using IconGrid.ViewModels;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;

namespace IconGrid.Views
{
    /// <summary>
    /// Skin Studio: a three-panel canvas editor (tools/module library, canvas with guides and the
    /// live overlay, layers + properties). The overlay row itself is rendered from the blueprint,
    /// so every change here is visible immediately in the canvas and in-game.
    /// </summary>
    public partial class SkinStudioPage : System.Windows.Controls.UserControl
    {
        public SkinStudioPage()
        {
            InitializeComponent();
        }

        private MainViewModel? ViewModel => System.Windows.Window.GetWindow(this)?.DataContext as MainViewModel;

        private bool _suppressOpacityUpdate;

        private void AddModule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string spec } || ViewModel == null)
            {
                return;
            }

            var parts = spec.Split(':');
            ViewModel.AddGamingOverlayModule(parts[0], parts.Length > 1 ? parts[1] : null);
        }

        // The layer list is reversed (topmost first), so "up" in the panel = later in the row.
        private void MoveModuleUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: OverlayBlock block } && ViewModel != null)
            {
                ViewModel.MoveGamingOverlayModule(block, 1);
            }
        }

        private void MoveModuleDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: OverlayBlock block } && ViewModel != null)
            {
                ViewModel.MoveGamingOverlayModule(block, -1);
            }
        }

        private void ToggleModuleHidden_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: OverlayBlock block } && ViewModel != null)
            {
                ViewModel.ToggleGamingOverlayModuleHidden(block);
            }
        }

        private void RemoveModule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: OverlayBlock block } && ViewModel != null)
            {
                ViewModel.RemoveGamingOverlayModule(block);
            }
        }

        private void ResetBlueprint_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ResetGamingOverlayBlueprint();
        }

        private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox { SelectedItem: OverlayBlock block })
            {
                SelectedModuleText.Text = "—";
                PropLabelBox.Text = string.Empty;
                PropColorBox.Text = string.Empty;
                _suppressOpacityUpdate = true;
                OpacitySlider.Value = 1.0;
                _suppressOpacityUpdate = false;
                OpacityValueText.Text = "100%";
                return;
            }

            SelectedModuleText.Text = block.DisplayName;
            PropLabelBox.Text = block.Label;
            PropColorBox.Text = block.ColorHex;

            _suppressOpacityUpdate = true;
            OpacitySlider.Value = block.OpacityValue;
            _suppressOpacityUpdate = false;
            OpacityValueText.Text = $"{(int)Math.Round(block.OpacityValue * 100)}%";
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressOpacityUpdate)
            {
                return;
            }

            if (OpacityValueText != null)
            {
                OpacityValueText.Text = $"{(int)Math.Round(e.NewValue * 100)}%";
            }

            if (LayersList?.SelectedItem is OverlayBlock block)
            {
                ViewModel?.SetGamingOverlayModuleOpacity(block, e.NewValue);
            }
        }

        private void DeleteSelectedModule_Click(object sender, RoutedEventArgs e)
        {
            if (LayersList?.SelectedItem is OverlayBlock block)
            {
                ViewModel?.RemoveGamingOverlayModule(block);
            }
        }

        private void PropLabelBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (LayersList?.SelectedItem is OverlayBlock block)
            {
                ViewModel?.SetGamingOverlayModuleProperty(block, "label", PropLabelBox.Text);
            }
        }

        private void PropColorBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (LayersList?.SelectedItem is OverlayBlock block)
            {
                ViewModel?.SetGamingOverlayModuleProperty(block, "color", PropColorBox.Text);
            }
        }
    }
}
