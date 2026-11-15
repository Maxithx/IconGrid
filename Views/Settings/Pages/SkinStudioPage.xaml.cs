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
        private OverlayBlock? _templateBlock;
        private System.Windows.Point _layerDragStart;
        private OverlayBlock? _layerDragCandidate;

        private void AddModule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string spec } || ViewModel == null)
            {
                return;
            }

            var parts = spec.Split(':');
            ViewModel.AddGamingOverlayModule(parts[0], parts.Length > 1 ? parts[1] : null);
        }

        private void ToggleGauges_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            // The button is a toggle: combined gauges get split, split ones get merged again.
            if (vm.GamingOverlayHasCombinedGauges)
            {
                vm.SplitGamingOverlayGaugeModules();
            }
            else if (vm.GamingOverlayHasMergeableGauges)
            {
                vm.MergeGamingOverlayGaugeModules();
            }
        }

        // ---------- Drag & drop in the layer list ----------

        private void LayersList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _layerDragStart = e.GetPosition(LayersList);
            _layerDragCandidate = FindLayerBlock(e.OriginalSource as System.Windows.DependencyObject);
        }

        private void LayersList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || _layerDragCandidate == null)
            {
                return;
            }

            var position = e.GetPosition(LayersList);
            if (Math.Abs(position.X - _layerDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _layerDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var block = _layerDragCandidate;
            _layerDragCandidate = null;

            // Folders are not draggable, and locked rows stay put.
            if (block.IsGroupLayer || block.IsLocked)
            {
                return;
            }

            System.Windows.DragDrop.DoDragDrop(LayersList, block, System.Windows.DragDropEffects.Move);
        }

        private void LayersList_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(OverlayBlock))
                ? System.Windows.DragDropEffects.Move
                : System.Windows.DragDropEffects.None;
            e.Handled = true;

            UpdateDropIndicator(e.OriginalSource as System.Windows.DependencyObject, e.GetPosition(LayersList));
        }

        private void LayersList_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            HideDropIndicator();
        }

        /// <summary>
        /// Places the light-blue insertion line at the row under the pointer: above/below a module
        /// (depending on which half is hovered) or right under a folder header (= into that group).
        /// </summary>
        private void UpdateDropIndicator(System.Windows.DependencyObject? hit, System.Windows.Point position)
        {
            var target = FindLayerBlock(hit);
            var container = target == null ? null : LayersList.ItemContainerGenerator.ContainerFromItem(target) as System.Windows.Controls.ListBoxItem;

            if (target == null || container == null)
            {
                HideDropIndicator();
                return;
            }

            var top = container.TranslatePoint(new System.Windows.Point(0, 0), LayersList).Y;
            var bottom = top + container.ActualHeight;

            var y = target.IsGroupLayer
                ? bottom                                   // dropped into the folder
                : position.Y < top + (container.ActualHeight / 2) ? top : bottom;

            LayerDropIndicator.Visibility = Visibility.Visible;
            LayerDropIndicatorTransform.Y = y - 1;
        }

        private void HideDropIndicator()
        {
            if (LayerDropIndicator != null)
            {
                LayerDropIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void LayersList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            HideDropIndicator();

            if (e.Data.GetData(typeof(OverlayBlock)) is not OverlayBlock dragged)
            {
                return;
            }

            var target = FindLayerBlock(e.OriginalSource as System.Windows.DependencyObject);
            var vm = ViewModel;
            if (vm == null || target == null || ReferenceEquals(target, dragged))
            {
                return;
            }

            // Dropped on a folder row -> move the layer into that group.
            if (target.IsGroupLayer)
            {
                vm.SetGamingOverlayModuleGroup(dragged, target.GroupId);
                KeepSelection(dragged);
                return;
            }

            // Chrome (settings/close/background) is fixed in the bar: it can only change group.
            if (dragged.IsChromeButton)
            {
                vm.SetGamingOverlayModuleGroup(dragged, target.GroupId);
                KeepSelection(dragged);
                return;
            }

            if (target.IsChromeButton)
            {
                return;
            }

            // Dropped on another module -> reorder, honouring which half of the row was hovered.
            var below = IsLowerHalf(target, e.GetPosition(LayersList));
            vm.MoveGamingOverlayModuleBefore(dragged, target, below);
            KeepSelection(dragged);
        }

        /// <summary>True when the pointer is in the lower half of the given layer row.</summary>
        private bool IsLowerHalf(OverlayBlock block, System.Windows.Point position)
        {
            if (LayersList.ItemContainerGenerator.ContainerFromItem(block) is not System.Windows.Controls.ListBoxItem container)
            {
                return false;
            }

            var top = container.TranslatePoint(new System.Windows.Point(0, 0), LayersList).Y;
            return position.Y > top + (container.ActualHeight / 2);
        }

        /// <summary>Walks up from a hit element to the layer row's OverlayBlock.</summary>
        private OverlayBlock? FindLayerBlock(System.Windows.DependencyObject? source)
        {
            var current = source;
            while (current != null)
            {
                if (current is System.Windows.Controls.ListBoxItem { DataContext: OverlayBlock block })
                {
                    return block;
                }

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        // ---------- Layer groups ----------

        private void NewGroup_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddGamingOverlayGroup();
        }

        private void UndoLayer_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.UndoGamingOverlayLayout();
        }

        /// <summary>
        /// The standard group is the base of every skin: neither it nor its layers can be
        /// deleted (they can still be hidden, locked, reordered and restyled).
        /// </summary>
        private void UpdateLayerToolbarState(OverlayBlock? block)
        {
            if (DeleteLayerButton == null)
            {
                return;
            }

            var deleteBlocked = block?.IsDeleteBlocked == true;
            DeleteLayerButton.IsEnabled = !deleteBlocked;

            if (!deleteBlocked)
            {
                DeleteLayerButton.ToolTip = "Delete layer or group";
                return;
            }

            var vm = ViewModel;
            DeleteLayerButton.ToolTip = block!.IsGroupLayer
                ? vm?.SkinStudioSystemGroupProtectedText ?? "The standard group cannot be deleted."
                : vm?.SkinStudioSystemGroupMemberProtectedText ?? "Layers in the standard group cannot be deleted.";
        }

        private void ToggleLock_Click(object sender, RoutedEventArgs e)
        {
            if (LayersList?.SelectedItem is OverlayBlock row)
            {
                ViewModel?.ToggleGamingOverlayLock(row);
                KeepSelection(row);
            }
        }

        /// <summary>Group folder selected -> rename field; module selected -> group picker.</summary>
        private void LoadGroupPanel(OverlayBlock block)
        {
            GroupNameSection.Visibility = block.IsGroupLayer ? Visibility.Visible : Visibility.Collapsed;

            var isModule = !block.IsGroupLayer;
            ModuleGroupSection.Visibility = isModule ? Visibility.Visible : Visibility.Collapsed;
            ModuleGroupBox.IsEnabled = isModule;

            // A folder is not a module: no code snippet / custom template for it.
            SnippetSection.Visibility = block.IsGroupLayer ? Visibility.Collapsed : Visibility.Visible;
            TemplateSection.Visibility = block.IsGroupLayer ? Visibility.Collapsed : Visibility.Visible;

            if (block.IsGroupLayer)
            {
                // A folder has no label/colour of its own.
                GenericColorSection.Visibility = Visibility.Collapsed;
                GroupNameBox.Text = block.GroupName;
                return;
            }

            if (!isModule)
            {
                return;
            }

            var groups = ViewModel?.GamingOverlayGroups;
            ModuleGroupBox.ItemsSource = groups;
            if (groups == null || groups.Count == 0)
            {
                return;
            }

            // Chrome rows can be organised too; they simply start in the standard (first) group.
            var current = groups.FirstOrDefault(group =>
                              string.Equals(group.Id, block.GroupId, StringComparison.OrdinalIgnoreCase))
                          ?? groups[0];
            ModuleGroupBox.SelectedItem = current;
        }

        private void GroupNameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (LayersList?.SelectedItem is OverlayBlock { IsGroupLayer: true } header)
            {
                ViewModel?.RenameGamingOverlayGroup(header.GroupId, GroupNameBox.Text);
                KeepSelection(header);
            }
        }

        private void ModuleGroupBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyPanels || ModuleGroupBox.SelectedItem is not OverlayLayerGroup group)
            {
                return;
            }

            if (LayersList?.SelectedItem is OverlayBlock block && !block.IsGroupLayer)
            {
                ViewModel?.SetGamingOverlayModuleGroup(block, group.Id);
                KeepSelection(block);
            }
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
            if (sender is not Button { Tag: OverlayBlock block } || ViewModel == null)
            {
                return;
            }

            // The eye on a group folder hides/shows every module inside it.
            if (block.IsGroupLayer)
            {
                ViewModel.ToggleGamingOverlayGroupHidden(block);
                return;
            }

            ViewModel.ToggleGamingOverlayModuleHidden(block);
        }

        private void ToggleGroupCollapsed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: OverlayBlock block })
            {
                ViewModel?.ToggleGamingOverlayGroupCollapsed(block);
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
            UpdateLayerToolbarState((sender as ListBox)?.SelectedItem as OverlayBlock);

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
                BackgroundSection.Visibility = Visibility.Collapsed;
                GroupNameSection.Visibility = Visibility.Collapsed;
                ModuleGroupSection.Visibility = Visibility.Collapsed;
                SnippetLibraryBox.ItemsSource = null;
                SnippetEditorBox.Text = string.Empty;
                SnippetStatusText.Text = string.Empty;
                TemplateEditorBox.Text = string.Empty;
                TemplateStatusText.Text = string.Empty;
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
                GenericColorSection.Visibility = Visibility.Visible;
                LoadChromePanel(block);
                LoadBackgroundPanel(block);
                LoadGroupPanel(block);
                LoadSnippetPanel(block);
                LoadTemplatePanel(block);
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
            if (LayersList?.SelectedItem is not OverlayBlock block)
            {
                return;
            }

            // A group row deletes the whole folder (its modules move to the first group).
            if (block.IsGroupLayer)
            {
                ViewModel?.RemoveGamingOverlayGroup(block.GroupId);
                return;
            }

            if (!block.IsChromeButton)
            {
                ViewModel?.RemoveGamingOverlayModule(block);
            }
        }

        // ---------- Overlay chrome buttons (settings / close) ----------

        /// <summary>Fills the chrome section (icon glyph + button size) for the fixed buttons.</summary>
        private void LoadChromePanel(OverlayBlock block)
        {
            ChromeSection.Visibility = block.IsButtonLayer ? Visibility.Visible : Visibility.Collapsed;
            if (!block.IsButtonLayer)
            {
                return;
            }

            ButtonIconBox.Text = block.Props != null && block.Props.TryGetValue("icon", out var icon)
                ? icon
                : string.Empty;
            ButtonWidthBox.Text = block.ButtonWidthValue.ToString("0.#");
            ButtonHeightBox.Text = block.ButtonHeightValue.ToString("0.#");
            ButtonBorderBox.Text = block.ButtonBorderThicknessValue.ToString("0.#");
            ButtonBorderColorBox.Text = block.Props != null && block.Props.TryGetValue("borderColor", out var borderColor)
                ? borderColor
                : string.Empty;
            ButtonBackgroundBox.Text = block.Props != null && block.Props.TryGetValue("background", out var background)
                ? background
                : string.Empty;
        }

        /// <summary>The background layer edits the skin tokens that paint the whole overlay bar.</summary>
        private void LoadBackgroundPanel(OverlayBlock block)
        {
            BackgroundSection.Visibility = block.IsBackgroundLayer ? Visibility.Visible : Visibility.Collapsed;
            if (!block.IsBackgroundLayer)
            {
                return;
            }

            var skin = ViewModel?.GamingOverlaySkin;
            if (skin == null)
            {
                return;
            }

            // The background has its own colour field; the generic label/colour pair does not apply.
            GenericColorSection.Visibility = Visibility.Collapsed;
            BackgroundColorBox.Text = skin.BarBackgroundColor;
            BackgroundRadiusBox.Text = skin.CornerRadius.ToString("0.#");
        }

        private void BackgroundColorBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var skin = ViewModel?.GamingOverlaySkin;
            var value = BackgroundColorBox.Text?.Trim();
            if (skin == null || string.IsNullOrWhiteSpace(value) || value == skin.BarBackgroundColor)
            {
                return;
            }

            skin.BarBackgroundColor = value;
            ViewModel?.SaveGamingOverlaySkin();
        }

        private void BackgroundRadiusBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var skin = ViewModel?.GamingOverlaySkin;
            if (skin == null || !double.TryParse(BackgroundRadiusBox.Text?.Trim(), out var radius) || radius < 0)
            {
                return;
            }

            if (Math.Abs(skin.CornerRadius - radius) < 0.01)
            {
                return;
            }

            skin.CornerRadius = radius;
            ViewModel?.SaveGamingOverlaySkin();
        }

        private void ButtonBackgroundBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("background", ButtonBackgroundBox.Text);

        private void ButtonIconBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("icon", ButtonIconBox.Text);

        private void ButtonWidthBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("buttonWidth", ButtonWidthBox.Text, numeric: true);

        private void ButtonHeightBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("buttonHeight", ButtonHeightBox.Text, numeric: true);

        private void ButtonBorderBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("borderThickness", ButtonBorderBox.Text, numeric: true);

        private void ButtonBorderColorBox_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyTextProperty("borderColor", ButtonBorderColorBox.Text);

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

        // ---------- Niveau B: custom XAML template ----------

        private void LoadTemplatePanel(OverlayBlock block)
        {
            var isNewBlock = !ReferenceEquals(_templateBlock, block);
            _templateBlock = block;

            // Show the module's own template when it has one, otherwise the matching starting code,
            // so the editor always explains what data this module exposes.
            if (string.IsNullOrWhiteSpace(block.Template))
            {
                TemplateEditorBox.Text = OverlayTemplateCatalog.GetStarter(block);
                if (isNewBlock)
                {
                    TemplateStatusText.Text = ViewModel?.SkinStudioTemplateStarterHintText ?? string.Empty;
                }
            }
            else
            {
                TemplateEditorBox.Text = block.Template;
                if (isNewBlock)
                {
                    TemplateStatusText.Text = string.Empty;
                }
            }
        }

        private void TemplateLoadCode_Click(object sender, RoutedEventArgs e)
        {
            if (LayersList?.SelectedItem is not OverlayBlock block)
            {
                return;
            }

            TemplateEditorBox.Text = OverlayTemplateCatalog.GetStarter(block);
            TemplateStatusText.Text = ViewModel?.SkinStudioTemplateStarterHintText ?? string.Empty;
            TemplateEditorBox.Focus();
        }

        private void TemplateApply_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            if (LayersList?.SelectedItem is not OverlayBlock block)
            {
                TemplateStatusText.Text = vm.SkinStudioSnippetNoModuleText;
                return;
            }

            if (!vm.TryApplyGamingOverlayTemplate(block, TemplateEditorBox.Text, out var error))
            {
                TemplateStatusText.Text = error switch
                {
                    "empty" => vm.SkinStudioSnippetEmptyText,
                    _ => vm.SkinStudioTemplateInvalidText
                };
                return;
            }

            TemplateStatusText.Text = vm.SkinStudioTemplateAppliedText;
            KeepSelection(block);
        }

        private void TemplateReset_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null || LayersList?.SelectedItem is not OverlayBlock block)
            {
                return;
            }

            vm.ResetGamingOverlayTemplate(block);
            TemplateEditorBox.Text = string.Empty;
            TemplateStatusText.Text = vm.SkinStudioTemplateResetDoneText;
            KeepSelection(block);
        }

        // ---------- Floating tool rail ----------

        private double _toolRailLeft = 12;
        private double _toolRailTop = 86;

        private void ToolRailGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var maxLeft = Math.Max(0, ToolRailLayer.ActualWidth - ToolRail.ActualWidth);
            var maxTop = Math.Max(0, ToolRailLayer.ActualHeight - ToolRail.ActualHeight);

            _toolRailLeft = Math.Clamp(_toolRailLeft + e.HorizontalChange, 0, maxLeft);
            _toolRailTop = Math.Clamp(_toolRailTop + e.VerticalChange, 0, maxTop);

            System.Windows.Controls.Canvas.SetLeft(ToolRail, _toolRailLeft);
            System.Windows.Controls.Canvas.SetTop(ToolRail, _toolRailTop);
        }

        private void ToolRailGrip_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _toolRailLeft = 12;
            _toolRailTop = 86;
            System.Windows.Controls.Canvas.SetLeft(ToolRail, _toolRailLeft);
            System.Windows.Controls.Canvas.SetTop(ToolRail, _toolRailTop);
        }

        private bool _toolRailHorizontal;

        private void ToolRailOrientation_Click(object sender, RoutedEventArgs e)
        {
            _toolRailHorizontal = !_toolRailHorizontal;
            ApplyToolRailOrientation();
        }

        /// <summary>Switches the tool rail between the vertical and horizontal layout.</summary>
        private void ApplyToolRailOrientation()
        {
            if (_toolRailHorizontal)
            {
                ToolRail.Width = double.NaN;
                ToolRail.Padding = new Thickness(6, 4, 6, 4);
                ToolRailPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                ToolRailOrientationButton.Content = "⇅";
            }
            else
            {
                ToolRail.Width = 56;
                ToolRail.Padding = new Thickness(5, 4, 5, 4);
                ToolRailPanel.Orientation = System.Windows.Controls.Orientation.Vertical;
                ToolRailOrientationButton.Content = "⇄";
            }

            foreach (var child in ToolRailPanel.Children)
            {
                if (child is not System.Windows.Shapes.Rectangle { Tag: "separator" } separator)
                {
                    continue;
                }

                if (_toolRailHorizontal)
                {
                    separator.Width = 1;
                    separator.Height = 24;
                    separator.Margin = new Thickness(8, 0, 8, 0);
                    separator.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    separator.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                }
                else
                {
                    separator.Width = double.NaN;
                    separator.Height = 1;
                    separator.Margin = new Thickness(2, 2, 2, 8);
                    separator.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    separator.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                }
            }

            // Keep the rail inside the canvas after its size changed.
            var maxLeft = Math.Max(0, ToolRailLayer.ActualWidth - ToolRail.ActualWidth);
            var maxTop = Math.Max(0, ToolRailLayer.ActualHeight - ToolRail.ActualHeight);
            _toolRailLeft = Math.Clamp(_toolRailLeft, 0, maxLeft);
            _toolRailTop = Math.Clamp(_toolRailTop, 0, maxTop);
            System.Windows.Controls.Canvas.SetLeft(ToolRail, _toolRailLeft);
            System.Windows.Controls.Canvas.SetTop(ToolRail, _toolRailTop);
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
