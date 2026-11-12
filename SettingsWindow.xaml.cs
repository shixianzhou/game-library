using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GameLibrary.Controls;
using GameLibrary.Models;
using GameLibrary.Services;
using Microsoft.Win32;

namespace GameLibrary;

public partial class SettingsWindow : Window
{
    private readonly LibraryStore _store;
    private readonly MainWindow _main;
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings, LibraryStore store, MainWindow main)
    {
        _store = store;
        _main = main;
        _settings = Clone(settings);

        InitializeComponent();
        Width = Math.Min(960, Math.Max(820, SystemParameters.WorkArea.Width - 56));
        Height = Math.Min(650, Math.Max(520, SystemParameters.WorkArea.Height - 56));

        ImageDirectoryBox.Text = _settings.Background.ImageDirectory;
        VideoDirectoryBox.Text = _settings.Background.VideoDirectory;
        SelectDisplayMode(_settings.Background.DisplayMode);
        SelectRotation(_settings.Background.RotationSeconds);
        DynamicColorToggle.IsChecked = _settings.Background.DynamicColor;
        PauseToggle.IsChecked = _settings.Background.PauseOnDeactivated;

        RefreshVersions();
        SelectTab(background: true);
    }

    public AppSettings? ResultSettings { get; private set; }

    private static AppSettings Clone(AppSettings source) => new()
    {
        Background = new BackgroundSettings
        {
            ImageDirectory = source.Background.ImageDirectory,
            VideoDirectory = source.Background.VideoDirectory,
            DisplayMode = source.Background.DisplayMode,
            RotationSeconds = source.Background.RotationSeconds,
            DynamicColor = source.Background.DynamicColor,
            PauseOnDeactivated = source.Background.PauseOnDeactivated,
        },
    };

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BackgroundNav.Focus();
    }

    // =====================================================================
    // 导航
    // =====================================================================

    private void OnBackgroundNavClick(object sender, RoutedEventArgs e) => SelectTab(true);

    private void OnVersionsNavClick(object sender, RoutedEventArgs e) => SelectTab(false);

    private void SelectTab(bool background)
    {
        BackgroundNav.Tag = background ? "selected" : null;
        VersionsNav.Tag = background ? null : "selected";
        BackgroundPanel.Visibility = background ? Visibility.Visible : Visibility.Collapsed;
        VersionsPanel.Visibility = background ? Visibility.Collapsed : Visibility.Visible;
        PageTitle.Text = background ? "背景" : "版本";
        FooterError.Text = "";
        if (!background) RefreshVersions();
    }

    private void OnBrowseImageDirectory(object sender, RoutedEventArgs e)
        => BrowseDirectory(ImageDirectoryBox);

    private void OnBrowseVideoDirectory(object sender, RoutedEventArgs e)
        => BrowseDirectory(VideoDirectoryBox);

    private void BrowseDirectory(TextBox target)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择素材目录",
            Multiselect = false,
            InitialDirectory = Directory.Exists(target.Text) ? target.Text : "",
        };
        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FolderName;
            target.CaretIndex = target.Text.Length;
            FooterError.Text = "";
        }
    }

    private void SelectDisplayMode(string mode)
    {
        foreach (var item in DisplayModeCombo.Items)
        {
            if (item is ComboBoxItem { Content: string text } && text == mode)
            {
                DisplayModeCombo.SelectedItem = item;
                return;
            }
        }
        DisplayModeCombo.SelectedIndex = 0;
    }

    private void SelectRotation(int seconds)
    {
        foreach (var item in RotationCombo.Items)
        {
            if (item is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out var value) && value == seconds)
            {
                RotationCombo.SelectedItem = item;
                return;
            }
        }
        RotationCombo.SelectedIndex = 0;
    }

    // =====================================================================
    // 版本列表
    // =====================================================================

    private void RefreshVersions()
    {
        if (VersionsListPanel == null) return;
        VersionsListPanel.Children.Clear();

        var games = _main.CurrentGames.OrderBy(g => g.AddedAt).ToList();
        VersionsCount.Text = $"共 {games.Count} 个";
        VersionsEmptyPanel.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var game in games)
        {
            var row = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x15, 0x18, 0x10)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x32, 0x23)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                ToolTip = game.PrimaryDirectory,
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var artBorder = new Border
            {
                Width = 60,
                Height = 42,
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0D, 0x08)),
                Child = new ArtworkView
                {
                    GameName = game.Name,
                    SeedText = game.Id.ToString(),
                    ImagePath = game.CoverPathResolved,
                    CornerRadius = 6,
                },
            };
            Grid.SetColumn(artBorder, 0);
            grid.Children.Add(artBorder);

            var info = new StackPanel
            {
                Margin = new Thickness(12, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            info.Children.Add(new TextBlock
            {
                Text = game.Name,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            info.Children.Add(new TextBlock
            {
                Text = $"{game.LastPlayedLabel} · {(game.IsSeparated ? "客户端 + 服务端" : "本地游戏")}",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8D, 0x92, 0x7F)),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var edit = new Button
            {
                Style = (Style)FindResource("GhostButton"),
                Content = "编辑",
                Height = 30,
                FontSize = 11,
                Padding = new Thickness(10, 0, 10, 0),
            };
            edit.Click += (_, _) =>
            {
                _main.EditGameFromSettings(this, game);
                RefreshVersions();
            };
            actions.Children.Add(edit);

            var remove = new Button
            {
                Style = (Style)FindResource("GhostButton"),
                Content = "移除",
                Height = 30,
                FontSize = 11,
                Padding = new Thickness(8, 0, 8, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0xD3, 0x7B, 0x70)),
                Margin = new Thickness(2, 0, 0, 0),
            };
            remove.Click += (_, _) =>
            {
                _main.DeleteGameFromSettings(this, game);
                RefreshVersions();
            };
            actions.Children.Add(remove);

            Grid.SetColumn(actions, 2);
            grid.Children.Add(actions);

            row.Child = grid;
            VersionsListPanel.Children.Add(row);
        }
    }

    private void OnAddVersionClick(object sender, RoutedEventArgs e)
    {
        _main.AddGameFromSettings(this);
        RefreshVersions();
    }

    // =====================================================================
    // 保存 / 关闭
    // =====================================================================

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var image = ImageDirectoryBox.Text.Trim();
        var video = VideoDirectoryBox.Text.Trim();

        if (!string.IsNullOrWhiteSpace(image) && !Directory.Exists(image))
        {
            SelectTab(background: true);
            FooterError.Text = "图片目录不存在，请重新选择。";
            ImageDirectoryBox.Focus();
            return;
        }
        if (!string.IsNullOrWhiteSpace(video) && !Directory.Exists(video))
        {
            SelectTab(background: true);
            FooterError.Text = "视频目录不存在，请重新选择。";
            VideoDirectoryBox.Focus();
            return;
        }

        _settings.Background.ImageDirectory = image;
        _settings.Background.VideoDirectory = video;
        _settings.Background.DisplayMode =
            DisplayModeCombo.SelectedItem is ComboBoxItem { Content: string mode } ? mode : "填充";
        _settings.Background.RotationSeconds =
            RotationCombo.SelectedItem is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out var seconds)
                ? seconds
                : 180;
        _settings.Background.DynamicColor = DynamicColorToggle.IsChecked == true;
        _settings.Background.PauseOnDeactivated = PauseToggle.IsChecked == true;
        ResultSettings = _settings;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;

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
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnSaveClick(sender, e);
            e.Handled = true;
        }
    }
}
