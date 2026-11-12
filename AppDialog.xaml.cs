using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GameLibrary;

/// <summary>
/// 应用统一的深色提示 / 确认弹窗。整个窗口铺在 Owner 之上，附带半透明遮罩，
/// 这样不用额外处理主窗口的点击穿透，也能保证模态感一致。
/// </summary>
public partial class AppDialog : Window
{
    private bool _confirmed;
    private bool _infoOnly;

    private AppDialog()
    {
        InitializeComponent();
    }

    private static AppDialog Create(
        Window owner,
        string title,
        string message,
        string confirmText,
        bool showCancel,
        bool danger,
        bool infoOnly)
    {
        var dialog = new AppDialog
        {
            Owner = owner,
            Title = title,
            Width = Math.Max(owner.ActualWidth, 480),
            Height = Math.Max(owner.ActualHeight, 320),
            Left = owner.Left,
            Top = owner.Top,
        };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmText;
        dialog.CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        dialog._infoOnly = infoOnly;
        dialog.CancelButton.Content = infoOnly ? "关闭" : "取消";

        if (danger)
        {
            dialog.ConfirmButton.Background = new SolidColorBrush(Color.FromRgb(0xC8, 0x35, 0x2C));
            dialog.GlyphText.Text = "\uE74D";
            dialog.GlyphText.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x9A, 0x91));
        }
        else if (infoOnly)
        {
            dialog.GlyphText.Text = "\uE946";
        }
        else
        {
            dialog.GlyphText.Text = "\uE7BA";
        }

        dialog.Loaded += dialog.OnLoaded;
        return dialog;
    }

    public static bool Confirm(Window owner, string title, string message,
        string confirmText = "确定", bool danger = false)
    {
        var dialog = Create(owner, title, message, confirmText, true, danger, false);
        return dialog.ShowDialog() == true && dialog._confirmed;
    }

    public static void ShowInfo(Window owner, string title, string message,
        string confirmText = "知道了")
    {
        var dialog = Create(owner, title, message, confirmText, false, false, true);
        dialog.ShowDialog();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Card.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
        CardOffset.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });
        ConfirmButton.Focus();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => Accept();

    private void OnCancelClick(object sender, RoutedEventArgs e) => Reject();

    private void OnBackdropMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == Backdrop) Reject();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Reject();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _infoOnly)
        {
            Accept();
            e.Handled = true;
        }
    }

    private void Accept()
    {
        _confirmed = true;
        try { DialogResult = true; } catch { Close(); }
    }

    private void Reject()
    {
        _confirmed = false;
        try { DialogResult = false; } catch { Close(); }
    }
}
