using System.IO;
using System.Windows.Media;
using GameLibrary.Models;

namespace GameLibrary.Services;

public enum BackgroundMediaKind
{
    None,
    Image,
    Video,
}

public sealed record BackgroundSource(
    BackgroundMediaKind Kind,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> Videos,
    string DisplayMode)
{
    public bool HasMedia => Kind != BackgroundMediaKind.None;
}

public static class BackgroundLibrary
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif",
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".wmv", ".avi", ".mov", ".m4v",
    };

    public static BackgroundSource Resolve(BackgroundSettings settings)
    {
        var images = Enumerate(settings.ImageDirectory, ImageExtensions);
        var videos = Enumerate(settings.VideoDirectory, VideoExtensions);

        if (videos.Count > 0)
            return new BackgroundSource(BackgroundMediaKind.Video, images, videos, settings.DisplayMode);
        if (images.Count > 0)
            return new BackgroundSource(BackgroundMediaKind.Image, images, videos, settings.DisplayMode);
        return new BackgroundSource(BackgroundMediaKind.None, Array.Empty<string>(), Array.Empty<string>(), settings.DisplayMode);
    }

    public static Stretch ToStretch(string displayMode) => displayMode switch
    {
        "适应" => Stretch.Uniform,
        "拉伸" => Stretch.Fill,
        _ => Stretch.UniformToFill,
    };

    public static bool IsTile(string displayMode) => displayMode == "平铺";

    private static IReadOnlyList<string> Enumerate(string directory, HashSet<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(directory)) return Array.Empty<string>();
        try
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            return Directory.EnumerateFiles(directory)
                .Where(path => extensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
