using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json.Serialization;
using IconGrid.Helpers;

namespace IconGrid.Models
{
    /// <summary>
    /// Represents a shortcut in the launcher.
    /// </summary>
    public class LauncherItem : INotifyPropertyChanged
    {
        /// <summary>
        /// Name displayed under the icon.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Full path to .exe or .lnk to launch.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Optional command-line arguments when launching the item.
        /// </summary>
        public string? Arguments { get; set; }

        /// <summary>
        /// Category / tab name (Games, Video etc.).
        /// </summary>
        public string Category { get; set; } = string.Empty;

        private string? _iconPath;
        private int _iconIndex;

        /// <summary>
        /// Optional icon file path (used by ShortcutHelper and XAML bindings).
        /// When set, we attempt to load an ImageSource into IconImage.
        /// </summary>
        public string? IconPath
        {
            get => _iconPath;
            set
            {
                if (value == _iconPath) return;
                _iconPath = value;
                OnPropertyChanged();
                LoadIconFromPathOrCache();
            }
        }

        /// <summary>
        /// Optional icon index inside .dll/.exe icon libraries (used for Windows system icons).
        /// </summary>
        public int IconIndex
        {
            get => _iconIndex;
            set
            {
                if (value == _iconIndex) return;
                _iconIndex = value;
                OnPropertyChanged();
                LoadIconFromPathOrCache();
            }
        }

        /// <summary>
        /// Base64-encoded PNG of the icon so we can render even if the source file is removed.
        /// </summary>
        public string? IconBase64 { get; set; }

        private ImageSource? _iconImage;
        /// <summary>
        /// ImageSource bound in XAML (LauncherIconTileStyle uses IconImage).
        /// </summary>
        [JsonIgnore]
        public ImageSource? IconImage
        {
            get => _iconImage;
            private set
            {
                if (Equals(value, _iconImage)) return;
                _iconImage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Optional WPF icon (if you load it directly as ImageSource instead of via path).
        /// </summary>
        [JsonIgnore]
        public ImageSource? Icon
        {
            get => IconImage;
            set
            {
                IconImage = value;
                OnPropertyChanged(nameof(Icon));
            }
        }

        private void LoadIconFromPathOrCache()
        {
            IconImage = null;

            if (string.IsNullOrWhiteSpace(_iconPath))
            {
                TryLoadFromBase64();
                return;
            }

            try
            {
                var iconLocation = IconHelper.NormalizeIconLocation(_iconPath, IconIndex);
                var isDirectory = !string.IsNullOrWhiteSpace(iconLocation.Path) && Directory.Exists(iconLocation.Path);

                // If this is a folder, see if a desktop.ini specifies a custom icon (e.g., OneDrive, Desktop, Documents).
                if (isDirectory)
                {
                    var folderIcon = IconHelper.TryGetFolderIconFromDesktopIni(iconLocation.Path!);
                    if (folderIcon.HasValue && !string.IsNullOrWhiteSpace(folderIcon.Value.Path))
                    {
                        iconLocation = folderIcon.Value;
                    }
                }

                if (string.IsNullOrWhiteSpace(iconLocation.Path))
                {
                    TryLoadFromBase64();
                    return;
                }

                var pathExistsAsFile = File.Exists(iconLocation.Path);
                var pathExistsAsDirectory = isDirectory || Directory.Exists(iconLocation.Path);

                if (!pathExistsAsFile && !pathExistsAsDirectory)
                {
                    TryLoadFromBase64();
                    return;
                }

                // If path points to a file that WPF can load directly (png, ico), use BitmapImage.
                var ext = pathExistsAsFile ? System.IO.Path.GetExtension(iconLocation.Path) : null;
                if (pathExistsAsFile &&
                    (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ext, ".ico", StringComparison.OrdinalIgnoreCase)))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(iconLocation.Path, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    var trimmed = IconHelper.TrimTransparentBorder(bmp) ?? bmp;
                    IconImage = trimmed;
                    IconBase64 = IconHelper.BitmapSourceToBase64(trimmed);
                    return;
                }

                BitmapSource? icon = null;

                // If an explicit index is provided (or .dll), extract that resource.
                if (pathExistsAsFile)
                {
                    var needsIndexedExtraction = iconLocation.HasIndex ||
                                                 _iconIndex != 0 ||
                                                 string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase);
                    if (needsIndexedExtraction)
                    {
                        icon = IconHelper.GetIconFromLibrary(iconLocation.Path!, iconLocation.Index);
                    }
                }

                // Otherwise attempt to extract a high-res shell icon (JUMBO/EXTRALARGE) to avoid blurriness.
                icon ??= IconHelper.GetHighResIcon(iconLocation.Path!);

                // Otherwise attempt to extract the associated icon from the file (exe, lnk, etc.)
                if (icon == null && File.Exists(iconLocation.Path!))
                {
                    var drawingIcon = System.Drawing.Icon.ExtractAssociatedIcon(iconLocation.Path!);
                    if (drawingIcon != null)
                    {
                        var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                            drawingIcon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bmpSource.Freeze();
                        var trimmed = IconHelper.TrimTransparentBorder(bmpSource) ?? bmpSource;
                        IconImage = trimmed;
                        IconBase64 = IconHelper.BitmapSourceToBase64(trimmed);
                        drawingIcon.Dispose();
                        return;
                    }
                }
            }
            catch
            {
                // ignore icon loading errors; leave IconImage null
            }

            // If everything else failed, try the cached base64 data.
            TryLoadFromBase64();
        }

        private void TryLoadFromBase64()
        {
            if (string.IsNullOrWhiteSpace(IconBase64))
            {
                return;
            }

            var cached = IconHelper.Base64ToBitmapImage(IconBase64);
            if (cached != null)
            {
                IconImage = IconHelper.TrimTransparentBorder(cached) ?? cached;
            }
        }

        /// <summary>
        /// Reloads the icon using the current IconPath or cached IconBase64.
        /// </summary>
        public void RefreshIcon() => LoadIconFromPathOrCache();

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
