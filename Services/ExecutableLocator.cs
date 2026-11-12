using System.IO;
using System.Text;

namespace GameLibrary.Services;

public sealed record ExecutableCandidate(string FullPath, string FileName, string RelativePath, int Score);

/// <summary>
/// 根据游戏目录猜测最像“启动程序”的文件。只扫描游戏目录下浅层目录，避开运行库和卸载器。
/// 用户可以在添加游戏时手动指定某个候选文件。
/// </summary>
public static class ExecutableLocator
{
    private static readonly HashSet<string> LaunchExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".lnk",
    };

    private static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "redist", "_commonredist", "directx", "dotnet", "jre", "runtime", "node_modules",
        "tools", "support", "installer", "uninstall", "__installer", "steamworks", "backup",
        ".git", ".vs", "cache", "logs", "crashdumps",
    };

    private static readonly string[] SkipNameFragments =
    {
        "unins", "uninstall", "setup", "installer", "update", "updater", "patcher", "crash",
        "report", "dxsetup", "vcredist", "vc_redist", "directx", "notification", "helper",
        "register", "repair", "diagnostic", "benchmark",
    };

    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "java", "javaw", "node", "python", "pythonw", "cmd", "powershell", "pwsh",
        "conhost", "7z", "winrar", "steam", "steamwebhelper", "msedge", "chrome",
    };

    public static IReadOnlyList<ExecutableCandidate> FindCandidates(string directory, string gameName, bool server)
    {
        if (string.IsNullOrWhiteSpace(directory)) return Array.Empty<ExecutableCandidate>();

        try
        {
            if (!Directory.Exists(directory)) return Array.Empty<ExecutableCandidate>();
        }
        catch
        {
            return Array.Empty<ExecutableCandidate>();
        }

        var candidates = new List<ExecutableCandidate>();
        var scanned = 0;
        Scan(directory, directory, 0, 2, gameName, server, candidates, ref scanned);
        return candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.RelativePath.Length)
            .ThenBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();
    }

    public static string? FindBest(string directory, string gameName, bool server)
        => FindCandidates(directory, gameName, server).FirstOrDefault()?.FullPath;

    /// <summary>校验用户在对话框里手动指定的启动程序，并把相对路径补全。</summary>
    public static string? ResolveConfigured(string directory, string configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return null;
        try
        {
            var value = configured.Trim().Trim('"');
            var path = Path.IsPathRooted(value)
                ? value
                : Path.Combine(directory, value);
            path = Path.GetFullPath(path);
            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    private static void Scan(
        string root,
        string directory,
        int depth,
        int maxDepth,
        string gameName,
        bool server,
        List<ExecutableCandidate> results,
        ref int scanned)
    {
        if (scanned > 4000) return;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            if (++scanned > 4000) return;
            var extension = Path.GetExtension(file);
            if (!LaunchExtensions.Contains(extension)) continue;
            var fileName = Path.GetFileName(file);
            if (IsSkipped(fileName)) continue;

            var relative = Path.GetRelativePath(root, file);
            results.Add(new ExecutableCandidate(
                file,
                fileName,
                relative,
                ScoreCandidate(fileName, relative, depth, gameName, server)));
        }

        if (depth >= maxDepth) return;

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directory);
        }
        catch
        {
            return;
        }

        foreach (var child in directories)
        {
            if (SkipDirectories.Contains(Path.GetFileName(child))) continue;
            Scan(root, child, depth + 1, maxDepth, gameName, server, results, ref scanned);
        }
    }

    private static bool IsSkipped(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (SkipNames.Contains(stem)) return true;
        var lower = stem.ToLowerInvariant();
        return SkipNameFragments.Any(fragment => lower.Contains(fragment, StringComparison.Ordinal));
    }

    private static int ScoreCandidate(string fileName, string relative, int depth, string gameName, bool server)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var lower = stem.ToLowerInvariant();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var normalizedGame = NormalizeName(gameName);
        var normalizedStem = NormalizeName(stem);

        var score = 0;
        if (normalizedGame.Length >= 2)
        {
            if (normalizedStem == normalizedGame) score += 160;
            else if (normalizedStem.Contains(normalizedGame, StringComparison.Ordinal)) score += 100;
            else if (normalizedGame.Contains(normalizedStem, StringComparison.Ordinal) && normalizedStem.Length >= 3) score += 40;
        }

        score += extension switch
        {
            ".exe" => 50,
            ".bat" or ".cmd" => 36,
            ".lnk" => 8,
            _ => 0,
        };

        if (depth == 0) score += 28;
        else score += Math.Max(0, 12 - depth * 4);

        if (lower is "start" or "launch" or "play" or "run" or "startserver" or "start_server"
            or "runserver" or "run_server" or "启动" or "启动游戏" or "启动服务器")
        {
            score += 46;
        }

        if (lower.Contains("launcher")) score += 22;
        if (lower.Contains("game") || lower.Contains("play")) score += 20;
        if (lower.Contains("client")) score += server ? -90 : 38;
        if (lower.Contains("server")) score += server ? 54 : -90;
        if (server && lower.Contains("backend")) score += 36;

        if (relative.Contains(Path.DirectorySeparatorChar) && depth > 0) score -= 5;
        return score;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) builder.Append(ch);
        }
        return builder.ToString();
    }
}
