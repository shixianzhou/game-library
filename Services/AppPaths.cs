using System.IO;

namespace GameLibrary.Services;

/// <summary>
/// 数据默认跟着 exe 走，方便整个文件夹拷走；exe 所在目录不可写时回退到本机应用数据。
/// 设置 GAME_LIBRARY_HOME 可以覆盖数据目录（开发调试、截图演示用）。
/// </summary>
public static class AppPaths
{
    public static string DataDirectory { get; } = ResolveDataDirectory();

    public static string ConfigPath => Path.Combine(DataDirectory, "library.json");

    public static string CoversDirectory => Path.Combine(DataDirectory, "covers");

    private static string ResolveDataDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable("GAME_LIBRARY_HOME");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            var dir = Path.GetFullPath(overridden.Trim().Trim('"'));
            Directory.CreateDirectory(dir);
            return dir;
        }

        var exeDir = AppContext.BaseDirectory;
        try
        {
            var probe = Path.Combine(exeDir, ".write-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return exeDir;
        }
        catch
        {
            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PixelGameLibrary");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
