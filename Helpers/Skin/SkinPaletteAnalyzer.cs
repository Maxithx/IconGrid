using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace IconGrid.Helpers.Skin
{
    /// <summary>
    /// Extracts a usable skin palette from one or more user-supplied images (game screenshots,
    /// key art, league art).
    ///
    /// Deliberately local and dependency-free: no upload, no API key, no AI. The goal is a
    /// starting point that "feels" like the reference, which the user then refines in Skin Studio.
    /// A vision model can later replace/enrich this class behind the same result type.
    /// </summary>
    public static class SkinPaletteAnalyzer
    {
        /// <summary>One palette derived from images. Colours are hex strings.</summary>
        public sealed class Result
        {
            public string Background = "#202020";
            public string Surface = "#202020";
            public string Text = "#FFFFFF";
            public string Accent = "#7CFF6B";
            public string CpuBar = "#22C55E";
            public string GpuBar = "#A855F7";
            public IReadOnlyList<string> Swatches = Array.Empty<string>();
        }

        private sealed class Bucket
        {
            public int R;
            public int G;
            public int B;
            public int Count;
        }

        /// <summary>
        /// Analyses the given image files. Unreadable files are skipped; an empty list falls back
        /// to a neutral dark palette so the caller always gets something usable.
        /// </summary>
        public static Result Analyze(IEnumerable<string> imagePaths)
        {
            var buckets = new Dictionary<int, Bucket>();

            foreach (var path in imagePaths)
            {
                AccumulateImage(path, buckets);
            }

            var clusters = buckets.Values
                .OrderByDescending(bucket => bucket.Count)
                .Take(64)
                .Select(bucket => new
                {
                    bucket,
                    Color = Color.FromRgb((byte)bucket.R, (byte)bucket.G, (byte)bucket.B)
                })
                .ToList();

            if (clusters.Count == 0)
            {
                return new Result();
            }

            var swatches = clusters
                .Take(8)
                .Select(cluster => ToHex(cluster.Color))
                .ToList();

            var byLuminance = clusters
                .OrderBy(cluster => Luminance(cluster.Color))
                .ToList();

            var darkest = byLuminance[0].Color;
            var lightest = byLuminance[byLuminance.Count - 1].Color;

            var bySaturation = clusters
                .Where(cluster => Saturation(cluster.Color) > 0.18 && Luminance(cluster.Color) > 0.12)
                .OrderByDescending(cluster => Saturation(cluster.Color) * (0.4 + Luminance(cluster.Color)))
                .ToList();

            var accent = bySaturation.Count > 0 ? bySaturation[0].Color : lightest;
            var second = bySaturation.Count > 1 ? bySaturation[1].Color : accent;
            var third = bySaturation.Count > 2 ? bySaturation[2].Color : second;

            return new Result
            {
                Background = ToHex(EnsureDark(darkest)),
                Surface = ToHex(EnsureDark(Lighten(darkest, 12))),
                Text = ToHex(EnsureReadableText(lightest, darkest)),
                Accent = ToHex(EnsureBright(accent)),
                CpuBar = ToHex(EnsureBright(second)),
                GpuBar = ToHex(EnsureBright(third)),
                Swatches = swatches
            };
        }
        private static void AccumulateImage(string path, Dictionary<int, Bucket> buckets)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.DecodePixelWidth = 160;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                var width = converted.PixelWidth;
                var height = converted.PixelHeight;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                var stride = width * 4;
                var pixels = new byte[stride * height];
                converted.CopyPixels(pixels, stride, 0);

                for (var y = 0; y < height; y += 2)
                {
                    for (var x = 0; x < width; x += 2)
                    {
                        var offset = (y * stride) + (x * 4);
                        var b = pixels[offset];
                        var g = pixels[offset + 1];
                        var r = pixels[offset + 2];

                        // Coarse bucket (5 bits per channel) keeps the analysis cheap and stable.
                        var key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);

                        if (!buckets.TryGetValue(key, out var bucket))
                        {
                            bucket = new Bucket { R = r, G = g, B = b };
                            buckets[key] = bucket;
                        }

                        bucket.Count++;
                    }
                }
            }
            catch
            {
                // Unsupported/corrupt file: skip it, keep analysing the rest.
            }
        }

        private static Color EnsureDark(Color color)
        {
            var luminance = Luminance(color);
            if (luminance <= 0.16)
            {
                return color;
            }

            return Scale(color, 0.16 / Math.Max(luminance, 0.01) * 0.85);
        }

        private static Color EnsureBright(Color color)
        {
            var luminance = Luminance(color);
            if (luminance >= 0.35)
            {
                return color;
            }

            return Lighten(color, 40);
        }

        private static Color EnsureReadableText(Color candidate, Color background)
        {
            var contrast = Math.Abs(Luminance(candidate) - Luminance(background));
            if (contrast >= 0.45)
            {
                return candidate;
            }

            return Luminance(background) > 0.35 ? Color.FromRgb(17, 17, 17) : Colors.White;
        }

        private static Color Lighten(Color color, int amount) => Color.FromRgb(
            (byte)Math.Min(255, color.R + amount),
            (byte)Math.Min(255, color.G + amount),
            (byte)Math.Min(255, color.B + amount));

        private static Color Scale(Color color, double factor) => Color.FromRgb(
            (byte)Math.Max(0, Math.Min(255, color.R * factor)),
            (byte)Math.Max(0, Math.Min(255, color.G * factor)),
            (byte)Math.Max(0, Math.Min(255, color.B * factor)));

        private static double Luminance(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;

        private static double Saturation(Color color)
        {
            var max = Math.Max(color.R, Math.Max(color.G, color.B)) / 255.0;
            var min = Math.Min(color.R, Math.Min(color.G, color.B)) / 255.0;
            return max <= 0 ? 0 : (max - min) / max;
        }

        private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
