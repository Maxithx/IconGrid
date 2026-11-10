using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using IconGrid.Helpers.Skin;
using IconGrid.Models;
using IconGrid.ViewModels;
using Button = System.Windows.Controls.Button;
using Forms = System.Windows.Forms;

namespace IconGrid.Views
{
    /// <summary>
    /// Skin Studio: edits the gaming overlay skin (colours, fonts, corner radius) and can derive
    /// a starting palette from user-supplied game screenshots/key art.
    ///
    /// The page only paints tokens from <see cref="MainViewModel.GamingOverlaySkin"/>; the overlay
    /// layout lives in the monitor row settings and is never touched here.
    /// </summary>
    public partial class SkinStudioPage : System.Windows.Controls.UserControl
    {
        public SkinStudioPage()
        {
            InitializeComponent();
            Presets = GamingOverlaySkin.CreatePresets();
            FontOptions = GamingOverlaySkin.AvailableFonts;
            MonoFontOptions = GamingOverlaySkin.AvailableMonoFonts;
        }

        public IReadOnlyList<GamingOverlaySkin> Presets { get; }

        public IReadOnlyList<string> FontOptions { get; }

        public IReadOnlyList<string> MonoFontOptions { get; }

        public ObservableCollection<string> ExtractedSwatches { get; } = new();

        private MainViewModel? ViewModel => System.Windows.Window.GetWindow(this)?.DataContext as MainViewModel;

        private GamingOverlaySkin? Skin => ViewModel?.GamingOverlaySkin;

        private void ShowOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            // Same manual show path as the Gaming overlay page: it never touches the automatic
            // game-start / game-exit pipeline.
            ViewModel?.RequestShowGamingOverlay();
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: GamingOverlaySkin preset })
            {
                return;
            }

            ViewModel?.ApplyGamingOverlaySkin(preset, save: true);
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string token || Skin == null)
            {
                return;
            }

            var current = token switch
            {
                nameof(GamingOverlaySkin.BarBackgroundColor) => Skin.BarBackgroundColor,
                nameof(GamingOverlaySkin.PopupBackgroundColor) => Skin.PopupBackgroundColor,
                nameof(GamingOverlaySkin.TextColor) => Skin.TextColor,
                nameof(GamingOverlaySkin.AccentColor) => Skin.AccentColor,
                nameof(GamingOverlaySkin.CpuBarColor) => Skin.CpuBarColor,
                nameof(GamingOverlaySkin.GpuBarColor) => Skin.GpuBarColor,
                nameof(GamingOverlaySkin.DividerColor) => Skin.DividerColor,
                _ => "#FFFFFF"
            };

            using var dialog = new Forms.ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                Color = System.Drawing.ColorTranslator.FromHtml(current)
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
            {
                return;
            }

            var hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";

            switch (token)
            {
                case nameof(GamingOverlaySkin.BarBackgroundColor):
                    Skin.BarBackgroundColor = hex;
                    break;
                case nameof(GamingOverlaySkin.PopupBackgroundColor):
                    Skin.PopupBackgroundColor = hex;
                    break;
                case nameof(GamingOverlaySkin.TextColor):
                    Skin.TextColor = hex;
                    break;
                case nameof(GamingOverlaySkin.AccentColor):
                    Skin.AccentColor = hex;
                    break;
                case nameof(GamingOverlaySkin.CpuBarColor):
                    Skin.CpuBarColor = hex;
                    break;
                case nameof(GamingOverlaySkin.GpuBarColor):
                    Skin.GpuBarColor = hex;
                    break;
                case nameof(GamingOverlaySkin.DividerColor):
                    Skin.DividerColor = hex;
                    break;
            }

            ViewModel?.SaveGamingOverlaySkin();
        }

        private void FontSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ViewModel?.SaveGamingOverlaySkin();
        }

        private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ViewModel?.SaveGamingOverlaySkin();
        }

        private void DividerOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ViewModel?.SaveGamingOverlaySkin();
        }

        private void ImportImages_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select game screenshots or key art",
                Multiselect = true,
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            {
                return;
            }

            var result = SkinPaletteAnalyzer.Analyze(dialog.FileNames);
            var skin = Skin;
            if (skin == null)
            {
                return;
            }

            skin.BarBackgroundColor = result.Background;
            skin.PopupBackgroundColor = result.Surface;
            skin.TextColor = result.Text;
            skin.AccentColor = result.Accent;
            skin.CpuBarColor = result.CpuBar;
            skin.GpuBarColor = result.GpuBar;

            ExtractedSwatches.Clear();
            foreach (var swatch in result.Swatches.Distinct().Take(8))
            {
                ExtractedSwatches.Add(swatch);
            }

            ViewModel?.SaveGamingOverlaySkin();
        }
    }
}
