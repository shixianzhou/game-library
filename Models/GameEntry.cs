using System.Text.Json.Serialization;

namespace GameLibrary.Models;

/// <summary>一款加入本地库的游戏。客户端 / 服务端分离时分别保存两个目录。</summary>
public sealed class GameEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "";

    /// <summary>不分离时使用的游戏目录。</summary>
    public string GameDirectory { get; set; } = "";

    public bool IsSeparated { get; set; }

    /// <summary>分离模式下的客户端目录。</summary>
    public string ClientDirectory { get; set; } = "";

    /// <summary>分离模式下的服务端目录。</summary>
    public string ServerDirectory { get; set; } = "";

    /// <summary>手动指定的启动程序（绝对路径，留空则自动识别）。</summary>
    public string GameExecutable { get; set; } = "";

    public string ClientExecutable { get; set; } = "";
    public string ServerExecutable { get; set; } = "";

    /// <summary>
    /// 启动客户端时附加的命令行参数，原样拼接到启动程序之后。
    /// 含空格的单个参数需要用英文双引号包起来；服务端启动不使用它。
    /// </summary>
    public string Arguments { get; set; } = "";

    /// <summary>封面在数据目录 covers 文件夹中的文件名。</summary>
    public string CoverFile { get; set; } = "";

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastPlayedAt { get; set; }

    /// <summary>在“全部游戏”中置顶；可同时置顶多张，PinOrder 越大越靠前。</summary>
    public bool Pinned { get; set; }

    /// <summary>置顶顺序；未置顶为 0，最近置顶的数值最大。</summary>
    public long PinOrder { get; set; }

    [JsonIgnore]
    public string? CoverPathResolved { get; set; }

    [JsonIgnore]
    public string PrimaryDirectory => IsSeparated ? ClientDirectory : GameDirectory;

    [JsonIgnore]
    public string DescriptionText => string.IsNullOrWhiteSpace(Description)
        ? "在本地收藏这款游戏，随时一键启动。"
        : Description.Trim();

    [JsonIgnore]
    public IReadOnlyList<string> Tags
    {
        get
        {
            var tags = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(Author)) tags.Add(Author.Trim());
            if (!string.IsNullOrWhiteSpace(Version))
            {
                var version = Version.Trim();
                tags.Add(version.StartsWith('v') || version.StartsWith('V') ? version : "v" + version);
            }
            tags.Add(IsSeparated ? "客户端 + 服务端" : "本地游戏");
            return tags.Take(3).ToList();
        }
    }

    [JsonIgnore]
    public string LastPlayedLabel
    {
        get
        {
            if (LastPlayedAt is not { } played) return "尚未游玩";
            var local = played.ToLocalTime();
            var today = DateTimeOffset.Now.Date;
            var date = local.Date;
            if (date == today) return "今天 " + local.ToString("HH:mm");
            if (date == today.AddDays(-1)) return "昨天 " + local.ToString("HH:mm");
            if (local.Year == DateTimeOffset.Now.Year) return local.ToString("M月d日 HH:mm");
            return local.ToString("yyyy年M月d日");
        }
    }

    public string DirectoryFor(bool server)
    {
        if (server) return ServerDirectory;
        return IsSeparated ? ClientDirectory : GameDirectory;
    }

    public string ExecutableFor(bool server)
    {
        if (server) return ServerExecutable;
        return IsSeparated ? ClientExecutable : GameExecutable;
    }

    public GameEntry Clone() => (GameEntry)MemberwiseClone();
}
