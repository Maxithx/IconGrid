using System;
using System.Collections.Generic;
using Brush = System.Windows.Media.Brush;

namespace IconGrid.Helpers.Skin
{
    /// <summary>
    /// Turns per-module colour overrides from the blueprint (hex strings such as "#00FF66") into
    /// frozen brushes. Results are cached because templates re-evaluate on every overlay layout
    /// pass, and an invalid value returns null so the template falls back to the skin brush.
    /// </summary>
    public static class OverlayBlockBrushes
    {
        private static readonly Dictionary<string, Brush?> Cache = new(StringComparer.OrdinalIgnoreCase);

        public static Brush? FromHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }

            var key = hex.Trim();
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Brush? brush = null;
            try
            {
                if (System.Windows.Media.ColorConverter.ConvertFromString(key) is System.Windows.Media.Color color)
                {
                    var solid = new System.Windows.Media.SolidColorBrush(color);
                    solid.Freeze();
                    brush = solid;
                }
            }
            catch
            {
                // Invalid hex in user-edited JSON: ignore the override, never throw in a binding.
                brush = null;
            }

            Cache[key] = brush;
            return brush;
        }
    }
}
