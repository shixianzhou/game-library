using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using GameLibrary.Models;

namespace GameLibrary.Services;

public sealed record LaunchResult(bool Success, string? Executable, string Message);

public static class GameLauncher
{
    public static LaunchResult Start(GameEntry game, bool server)
    {
        var directory = game.DirectoryFor(server);
        if (string.IsNullOrWhiteSpace(directory))
            return new(false, null, server ? "还没有设置服务端目录。" : "还没有设置游戏目录。");

        if (!Directory.Exists(directory))
            return new(false, null, $"目录不存在：\n{directory}\n\n请检查游戏是否被移动，或编辑该游戏重新选择目录。");

        var configured = game.ExecutableFor(server);
        string? executable;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            executable = ExecutableLocator.ResolveConfigured(directory, configured);
            if (executable == null)
                return new(false, null, $"指定的启动程序不存在：\n{configured}\n\n请编辑该游戏重新指定启动程序。");
        }
        else
        {
            executable = ExecutableLocator.FindBest(directory, game.Name, server);
            if (executable == null)
                return new(false, null,
                    $"没有在目录中找到可启动的 .exe / .bat / .cmd 文件：\n{directory}\n\n" +
                    "请编辑该游戏，在“启动程序”中手动选择。");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? directory,
                UseShellExecute = true,
            };

            // 参数只作用于客户端；服务端启动保持干净的命令行。
            var arguments = server ? "" : game.Arguments;
            if (!string.IsNullOrWhiteSpace(arguments)) startInfo.Arguments = arguments.Trim();

            Process.Start(startInfo);
            var action = server ? "正在启动后台服务" : "正在启动游戏";
            return new(true, executable, $"{action} · {Path.GetFileName(executable)}");
        }
        catch (Win32Exception ex)
        {
            return new(false, executable, Describe(ex));
        }
        catch (Exception ex)
        {
            return new(false, executable, "启动失败：" + ex.Message);
        }
    }

    private static string Describe(Win32Exception ex) => ex.NativeErrorCode switch
    {
        2 => "找不到启动程序。",
        5 => "拒绝访问，可能需要管理员权限。",
        740 => "该程序需要管理员权限运行。",
        1223 => "已取消管理员授权。",
        193 => "这不是有效的 Windows 应用程序。",
        _ => "启动失败：" + ex.Message,
    };
}
