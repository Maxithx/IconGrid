using System;
using System.Linq;
using System.Windows;
using IconGrid.Helpers.Skin;
using IconGrid.Models;
using IconGrid.ViewModels;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
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
        private bool _suppressPropertyPanels;
        private OverlayBlock? _snippetBlock;

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
                _suppressPropertyPanels = true;
                GaugeSection.Visibility = Visibility.Collapsed;
                ChromeSection.Visibility = Visibility.Collapsed;
                SnippetLibraryBox.ItemsSource = null;
                SnippetEditorBox.Text = string.Empty;
                SnippetStatusText.Text = string.Empty;
                _suppressPropertyPanels = false;
                return;
            }

            SelectedModuleText.Text = block.DisplayName;
            PropLabelBox.Text = block.Label;
            PropColorBox.Text = block.ColorHex;

            _suppressOpacityUpdate = true;
            OpacitySlider.Value = block.OpacityValue;
            _suppressOpacityUpdate = false;
            OpacityValueText.Text = $"{(int)Math.Round(block.OpacityValue * 100)}%";

            _suppressPropertyPanels = true;
            try
            {
                LoadGaugePanel(block);
                LoadChromePanel(block);
                LoadSnippetPanel(block);
            }
            finally
            {
                _suppressPropertyPanels = false;
            }
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
            if (LayersList?.SelectedItem is OverlayBlock block && !block.IsChromeButton)
            {
                ViewModel?.RemoveGamingOverlayModule(block);
            }
        }

        // ---------- Overlay chrome buttons (settings / close) ----------

        /// <summary>Fills the chrome section (icon glyph + button size) for the fixed buttons.</summary>
        private void LoadChromePanel(OverlayBlock block)
        {
            ChromeSection.Visibility = block.IsChromeButton ? Visibility.Visible : Visibility.Collapsed;
            if (!block.IsChromeButton)
            {
                return;
            }

            ButtonIconBox.Text = block.Props != null && block.Props.TryGetValue("icon", out var icon)
                ? icon
                : string.Empty;
            ButtonWidthBox.Text = block.ButtonWidthValue.ToString("0.#");
            ButtonHeightBox.Text = block.ButtonHeightValue.ToString("0.#");
        }

        private void ButtonIconBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("icon", ButtonIconBox.Text);

        private void ButtonWidthBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("buttonWidth", ButtonWidthBox.Text, numeric: true);

        private void ButtonHeightBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("buttonHeight", ButtonHeightBox.Text, numeric: true);

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

        // ---------- Gauge appearance (CPU / GPU usage) ----------

        /// <summary>Fills the gauge section from the module props (hidden for non-gauge modules).</summary>
        private void LoadGaugePanel(OverlayBlock block)
        {
            var isGauge = block.IsMeterModule;
            GaugeSection.Visibility = isGauge ? Visibility.Visible : Visibility.Collapsed;
            if (!isGauge)
            {
                return;
            }

            SelectMeterStyle(block.MeterStyle);
            MeterColorBox.Text = block.MeterColorHex;
            MeterWidthBox.Text = block.MeterWidthValue.ToString("0.#");
            MeterHeightBox.Text = block.MeterHeightValue.ToString("0.#");
            ShowTempBox.IsChecked = block.ShowTemp;
            ShowPercentBox.IsChecked = block.ShowPercent;
            ShowGridBox.IsChecked = block.HasGrid;
        }

        private void SelectMeterStyle(string meterStyle)
        {
            foreach (var item in MeterStyleBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag as string, meterStyle, StringComparison.OrdinalIgnoreCase))
                {
                    MeterStyleBox.SelectedItem = item;
                    return;
                }
            }

            MeterStyleBox.SelectedIndex = 0;
        }

        private void MeterStyleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyPanels || MeterStyleBox.SelectedItem is not ComboBoxItem { Tag: string style })
            {
                return;
            }

            if (LayersList?.SelectedItem is OverlayBlock block)
            {
                ViewModel?.SetGamingOverlayModuleProperty(block, "meter", style);
                _suppressPropertyPanels = true;
                LoadGaugePanel(block);
                _suppressPropertyPanels = false;
                KeepSelection(block);
            }
        }

        private void GaugeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyPanels || sender is not CheckBox { Tag: string key } box)
            {
                return;
            }

            if (LayersList?.SelectedItem is OverlayBlock block)
            {
                ViewModel?.SetGamingOverlayModuleProperty(block, key, box.IsChecked == true ? "true" : "false");
                KeepSelection(block);
            }
        }

        private void MeterColorBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("meterColor", MeterColorBox.Text);

        private void MeterWidthBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("meterWidth", MeterWidthBox.Text, numeric: true);

        private void MeterHeightBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("meterHeight", MeterHeightBox.Text, numeric: true);

        /// <summary>Writes one module property from a text box, ignoring junk in numeric fields.</summary>
        private void ApplyTextProperty(string key, string? raw, bool numeric = false)
        {
            if (_suppressPropertyPanels || LayersList?.SelectedItem is not OverlayBlock block)
            {
                return;
            }

            var value = raw?.Trim() ?? string.Empty;
            if (numeric && value.Length > 0 && !double.TryParse(value, out _))
            {
                return;
            }

            ViewModel?.SetGamingOverlayModuleProperty(block, key, value);
            KeepSelection(block);
        }

        // ---------- Code snippets (JSON property patches) ----------

        private void LoadSnippetPanel(OverlayBlock block)
        {
            // Keep the status line when the panel is only refreshed for the same module (a commit
            // rebuilds the layer list and re-selects it, which would otherwise wipe the message).
            if (!ReferenceEquals(_snippetBlock, block))
            {
                _snippetBlock = block;
                SnippetStatusText.Text = string.Empty;
            }

            SnippetLibraryBox.ItemsSource = OverlaySnippetLibrary.ForBlock(block);
            SnippetLibraryBox.SelectedIndex = -1;
            SnippetEditorBox.Text = block.Snippet ?? string.Empty;
        }

        private void SnippetLibraryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SnippetLibraryBox.SelectedItem is OverlaySnippet snippet)
            {
                SnippetEditorBox.Text = snippet.Code;
            }
        }

        private void SnippetInsert_Click(object sender, RoutedEventArgs e) => SnippetApply_Click(sender, e);

        private void SnippetApply_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            if (LayersList?.SelectedItem is not OverlayBlock block)
            {
                SnippetStatusText.Text = vm.SkinStudioSnippetNoModuleText;
                return;
            }

            if (!vm.TryApplyGamingOverlaySnippet(block, SnippetEditorBox.Text, out var error))
            {
                SnippetStatusText.Text = string.Equals(error, "empty", StringComparison.Ordinal)
                    ? vm.SkinStudioSnippetEmptyText
                    : vm.SkinStudioSnippetInvalidText;
                return;
            }

            SnippetStatusText.Text = vm.SkinStudioSnippetAppliedText;
            _suppressPropertyPanels = true;
            LoadGaugePanel(block);
            _suppressPropertyPanels = false;
            KeepSelection(block);
        }

        private void SnippetReset_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null || LayersList?.SelectedItem is not OverlayBlock block)
            {
                return;
            }

            vm.ResetGamingOverlaySnippet(block);
            SnippetEditorBox.Text = string.Empty;
            SnippetStatusText.Text = vm.SkinStudioSnippetResetDoneText;
            _suppressPropertyPanels = true;
            LoadGaugePanel(block);
            _suppressPropertyPanels = false;
            KeepSelection(block);
        }

        /// <summary>
        /// Re-selects the module after a commit replaced the layer list. Without this the ListBox
        /// would clear its selection on every edit, which would close the properties panel.
        /// </summary>
        private void KeepSelection(OverlayBlock block)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (LayersList != null && LayersList.SelectedItem == null)
                    {
                        LayersList.SelectedItem = block;
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
