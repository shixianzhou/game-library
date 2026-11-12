using System.IO;
using System.Windows;
using System.Windows.Threading;
using GameLibrary.Services;

namespace GameLibrary;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        LogCrash(e.Exception);
        MessageBox.Show(
            "程序遇到一个未处理的问题：\n\n" + e.Exception.Message +
            "\n\n详细信息已写入 crash.log。",
            "游戏库",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void LogCrash(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var path = Path.Combine(AppPaths.DataDirectory, "crash.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 日志失败时不再抛出新异常。
        }
    }
}
