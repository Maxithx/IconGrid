using System;
using System.Windows.Media.Imaging;
using IconGrid.Helpers.Logging;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Caches the vendor logos used by the Hardware page.
///
/// The images are embedded WPF resources inside IconGrid.dll, so no separate
/// files have to be read next to IconGrid.exe. Without a cache WPF creates a
/// new BitmapImage for every Image element each time the Hardware page is
/// instantiated, which makes the logos appear noticeably after the rest of the
/// card. These bitmaps are decoded once, loaded completely
/// (CacheOption.OnLoad), frozen, and then shared by every Hardware page.
/// </summary>
public static class HardwareVendorLogoCache
{
    private const string BaseUri = "pack://application:,,,/Assets/Hw-logo/";

    private static readonly Lazy<BitmapImage?> AsusDarkLazy =
        new(() => Load("Asuslogo-dark-theme.png"));
    private static readonly Lazy<BitmapImage?> AsusLightLazy =
        new(() => Load("Asuslogo-light-theme.png"));
    private static readonly Lazy<BitmapImage?> AmdDarkLazy =
        new(() => Load("AMDlogo-darktheme.png"));
    private static readonly Lazy<BitmapImage?> AmdLightLazy =
        new(() => Load("AMDlogo-lighttheme.png"));
    private static readonly Lazy<BitmapImage?> IntelLazy =
        new(() => Load("Intel-logo.png"));
    private static readonly Lazy<BitmapImage?> NvidiaRtxLazy =
        new(() => Load("Nvidia-RTX-logo.png"));
    private static readonly Lazy<BitmapImage?> NvidiaGtxLazy =
        new(() => Load("Nvidia-GTX-logo.png"));

    public static BitmapImage? AsusDark => AsusDarkLazy.Value;
    public static BitmapImage? AsusLight => AsusLightLazy.Value;
    public static BitmapImage? AmdDark => AmdDarkLazy.Value;
    public static BitmapImage? AmdLight => AmdLightLazy.Value;
    public static BitmapImage? Intel => IntelLazy.Value;
    public static BitmapImage? NvidiaRtx => NvidiaRtxLazy.Value;
    public static BitmapImage? NvidiaGtx => NvidiaGtxLazy.Value;

    /// <summary>
    /// Decodes every logo once during startup, before the first window is shown,
    /// so the first Hardware page visit already has frozen bitmaps available.
    /// </summary>
    public static void Preload()
    {
        _ = AsusDark;
        _ = AsusLight;
        _ = AmdDark;
        _ = AmdLight;
        _ = Intel;
        _ = NvidiaRtx;
        _ = NvidiaGtx;
        AppTrace.Write("[HardwareVendorLogoCache] Vendor logos preloaded.");
    }

    private static BitmapImage? Load(string fileName)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            // The logos render at 80-100 px wide; decode near that size instead
            // of scaling the full-resolution PNG during the first render.
            bitmap.DecodePixelWidth = 200;
            bitmap.UriSource = new Uri(BaseUri + fileName, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            // A missing optional logo must never break the Hardware page.
            AppTrace.Write($"[HardwareVendorLogoCache] Failed to load {fileName}: {ex.Message}");
            return null;
        }
    }
}
