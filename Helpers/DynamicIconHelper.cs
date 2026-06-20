using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGrid.Helpers;

/// <summary>
/// Generates an accent-colored icon (like the titlebar icon) dynamically from the provided accent color using WPF drawing (no GDI+ dependency).
/// </summary>
internal static class DynamicIconHelper
{
    public static ImageSource? CreateAccentIconImageSource(System.Windows.Media.Color accentColor, int size = 256)
    {
        try
        {
            var dotColor = Colors.White;

            var dpi = 96d;
            var rtb = new RenderTargetBitmap(size, size, dpi, dpi, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();

            using (var dc = dv.RenderOpen())
            {
                // Background rounded square
                double radius = size * 0.18;
                dc.DrawRoundedRectangle(new SolidColorBrush(accentColor), null,
                    new Rect(0, 0, size, size), radius, radius);

                // 3x3 dots
                int dotCount = 3;
                double dotDiameter = size * 0.18;
                double gap = size * 0.04;
                double gridWidth = (dotCount * dotDiameter) + ((dotCount - 1) * gap);
                double startX = (size - gridWidth) / 2;
                double startY = startX;
                var dotBrush = new SolidColorBrush(dotColor);

                for (int row = 0; row < dotCount; row++)
                {
                    for (int col = 0; col < dotCount; col++)
                    {
                        double x = startX + col * (dotDiameter + gap);
                        double y = startY + row * (dotDiameter + gap);
                        dc.DrawEllipse(dotBrush, null, new System.Windows.Point(x + dotDiameter / 2, y + dotDiameter / 2), dotDiameter / 2, dotDiameter / 2);
                    }
                }
            }

            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
        catch
        {
            return null;
        }
    }

    public static System.Drawing.Icon? CreateIcon(BitmapSource source)
    {
        IntPtr hIcon = IntPtr.Zero;
        try
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            using var bitmap = new Bitmap(ms);
            hIcon = bitmap.GetHicon();
            if (hIcon == IntPtr.Zero)
                return null;

            using var icon = System.Drawing.Icon.FromHandle(hIcon);
            return (System.Drawing.Icon)icon.Clone();
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
            {
                DestroyIcon(hIcon);
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
