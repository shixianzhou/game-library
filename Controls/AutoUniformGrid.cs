using System.Windows;
using System.Windows.Controls;

namespace GameLibrary.Controls;

/// <summary>按可用宽度自动排列的卡片网格，最多四列，最后一行不会拉伸变形。</summary>
public sealed class AutoUniformGrid : Panel
{
    public static readonly DependencyProperty CardHeightProperty = DependencyProperty.Register(
        nameof(CardHeight), typeof(double), typeof(AutoUniformGrid),
        new FrameworkPropertyMetadata(310d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(AutoUniformGrid),
        new FrameworkPropertyMetadata(18d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MinCardWidthProperty = DependencyProperty.Register(
        nameof(MinCardWidth), typeof(double), typeof(AutoUniformGrid),
        new FrameworkPropertyMetadata(250d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double CardHeight
    {
        get => (double)GetValue(CardHeightProperty);
        set => SetValue(CardHeightProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double MinCardWidth
    {
        get => (double)GetValue(MinCardWidthProperty);
        set => SetValue(MinCardWidthProperty, value);
    }

    public int LastColumnCount { get; private set; }

    private int GetColumnCount(double width)
    {
        var gap = Gap;
        var min = Math.Max(80, MinCardWidth);
        return Math.Clamp((int)((width + gap) / (min + gap)), 1, 4);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 1200 : availableSize.Width;
        var columns = GetColumnCount(width);
        LastColumnCount = columns;
        var cellWidth = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(cellWidth, CardHeight));
        }

        var rows = (int)Math.Ceiling(InternalChildren.Count / (double)columns);
        var height = rows == 0 ? 0 : rows * CardHeight + (rows - 1) * Gap;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = GetColumnCount(finalSize.Width);
        LastColumnCount = columns;
        var cellWidth = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var rect = new Rect(
                column * (cellWidth + Gap),
                row * (CardHeight + Gap),
                cellWidth,
                CardHeight);
            InternalChildren[i].Arrange(rect);
        }
        return finalSize;
    }
}
