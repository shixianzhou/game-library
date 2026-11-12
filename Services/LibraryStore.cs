using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GameLibrary.Models;

namespace GameLibrary.Services;

public sealed class LibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string DataDirectory => AppPaths.DataDirectory;
    public string ConfigPath => AppPaths.ConfigPath;
    public string CoversDirectory => AppPaths.CoversDirectory;

    public LibraryData Load(out string? warning)
    {
        warning = null;
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(CoversDirectory);

        if (!File.Exists(ConfigPath)) return new LibraryData();

        try
        {
            var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<LibraryData>(json, JsonOptions) ?? new LibraryData();
            Normalize(data);
            AttachCoverPaths(data);
            return data;
        }
        catch (Exception ex)
        {
            warning = "读取游戏库配置失败，已备份原文件并新建空库：" + ex.Message;
            try
            {
                var backup = ConfigPath + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(ConfigPath, backup, true);
            }
            catch
            {
                // 备份失败不影响启动。
            }
            return new LibraryData();
        }
    }

    public void Save(LibraryData data)
    {
        Directory.CreateDirectory(DataDirectory);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Move(temp, ConfigPath, true);
    }

    public string ImportCover(Guid gameId, string sourcePath)
    {
        Directory.CreateDirectory(CoversDirectory);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension)) extension = ".png";
        var fileName = $"{gameId:N}_{DateTime.Now:yyyyMMddHHmmssfff}{extension}";
        var destination = Path.Combine(CoversDirectory, fileName);
        File.Copy(sourcePath, destination, true);
        return fileName;
    }

    public void DeleteCover(string? coverFile)
    {
        if (string.IsNullOrWhiteSpace(coverFile)) return;
        try
        {
            var path = Path.Combine(CoversDirectory, Path.GetFileName(coverFile));
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 孤立封面不影响使用，静默忽略。
        }
    }

    public string? ResolveCover(string? coverFile)
    {
        if (string.IsNullOrWhiteSpace(coverFile)) return null;
        try
        {
            var path = Path.IsPathRooted(coverFile)
                ? coverFile
                : Path.Combine(CoversDirectory, Path.GetFileName(coverFile));
            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    private void AttachCoverPaths(LibraryData data)
    {
        foreach (var game in data.Games)
        {
            game.CoverPathResolved = ResolveCover(game.CoverFile);
        }
    }

    private static void Normalize(LibraryData data)
    {
        data.Games ??= new List<GameEntry>();
        data.Window ??= new WindowSettings();
        data.Settings ??= new AppSettings();
        data.Settings.Background ??= new BackgroundSettings();
        if (data.Settings.Background.DisplayMode is not ("填充" or "适应" or "拉伸" or "平铺"))
            data.Settings.Background.DisplayMode = "填充";
        data.Version = 1;

        var seen = new HashSet<Guid>();
        var fallbackDate = DateTimeOffset.Now.AddMinutes(-data.Games.Count);
        var normalized = new List<GameEntry>(data.Games.Count);
        foreach (var game in data.Games)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.Name)) continue;
            if (game.Id == Guid.Empty || !seen.Add(game.Id))
            {
                game.Id = Guid.NewGuid();
                seen.Add(game.Id);
            }
            if (game.AddedAt == default) game.AddedAt = fallbackDate;
            fallbackDate = fallbackDate.AddMinutes(1);
            game.Name = game.Name.Trim();
            game.Description ??= "";
            game.Author ??= "";
            game.Version ??= "";
            game.GameDirectory ??= "";
            game.ClientDirectory ??= "";
            game.ServerDirectory ??= "";
            game.GameExecutable ??= "";
            game.ClientExecutable ??= "";
            game.ServerExecutable ??= "";
            game.CoverFile ??= "";
            normalized.Add(game);
        }
        data.Games = normalized;

        // 兼容旧数据：置顶项必须带有效顺序；多个置顶按原列表顺序补序号。
        long pinSeed = DateTimeOffset.UtcNow.Ticks;
        for (int i = 0; i < normalized.Count; i++)
        {
            var game = normalized[i];
            if (!game.Pinned)
            {
                game.PinOrder = 0;
                continue;
            }
            if (game.PinOrder <= 0)
            {
                game.PinOrder = pinSeed - i;
            }
        }
    }
}
