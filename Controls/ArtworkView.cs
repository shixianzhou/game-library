using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLibrary.Controls;

/// <summary>
/// 游戏封面。用户没选封面时，按游戏名稳定生成一张方块风的场景插画，
/// 这样空库和随手添加的游戏也不会出现灰底占位图。
/// </summary>
public sealed class ArtworkView : FrameworkElement
{
    private static readonly Dictionary<string, ImageSource?> ImageCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheGate = new();

    public static readonly DependencyProperty GameNameProperty = DependencyProperty.Register(
        nameof(GameName), typeof(string), typeof(ArtworkView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender, OnVisualChanged));

    public static readonly DependencyProperty SeedTextProperty = DependencyProperty.Register(
        nameof(SeedText), typeof(string), typeof(ArtworkView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender, OnVisualChanged));

    public static readonly DependencyProperty ImagePathProperty = DependencyProperty.Register(
        nameof(ImagePath), typeof(string), typeof(ArtworkView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualChanged));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(ArtworkView),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualChanged));

    public static readonly DependencyProperty TopCornerRadiusProperty = DependencyProperty.Register(
        nameof(TopCornerRadius), typeof(double), typeof(ArtworkView),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualChanged));

    public static readonly DependencyProperty IsHeroProperty = DependencyProperty.Register(
        nameof(IsHero), typeof(bool), typeof(ArtworkView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualChanged));

    public string GameName
    {
        get => (string)GetValue(GameNameProperty);
        set => SetValue(GameNameProperty, value);
    }

    public string SeedText
    {
        get => (string)GetValue(SeedTextProperty);
        set => SetValue(SeedTextProperty, value);
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double TopCornerRadius
    {
        get => (double)GetValue(TopCornerRadiusProperty);
        set => SetValue(TopCornerRadiusProperty, value);
    }

    public bool IsHero
    {
        get => (bool)GetValue(IsHeroProperty);
        set => SetValue(IsHeroProperty, value);
    }

    public ArtworkView()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ArtworkView)d).InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 2 || height < 2) return;

        var rect = new Rect(0, 0, width, height);
        var clip = CreateClip(rect, TopCornerRadius > 0 ? TopCornerRadius : CornerRadius, CornerRadius);
        dc.PushClip(clip);
        try
        {
            var image = TryLoadImage(ImagePath);
            var seed = StableHash((SeedText ?? "") + "|" + (GameName ?? ""));

            if (image != null)
            {
                DrawImageCover(dc, rect, image);
            }
            else
            {
                DrawScene(dc, rect, seed, IsHero);
            }
        }
        finally
        {
            dc.Pop();
        }
    }

    private static Geometry CreateClip(Rect rect, double topRadius, double bottomRadius)
    {
        var top = Math.Max(0, Math.Min(topRadius, Math.Min(rect.Width, rect.Height) / 2));
        var bottom = Math.Max(0, Math.Min(bottomRadius, Math.Min(rect.Width, rect.Height) / 2));
        if (top < 0.5 && bottom < 0.5) return new RectangleGeometry(rect);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(rect.Left + top, rect.Top), true, true);
            if (top > 0)
                ctx.ArcTo(new Point(rect.Right - top, rect.Top), new Size(top, top), 0, false, SweepDirection.Clockwise, true, false);
            else
                ctx.LineTo(new Point(rect.Right, rect.Top), true, false);

            if (bottom > 0)
                ctx.ArcTo(new Point(rect.Right, rect.Top + top), new Size(top, top), 0, false, SweepDirection.Clockwise, true, false);
            else
                ctx.LineTo(new Point(rect.Right, rect.Top), true, false);

            ctx.LineTo(new Point(rect.Right, rect.Bottom - bottom), true, false);
            if (bottom > 0)
                ctx.ArcTo(new Point(rect.Right - bottom, rect.Bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise, true, false);

            ctx.LineTo(new Point(rect.Left + bottom, rect.Bottom), true, false);
            if (bottom > 0)
                ctx.ArcTo(new Point(rect.Left, rect.Bottom - bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise, true, false);

            ctx.LineTo(new Point(rect.Left, rect.Top + top), true, false);
            if (top > 0)
                ctx.ArcTo(new Point(rect.Left + top, rect.Top), new Size(top, top), 0, false, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static void DrawImageCover(DrawingContext dc, Rect rect, ImageSource image)
    {
        var sourceWidth = image.Width;
        var sourceHeight = image.Height;
        if (sourceWidth <= 0 || sourceHeight <= 0) return;

        var scale = Math.Max(rect.Width / sourceWidth, rect.Height / sourceHeight);
        var drawWidth = sourceWidth * scale;
        var drawHeight = sourceHeight * scale;
        var destination = new Rect(
            rect.Left + (rect.Width - drawWidth) / 2,
            rect.Top + (rect.Height - drawHeight) / 2,
            drawWidth,
            drawHeight);
        dc.DrawImage(image, destination);

        var shade = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0.55),
            EndPoint = new Point(0.5, 1),
        };
        shade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
        shade.GradientStops.Add(new GradientStop(Color.FromArgb(75, 0, 0, 0), 1));
        dc.DrawRectangle(shade, null, rect);
    }

    private static ImageSource? TryLoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            if (!File.Exists(path)) return null;
            lock (CacheGate)
            {
                if (ImageCache.TryGetValue(path, out var cached)) return cached;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = 1600;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            lock (CacheGate)
            {
                if (ImageCache.Count > 64) ImageCache.Clear();
                ImageCache[path] = bitmap;
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    // =====================================================================
    // 程序化封面
    // =====================================================================

    private sealed record Palette(
        Color SkyTop, Color SkyBottom, Color Sun, Color Cloud,
        Color MountainBack, Color MountainMid, Color MountainFront,
        Color Grass, Color GrassDark, Color Soil, Color SoilDark,
        Color Leaf, Color LeafDark, Color Trunk, Color Glow, bool Night);

    private static readonly Palette[] Palettes =
    {
        // 森林（主视觉固定使用这一套，保证大幅封面饱和、明亮）
        new(Color.FromRgb(0x4C, 0x9C, 0xD6), Color.FromRgb(0xD9, 0xEF, 0xCB), Color.FromRgb(0xFF, 0xF4, 0xC9), Color.FromRgb(0xFF, 0xFF, 0xFF),
            Color.FromRgb(0x78, 0x96, 0xA0), Color.FromRgb(0x5B, 0x81, 0x60), Color.FromRgb(0x3C, 0x63, 0x45),
            Color.FromRgb(0x78, 0xB6, 0x4B), Color.FromRgb(0x4B, 0x75, 0x30), Color.FromRgb(0x55, 0x3E, 0x2D), Color.FromRgb(0x3D, 0x2D, 0x23),
            Color.FromRgb(0x3C, 0x74, 0x36), Color.FromRgb(0x28, 0x51, 0x29), Color.FromRgb(0x5C, 0x41, 0x2E), Color.FromRgb(0xCF, 0xF0, 0x7A), false),
        // 黄昏
        new(Color.FromRgb(0x2E, 0x3A, 0x67), Color.FromRgb(0xE0, 0x8B, 0x60), Color.FromRgb(0xFF, 0xD0, 0x86), Color.FromRgb(0xF3, 0xBF, 0xA3),
            Color.FromRgb(0x3C, 0x41, 0x68), Color.FromRgb(0x55, 0x46, 0x57), Color.FromRgb(0x2E, 0x33, 0x3B),
            Color.FromRgb(0x6D, 0x7E, 0x3F), Color.FromRgb(0x42, 0x53, 0x2F), Color.FromRgb(0x43, 0x32, 0x2C), Color.FromRgb(0x2C, 0x22, 0x20),
            Color.FromRgb(0x3C, 0x4E, 0x37), Color.FromRgb(0x27, 0x37, 0x29), Color.FromRgb(0x54, 0x39, 0x2A), Color.FromRgb(0xFF, 0xBE, 0x5F), false),
        // 雪原
        new(Color.FromRgb(0x9A, 0xC7, 0xDC), Color.FromRgb(0xEF, 0xF3, 0xEB), Color.FromRgb(0xFF, 0xFC, 0xE4), Color.FromRgb(0xFA, 0xFD, 0xFB),
            Color.FromRgb(0x8A, 0xA5, 0xB3), Color.FromRgb(0x71, 0x8D, 0x9A), Color.FromRgb(0x4F, 0x69, 0x77),
            Color.FromRgb(0xE2, 0xEB, 0xE1), Color.FromRgb(0xA6, 0xB9, 0xB0), Color.FromRgb(0x60, 0x65, 0x61), Color.FromRgb(0x48, 0x4C, 0x4A),
            Color.FromRgb(0x3A, 0x5B, 0x52), Color.FromRgb(0x28, 0x44, 0x3E), Color.FromRgb(0x5D, 0x4B, 0x3E), Color.FromRgb(0xAA, 0xF0, 0xFF), false),
        // 赤林
        new(Color.FromRgb(0x1E, 0x16, 0x29), Color.FromRgb(0x7A, 0x2C, 0x38), Color.FromRgb(0xFF, 0xAE, 0x67), Color.FromRgb(0xB5, 0x68, 0x6B),
            Color.FromRgb(0x38, 0x24, 0x34), Color.FromRgb(0x5A, 0x2D, 0x39), Color.FromRgb(0x33, 0x1C, 0x26),
            Color.FromRgb(0x74, 0x47, 0x38), Color.FromRgb(0x4B, 0x2B, 0x28), Color.FromRgb(0x30, 0x1E, 0x20), Color.FromRgb(0x20, 0x14, 0x16),
            Color.FromRgb(0x60, 0x2C, 0x36), Color.FromRgb(0x44, 0x1D, 0x2A), Color.FromRgb(0x45, 0x2C, 0x25), Color.FromRgb(0xFF, 0x6E, 0x53), false),
        // 星夜
        new(Color.FromRgb(0x08, 0x0E, 0x21), Color.FromRgb(0x2A, 0x3B, 0x63), Color.FromRgb(0xE6, 0xEF, 0xFF), Color.FromRgb(0x9D, 0xB1, 0xD9),
            Color.FromRgb(0x1A, 0x27, 0x44), Color.FromRgb(0x21, 0x31, 0x50), Color.FromRgb(0x16, 0x22, 0x36),
            Color.FromRgb(0x38, 0x54, 0x43), Color.FromRgb(0x25, 0x3A, 0x31), Color.FromRgb(0x21, 0x1F, 0x27), Color.FromRgb(0x16, 0x15, 0x1B),
            Color.FromRgb(0x22, 0x42, 0x3E), Color.FromRgb(0x16, 0x2E, 0x2B), Color.FromRgb(0x3D, 0x31, 0x2B), Color.FromRgb(0x6A, 0xE4, 0xD1), true),
        // 沙漠
        new(Color.FromRgb(0xF0, 0xB2, 0x67), Color.FromRgb(0xF8, 0xE1, 0xAD), Color.FromRgb(0xFF, 0xF5, 0xD0), Color.FromRgb(0xFF, 0xF6, 0xDF),
            Color.FromRgb(0xC4, 0x8C, 0x59), Color.FromRgb(0xAA, 0x6D, 0x47), Color.FromRgb(0x83, 0x50, 0x36),
            Color.FromRgb(0xC7, 0x9D, 0x58), Color.FromRgb(0x99, 0x72, 0x3C), Color.FromRgb(0x7E, 0x53, 0x37), Color.FromRgb(0x5C, 0x39, 0x27),
            Color.FromRgb(0x6D, 0x7E, 0x3A), Color.FromRgb(0x4D, 0x5D, 0x2B), Color.FromRgb(0x6A, 0x4A, 0x30), Color.FromRgb(0xFF, 0xDE, 0x85), false),
        // 秋日
        new(Color.FromRgb(0x8E, 0xB8, 0xC8), Color.FromRgb(0xF2, 0xD0, 0x9F), Color.FromRgb(0xFF, 0xEC, 0xBE), Color.FromRgb(0xF8, 0xEB, 0xD3),
            Color.FromRgb(0x7C, 0x73, 0x6A), Color.FromRgb(0x87, 0x64, 0x4A), Color.FromRgb(0x59, 0x49, 0x3E),
            Color.FromRgb(0xA5, 0x89, 0x3E), Color.FromRgb(0x72, 0x5B, 0x2D), Color.FromRgb(0x58, 0x3D, 0x2C), Color.FromRgb(0x3D, 0x2B, 0x21),
            Color.FromRgb(0xB0, 0x68, 0x30), Color.FromRgb(0x86, 0x47, 0x25), Color.FromRgb(0x5D, 0x43, 0x2E), Color.FromRgb(0xFF, 0xD0, 0x73), false),
        // 沼泽
        new(Color.FromRgb(0x4C, 0x6C, 0x61), Color.FromRgb(0xAC, 0xB8, 0x8A), Color.FromRgb(0xE8, 0xF0, 0xC0), Color.FromRgb(0xC9, 0xD3, 0xB2),
            Color.FromRgb(0x3D, 0x51, 0x4E), Color.FromRgb(0x4C, 0x5F, 0x46), Color.FromRgb(0x2D, 0x3D, 0x33),
            Color.FromRgb(0x69, 0x7C, 0x3F), Color.FromRgb(0x45, 0x57, 0x31), Color.FromRgb(0x3C, 0x35, 0x2A), Color.FromRgb(0x2B, 0x25, 0x1E),
            Color.FromRgb(0x42, 0x5F, 0x3D), Color.FromRgb(0x2E, 0x47, 0x2E), Color.FromRgb(0x44, 0x35, 0x2A), Color.FromRgb(0xB2, 0xE2, 0x69), false),
    };

    private static void DrawScene(DrawingContext dc, Rect rect, int seed, bool isHero)
    {
        var rng = new Random(seed);
        var palette = isHero ? Palettes[0] : Palettes[Math.Abs(seed) % Palettes.Length];
        var width = rect.Width;
        var height = rect.Height;
        var horizon = height * (0.60 + rng.NextDouble() * 0.08);
        var sunX = width * (0.64 + rng.NextDouble() * 0.18);
        var sunY = height * (0.14 + rng.NextDouble() * 0.10);
        var sunRadius = Math.Max(8, height * (isHero ? 0.055 : 0.07));

        // 天空
        var sky = new LinearGradientBrush(palette.SkyTop, palette.SkyBottom, 90);
        dc.DrawRectangle(sky, null, rect);

        if (palette.Night)
        {
            for (var i = 0; i < (isHero ? 90 : 34); i++)
            {
                var x = rng.NextDouble() * width;
                var y = rng.NextDouble() * horizon * 0.85;
                var size = rng.NextDouble() < 0.12 ? 2.2 : 1.1;
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(80 + rng.Next(120)), 255, 255, 240)), null,
                    new Rect(x, y, size, size));
            }
        }

        // 太阳 / 月亮光晕
        var glow = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new Point(sunX, sunY),
            GradientOrigin = new Point(sunX, sunY),
            RadiusX = sunRadius * 3.6,
            RadiusY = sunRadius * 3.6,
        };
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(palette.Night ? (byte)55 : (byte)90, palette.Sun.R, palette.Sun.G, palette.Sun.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, palette.Sun.R, palette.Sun.G, palette.Sun.B), 1));
        dc.DrawEllipse(glow, null, new Point(sunX, sunY), sunRadius * 3.6, sunRadius * 3.6);
        dc.DrawEllipse(new SolidColorBrush(palette.Sun), null, new Point(sunX, sunY), sunRadius, sunRadius);

        // 云
        for (var i = 0; i < (isHero ? 5 : 3); i++)
        {
            var cx = rng.NextDouble() * width;
            var cy = height * (0.10 + rng.NextDouble() * 0.22);
            var cw = width * (0.06 + rng.NextDouble() * 0.10);
            var ch = Math.Max(3, height * 0.014);
            var cloudBrush = new SolidColorBrush(Color.FromArgb((byte)(45 + rng.Next(35)), palette.Cloud.R, palette.Cloud.G, palette.Cloud.B));
            dc.DrawRectangle(cloudBrush, null, new Rect(cx, cy, cw, ch));
            dc.DrawRectangle(cloudBrush, null, new Rect(cx + cw * 0.12, cy - ch * 0.8, cw * 0.55, ch * 0.85));
            dc.DrawRectangle(cloudBrush, null, new Rect(cx + cw * 0.55, cy - ch * 0.45, cw * 0.38, ch * 0.5));
        }

        // 远山三层
        DrawMountainLayer(dc, width, horizon, height * 0.16, palette.MountainBack, rng, 0.75);
        DrawMountainLayer(dc, width, horizon + 1, height * 0.12, palette.MountainMid, rng, 0.95);
        DrawMountainLayer(dc, width, horizon + 2, height * 0.075, palette.MountainFront, rng, 1.1);

        // 地平线雾气
        var fog = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
        };
        fog.GradientStops.Add(new GradientStop(Color.FromArgb(0, palette.SkyBottom.R, palette.SkyBottom.G, palette.SkyBottom.B), 0));
        fog.GradientStops.Add(new GradientStop(Color.FromArgb(95, palette.SkyBottom.R, palette.SkyBottom.G, palette.SkyBottom.B), 0.55));
        fog.GradientStops.Add(new GradientStop(Color.FromArgb(0, palette.SkyBottom.R, palette.SkyBottom.G, palette.SkyBottom.B), 1));
        dc.DrawRectangle(fog, null, new Rect(0, horizon - height * 0.07, width, height * 0.13));

        DrawGround(dc, width, height, horizon, palette, rng);
        DrawDistantTrees(dc, width, height, horizon, palette, rng);
        DrawGate(dc, width, height, horizon, palette, rng);

        // 两侧树
        var treeCount = isHero ? 4 : 2;
        for (var i = 0; i < treeCount; i++)
        {
            var leftSide = i % 2 == 0;
            var x = leftSide
                ? width * (0.02 + rng.NextDouble() * 0.14)
                : width * (0.84 + rng.NextDouble() * 0.13);
            var baseY = horizon + height * (0.03 + rng.NextDouble() * 0.16);
            var treeHeight = height * (0.20 + rng.NextDouble() * 0.15);
            var treeWidth = treeHeight * (0.48 + rng.NextDouble() * 0.14);
            DrawTree(dc, x, baseY, treeHeight, treeWidth, palette, rng, palette.Night);
        }

        // 空中方块
        var cubeCount = isHero ? 6 : 3;
        for (var i = 0; i < cubeCount; i++)
        {
            var cx = width * (0.12 + rng.NextDouble() * 0.76);
            var cy = height * (0.12 + rng.NextDouble() * 0.30);
            var size = Math.Max(3, height * (0.010 + rng.NextDouble() * 0.014));
            DrawCube(dc, cx, cy, size, palette);
        }

        // 方块冒险者：给画面一个明确的主角，避免只有空景
        var characterBase = horizon + height * (isHero ? 0.20 : 0.24);
        var characterHeight = height * (isHero ? 0.30 : 0.27);
        if (isHero)
        {
            DrawAdventurer(dc, width * 0.64, characterBase, characterHeight, seed, rng, 0);
            DrawAdventurer(dc, width * 0.755, characterBase + height * 0.010, characterHeight * 0.93, seed, rng, 1);
            DrawCompanion(dc, width * 0.555, characterBase + height * 0.006, characterHeight * 0.58, palette);
        }
        else
        {
            DrawAdventurer(dc, width * 0.62, characterBase, characterHeight, seed, rng, 0);
            DrawCompanion(dc, width * 0.46, characterBase + height * 0.004, characterHeight * 0.50, palette);
        }
        DrawForegroundDetails(dc, width, height, horizon, palette, rng, isHero);

        // 前景压暗 + 暗角
        var bottomShade = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0.64),
            EndPoint = new Point(0.5, 1),
        };
        bottomShade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
        bottomShade.GradientStops.Add(new GradientStop(Color.FromArgb(isHero ? (byte)140 : (byte)75, 4, 5, 3), 1));
        dc.DrawRectangle(bottomShade, null, rect);

        var vignette = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            Center = new Point(0.5, 0.42),
            GradientOrigin = new Point(0.5, 0.42),
            RadiusX = 0.78,
            RadiusY = 0.78,
        };
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.62));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(75, 0, 0, 0), 1));
        dc.DrawRectangle(vignette, null, rect);

        // 轻微颗粒，避免大面积渐变显得发灰
        var grain = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255));
        for (var i = 0; i < 70; i++)
        {
            dc.DrawRectangle(grain, null, new Rect(rng.NextDouble() * width, rng.NextDouble() * height, 1, 1));
        }
    }

    private static void DrawAdventurer(
        DrawingContext dc, double x, double baseY, double height, int seed, Random rng, int variant)
    {
        var u = height / 13.2;
        var female = variant >= 2;
        var shirt = female
            ? Color.FromRgb(0xB7, 0x5A, 0x72)
            : variant % 2 == 0
                ? Color.FromRgb(0x2E, 0x91, 0x8C)
                : Color.FromRgb(0x9B, 0x5B, 0x35);
        var shirtDark = Darken(shirt, 0.24);
        var pants = female
            ? Color.FromRgb(0x46, 0x35, 0x48)
            : variant % 2 == 0
                ? Color.FromRgb(0x3E, 0x4B, 0x78)
                : Color.FromRgb(0x4A, 0x40, 0x35);
        var skin = female ? Color.FromRgb(0xE0, 0xAD, 0x86) : Color.FromRgb(0xD7, 0x9C, 0x6B);
        var skinDark = Darken(skin, 0.18);
        var hair = female
            ? Color.FromRgb(0xC8, 0x8A, 0x45)
            : variant % 2 == 0
                ? Color.FromRgb(0x3A, 0x2A, 0x1D)
                : Color.FromRgb(0x7A, 0x4A, 0x27);
        var shoe = Color.FromRgb(0x2B, 0x27, 0x22);

        var shadow = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        dc.DrawEllipse(shadow, null, new Point(x, baseY + u * 0.15), u * 3.0, u * 0.42);

        void Box(Brush brush, double bx, double by, double bw, double bh)
            => dc.DrawRectangle(brush, null, new Rect(bx, by, bw, bh));

        var body = new SolidColorBrush(shirt);
        var bodyDark = new SolidColorBrush(shirtDark);
        var trousers = new SolidColorBrush(pants);
        var skinBrush = new SolidColorBrush(skin);
        var skinShadow = new SolidColorBrush(skinDark);
        var hairBrush = new SolidColorBrush(hair);
        var shoeBrush = new SolidColorBrush(shoe);

        // 腿和鞋
        Box(trousers, x - u * 1.85, baseY - u * 4.2, u * 1.65, u * 3.4);
        Box(trousers, x + u * 0.20, baseY - u * 4.2, u * 1.65, u * 3.4);
        Box(shoeBrush, x - u * 1.95, baseY - u * 0.85, u * 1.85, u * 0.85);
        Box(shoeBrush, x + u * 0.10, baseY - u * 0.85, u * 1.85, u * 0.85);

        // 长发（女性角色，先画在身体后面）
        if (female)
        {
            Box(hairBrush, x - u * 2.55, baseY - u * 10.9, u * 5.1, u * 5.6);
        }

        // 躯干
        Box(body, x - u * 1.9, baseY - u * 9.25, u * 3.8, u * 5.15);
        Box(bodyDark, x - u * 1.9, baseY - u * 4.55, u * 3.8, u * 0.45);
        Box(bodyDark, x - u * 1.9, baseY - u * 9.25, u * 3.8, u * 0.35);

        // 裙子
        if (female)
        {
            var skirt = new SolidColorBrush(Lighten(shirt, 0.08));
            Box(skirt, x - u * 2.55, baseY - u * 5.25, u * 5.1, u * 1.8);
            Box(bodyDark, x - u * 2.55, baseY - u * 3.55, u * 5.1, u * 0.30);
            Box(skirt, x - u * 2.95, baseY - u * 4.05, u * 5.9, u * 0.55);
        }

        // 手臂与手
        Box(body, x - u * 3.15, baseY - u * 8.85, u * 1.22, u * 4.15);
        Box(body, x + u * 1.93, baseY - u * 8.85, u * 1.22, u * 4.15);
        Box(skinBrush, x - u * 3.15, baseY - u * 4.85, u * 1.22, u * 1.05);
        Box(skinBrush, x + u * 1.93, baseY - u * 4.85, u * 1.22, u * 1.05);
        Box(skinShadow, x - u * 3.15, baseY - u * 4.15, u * 1.22, u * 0.35);
        Box(skinShadow, x + u * 1.93, baseY - u * 4.15, u * 1.22, u * 0.35);

        // 头
        Box(skinBrush, x - u * 1.95, baseY - u * 13.15, u * 3.9, u * 3.9);
        Box(hairBrush, x - u * 2.05, baseY - u * 13.55, u * 4.1, u * 1.15);
        Box(hairBrush, x - u * 2.05, baseY - u * 13.55, u * 0.55, u * 2.55);
        Box(hairBrush, x + u * 1.50, baseY - u * 13.55, u * 0.55, u * 1.75);
        Box(hairBrush, x - u * 1.20, baseY - u * 12.55, u * 0.55, u * 0.35);
        Box(hairBrush, x + u * 0.55, baseY - u * 12.55, u * 0.55, u * 0.35);
        Box(skinShadow, x - u * 1.95, baseY - u * 10.15, u * 3.9, u * 0.35);
        Box(skinBrush, x - u * 1.0, baseY - u * 11.85, u * 0.52, u * 0.55);
        Box(skinBrush, x + u * 0.48, baseY - u * 11.85, u * 0.52, u * 0.55);
        Box(new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)), x - u * 0.86, baseY - u * 11.78, u * 0.20, u * 0.20);
        Box(new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)), x + u * 0.62, baseY - u * 11.78, u * 0.20, u * 0.20);
        Box(skinShadow, x - u * 0.62, baseY - u * 10.55, u * 1.24, u * 0.32);

        if (female)
        {
            Box(hairBrush, x - u * 2.12, baseY - u * 12.15, u * 0.58, u * 4.2);
            Box(hairBrush, x + u * 1.54, baseY - u * 12.15, u * 0.58, u * 4.2);
            Box(new SolidColorBrush(Color.FromRgb(0xF4, 0xD0, 0x5E)),
                x + u * 1.15, baseY - u * 12.95, u * 0.48, u * 0.48);
            Box(new SolidColorBrush(Color.FromRgb(0xD8, 0x75, 0x86)),
                x + u * 1.36, baseY - u * 12.76, u * 0.26, u * 0.26);
        }

        // 衣领 / 背带细节
        Box(new SolidColorBrush(Lighten(shirt, 0.18)), x - u * 1.9, baseY - u * 9.0, u * 3.8, u * 0.28);
        Box(bodyDark, x - u * 0.35, baseY - u * 8.65, u * 0.7, u * 4.1);
        if (variant % 2 == 1)
        {
            Box(new SolidColorBrush(Color.FromRgb(0x6A, 0x46, 0x2B)), x + u * 1.2, baseY - u * 9.0, u * 0.55, u * 4.5);
        }

        // 让每个角色的发型有一点随机差异
        if (rng.NextDouble() < 0.5)
        {
            Box(hairBrush, x - u * 2.05, baseY - u * 12.45, u * 0.42, u * 1.3);
            Box(hairBrush, x + u * 1.63, baseY - u * 12.45, u * 0.42, u * 1.3);
        }
    }

    private static void DrawCompanion(DrawingContext dc, double x, double baseY, double height, Palette palette)
    {
        var u = height / 2.8;
        var fur = new SolidColorBrush(Color.FromRgb(0xD6, 0xD2, 0xC4));
        var furDark = new SolidColorBrush(Color.FromRgb(0x8B, 0x88, 0x7B));
        var shadow = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));

        dc.DrawEllipse(shadow, null, new Point(x, baseY), u * 2.1, u * 0.28);
        dc.DrawRectangle(fur, null, new Rect(x - u * 1.5, baseY - u * 1.35, u * 2.5, u * 1.05));
        dc.DrawRectangle(fur, null, new Rect(x + u * 0.72, baseY - u * 1.85, u * 1.15, u * 1.15));
        dc.DrawRectangle(fur, null, new Rect(x + u * 1.62, baseY - u * 1.42, u * 0.55, u * 0.38));
        dc.DrawRectangle(furDark, null, new Rect(x + u * 0.82, baseY - u * 2.02, u * 0.32, u * 0.5));
        dc.DrawRectangle(furDark, null, new Rect(x + u * 1.42, baseY - u * 2.02, u * 0.32, u * 0.5));
        dc.DrawRectangle(furDark, null, new Rect(x + u * 1.88, baseY - u * 1.55, u * 0.16, u * 0.16));
        dc.DrawRectangle(fur, null, new Rect(x - u * 1.2, baseY - u * 0.42, u * 0.36, u * 0.55));
        dc.DrawRectangle(fur, null, new Rect(x + u * 0.55, baseY - u * 0.42, u * 0.36, u * 0.55));
        dc.DrawRectangle(furDark, null, new Rect(x - u * 1.72, baseY - u * 1.55, u * 0.55, u * 0.28));
        dc.DrawRectangle(furDark, null, new Rect(x - u * 1.86, baseY - u * 1.93, u * 0.42, u * 0.55));
    }

    private static void DrawCat(DrawingContext dc, double x, double baseY, double height)
    {
        var u = height / 3.0;
        var fur = new SolidColorBrush(Color.FromRgb(0xD9, 0x8B, 0x4B));
        var furDark = new SolidColorBrush(Color.FromRgb(0xAE, 0x5F, 0x30));
        var furLight = new SolidColorBrush(Color.FromRgb(0xF3, 0xC4, 0x91));
        var ink = new SolidColorBrush(Color.FromRgb(0x3B, 0x27, 0x1D));
        var pink = new SolidColorBrush(Color.FromRgb(0xE7, 0x9A, 0x94));
        var green = new SolidColorBrush(Color.FromRgb(0x9B, 0xC8, 0x63));
        var shadow = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0));

        dc.DrawEllipse(shadow, null, new Point(x + u * 0.25, baseY), u * 1.8, u * 0.24);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(26, 0xFF, 0xE8, 0xB8)), null,
            new Point(x, baseY - u * 1.45), u * 1.65, u * 2.05);

        // 长尾巴，从身体右侧卷到身后
        var tail = new StreamGeometry();
        using (var ctx = tail.Open())
        {
            ctx.BeginFigure(new Point(x + u * 0.64, baseY - u * 0.72), false, false);
            ctx.QuadraticBezierTo(new Point(x + u * 2.25, baseY - u * 0.45), new Point(x + u * 2.05, baseY - u * 1.78), true, true);
        }
        dc.DrawGeometry(null, new Pen(furDark, Math.Max(1.4, u * 0.42))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        }, tail);
        dc.DrawGeometry(null, new Pen(fur, Math.Max(1, u * 0.25))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        }, tail);

        // 身体与胸口
        dc.DrawEllipse(fur, null, new Point(x, baseY - u * 1.12), u * 0.82, u * 1.10);
        dc.DrawEllipse(furLight, null, new Point(x + u * 0.05, baseY - u * 1.02), u * 0.42, u * 0.70);

        // 两只前爪
        dc.DrawEllipse(furLight, null, new Point(x - u * 0.38, baseY - u * 0.20), u * 0.32, u * 0.23);
        dc.DrawEllipse(furLight, null, new Point(x + u * 0.38, baseY - u * 0.20), u * 0.32, u * 0.23);
        dc.DrawEllipse(furDark, null, new Point(x - u * 0.38, baseY - u * 0.14), u * 0.10, u * 0.05);
        dc.DrawEllipse(furDark, null, new Point(x + u * 0.38, baseY - u * 0.14), u * 0.10, u * 0.05);

        // 头部
        dc.DrawEllipse(fur, null, new Point(x, baseY - u * 2.32), u * 0.76, u * 0.66);

        // 尖耳朵
        DrawTriangle(dc, fur, new Point(x - u * 0.66, baseY - u * 2.66), new Point(x - u * 0.32, baseY - u * 3.12), new Point(x - u * 0.20, baseY - u * 2.55));
        DrawTriangle(dc, fur, new Point(x + u * 0.66, baseY - u * 2.66), new Point(x + u * 0.32, baseY - u * 3.12), new Point(x + u * 0.20, baseY - u * 2.55));
        DrawTriangle(dc, pink, new Point(x - u * 0.56, baseY - u * 2.71), new Point(x - u * 0.37, baseY - u * 2.98), new Point(x - u * 0.29, baseY - u * 2.63));
        DrawTriangle(dc, pink, new Point(x + u * 0.56, baseY - u * 2.71), new Point(x + u * 0.37, baseY - u * 2.98), new Point(x + u * 0.29, baseY - u * 2.63));

        // 眼睛、鼻子、胡须
        dc.DrawEllipse(green, null, new Point(x - u * 0.30, baseY - u * 2.39), u * 0.14, u * 0.18);
        dc.DrawEllipse(green, null, new Point(x + u * 0.30, baseY - u * 2.39), u * 0.14, u * 0.18);
        dc.DrawEllipse(ink, null, new Point(x - u * 0.30, baseY - u * 2.39), u * 0.045, u * 0.15);
        dc.DrawEllipse(ink, null, new Point(x + u * 0.30, baseY - u * 2.39), u * 0.045, u * 0.15);
        DrawTriangle(dc, pink, new Point(x, baseY - u * 2.18), new Point(x - u * 0.09, baseY - u * 2.10), new Point(x + u * 0.09, baseY - u * 2.10));

        var whisker = new Pen(new SolidColorBrush(Color.FromArgb(200, 0xF4, 0xE6, 0xCF)), Math.Max(0.8, u * 0.035));
        dc.DrawLine(whisker, new Point(x - u * 0.18, baseY - u * 2.16), new Point(x - u * 0.72, baseY - u * 2.23));
        dc.DrawLine(whisker, new Point(x - u * 0.18, baseY - u * 2.10), new Point(x - u * 0.74, baseY - u * 2.05));
        dc.DrawLine(whisker, new Point(x + u * 0.18, baseY - u * 2.16), new Point(x + u * 0.72, baseY - u * 2.23));
        dc.DrawLine(whisker, new Point(x + u * 0.18, baseY - u * 2.10), new Point(x + u * 0.74, baseY - u * 2.05));

        // 头顶与身体条纹
        dc.DrawRectangle(furDark, null, new Rect(x - u * 0.30, baseY - u * 2.94, u * 0.16, u * 0.34));
        dc.DrawRectangle(furDark, null, new Rect(x - u * 0.06, baseY - u * 3.00, u * 0.16, u * 0.38));
        dc.DrawRectangle(furDark, null, new Rect(x + u * 0.18, baseY - u * 2.94, u * 0.16, u * 0.34));
        dc.DrawRectangle(furDark, null, new Rect(x - u * 0.68, baseY - u * 1.36, u * 0.34, u * 0.13));
        dc.DrawRectangle(furDark, null, new Rect(x + u * 0.34, baseY - u * 1.36, u * 0.34, u * 0.13));
        dc.DrawRectangle(furDark, null, new Rect(x - u * 0.66, baseY - u * 1.08, u * 0.30, u * 0.12));
        dc.DrawRectangle(furDark, null, new Rect(x + u * 0.36, baseY - u * 1.08, u * 0.30, u * 0.12));
    }

    private static void DrawTriangle(DrawingContext dc, Brush brush, Point a, Point b, Point c)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(a, true, true);
            ctx.LineTo(b, true, true);
            ctx.LineTo(c, true, true);
        }
        dc.DrawGeometry(brush, null, geometry);
    }

    private static void DrawForegroundDetails(
        DrawingContext dc, double width, double height, double horizon, Palette palette, Random rng, bool isHero)
    {
        var grass = new SolidColorBrush(Color.FromArgb(205, palette.LeafDark.R, palette.LeafDark.G, palette.LeafDark.B));
        var grassLight = new SolidColorBrush(Color.FromArgb(190, palette.Leaf.R, palette.Leaf.G, palette.Leaf.B));
        var tufts = isHero ? 34 : 13;
        for (var i = 0; i < tufts; i++)
        {
            var x = rng.NextDouble() * width;
            var y = horizon + height * (0.10 + rng.NextDouble() * 0.34);
            var blade = height * (0.012 + rng.NextDouble() * 0.018);
            var brush = rng.NextDouble() < 0.5 ? grass : grassLight;
            dc.DrawRectangle(brush, null, new Rect(x, y - blade, Math.Max(1, width * 0.0017), blade));
            dc.DrawRectangle(brush, null, new Rect(x + width * 0.0018, y - blade * 0.7, Math.Max(1, width * 0.0014), blade * 0.7));
            if (rng.NextDouble() < 0.45)
                dc.DrawRectangle(brush, null, new Rect(x - width * 0.0016, y - blade * 0.55, Math.Max(1, width * 0.0013), blade * 0.55));
        }

        var flowers = isHero ? 12 : 5;
        for (var i = 0; i < flowers; i++)
        {
            var x = rng.NextDouble() * width;
            var y = horizon + height * (0.14 + rng.NextDouble() * 0.26);
            var size = Math.Max(1.5, height * 0.007);
            var petal = rng.NextDouble() < 0.5
                ? new SolidColorBrush(Color.FromArgb(225, 0xF6, 0xE6, 0x9A))
                : new SolidColorBrush(Color.FromArgb(225, 0xE8, 0x9C, 0xB0));
            dc.DrawRectangle(grass, null, new Rect(x, y, 1, size * 2.2));
            dc.DrawRectangle(petal, null, new Rect(x - size, y - size * 0.5, size * 2.4, size));
            dc.DrawRectangle(petal, null, new Rect(x - size * 0.2, y - size * 1.4, size * 1.0, size * 2.3));
        }

        if (!isHero) return;
        // 飘落的叶子
        for (var i = 0; i < 22; i++)
        {
            var x = width * (0.40 + rng.NextDouble() * 0.58);
            var y = height * (0.18 + rng.NextDouble() * 0.55);
            var size = 1.4 + rng.NextDouble() * 2.8;
            var color = rng.NextDouble() < 0.55
                ? Color.FromArgb(190, palette.Leaf.R, palette.Leaf.G, palette.Leaf.B)
                : Color.FromArgb(190, palette.Glow.R, palette.Glow.G, palette.Glow.B);
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(x, y, size, size * 0.42));
        }
    }

    private static void DrawMountainLayer(DrawingContext dc, double width, double horizon, double maxHeight, Color color, Random rng, double sharpness)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(-8, horizon), true, true);
            var x = -8d;
            while (x < width + 20)
            {
                var span = width * (0.07 + rng.NextDouble() * 0.10);
                var peak = horizon - maxHeight * (0.35 + rng.NextDouble() * 0.65 * sharpness);
                var saddle = horizon - maxHeight * (0.05 + rng.NextDouble() * 0.22);
                ctx.LineTo(new Point(x + span * 0.50, peak), true, false);
                ctx.LineTo(new Point(x + span, saddle), true, false);
                x += span;
            }
            ctx.LineTo(new Point(width + 20, horizon + 2), true, false);
            ctx.LineTo(new Point(-8, horizon + 2), true, false);
        }
        geometry.Freeze();

        var brush = new LinearGradientBrush(Lighten(color, 0.10), Darken(color, 0.10), 90);
        dc.DrawGeometry(brush, null, geometry);
    }

    private static void DrawGround(DrawingContext dc, double width, double height, double horizon, Palette p, Random rng)
    {
        dc.DrawRectangle(new LinearGradientBrush(p.Grass, p.Soil, 90), null,
            new Rect(0, horizon, width, height - horizon));

        // 草皮顶边：一格一格地起伏
        var grass = new StreamGeometry();
        using (var ctx = grass.Open())
        {
            ctx.BeginFigure(new Point(0, horizon), true, true);
            var step = Math.Max(6, width / 46);
            var x = 0d;
            var toggle = false;
            while (x < width)
            {
                var y = horizon + (toggle ? height * 0.010 : height * 0.003);
                ctx.LineTo(new Point(x + step, y), true, false);
                ctx.LineTo(new Point(x + step, y + height * 0.038), true, false);
                x += step;
                toggle = !toggle;
            }
            ctx.LineTo(new Point(width, horizon + height * 0.045), true, false);
            ctx.LineTo(new Point(0, horizon + height * 0.045), true, false);
        }
        grass.Freeze();
        dc.DrawGeometry(new SolidColorBrush(p.Grass), null, grass);

        // 草皮侧面的深色方块
        var side = new SolidColorBrush(p.GrassDark);
        var step2 = Math.Max(6, width / 46);
        for (var x = 0d; x < width; x += step2)
        {
            if (rng.NextDouble() < 0.55)
                dc.DrawRectangle(side, null, new Rect(x, horizon + height * 0.042, step2 * 0.72, height * 0.012));
        }

        // 泥土层理与碎石
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), null,
            new Rect(0, horizon + height * 0.17, width, height * 0.012));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(28, 255, 235, 200)), null,
            new Rect(0, horizon + height * 0.26, width, height * 0.006));
        for (var i = 0; i < (int)(width / 12); i++)
        {
            var x = rng.NextDouble() * width;
            var y = horizon + height * (0.07 + rng.NextDouble() * 0.30);
            var size = 1.5 + rng.NextDouble() * 3.5;
            var color = rng.NextDouble() < 0.5 ? p.SoilDark : Lighten(p.Soil, 0.10);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(140, color.R, color.G, color.B)), null,
                new Rect(x, y, size, size * 0.8));
        }

        // 地平线阴影，增加纵深
        var edge = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(90, 0, 0, 0), 0));
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
        dc.DrawRectangle(edge, null, new Rect(0, horizon, width, height * 0.05));
    }

    private static void DrawDistantTrees(DrawingContext dc, double width, double height, double horizon, Palette p, Random rng)
    {
        var color = Blend(p.MountainFront, p.LeafDark, 0.55);
        var brush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
        var count = (int)Math.Max(6, width / 55);
        for (var i = 0; i < count; i++)
        {
            var x = rng.NextDouble() * width;
            var treeHeight = height * (0.035 + rng.NextDouble() * 0.045);
            var baseY = horizon - height * 0.002;
            dc.DrawRectangle(brush, null, new Rect(x - 1, baseY - treeHeight * 0.35, 2, treeHeight * 0.35));
            for (var tier = 0; tier < 3; tier++)
            {
                var tierWidth = treeHeight * (0.55 - tier * 0.12);
                var y = baseY - treeHeight * (0.28 + tier * 0.19);
                var points = new[]
                {
                    new Point(x, y - treeHeight * 0.24),
                    new Point(x - tierWidth / 2, y),
                    new Point(x + tierWidth / 2, y),
                };
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(points[0], true, true);
                    ctx.LineTo(points[1], true, false);
                    ctx.LineTo(points[2], true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(brush, null, geo);
            }
        }
    }

    private static void DrawCabin(DrawingContext dc, double width, double height, double horizon, Palette p, Random rng)
    {
        if (width < 120 || height < 220) return;

        var cabinWidth = Math.Max(44, width * (0.17 + rng.NextDouble() * 0.035));
        var cabinHeight = height * (0.17 + rng.NextDouble() * 0.04);
        var centerX = width * (0.53 + rng.NextDouble() * 0.09);
        var baseY = horizon + height * (0.012 + rng.NextDouble() * 0.03);
        var left = centerX - cabinWidth / 2;
        var wallH = cabinHeight * 0.58;
        var wallTop = baseY - wallH;
        var roofTop = baseY - cabinHeight;
        var roofEave = cabinWidth * 0.62;

        var wood = Blend(p.Trunk, p.SoilDark, 0.25);
        var woodLight = Lighten(wood, 0.18);
        var woodDark = Darken(wood, 0.25);
        var roof = Blend(woodDark, p.Soil, 0.35);
        var roofDark = Darken(roof, 0.28);
        var warm = Color.FromRgb(0xFF, 0xC9, 0x72);

        // 木屋周围的暖光
        var glow = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new Point(centerX, baseY - wallH * 0.45),
            GradientOrigin = new Point(centerX, baseY - wallH * 0.45),
            RadiusX = cabinWidth * 0.95,
            RadiusY = cabinHeight * 0.85,
        };
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(80, warm.R, warm.G, warm.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(22, warm.R, warm.G, warm.B), 0.55));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, warm.R, warm.G, warm.B), 1));
        dc.DrawEllipse(glow, null, new Point(centerX, baseY - wallH * 0.45), cabinWidth * 0.95, cabinHeight * 0.85);

        // 木屋主体：横向原木
        dc.DrawRectangle(new SolidColorBrush(woodDark), null, new Rect(left - 2, wallTop - 2, cabinWidth + 4, wallH + 4));
        var logH = wallH / 6.2;
        for (var row = 0; row < 7; row++)
        {
            var y = wallTop + row * logH;
            var brush = new SolidColorBrush(row % 2 == 0 ? wood : woodLight);
            dc.DrawRectangle(brush, null, new Rect(left, y, cabinWidth, Math.Max(1, logH - 1)));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(95, woodDark.R, woodDark.G, woodDark.B)), null,
                new Rect(left, y + logH - 2, cabinWidth, 1.4));
            // 原木端面
            dc.DrawRectangle(new SolidColorBrush(woodDark), null,
                new Rect(left + cabinWidth - 3, y + 1, 3, Math.Max(1, logH - 3)));
        }

        // 石砌地基
        var stone = Blend(p.MountainFront, p.Soil, 0.35);
        for (var i = 0; i < 5; i++)
        {
            var w = cabinWidth / 4.4;
            var x = left + i * w;
            dc.DrawRectangle(new SolidColorBrush(i % 2 == 0 ? stone : Darken(stone, 0.12)), null,
                new Rect(x, baseY - wallH * 0.13, w - 1, wallH * 0.13));
        }

        // 三角木屋顶
        var roofGeo = new StreamGeometry();
        using (var ctx = roofGeo.Open())
        {
            ctx.BeginFigure(new Point(centerX, roofTop), true, true);
            ctx.LineTo(new Point(left - roofEave * 0.12, wallTop + 1), true, false);
            ctx.LineTo(new Point(left + cabinWidth + roofEave * 0.12, wallTop + 1), true, false);
        }
        roofGeo.Freeze();
        dc.DrawGeometry(new SolidColorBrush(roof), new Pen(new SolidColorBrush(roofDark), 1.3), roofGeo);

        // 屋顶木板与屋脊
        dc.DrawLine(new Pen(new SolidColorBrush(roofDark), 1.2), new Point(centerX, roofTop), new Point(centerX, wallTop));
        for (var i = 1; i <= 3; i++)
        {
            var t = i / 4.0;
            var y = roofTop + (wallTop - roofTop) * t;
            var half = (cabinWidth / 2 + roofEave * 0.10) * t;
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, roofDark.R, roofDark.G, roofDark.B)), 1),
                new Point(centerX - half, y), new Point(centerX + half, y));
        }
        dc.DrawRectangle(new SolidColorBrush(roofDark), null,
            new Rect(left - roofEave * 0.15, wallTop - 2, cabinWidth + roofEave * 0.30, 4));

        // 烟囱和烟
        var chimneyX = left + cabinWidth * 0.72;
        var chimneyTop = roofTop + cabinHeight * 0.12;
        dc.DrawRectangle(new SolidColorBrush(Darken(stone, 0.18)), null,
            new Rect(chimneyX, chimneyTop, cabinWidth * 0.10, wallTop - chimneyTop + 3));
        dc.DrawRectangle(new SolidColorBrush(stone), null,
            new Rect(chimneyX - 2, chimneyTop - 3, cabinWidth * 0.10 + 4, 4));
        for (var i = 0; i < 5; i++)
        {
            var smoke = new SolidColorBrush(Color.FromArgb((byte)(55 - i * 8), 210, 210, 200));
            dc.DrawRectangle(smoke, null,
                new Rect(chimneyX + cabinWidth * 0.03 + i * 4, chimneyTop - 12 - i * 7, 3 + i, 3 + i));
        }

        // 门与暖光窗
        var doorW = cabinWidth * 0.20;
        var doorH = wallH * 0.56;
        var doorRect = new Rect(centerX - doorW / 2, baseY - doorH, doorW, doorH);
        dc.DrawRectangle(new SolidColorBrush(woodDark), null, doorRect);
        dc.DrawRectangle(new SolidColorBrush(warm), null,
            new Rect(doorRect.X + doorW * 0.18, doorRect.Y + doorH * 0.10, doorW * 0.64, doorH * 0.30));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(210, 255, 241, 190)), null,
            new Rect(doorRect.X + doorW * 0.28, doorRect.Y + doorH * 0.16, doorW * 0.44, doorH * 0.18));
        dc.DrawRectangle(new SolidColorBrush(wood), null,
            new Rect(doorRect.X, doorRect.Y + doorH * 0.68, doorW, 2));

        for (var side = -1; side <= 1; side += 2)
        {
            var wx = centerX + side * cabinWidth * 0.30;
            var wy = wallTop + wallH * 0.20;
            var ww = cabinWidth * 0.13;
            var wh = wallH * 0.22;
            dc.DrawRectangle(new SolidColorBrush(woodDark), null, new Rect(wx - ww / 2 - 1.5, wy - 1.5, ww + 3, wh + 3));
            dc.DrawRectangle(new SolidColorBrush(warm), null, new Rect(wx - ww / 2, wy, ww, wh));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(210, 255, 244, 196)), null,
                new Rect(wx - ww * 0.28, wy + wh * 0.15, ww * 0.56, wh * 0.50));
            dc.DrawLine(new Pen(new SolidColorBrush(woodDark), 1.2),
                new Point(wx, wy), new Point(wx, wy + wh));
            dc.DrawLine(new Pen(new SolidColorBrush(woodDark), 1.2),
                new Point(wx - ww / 2, wy + wh / 2), new Point(wx + ww / 2, wy + wh / 2));
        }

        // 台阶与墙角木柱
        dc.DrawRectangle(new SolidColorBrush(stone), null,
            new Rect(centerX - doorW * 0.75, baseY, doorW * 1.5, 3));
        dc.DrawRectangle(new SolidColorBrush(woodDark), null,
            new Rect(left - 3, wallTop, 3.5, wallH));
        dc.DrawRectangle(new SolidColorBrush(woodDark), null,
            new Rect(left + cabinWidth - 0.5, wallTop, 3.5, wallH));
    }

    private static void DrawGate(DrawingContext dc, double width, double height, double horizon, Palette p, Random rng)
    {
        var gateWidth = Math.Max(30, width * (0.13 + rng.NextDouble() * 0.04));
        var gateHeight = height * (0.20 + rng.NextDouble() * 0.06);
        var centerX = width * (0.52 + rng.NextDouble() * 0.12);
        var baseY = horizon + height * (0.012 + rng.NextDouble() * 0.03);
        var openingWidth = gateWidth * 0.48;
        var openingHeight = gateHeight * 0.68;

        // 传送门的光
        var glow = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new Point(centerX, baseY - openingHeight * 0.52),
            GradientOrigin = new Point(centerX, baseY - openingHeight * 0.52),
            RadiusX = gateWidth * 1.05,
            RadiusY = gateHeight * 0.88,
        };
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(155, p.Glow.R, p.Glow.G, p.Glow.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(55, p.Glow.R, p.Glow.G, p.Glow.B), 0.5));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, p.Glow.R, p.Glow.G, p.Glow.B), 1));
        dc.DrawEllipse(glow, null, new Point(centerX, baseY - openingHeight * 0.52), gateWidth * 1.05, gateHeight * 0.88);

        // 门洞：发光的能量场 + 竖向光带
        var openingRect = new Rect(centerX - openingWidth / 2, baseY - openingHeight, openingWidth, openingHeight);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x11, 0x1A, 0x10)), null, openingRect);
        dc.PushClip(new RectangleGeometry(openingRect));
        var portal = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new Point(centerX, baseY - openingHeight * 0.48),
            GradientOrigin = new Point(centerX, baseY - openingHeight * 0.48),
            RadiusX = openingWidth * 0.62,
            RadiusY = openingHeight * 0.72,
        };
        portal.GradientStops.Add(new GradientStop(Color.FromArgb(235, p.Glow.R, p.Glow.G, p.Glow.B), 0));
        portal.GradientStops.Add(new GradientStop(Color.FromArgb(150, p.Glow.R, p.Glow.G, p.Glow.B), 0.45));
        portal.GradientStops.Add(new GradientStop(Color.FromArgb(10, 20, 35, 16), 1));
        dc.DrawRectangle(portal, null, openingRect);
        for (var i = 0; i < 7; i++)
        {
            var x = openingRect.Left + openingRect.Width * (0.08 + i * 0.14) + rng.NextDouble() * 2;
            var alpha = (byte)(70 + rng.Next(120));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(alpha, p.Glow.R, p.Glow.G, p.Glow.B)), null,
                new Rect(x, openingRect.Top + rng.NextDouble() * openingRect.Height * 0.25, 1.5,
                    openingRect.Height * (0.40 + rng.NextDouble() * 0.5)));
        }
        dc.Pop();

        // 石块
        var stone = Blend(p.Soil, p.MountainFront, 0.55);
        var stoneLight = Lighten(stone, 0.22);
        var stoneDark = Darken(stone, 0.22);
        var blockH = gateHeight / 9.2;
        var pillarW = gateWidth * 0.24;

        for (var side = -1; side <= 1; side += 2)
        {
            var pillarX = centerX + side * (openingWidth / 2 + pillarW / 2);
            for (var row = 0; row < 8; row++)
            {
                var y = baseY - (row + 1) * blockH;
                var inset = rng.NextDouble() * blockH * 0.12;
                var block = new Rect(pillarX - pillarW / 2, y + inset, pillarW, blockH - inset - 0.8);
                DrawStoneBlock(dc, block, stoneLight, stoneDark, rng);
            }
        }

        var topY = baseY - gateHeight;
        var lintelWidth = openingWidth + pillarW * 2.25;
        var lintelBlock = lintelWidth / 3.2;
        for (var i = 0; i < 3; i++)
        {
            var block = new Rect(centerX - lintelWidth / 2 + i * lintelBlock, topY, lintelBlock - 1, blockH * 1.06);
            DrawStoneBlock(dc, block, stoneLight, stoneDark, rng);
        }

        // 顶部与底座碎块
        for (var i = 0; i < 5; i++)
        {
            var w = pillarW * (0.45 + rng.NextDouble() * 0.55);
            var x = centerX + (rng.NextDouble() - 0.5) * lintelWidth;
            var y = topY - blockH * (0.35 + rng.NextDouble() * 0.55);
            dc.DrawRectangle(new SolidColorBrush(stoneDark), null, new Rect(x, y, w, blockH * 0.42));
        }

        for (var i = 0; i < 4; i++)
        {
            var w = pillarW * (0.7 + rng.NextDouble() * 0.9);
            var x = centerX + (rng.NextDouble() - 0.5) * (openingWidth + pillarW * 2.2);
            var h = blockH * (0.3 + rng.NextDouble() * 0.3);
            dc.DrawRectangle(new SolidColorBrush(stone), null, new Rect(x, baseY - h, w, h));
        }

        // 门上青苔
        var moss = new SolidColorBrush(Color.FromArgb(190, p.LeafDark.R, p.LeafDark.G, p.LeafDark.B));
        for (var i = 0; i < 7; i++)
        {
            var x = centerX + (rng.NextDouble() - 0.5) * gateWidth * 1.1;
            var y = baseY - rng.NextDouble() * gateHeight * 0.33;
            dc.DrawRectangle(moss, null, new Rect(x, y, 2 + rng.NextDouble() * 4, 1.5 + rng.NextDouble() * 2));
        }
    }

    private static void DrawStoneBlock(DrawingContext dc, Rect rect, Color light, Color dark, Random rng)
    {
        var baseColor = Blend(light, dark, rng.NextDouble() * 0.35);
        dc.DrawRectangle(new SolidColorBrush(baseColor), null, rect);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(70, light.R, light.G, light.B)), null,
            new Rect(rect.Left, rect.Top, rect.Width, Math.Max(1, rect.Height * 0.12)));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(90, dark.R, dark.G, dark.B)), null,
            new Rect(rect.Right - Math.Max(1.4, rect.Width * 0.10), rect.Top, Math.Max(1.4, rect.Width * 0.10), rect.Height));
        if (rng.NextDouble() < 0.45)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(60, dark.R, dark.G, dark.B)), null,
                new Rect(rect.Left + rect.Width * 0.18, rect.Top + rect.Height * 0.4, rect.Width * 0.18, rect.Height * 0.16));
        }
    }

    private static void DrawTree(DrawingContext dc, double x, double baseY, double treeHeight, double treeWidth, Palette p, Random rng, bool night)
    {
        // 树影
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null,
            new Point(x, baseY), treeWidth * 0.55, Math.Max(1.5, treeHeight * 0.035));

        var trunkWidth = Math.Max(2.5, treeWidth * 0.13);
        var trunkHeight = treeHeight * 0.55;
        var trunkRect = new Rect(x - trunkWidth / 2, baseY - trunkHeight, trunkWidth, trunkHeight);
        dc.DrawRectangle(new SolidColorBrush(p.Trunk), null, trunkRect);
        dc.DrawRectangle(new SolidColorBrush(Darken(p.Trunk, 0.18)), null,
            new Rect(trunkRect.Right - trunkWidth * 0.32, trunkRect.Top, trunkWidth * 0.32, trunkRect.Height));

        var broadleaf = rng.NextDouble() < 0.55;
        if (broadleaf)
        {
            var crownY = baseY - treeHeight * 0.58;
            var blocks = new (double dx, double dy, double w, double h)[]
            {
                (-0.50, 0.00, 0.48, 0.20),
                (0.02, 0.00, 0.50, 0.20),
                (-0.34, -0.16, 0.46, 0.20),
                (0.12, -0.16, 0.38, 0.18),
                (-0.20, -0.32, 0.44, 0.19),
                (0.05, -0.43, 0.30, 0.15),
            };
            foreach (var block in blocks)
            {
                var leafRect = new Rect(
                    x + block.dx * treeWidth,
                    crownY + block.dy * treeHeight,
                    block.w * treeWidth,
                    block.h * treeHeight);
                dc.DrawRectangle(new SolidColorBrush(p.Leaf), null, leafRect);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(115, p.LeafDark.R, p.LeafDark.G, p.LeafDark.B)), null,
                    new Rect(leafRect.Left, leafRect.Top + leafRect.Height * 0.55, leafRect.Width, leafRect.Height * 0.45));
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(55, 255, 255, 220)), null,
                    new Rect(leafRect.Left, leafRect.Top, leafRect.Width, Math.Max(1, leafRect.Height * 0.12)));
            }
        }
        else
        {
            // 针叶树：层层收窄的方块树冠
            for (var tier = 0; tier < 4; tier++)
            {
                var tierWidth = treeWidth * (1.0 - tier * 0.18);
                var tierHeight = treeHeight * 0.16;
                var y = baseY - treeHeight * 0.40 - tier * treeHeight * 0.13;
                var left = x - tierWidth / 2;
                dc.DrawRectangle(new SolidColorBrush(p.LeafDark), null, new Rect(left, y, tierWidth, tierHeight));
                dc.DrawRectangle(new SolidColorBrush(p.Leaf), null, new Rect(left + tierWidth * 0.12, y, tierWidth * 0.52, tierHeight * 0.85));
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(80, 255, 255, 220)), null,
                    new Rect(left + tierWidth * 0.12, y, tierWidth * 0.52, 1));
            }
        }

        if (night)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(55, 180, 220, 255)), null,
                new Rect(trunkRect.Left, trunkRect.Top, trunkRect.Width * 0.4, trunkRect.Height * 0.55));
        }
    }

    private static void DrawCube(DrawingContext dc, double cx, double cy, double size, Palette p)
    {
        var top = new Point(cx, cy - size);
        var left = new Point(cx - size, cy - size * 0.45);
        var bottom = new Point(cx, cy + size * 0.12);
        var right = new Point(cx + size, cy - size * 0.45);
        var center = new Point(cx, cy - size * 0.28);

        var topGeo = Polygon(top, right, center, left);
        var leftGeo = Polygon(left, center, bottom, new Point(cx - size, cy + size * 0.55));
        var rightGeo = Polygon(right, center, bottom, new Point(cx + size, cy + size * 0.55));

        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(235, Lighten(p.Grass, 0.28).R, Lighten(p.Grass, 0.28).G, Lighten(p.Grass, 0.28).B)), null, topGeo);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(235, p.GrassDark.R, p.GrassDark.G, p.GrassDark.B)), null, leftGeo);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(235, Darken(p.GrassDark, 0.22).R, Darken(p.GrassDark, 0.22).G, Darken(p.GrassDark, 0.22).B)), null, rightGeo);
    }

    private static Geometry Polygon(params Point[] points)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(points[0], true, true);
            for (var i = 1; i < points.Length; i++) ctx.LineTo(points[i], true, false);
        }
        geo.Freeze();
        return geo;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= 16777619u;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static Color Lighten(Color color, double amount)
        => Color.FromArgb(color.A,
            (byte)Math.Clamp(color.R + (255 - color.R) * amount, 0, 255),
            (byte)Math.Clamp(color.G + (255 - color.G) * amount, 0, 255),
            (byte)Math.Clamp(color.B + (255 - color.B) * amount, 0, 255));

    private static Color Darken(Color color, double amount)
        => Color.FromArgb(color.A,
            (byte)Math.Clamp(color.R * (1 - amount), 0, 255),
            (byte)Math.Clamp(color.G * (1 - amount), 0, 255),
            (byte)Math.Clamp(color.B * (1 - amount), 0, 255));

    private static Color Blend(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * amount),
            (byte)(a.R + (b.R - a.R) * amount),
            (byte)(a.G + (b.G - a.G) * amount),
            (byte)(a.B + (b.B - a.B) * amount));
    }
}
