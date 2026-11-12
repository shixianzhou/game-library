using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GameLibrary.Models;
using GameLibrary.Services;
using Microsoft.Win32;

namespace GameLibrary;

public partial class AddGameDialog : Window
{
    private readonly LibraryStore _store;
    private readonly GameEntry? _original;
    private readonly GameEntry _draft;

    private string? _previewCoverPath;
    private string? _newCoverSource;
    private bool _coverRemoved;
    private bool _ready;

    public AddGameDialog(LibraryStore store, GameEntry? original)
    {
        _store = store;
        _original = original?.Clone();
        _draft = original?.Clone() ?? new GameEntry
        {
            Id = Guid.NewGuid(),
            AddedAt = DateTimeOffset.Now,
        };

        InitializeComponent();
        Width = Math.Min(920, Math.Max(760, SystemParameters.WorkArea.Width - 40));
        Height = Math.Min(780, Math.Max(600, SystemParameters.WorkArea.Height - 30));

        var editing = original != null;
        Title = editing ? "编辑游戏" : "添加游戏";
        DialogTitle.Text = editing ? "编辑游戏" : "添加游戏";
        DialogSubtitle.Text = editing
            ? "调整后会立即更新游戏库中的这条记录。"
            : "把本地游戏收进库里。目录可以现在选，也可以稍后编辑。";
        ModeBadge.Text = editing ? "编辑" : "新游戏";
        SubmitButton.Content = editing ? "保存修改" : "添加游戏";

        NameBox.Text = _draft.Name;
        DescriptionBox.Text = _draft.Description;
        AuthorBox.Text = _draft.Author;
        VersionBox.Text = _draft.Version;
        SingleDirectoryBox.Text = _draft.GameDirectory;
        SingleExecutableBox.Text = _draft.GameExecutable;
        ClientDirectoryBox.Text = _draft.ClientDirectory;
        ClientExecutableBox.Text = _draft.ClientExecutable;
        ServerDirectoryBox.Text = _draft.ServerDirectory;
        ServerExecutableBox.Text = _draft.ServerExecutable;
        ArgumentsBox.Text = _draft.Arguments;

        _previewCoverPath = _draft.CoverPathResolved;
        CoverPreview.GameName = string.IsNullOrWhiteSpace(_draft.Name) ? "新游戏" : _draft.Name;
        CoverPreview.SeedText = _draft.Id.ToString();
        CoverPreview.ImagePath = _previewCoverPath;
        RemoveCoverButton.Visibility = _previewCoverPath == null ? Visibility.Collapsed : Visibility.Visible;

        SeparatedToggle.IsChecked = _draft.IsSeparated;
        _ready = true;
        UpdateSeparatedState();
    }

    public GameEntry? Result { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        if (CoverPreview == null) return;
        CoverPreview.GameName = string.IsNullOrWhiteSpace(NameBox.Text) ? "新游戏" : NameBox.Text.Trim();
    }

