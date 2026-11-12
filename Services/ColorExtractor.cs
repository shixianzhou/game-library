using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLibrary.Services;

/// <summary>从背景图中提取一个足够鲜亮、又能当高亮色的主色。</summary>
public static class ColorExtractor
{
    public static Color FromImage(string path)
    {
        try
        {
            if (!File.Exists(path)) return DefaultAccent;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = 96;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(bitmap, new Rect(0, 0, 32, 32));
            }

            var target = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            var stride = 32 * 4;
            var pixels = new byte[stride * 32];
            target.CopyPixels(pixels, stride, 0);

            double weightSum = 0, red = 0, green = 0, blue = 0;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var b = pixels[i] / 255d;
                var g = pixels[i + 1] / 255d;
                var r = pixels[i + 2] / 255d;
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                if (luminance < 0.10 || luminance > 0.93) continue;

                var saturation = max <= 0.001 ? 0 : (max - min) / max;
                var weight = 0.25 + saturation * 0.75;
                weightSum += weight;
                red += r * weight;
                green += g * weight;
                blue += b * weight;
            }

            if (weightSum < 0.5) return DefaultAccent;
            var color = Color.FromRgb(
                (byte)Math.Clamp(red / weightSum * 255, 0, 255),
                (byte)Math.Clamp(green / weightSum * 255, 0, 255),
                (byte)Math.Clamp(blue / weightSum * 255, 0, 255));

            // 提高饱和度，避免背景主色太灰；同时保证在深色背景上足够亮。
            var boosted = Boost(color, 0.32, 0.45);
            var luminance2 = (0.2126 * boosted.R + 0.7152 * boosted.G + 0.0722 * boosted.B) / 255;
            if (luminance2 < 0.38) boosted = Lighten(boosted, 0.35);
            if (luminance2 > 0.82) boosted = Darken(boosted, 0.25);
            return boosted;
        }
        catch
        {
            return DefaultAccent;
        }
    }

    public static Color DefaultAccent => Color.FromRgb(0x80, 0xA9, 0x4E);

    private static Color Boost(Color color, double saturationBoost, double valueBoost)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        double h = 0;
        if (delta > 0.0001)
        {
            if (Math.Abs(max - r) < 0.0001) h = 60 * (((g - b) / delta) % 6);
            else if (Math.Abs(max - g) < 0.0001) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
        }
        if (h < 0) h += 360;
        var s = max <= 0.0001 ? 0 : delta / max;
        var v = max;
        s = Math.Clamp(s + saturationBoost, 0, 1);
        v = Math.Clamp(v + valueBoost * (1 - v), 0.2, 0.95);
        return ColorFromHsv(h, s, v);
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        var m = value - c;
        double r = 0, g = 0, b = 0;
        if (hue < 60) { r = c; g = x; }
        else if (hue < 120) { r = x; g = c; }
        else if (hue < 180) { g = c; b = x; }
        else if (hue < 240) { g = x; b = c; }
        else if (hue < 300) { r = x; b = c; }
        else { r = c; b = x; }
        return Color.FromRgb(
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255));
    }

    private static Color Lighten(Color color, double amount)
        => Color.FromRgb(
            (byte)Math.Clamp(color.R + (255 - color.R) * amount, 0, 255),
            (byte)Math.Clamp(color.G + (255 - color.G) * amount, 0, 255),
            (byte)Math.Clamp(color.B + (255 - color.B) * amount, 0, 255));

    private static Color Darken(Color color, double amount)
        => Color.FromRgb(
            (byte)Math.Clamp(color.R * (1 - amount), 0, 255),
            (byte)Math.Clamp(color.G * (1 - amount), 0, 255),
            (byte)Math.Clamp(color.B * (1 - amount), 0, 255));
}
