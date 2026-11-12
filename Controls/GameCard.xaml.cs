using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using GameLibrary.Models;

namespace GameLibrary.Controls;

public sealed class GameCardActionEventArgs : EventArgs
{
    public GameCardActionEventArgs(GameEntry game, bool server = false)
    {
        Game = game;
        Server = server;
    }

    public GameEntry Game { get; }
    public bool Server { get; }
}

public partial class GameCard : UserControl
{
    public static readonly DependencyProperty GameProperty = DependencyProperty.Register(
        nameof(Game), typeof(GameEntry), typeof(GameCard),
        new PropertyMetadata(null, OnGameChanged));

    public GameEntry? Game
    {
        get => (GameEntry?)GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    public event EventHandler<GameCardActionEventArgs>? LaunchRequested;
    public event EventHandler<GameCardActionEventArgs>? LaunchServerRequested;
    public event EventHandler<GameCardActionEventArgs>? EditRequested;
    public event EventHandler<GameCardActionEventArgs>? DeleteRequested;
    public event EventHandler<GameCardActionEventArgs>? OpenFolderRequested;
    public event EventHandler<GameCardActionEventArgs>? PinRequested;

    public GameCard()
    {
        InitializeComponent();
        CardBorder.SizeChanged += (_, _) => ApplyCardClip();
    }

    private void ApplyCardClip()
    {
        var w = CardBorder.ActualWidth;
        var h = CardBorder.ActualHeight;
        if (w <= 0 || h <= 0) return;
        CardBorder.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 10, 10);
    }

    private static void OnGameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (GameCard)d;
        card.DataContext = e.NewValue;
        card.ServerButton.Visibility = card.Game?.IsSeparated == true ? Visibility.Visible : Visibility.Collapsed;
        card.MenuLaunchServer.Visibility = card.ServerButton.Visibility;
        card.MenuPin.Header = card.Game?.Pinned == true ? "取消置顶" : "置顶";
        card.CardMenu.DataContext = e.NewValue;
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (Game == null) return;
        // 直接铺满整张卡：不再从底部滑入，避免滑动过程中露出原卡片内容。
        ShadeTransform.BeginAnimation(TranslateTransform.YProperty, null);
        ShadeTransform.Y = 0;
        Shade.Visibility = Visibility.Visible;
        Shade.Opacity = 1;
        Shade.IsHitTestVisible = true;
        ActionsPanel.Visibility = Visibility.Visible;
        Art.Effect = new BlurEffect { Radius = 4.0, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
        InfoPanel.Effect = new BlurEffect { Radius = 2.2, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
        HoverRing.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        Shade.IsHitTestVisible = false;
        Shade.Visibility = Visibility.Collapsed;
        ActionsPanel.Visibility = Visibility.Collapsed;
        Art.Effect = null;
        InfoPanel.Effect = null;
        ShadeTransform.BeginAnimation(TranslateTransform.YProperty, null);
        ShadeTransform.Y = 140;
        HoverRing.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(110)));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && Game != null)
        {
            EditRequested?.Invoke(this, new GameCardActionEventArgs(Game));
            e.Handled = true;
        }
    }

    private void OnLaunchClick(object sender, RoutedEventArgs e)
    {
        if (Game == null) return;
        e.Handled = true;
        LaunchRequested?.Invoke(this, new GameCardActionEventArgs(Game));
    }

    private void OnServerClick(object sender, RoutedEventArgs e)
    {
        if (Game == null) return;
        e.Handled = true;
        LaunchServerRequested?.Invoke(this, new GameCardActionEventArgs(Game, true));
    }

    private void OnMenuLaunch(object sender, RoutedEventArgs e)
    {
        if (Game != null) LaunchRequested?.Invoke(this, new GameCardActionEventArgs(Game));
    }

    private void OnMenuLaunchServer(object sender, RoutedEventArgs e)
    {
        if (Game != null) LaunchServerRequested?.Invoke(this, new GameCardActionEventArgs(Game, true));
    }

    private void OnMenuEdit(object sender, RoutedEventArgs e)
    {
        if (Game != null) EditRequested?.Invoke(this, new GameCardActionEventArgs(Game));
    }

    private void OnMenuOpenFolder(object sender, RoutedEventArgs e)
    {
        if (Game != null) OpenFolderRequested?.Invoke(this, new GameCardActionEventArgs(Game));
    }

    private void OnMenuDelete(object sender, RoutedEventArgs e)
    {
        if (Game != null) DeleteRequested?.Invoke(this, new GameCardActionEventArgs(Game));
    }

    private void OnMenuPin(object sender, RoutedEventArgs e)
    {
        if (Game != null) PinRequested?.Invoke(this, new GameCardActionEventArgs(Game));
    }
}