    private void OnSeparatedChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        UpdateSeparatedState();
    }

    private void UpdateSeparatedState()
    {
        var separated = SeparatedToggle.IsChecked == true;
        SinglePanel.Visibility = separated ? Visibility.Collapsed : Visibility.Visible;
        SeparatedPanel.Visibility = separated ? Visibility.Visible : Visibility.Collapsed;
        SeparatedHint.Text = separated
            ? "客户端和服务端存放在两个目录，主界面和卡片会分别提供启动入口。"
            : "客户端和服务端在同一个目录，启动时只需要一个程序。";
        ClearError();
    }

    // =====================================================================
    // 选择目录 / 程序
    // =====================================================================

    private void OnBrowseSingleDirectory(object sender, RoutedEventArgs e)
        => BrowseDirectory(SingleDirectoryBox);

    private void OnBrowseClientDirectory(object sender, RoutedEventArgs e)
        => BrowseDirectory(ClientDirectoryBox);

    private void OnBrowseServerDirectory(object sender, RoutedEventArgs e)
        => BrowseDirectory(ServerDirectoryBox);

    private void BrowseDirectory(TextBox target)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择游戏目录",
            Multiselect = false,
            InitialDirectory = Directory.Exists(target.Text) ? target.Text : "",
        };
        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FolderName;
            target.CaretIndex = target.Text.Length;
            ClearError();
        }
    }

    private void OnBrowseSingleExecutable(object sender, RoutedEventArgs e)
        => BrowseExecutable(SingleExecutableBox, SingleDirectoryBox);

    private void OnBrowseClientExecutable(object sender, RoutedEventArgs e)
        => BrowseExecutable(ClientExecutableBox, ClientDirectoryBox);

    private void OnBrowseServerExecutable(object sender, RoutedEventArgs e)
        => BrowseExecutable(ServerExecutableBox, ServerDirectoryBox);

    private void BrowseExecutable(TextBox target, TextBox directory)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择启动程序",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "可启动文件|*.exe;*.bat;*.cmd;*.lnk|所有文件|*.*",
            InitialDirectory = Directory.Exists(directory.Text) ? directory.Text : "",
            FileName = File.Exists(target.Text) ? Path.GetFileName(target.Text) : "",
        };
        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FileName;
            target.CaretIndex = target.Text.Length;
            ClearError();
        }
    }

    private void OnClearSingleExecutable(object sender, RoutedEventArgs e)
    {
        SingleExecutableBox.Text = "";
        SingleExecutableBox.Focus();
    }

    // =====================================================================
    // 封面
    // =====================================================================

    private void OnChooseCoverClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择封面图片",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;

        _newCoverSource = dialog.FileName;
        _coverRemoved = false;
        _previewCoverPath = dialog.FileName;
        CoverPreview.ImagePath = _previewCoverPath;
        RemoveCoverButton.Visibility = Visibility.Visible;
        ClearError();
    }

    private void OnRemoveCoverClick(object sender, RoutedEventArgs e)
    {
        _newCoverSource = null;
        _coverRemoved = true;
        _previewCoverPath = null;
        CoverPreview.ImagePath = null;
        RemoveCoverButton.Visibility = Visibility.Collapsed;
    }

    // =====================================================================
    // 提交
    // =====================================================================

    private void OnSubmitClick(object sender, RoutedEventArgs e)
    {
        if (!TryBuild(out var result, out var error))
        {
            ShowError(error);
            return;
        }

        try
        {
            if (_newCoverSource != null)
            {
                var fileName = _store.ImportCover(result.Id, _newCoverSource);
                if (!string.IsNullOrWhiteSpace(_original?.CoverFile) &&
                    !string.Equals(_original.CoverFile, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    _store.DeleteCover(_original.CoverFile);
                }
                result.CoverFile = fileName;
            }
            else if (_coverRemoved)
            {
                _store.DeleteCover(_original?.CoverFile);
                result.CoverFile = "";
            }
            else if (_original != null)
            {
                result.CoverFile = _original.CoverFile;
            }

            result.CoverPathResolved = _store.ResolveCover(result.CoverFile);
        }
        catch (Exception ex)
        {
            ShowError("保存封面失败：" + ex.Message);
            return;
        }

        Result = result;
        DialogResult = true;
    }

    private bool TryBuild(out GameEntry result, out string error)
    {
        result = _original?.Clone() ?? new GameEntry();
        result.Id = _draft.Id;
        result.AddedAt = _draft.AddedAt;
        result.LastPlayedAt = _draft.LastPlayedAt;

        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "给游戏起个名字吧，游戏名称不能为空。";
            NameBox.Focus();
            return false;
        }

        var separated = SeparatedToggle.IsChecked == true;
        string directory;
        if (separated)
        {
            var client = ClientDirectoryBox.Text.Trim();
            var server = ServerDirectoryBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(client))
            {
                error = "请选择客户端目录。";
                ClientDirectoryBox.Focus();
                return false;
            }
            if (!Directory.Exists(client))
            {
                error = "客户端目录不存在：" + client;
                ClientDirectoryBox.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(server))
            {
                error = "请选择服务端目录。";
                ServerDirectoryBox.Focus();
                return false;
            }
            if (!Directory.Exists(server))
            {
                error = "服务端目录不存在：" + server;
                ServerDirectoryBox.Focus();
                return false;
            }

            result.ClientDirectory = client;
            result.ServerDirectory = server;
            result.ClientExecutable = ClientExecutableBox.Text.Trim();
            result.ServerExecutable = ServerExecutableBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(result.ClientExecutable) &&
                ExecutableLocator.ResolveConfigured(client, result.ClientExecutable) == null)
            {
                error = "客户端启动程序不存在：" + result.ClientExecutable;
                ClientExecutableBox.Focus();
                return false;
            }
            if (!string.IsNullOrWhiteSpace(result.ServerExecutable) &&
                ExecutableLocator.ResolveConfigured(server, result.ServerExecutable) == null)
            {
                error = "服务端启动程序不存在：" + result.ServerExecutable;
                ServerExecutableBox.Focus();
                return false;
            }

            result.GameDirectory = "";
            result.GameExecutable = "";
            directory = client;
        }
        else
        {
            directory = SingleDirectoryBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(directory))
            {
                error = "请选择游戏目录。";
                SingleDirectoryBox.Focus();
                return false;
            }
            if (!Directory.Exists(directory))
            {
                error = "游戏目录不存在：" + directory;
                SingleDirectoryBox.Focus();
                return false;
            }

            result.GameDirectory = directory;
            result.GameExecutable = SingleExecutableBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(result.GameExecutable) &&
                ExecutableLocator.ResolveConfigured(directory, result.GameExecutable) == null)
            {
                error = "启动程序不存在：" + result.GameExecutable;
                SingleExecutableBox.Focus();
                return false;
            }

            result.ClientDirectory = "";
            result.ServerDirectory = "";
            result.ClientExecutable = "";
            result.ServerExecutable = "";
        }

        result.IsSeparated = separated;
        result.Name = name;
        result.Arguments = ArgumentsBox.Text.Trim();
        result.Description = DescriptionBox.Text.Trim();
        result.Author = AuthorBox.Text.Trim();
        result.Version = VersionBox.Text.Trim();
        error = "";
        return true;
    }

    private void ShowError(string message)
    {
        ErrorGlyph.Visibility = Visibility.Visible;
        ErrorText.Text = message;
    }

    private void ClearError()
    {
        if (ErrorText == null) return;
        ErrorText.Text = "";
        ErrorGlyph.Visibility = Visibility.Collapsed;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsInsideButton(e.OriginalSource as DependencyObject)) return;
        try { DragMove(); } catch { }
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        var guard = 0;
        while (source != null && guard++ < 20)
        {
            if (source is ButtonBase) return true;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnSubmitClick(sender, e);
            e.Handled = true;
        }
    }
}
