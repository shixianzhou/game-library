using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameLibrary.Controls;
using GameLibrary.Models;
using GameLibrary.Services;

namespace GameLibrary;

public partial class MainWindow : Window
{
    private readonly LibraryStore _store = new();
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3.2) };
    private readonly DispatcherTimer _slideshowTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<string, BitmapSource> _backgroundImageCache = new(StringComparer.OrdinalIgnoreCase);

    private LibraryData _data = new();
    private GameEntry? _featured;
    private BackgroundSource _background = new(BackgroundMediaKind.None, Array.Empty<string>(), Array.Empty<string>(), "填充");
    private Color _accent = ColorExtractor.DefaultAccent;
    private int _imageIndex = -1;
    private int _videoIndex;
    private string? _currentImagePath;
    private string? _currentVideoPath;
    private bool _frontIsA = true;
    private bool _loaded;
    private bool _closing;
    private bool _videoFailed;
    private bool _videoTransitioning;
    private int _videoTransitionId;
    private string? _loadWarning;
    private double _windowedWidth;
    private double _windowedHeight;
    private WindowState _lastWindowState = WindowState.Normal;
    private bool _applyingWindowedSize;

    public MainWindow()
    {
        InitializeComponent();
        _toastTimer.Tick += OnToastTick;
        _slideshowTimer.Tick += OnSlideshowTick;
        Hero.SizeChanged += OnHeroSizeChanged;
        SizeChanged += OnWindowSizeChanged;

        var work = SystemParameters.WorkArea;
        var comfortableWidth = Math.Min(1380, Math.Max(MinWidth, work.Width - 40));
        Width = comfortableWidth;
        var preferredHeight = Math.Min(960, Math.Max(MinHeight, work.Height - 6));
        Height = preferredHeight;

        _data = _store.Load(out _loadWarning);
        if (_data.Window.Width >= MinWidth && _data.Window.Height >= MinHeight)
        {
            // 旧配置里的小窗口不把新版压回“迷你”尺寸；只允许用户保存更大的尺寸。
            Width = Math.Min(Math.Max(_data.Window.Width, comfortableWidth), Math.Max(MinWidth, work.Width - 20));
            var comfortableHeight = Math.Min(850, preferredHeight);
            Height = Math.Min(preferredHeight, Math.Max(_data.Window.Height, comfortableHeight));
        }
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _windowedWidth = Width;
        _windowedHeight = Height;
    }

    public IReadOnlyList<GameEntry> CurrentGames => _data.Games;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        source?.AddHook(WindowProc);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;

        if (_data.Window.Maximized) WindowState = WindowState.Maximized;
        _lastWindowState = WindowState;
        UpdateMaximizeGlyph();
        RefreshAll();
        ApplyBackground();

        if (!string.IsNullOrWhiteSpace(_loadWarning))
            ShowToast(_loadWarning, true);
    }

    // =====================================================================
    // 数据刷新
    // =====================================================================

    private void RefreshAll()
    {
        _featured = ResolveFeatured();
        RefreshHero();
        RefreshCards();
        RefreshRail();
    }

    private GameEntry? ResolveFeatured()
    {
        if (_featured != null)
        {
            var current = _data.Games.FirstOrDefault(g => g.Id == _featured.Id);
            if (current != null) return current;
        }

        var recent = _data.Games
            .Where(g => g.LastPlayedAt.HasValue)
            .OrderByDescending(g => g.LastPlayedAt!.Value)
            .FirstOrDefault();
        return recent ?? _data.Games.OrderBy(g => g.AddedAt).FirstOrDefault();
    }

    private void RefreshHero()
    {
        var game = _featured;
        HeroGamePanel.Visibility = game == null ? Visibility.Collapsed : Visibility.Visible;
        HeroEmptyPanel.Visibility = game == null ? Visibility.Visible : Visibility.Collapsed;

        if (game == null)
        {
            HeroArtwork.GameName = "游戏库";
            HeroArtwork.SeedText = "library-default";
            HeroArtwork.ImagePath = null;
            HeroTags.ItemsSource = null;
            return;
        }

        HeroArtwork.GameName = game.Name;
        HeroArtwork.SeedText = game.Id.ToString();
        HeroArtwork.ImagePath = game.CoverPathResolved;

        HeroTitle.Text = game.Name;
        HeroDescription.Text = game.DescriptionText;
        HeroTags.ItemsSource = BuildTagPills(game);
        HeroEyebrow.Text = game.LastPlayedAt.HasValue
            ? "最近游玩 · " + game.LastPlayedLabel
            : "游戏库 · 尚未开始";
        HeroServerButton.Visibility = game.IsSeparated ? Visibility.Visible : Visibility.Collapsed;
        HeroFolderButton.IsEnabled = Directory.Exists(game.PrimaryDirectory);
        HeroFolderButton.ToolTip = string.IsNullOrWhiteSpace(game.PrimaryDirectory)
            ? "还没有设置游戏目录"
            : game.PrimaryDirectory;
    }

    private static List<FrameworkElement> BuildTagPills(GameEntry game)
    {
        var pills = new List<FrameworkElement>();
        foreach (var tag in game.Tags)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(tag.IsHighlight
                    ? Color.FromRgb(0x21, 0x2A, 0x15)
                    : Color.FromRgb(0x1D, 0x22, 0x17)),
                BorderBrush = new SolidColorBrush(tag.IsHighlight
                    ? Color.FromRgb(0x8F, 0xAF, 0x52)
                    : Color.FromRgb(0x3A, 0x41, 0x30)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(tag.IsHighlight ? 9 : 4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock
                {
                    Text = tag.Text,
                    FontSize = 10.5,
                    FontWeight = tag.IsHighlight ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(tag.IsHighlight
                        ? Color.FromRgb(0xDC, 0xE8, 0xBC)
                        : Color.FromRgb(0xB9, 0xBC, 0xA9)),
                },
            };
            pills.Add(border);
        }
        return pills;
    }

    private void RefreshCards()
    {
        GamesPanel.Children.Clear();
        var ordered = _data.Games
            .OrderByDescending(g => g.Pinned)
            .ThenByDescending(g => g.PinOrder)
            .ThenBy(g => g.AddedAt)
            .ToList();
        foreach (var game in ordered)
        {
            var card = new GameCard { Game = game };
            card.LaunchRequested += (_, args) => LaunchGame(args.Game, false);
            card.LaunchServerRequested += (_, args) => LaunchGame(args.Game, true);
            card.EditRequested += (_, args) => EditGame(args.Game, this);
            card.DeleteRequested += (_, args) => DeleteGame(args.Game, this);
            card.OpenFolderRequested += (_, args) => OpenGameFolder(args.Game);
            card.PinRequested += (_, args) => TogglePin(args.Game);
            GamesPanel.Children.Add(card);
        }

        var hasGames = ordered.Count > 0;
        GamesScroll.Visibility = hasGames ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = hasGames ? Visibility.Collapsed : Visibility.Visible;
        GameCountText.Text = ordered.Count.ToString();

        var latest = _data.Games
            .Where(g => g.LastPlayedAt.HasValue)
            .OrderByDescending(g => g.LastPlayedAt!.Value)
            .FirstOrDefault();
        LibraryMetaText.Text = latest == null
            ? (hasGames ? "还没有启动过游戏" : "从一款游戏开始")
            : $"最近启动 · {latest.Name} · {latest.LastPlayedLabel}";
    }

    private void RefreshRail()
    {
        RailPanel.Children.Clear();
        var capacity = RailCapacity();
        var games = _data.Games
            .OrderByDescending(g => g.LastPlayedAt ?? DateTimeOffset.MinValue)
            .ThenBy(g => g.AddedAt)
            .Take(capacity)
            .ToList();

        foreach (var game in games)
        {
            var selected = game.Id == _featured?.Id;
            var tileSize = selected ? 68 : 58;
            var border = new Border
            {
                Width = tileSize,
                Height = tileSize,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Padding = selected ? new Thickness(3) : new Thickness(0),
                BorderBrush = selected
                    ? (Brush)FindResource("AppAccentBrush")
                    : new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
                ClipToBounds = true,
                Child = new ArtworkView
                {
                    GameName = game.Name,
                    SeedText = game.Id.ToString(),
                    ImagePath = game.CoverPathResolved,
                    CornerRadius = selected ? 8 : 9,
                },
            };

            var button = new Button
            {
                Style = (Style)FindResource("RailButton"),
                Width = 72,
                Height = 74,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 2, 0, 2),
                Content = border,
                ToolTip = game.Name,
                Tag = game,
            };
            button.Click += OnRailGameClick;
            RailPanel.Children.Add(button);
        }

    }

    // =====================================================================
    // 启动 / 增删改
    // =====================================================================

    private int RailCapacity()
    {
        // 放大的图标更占纵向空间，窗口较矮时自动减少。
        if (ActualHeight <= 0) return 5;
        return ActualHeight >= 850 ? 5 : 4;
    }

    private void OnHeroSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_loaded || !e.HeightChanged) return;
        RefreshRail();
    }

    private void LaunchGame(GameEntry game, bool server)
    {
        var result = GameLauncher.Start(game, server);
        if (!result.Success)
        {
            AppDialog.ShowInfo(this, server ? "后台启动失败" : "游戏启动失败", result.Message);
            return;
        }

        game.LastPlayedAt = DateTimeOffset.Now;
        _featured = game;
        SaveLibrary();
        RefreshHero();
        RefreshCards();
        RefreshRail();
        ShowToast(result.Message);
    }

    private void AddGame(Window? owner = null)
    {
        var dialog = new AddGameDialog(_store, null)
        {
            Owner = owner ?? this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        _data.Games.Add(dialog.Result);
        SaveLibrary();
        RefreshAll();
        ShowToast($"已添加「{dialog.Result.Name}」");
    }

    private void EditGame(GameEntry game, Window? owner = null)
    {
        var index = _data.Games.FindIndex(g => g.Id == game.Id);
        if (index < 0) return;

        var dialog = new AddGameDialog(_store, game.Clone())
        {
            Owner = owner ?? this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        var result = dialog.Result;
        result.Id = game.Id;
        result.AddedAt = game.AddedAt;
        result.LastPlayedAt = game.LastPlayedAt;
        _data.Games[index] = result;
        if (_featured?.Id == game.Id) _featured = result;
        SaveLibrary();
        RefreshAll();
        ShowToast($"已保存「{result.Name}」");
    }

    private void DeleteGame(GameEntry game, Window? owner = null)
    {
        var confirmed = AppDialog.Confirm(owner ?? this, "移除游戏",
            $"确定要从库中移除「{game.Name}」吗？\n\n只移除游戏库记录，不会删除游戏文件。",
            "移除", true);
        if (!confirmed) return;

        _data.Games.RemoveAll(g => g.Id == game.Id);
        _store.DeleteCover(game.CoverFile);
        if (_featured?.Id == game.Id) _featured = null;
        SaveLibrary();
        RefreshAll();
        ShowToast($"已移除「{game.Name}」");
    }

    private void TogglePin(GameEntry game)
    {
        if (game.Pinned)
        {
            game.Pinned = false;
            game.PinOrder = 0;
        }
        else
        {
            long nextOrder = _data.Games
                .Where(g => g.Pinned)
                .Select(g => g.PinOrder)
                .DefaultIfEmpty(0)
                .Max() + 1;
            game.Pinned = true;
            game.PinOrder = nextOrder;
        }

        SaveLibrary();
        // 等右键菜单完全关闭后再重建卡片，避免鼠标抬起落到新卡片上误触“启动游戏”。
        Dispatcher.BeginInvoke(new Action(RefreshCards), DispatcherPriority.Background);
        ShowToast(game.Pinned ? $"已置顶「{game.Name}」" : $"已取消置顶「{game.Name}」");
    }

    private void OpenGameFolder(GameEntry game)
    {
        var path = game.PrimaryDirectory;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            AppDialog.ShowInfo(this, "目录不存在",
                string.IsNullOrWhiteSpace(path)
                    ? "这款游戏还没有设置游戏目录。"
                    : $"找不到目录：\n{path}\n\n游戏可能被移动了，可以编辑游戏重新选择。");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppDialog.ShowInfo(this, "打开目录失败", ex.Message);
        }
    }

    private void OpenDataDirectory()
    {
        try
        {
            Directory.CreateDirectory(_store.DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_store.DataDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppDialog.ShowInfo(this, "打开数据目录失败", ex.Message);
        }
    }

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(_data.Settings, _store, this)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (dialog.ShowDialog() == true && dialog.ResultSettings is { } settings)
        {
            _data.Settings = settings;
            SaveLibrary();
            ApplyBackground(keepCurrent: true);
            ShowToast("设置已保存");
        }
        RefreshAll();
    }

    public void AddGameFromSettings(Window owner) => AddGame(owner);
    public void EditGameFromSettings(Window owner, GameEntry game) => EditGame(game, owner);
    public void DeleteGameFromSettings(Window owner, GameEntry game) => DeleteGame(game, owner);

    private void SaveLibrary()
    {
        try
        {
            _store.Save(_data);
        }
        catch (Exception ex)
        {
            ShowToast("保存失败：" + ex.Message, true);
        }
    }

    // =====================================================================
    // 背景
    // =====================================================================

    private void ApplyBackground(bool keepCurrent = false)
    {
        var previousKind = _background.Kind;
        var previousDisplayMode = _background.DisplayMode;
        var previousVideo = _currentVideoPath;
        var previousImage = _currentImagePath;

        _slideshowTimer.Stop();
        _slideshowTimer.Interval = TimeSpan.FromSeconds(
            _data.Settings.Background.RotationSeconds is 180 or 300 or 600
                ? _data.Settings.Background.RotationSeconds
                : 180);

        var next = BackgroundLibrary.Resolve(_data.Settings.Background);

        // 只改了轮换时间这类设置时，继续用当前这张图/这段视频，不重新随机。
        if (keepCurrent
            && next.Kind == previousKind
            && next.DisplayMode == previousDisplayMode
            && CanKeepCurrent(next, previousVideo, previousImage))
        {
            _background = next;
            StartRotation();
            SetHeaderMediaBackground(next.Kind != BackgroundMediaKind.None);
            return;
        }

        _videoFailed = false;
        _videoTransitioning = false;
        _videoTransitionId++;
        _imageIndex = -1;
        _videoIndex = 0;

        try
        {
            HeroVideo.BeginAnimation(OpacityProperty, null);
            HeroVideo.Opacity = 1;
            HeroVideo.Clock = null;
            HeroVideo.Stop();
            HeroVideo.Source = null;
        }
        catch
        {
            // 某些解码器在关闭时会抛异常，忽略即可。
        }
        HeroVideoFade.BeginAnimation(OpacityProperty, null);
        HeroVideoFade.Opacity = 0;
        HeroVideoFade.Visibility = Visibility.Collapsed;
        HeroVideo.Visibility = Visibility.Collapsed;
        HeroImageLayer.Visibility = Visibility.Collapsed;
        HeroTileRect.Visibility = Visibility.Collapsed;
        HeroImageA.Source = null;
        HeroImageB.Source = null;
        _currentImagePath = null;
        _currentVideoPath = null;
        HeroImageA.Opacity = 1;
        HeroImageB.Opacity = 0;

        _background = next;
        switch (_background.Kind)
        {
            case BackgroundMediaKind.Video:
                HeroArtwork.Visibility = Visibility.Collapsed;
                HeroVideo.Stretch = BackgroundLibrary.ToStretch(_background.DisplayMode);
                HeroVideo.Visibility = Visibility.Visible;
                _videoIndex = IndexOrRandom(_background.Videos, previousVideo);
                PlayCurrentVideo();
                break;
            case BackgroundMediaKind.Image:
                HeroArtwork.Visibility = Visibility.Collapsed;
                ShowImageLayer(previousImage);
                break;
            default:
                HeroArtwork.Visibility = Visibility.Visible;
                ApplyAccent(ColorExtractor.DefaultAccent);
                break;
        }

        StartRotation();
        SetHeaderMediaBackground(_background.Kind != BackgroundMediaKind.None);
    }

    private static int PickRandomIndex(int count) =>
        count > 1 ? Random.Shared.Next(count) : 0;

    /// <summary>优先沿用上一次的素材；它已不在新列表里时才随机挑一个。</summary>
    private static int IndexOrRandom(IReadOnlyList<string> items, string? preferred)
    {
        if (preferred != null)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i], preferred, StringComparison.OrdinalIgnoreCase)) return i;
            }
        }
        return PickRandomIndex(items.Count);
    }

    /// <summary>当前显示的素材是否仍然有效（保存设置时用来避免无谓的重新随机）。</summary>
    private bool CanKeepCurrent(BackgroundSource next, string? video, string? image) => next.Kind switch
    {
        BackgroundMediaKind.Video =>
            HeroVideo.Visibility == Visibility.Visible && video != null && next.Videos.Contains(video),
        BackgroundMediaKind.Image =>
            HeroImageLayer.Visibility == Visibility.Visible && image != null && next.Images.Contains(image),
        _ => true,
    };

    /// <summary>按当前的可见图层与素材数量决定是否启动轮换计时器。</summary>
    private void StartRotation()
    {
        _slideshowTimer.Stop();
        var useVideo = HeroVideo.Visibility == Visibility.Visible;
        var rotating = useVideo
            ? _background.Videos.Count > 1
            : HeroImageLayer.Visibility == Visibility.Visible && _background.Images.Count > 1;
        if (rotating) _slideshowTimer.Start();
    }

    private void SetHeaderMediaBackground(bool active)
    {
        GamesHeader.Background = active
            ? Brushes.Transparent
            : (Brush)FindResource("BgBrush");
    }

    private void ShowImageLayer(string? preferredPath = null)
    {
        if (_background.Images.Count == 0)
        {
            _currentImagePath = null;
            HeroArtwork.Visibility = Visibility.Visible;
            return;
        }

        HeroImageLayer.Visibility = Visibility.Visible;
        _imageIndex = IndexOrRandom(_background.Images, preferredPath);
        var path = _background.Images[_imageIndex];
        _currentImagePath = path;
        if (BackgroundLibrary.IsTile(_background.DisplayMode))
        {
            HeroImageA.Visibility = Visibility.Collapsed;
            HeroImageB.Visibility = Visibility.Collapsed;
            HeroTileRect.Visibility = Visibility.Visible;
            UpdateTileBrush(path);
        }
        else
        {
            HeroTileRect.Visibility = Visibility.Collapsed;
            HeroImageA.Visibility = Visibility.Visible;
            HeroImageB.Visibility = Visibility.Visible;
            HeroImageA.Stretch = BackgroundLibrary.ToStretch(_background.DisplayMode);
            HeroImageB.Stretch = BackgroundLibrary.ToStretch(_background.DisplayMode);
            HeroImageA.BeginAnimation(OpacityProperty, null);
            HeroImageB.BeginAnimation(OpacityProperty, null);
            SetImage(HeroImageA, path);
            HeroImageA.Opacity = 1;
            HeroImageB.Opacity = 0;
            _frontIsA = true;
        }

        RefreshDynamicAccent(path);
    }

    private void OnSlideshowTick(object? sender, EventArgs e)
    {
        if (HeroVideo.Visibility == Visibility.Visible)
        {
            if (_background.Videos.Count < 2) return;
            _videoIndex = (_videoIndex + 1) % _background.Videos.Count;
            PlayCurrentVideo(true);
            return;
        }

        if (_background.Kind != BackgroundMediaKind.Image || _background.Images.Count < 2) return;
        _imageIndex = (_imageIndex + 1) % _background.Images.Count;
        var path = _background.Images[_imageIndex];
        _currentImagePath = path;

        if (BackgroundLibrary.IsTile(_background.DisplayMode))
        {
            UpdateTileBrush(path);
        }
        else
        {
            var incoming = _frontIsA ? HeroImageB : HeroImageA;
            var outgoing = _frontIsA ? HeroImageA : HeroImageB;
            incoming.BeginAnimation(OpacityProperty, null);
            outgoing.BeginAnimation(OpacityProperty, null);
            SetImage(incoming, path);
            incoming.Stretch = BackgroundLibrary.ToStretch(_background.DisplayMode);
            incoming.Opacity = 0;
            incoming.Visibility = Visibility.Visible;

            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            incoming.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(500)) { EasingFunction = ease });
            outgoing.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, TimeSpan.FromMilliseconds(500)) { EasingFunction = ease });
            _frontIsA = !_frontIsA;
        }

        RefreshDynamicAccent(path);
    }

    private void SetImage(Image target, string path)
    {
        try
        {
            target.Source = LoadBackgroundBitmap(path);
        }
        catch
        {
            // 单张图片失败时保留上一张。
        }
    }

    private BitmapSource? LoadBackgroundBitmap(string path)
    {
        if (_backgroundImageCache.TryGetValue(path, out var cached)) return cached;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = 1920;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            if (_backgroundImageCache.Count > 12) _backgroundImageCache.Clear();
            _backgroundImageCache[path] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateTileBrush(string path)
    {
        var bitmap = LoadBackgroundBitmap(path);
        if (bitmap == null) return;
        var brush = new ImageBrush(bitmap)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 420, 236),
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox = new Rect(0, 0, 1, 1),
        };
        HeroTileRect.Fill = brush;
    }

    private void PlayCurrentVideo(bool fade = false)
    {
        if (_background.Videos.Count == 0) return;
        try
        {
            _videoFailed = false;
            _currentVideoPath = _background.Videos[_videoIndex];
            var uri = new Uri(_background.Videos[_videoIndex], UriKind.Absolute);
            if (!fade)
            {
                HeroVideo.BeginAnimation(OpacityProperty, null);
                HeroVideo.Opacity = 1;
                StartLoopingVideo(uri);
                return;
            }

            _videoTransitioning = true;
            var token = ++_videoTransitionId;
            FadeCover(1, 150, () =>
            {
                if (token != _videoTransitionId || _closing) return;
                try
                {
                    StartLoopingVideo(uri);
                }
                catch
                {
                    OnVideoUnavailable();
                }
            });
        }
        catch
        {
            OnVideoUnavailable();
        }
    }

    /// <summary>
    /// 用 MediaTimeline 的重复行为在媒体管线内部循环，避免 MediaEnded → seek 回开头造成的卡顿。
    /// </summary>
    private void StartLoopingVideo(Uri uri)
    {
        HeroVideo.Stop();
        HeroVideo.Clock = null;
        var timeline = new MediaTimeline(uri) { RepeatBehavior = RepeatBehavior.Forever };
        HeroVideo.Clock = timeline.CreateClock();
    }

    /// <summary>用一层黑色遮罩做短暂淡入淡出，遮住视频切换和回到开头的跳变。</summary>
    private void FadeCover(double to, int milliseconds, Action? completed = null)
    {
        HeroVideoFade.Visibility = Visibility.Visible;
        HeroVideoFade.BeginAnimation(OpacityProperty, null);
        var animation = new DoubleAnimation(HeroVideoFade.Opacity, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            FillBehavior = FillBehavior.Stop,
        };
        animation.Completed += (_, _) =>
        {
            HeroVideoFade.BeginAnimation(OpacityProperty, null);
            HeroVideoFade.Opacity = to;
            if (to <= 0) HeroVideoFade.Visibility = Visibility.Collapsed;
            completed?.Invoke();
        };
        HeroVideoFade.BeginAnimation(OpacityProperty, animation);
    }

    /// <summary>时钟驱动的视频用控制器暂停/恢复；普通 Source 播放走 MediaElement 自身。</summary>
    private void PauseVideoClock()
    {
        try { (HeroVideo.Clock?.Controller as ClockController)?.Pause(); } catch { }
    }

    private void ResumeVideoClock()
    {
        try { (HeroVideo.Clock?.Controller as ClockController)?.Resume(); } catch { }
    }

    private void OnVideoMediaOpened(object sender, RoutedEventArgs e)
    {
        if (_background.Kind != BackgroundMediaKind.Video || _closing) return;
        if (HeroVideo.Clock == null)
        {
            if (!(_data.Settings.Background.PauseOnDeactivated && !IsActive))
            {
                try { HeroVideo.Play(); } catch { OnVideoUnavailable(); }
            }
        }
        else if (_data.Settings.Background.PauseOnDeactivated && !IsActive)
        {
            PauseVideoClock();
        }

        if (_videoTransitioning) FadeCover(0, 240, () => _videoTransitioning = false);
    }

    private void OnVideoMediaEnded(object sender, RoutedEventArgs e)
    {
        if (_closing || _videoTransitioning || _background.Kind != BackgroundMediaKind.Video || _background.Videos.Count == 0) return;
        if (HeroVideo.Clock != null) return; // 时钟重复播放由媒体管线自己接管

        // 轮换节奏由「轮换时间」计时器控制：短片直接从头循环，切换新视频时才做渐隐。
        try
        {
            HeroVideo.Position = TimeSpan.Zero;
            HeroVideo.Play();
        }
        catch
        {
            OnVideoUnavailable();
        }
    }

    private void OnVideoMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (_closing) return;
        OnVideoUnavailable(e.ErrorException?.Message);
    }

    private void OnVideoUnavailable(string? reason = null)
    {
        if (_videoFailed) return;
        _videoFailed = true;
        _videoTransitioning = false;
        HeroVideo.BeginAnimation(OpacityProperty, null);
        HeroVideo.Opacity = 1;
        HeroVideoFade.BeginAnimation(OpacityProperty, null);
        HeroVideoFade.Opacity = 0;
        HeroVideoFade.Visibility = Visibility.Collapsed;
        HeroVideo.Visibility = Visibility.Collapsed;
        try { HeroVideo.Stop(); } catch { }

        if (_background.Images.Count > 0)
        {
            HeroArtwork.Visibility = Visibility.Collapsed;
            ShowImageLayer();
            StartRotation();
            ShowToast("视频背景无法播放，已切换为图片背景", true);
        }
        else
        {
            HeroArtwork.Visibility = Visibility.Visible;
            ApplyAccent(ColorExtractor.DefaultAccent);
            SetHeaderMediaBackground(false);
            ShowToast("视频背景无法播放，已恢复默认背景", true);
        }
    }

    private void RefreshDynamicAccent(string imagePath)
    {
        if (!_data.Settings.Background.DynamicColor)
        {
            ApplyAccent(ColorExtractor.DefaultAccent);
            return;
        }
        ApplyAccent(ColorExtractor.FromImage(imagePath));
    }

    private void ApplyAccent(Color color)
    {
        _accent = color;
        Resources["AppAccentBrush"] = new SolidColorBrush(color);
        var tint = new RadialGradientBrush
        {
            Center = new Point(0.78, 0.35),
            GradientOrigin = new Point(0.78, 0.35),
            RadiusX = 0.65,
            RadiusY = 0.8,
        };
        tint.GradientStops.Add(new GradientStop(Color.FromArgb(72, color.R, color.G, color.B), 0));
        tint.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        HeroTint.Background = tint;
        if (_loaded) RefreshRail();
    }

    // =====================================================================
    // 窗口与交互
    // =====================================================================

    private void OnAddGameClick(object sender, RoutedEventArgs e) => AddGame();

    private void OnHeroLaunchClick(object sender, RoutedEventArgs e)
    {
        if (_featured != null) LaunchGame(_featured, false);
    }

    private void OnHeroServerClick(object sender, RoutedEventArgs e)
    {
        if (_featured != null) LaunchGame(_featured, true);
    }

    private void OnHeroFolderClick(object sender, RoutedEventArgs e)
    {
        if (_featured != null) OpenGameFolder(_featured);
    }

    private void OnRailGameClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameEntry game })
        {
            _featured = game;
            RefreshHero();
            RefreshRail();
        }
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        AppMenu.PlacementTarget = MenuButton;
        AppMenu.Placement = PlacementMode.Bottom;
        AppMenu.IsOpen = true;
    }

    private void OnMenuAddGame(object sender, RoutedEventArgs e) => AddGame();

    private void OnMenuImportConfig(object sender, RoutedEventArgs e) => ImportConfig();

    private void ImportConfig()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入配置",
            Filter = "游戏库配置 (library.json)|library.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true) return;

        var imported = _store.TryReadConfig(dialog.FileName, out var error);
        if (imported == null)
        {
            AppDialog.ShowInfo(this, "导入失败", error ?? "无法读取这个文件。");
            return;
        }

        var importedCount = imported.Games.Count;
        if (importedCount == 0)
        {
            AppDialog.ShowInfo(this, "导入失败", "这个配置里没有游戏。");
            return;
        }

        var currentCount = _data.Games.Count;
        var confirmed = AppDialog.Confirm(this, "导入配置",
            $"将用「{Path.GetFileName(dialog.FileName)}」里的 {importedCount} 个游戏替换当前库" +
            (currentCount > 0 ? $"（现有 {currentCount} 个）" : "") + "。\n\n" +
            "当前配置会先备份到数据目录，封面也会一并复制过来。",
            "导入");
        if (!confirmed) return;

        try
        {
            var backup = _store.BackupConfig();
            var sourceCovers = Path.Combine(Path.GetDirectoryName(dialog.FileName) ?? "", "covers");
            var copiedCovers = _store.CopyCoversFrom(sourceCovers);

            // 导入的配置可能引用了不存在的封面：这类封面直接清掉，按普通条目导入，
            // 否则卡片会指向一个永远找不到的文件。
            foreach (var game in imported.Games)
            {
                if (string.IsNullOrWhiteSpace(game.CoverFile)) continue;
                var coverPath = Path.Combine(_store.CoversDirectory, Path.GetFileName(game.CoverFile));
                if (!File.Exists(coverPath)) game.CoverFile = "";
            }

            _store.Save(imported);
            _data = _store.Load(out var warning);
            if (!string.IsNullOrWhiteSpace(warning)) ShowToast(warning, true);

            _featured = null;
            RefreshAll();
            ApplyBackground();
            ShowToast(copiedCovers > 0
                ? $"已导入 {importedCount} 个游戏，复制 {copiedCovers} 张封面"
                : $"已导入 {importedCount} 个游戏");
            if (!string.IsNullOrWhiteSpace(backup)) Debug.WriteLine("已备份原配置：" + backup);
        }
        catch (Exception ex)
        {
            AppDialog.ShowInfo(this, "导入失败", ex.Message);
        }
    }
    private void OnMenuSettings(object sender, RoutedEventArgs e) => OpenSettings();
    private void OnMenuOpenData(object sender, RoutedEventArgs e) => OpenDataDirectory();

    private void OnMenuAbout(object sender, RoutedEventArgs e)
    {
        AppDialog.ShowInfo(this, "关于游戏库",
            "本地游戏库  v1.0.0\n\n" +
            "一个不需要联网的本地游戏收藏夹：添加游戏目录、记录封面与描述，客户端与服务端可以分开放置并分别启动。\n\n" +
            $"数据目录：\n{_store.DataDirectory}");
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnTopBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsInteractiveElement(e.OriginalSource as DependencyObject)) return;
        if (e.ClickCount == 2)
        {
            OnMaximizeClick(sender, e);
            return;
        }

        try
        {
            if (WindowState == WindowState.Maximized)
            {
                var mouse = PointToScreen(e.GetPosition(this));
                var restoreWidth = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width;
                WindowState = WindowState.Normal;
                Left = mouse.X - restoreWidth / 2;
                Top = mouse.Y - 18;
            }
            DragMove();
        }
        catch
        {
            // 拖动时窗口状态变化可能抛异常，忽略。
        }
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        var guard = 0;
        while (source != null && guard++ < 30)
        {
            if (source is ButtonBase or TextBoxBase or MenuItem) return true;
            source = source switch
            {
                Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(source),
                FrameworkContentElement content => content.Parent,
                _ => LogicalTreeHelper.GetParent(source),
            };
        }
        return false;
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        var previous = _lastWindowState;
        _lastWindowState = WindowState;

        if (WindowState == WindowState.Normal && previous == WindowState.Maximized && _loaded)
        {
            _applyingWindowedSize = true;
            Width = _windowedWidth;
            Height = _windowedHeight;
            _applyingWindowedSize = false;
        }

        UpdateMaximizeGlyph();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_loaded || _applyingWindowedSize || WindowState != WindowState.Normal) return;
        _windowedWidth = ActualWidth;
        _windowedHeight = ActualHeight;
        Dispatcher.BeginInvoke(new Action(() => { if (_loaded) RefreshRail(); }), DispatcherPriority.Background);
    }

    private void UpdateMaximizeGlyph()
    {
        if (MaximizeGlyph == null) return;
        MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "还原" : "最大化";
    }

    private void OnActivated(object sender, EventArgs e)
    {
        if (!_loaded) return;
        if (_data.Settings.Background.PauseOnDeactivated &&
            _background.Kind == BackgroundMediaKind.Video &&
            (HeroVideo.Source != null || HeroVideo.Clock != null))
        {
            try
            {
                if (HeroVideo.Clock == null) HeroVideo.Play();
                else ResumeVideoClock();
            }
            catch { }
            StartRotation();
        }
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        if (_loaded && _data.Settings.Background.PauseOnDeactivated &&
            _background.Kind == BackgroundMediaKind.Video)
        {
            try
            {
                if (HeroVideo.Clock == null) HeroVideo.Pause();
                else PauseVideoClock();
            }
            catch { }
            _slideshowTimer.Stop();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            if (_featured != null) LaunchGame(_featured, false);
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            AddGame();
            e.Handled = true;
        }
        else if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenSettings();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && AppMenu.IsOpen)
        {
            AppMenu.IsOpen = false;
            e.Handled = true;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        _toastTimer.Stop();
        _slideshowTimer.Stop();
        try { HeroVideo.Stop(); HeroVideo.Close(); } catch { }

        var bounds = RestoreBounds;
        if (bounds.Width >= MinWidth && bounds.Height >= MinHeight)
        {
            _data.Window.Width = bounds.Width;
            _data.Window.Height = bounds.Height;
        }
        _data.Window.Maximized = WindowState == WindowState.Maximized;
        try { _store.Save(_data); } catch { }
    }

    private void ShowToast(string message, bool error = false)
    {
        ToastText.Text = message;
        ToastGlyph.Text = error ? "\uE783" : "\uE73E";
        ToastGlyph.Foreground = new SolidColorBrush(error
            ? Color.FromRgb(0xE0, 0x7E, 0x70)
            : _accent);

        if (Toast.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            Toast.RenderTransform = transform;
        }
        Toast.BeginAnimation(OpacityProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        Toast.Opacity = 0;
        Toast.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void OnToastTick(object? sender, EventArgs e)
    {
        _toastTimer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) => { };
        Toast.BeginAnimation(OpacityProperty, fade);
    }

    // =====================================================================
    // 最大化时不要遮住任务栏
    // =====================================================================

    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RectStruct Monitor;
        public RectStruct Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo && WindowState == WindowState.Maximized)
        {
            ApplyWorkArea(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ApplyWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var info = new MinMaxInfo();
        Marshal.StructureToPtr(info, lParam, false);
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return;

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo)) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var work = monitorInfo.Work;
        var screen = monitorInfo.Monitor;
        info.MaxPosition.X = Math.Abs(work.Left - screen.Left);
        info.MaxPosition.Y = Math.Abs(work.Top - screen.Top);
        info.MaxSize.X = work.Right - work.Left;
        info.MaxSize.Y = work.Bottom - work.Top;
        info.MinTrackSize.X = (int)(MinWidth * dpi.DpiScaleX);
        info.MinTrackSize.Y = (int)(MinHeight * dpi.DpiScaleY);
        Marshal.StructureToPtr(info, lParam, true);
    }
}


