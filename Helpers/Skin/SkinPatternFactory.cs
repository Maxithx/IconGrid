using System;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace IconGrid.Helpers.Skin
{
    /// <summary>
    /// Builds procedural overlay patterns (camo, hex, scanlines, meter segments) as tileable
    /// <see cref="DrawingBrush"/> instances.
    ///
    /// No image assets are involved: the patterns are generated from geometry, exactly like the
    /// CSS radial-gradient trick in the HTML mockup. That keeps the repository free of third-party
    /// artwork and lets a skin recolour a pattern without shipping new files.
    /// </summary>
    public static class SkinPatternFactory
    {
        public const string PatternNone = "None";
        public const string PatternCamo = "Camo";
        public const string PatternHex = "Hex";
        public const string PatternScanlines = "Scanlines";

        /// <summary>Creates the pattern requested by a skin token name; null when disabled.</summary>
        public static Brush? Create(string? kind, string colorA, string colorB, string colorC, string lineColor)
        {
            if (string.IsNullOrWhiteSpace(kind) || string.Equals(kind, PatternNone, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return kind switch
            {
                PatternCamo => CreateCamoBrush(colorA, colorB, colorC),
                PatternHex => CreateHexBrush(lineColor),
                PatternScanlines => CreateScanlineBrush(lineColor),
                _ => null
            };
        }

        /// <summary>Classic camo: overlapping blob tiles, like layered radial gradients.</summary>
        public static Brush CreateCamoBrush(string colorA, string colorB, string colorC, double tile = 60)
        {
            var group = new DrawingGroup();
            AddBlob(group, colorA, 0.15, 0.15, tile);
            AddBlob(group, colorB, 0.65, 0.65, tile);
            AddBlob(group, colorC, 0.40, 0.90, tile);
            AddBlob(group, colorA, 0.90, 0.40, tile);
            return CreateTiledBrush(group, tile, tile);
        }
        /// <summary>Thin horizontal CRT scanlines (static; no animation → no FPS cost).</summary>
        public static Brush CreateScanlineBrush(string lineColor = "#33000000", double spacing = 3)
        {
            var group = new DrawingGroup();
            var pen = new Pen(CreateBrush(lineColor), 1);
            pen.Freeze();
            group.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, 0.5), new Point(400, 0.5))));
            return CreateTiledBrush(group, 8, spacing);
        }

        /// <summary>Vertical segment grid painted on top of the usage meters.</summary>
        public static Brush CreateMeterSegmentsBrush(string lineColor = "#66000000", double spacing = 4)
        {
            var group = new DrawingGroup();
            var pen = new Pen(CreateBrush(lineColor), 1);
            pen.Freeze();
            group.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0.5, 0), new Point(0.5, 40))));
            return CreateTiledBrush(group, spacing, 40);
        }

        /// <summary>Honeycomb-ish hex pattern built from a stroked diamond (cheap approximation).</summary>
        public static Brush CreateHexBrush(string lineColor = "#33FFFFFF", double tile = 18)
        {
            var group = new DrawingGroup();
            var pen = new Pen(CreateBrush(lineColor), 1);
            pen.Freeze();

            var half = tile / 2;
            var points = new PointCollection
            {
                new(half, 0),
                new(tile, half * 0.5),
                new(tile, half * 1.5),
                new(half, tile),
                new(0, half * 1.5),
                new(0, half * 0.5)
            };

            var figure = new PathFigure(points[0], new[] { new PolyLineSegment(points, false) }, true);
            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();

            group.Children.Add(new GeometryDrawing(null, pen, geometry));
            return CreateTiledBrush(group, tile, tile);
        }

        private static Brush CreateTiledBrush(DrawingGroup group, double tileWidth, double tileHeight)
        {
            var brush = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, tileWidth, tileHeight),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, tileWidth, tileHeight),
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None
            };

            brush.Freeze();
            return brush;
        }

        private static void AddBlob(DrawingGroup group, string color, double centerX, double centerY, double tile)
        {
            var ellipse = new EllipseGeometry(new Point(centerX * tile, centerY * tile), tile * 0.22, tile * 0.22);
            ellipse.Freeze();
            var drawing = new GeometryDrawing(CreateBrush(color), null, ellipse);
            drawing.Freeze();
            group.Children.Add(drawing);
        }

        private static Brush CreateBrush(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }
}
