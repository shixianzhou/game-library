游戏库 —— Windows 本地游戏收藏与一键启动工具
项目主页：https://github.com/shixianzhou/game-library

本包是「依赖版」（框架依赖）：体积小，但需要先安装 .NET 10 Desktop Runtime。

安装运行时
1. 打开 https://dotnet.microsoft.com/download/dotnet/10.0
2. 下载「.NET Desktop Runtime 10.0.x」的 Windows x64 安装包并安装。
3. 如果不确定是否已安装，可以先直接双击 游戏库.exe，系统会提示缺少运行时。

使用方法
1. 把 游戏库.exe 放到一个可写的文件夹里，例如 D:\游戏库\。
2. 双击运行，首次启动会请求管理员权限（UAC），选择「是」。
3. 数据保存在 exe 同目录的 library.json 和 covers\ 中；
   如果 exe 所在目录不可写（例如 Program Files），会自动改用
   %LocalAppData%\PixelGameLibrary\。
4. 也可以用环境变量 GAME_LIBRARY_HOME 指定数据目录。

注意
- 程序没有代码签名，Windows SmartScreen 可能提示「已保护你的电脑」，
  选择「更多信息 → 仍要运行」即可。
- 程序以管理员权限启动是为了能启动需要管理员权限的游戏或后台服务。
