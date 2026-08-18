using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace BackgroundImageRemover.Views.Controls;

/// <summary>Before/after reveal slider: "After" is drawn over "Before", clipped to the divider position.</summary>
public partial class CompareImageControl : UserControl
{
    public static readonly DependencyProperty BeforeSourceProperty =
        DependencyProperty.Register(nameof(BeforeSource), typeof(BitmapSource), typeof(CompareImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AfterSourceProperty =
        DependencyProperty.Register(nameof(AfterSource), typeof(BitmapSource), typeof(CompareImageControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DividerPositionProperty =
        DependencyProperty.Register(nameof(DividerPosition), typeof(double), typeof(CompareImageControl),
            new FrameworkPropertyMetadata(0.5,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnDividerPositionChanged));

    public BitmapSource? BeforeSource
    {
        get => (BitmapSource?)GetValue(BeforeSourceProperty);
        set => SetValue(BeforeSourceProperty, value);
    }

    public BitmapSource? AfterSource
    {
        get => (BitmapSource?)GetValue(AfterSourceProperty);
        set => SetValue(AfterSourceProperty, value);
    }

    /// <summary>0 = fully "before", 1 = fully "after".</summary>
    public double DividerPosition
    {
        get => (double)GetValue(DividerPositionProperty);
        set => SetValue(DividerPositionProperty, value);
    }

    public CompareImageControl()
    {
        InitializeComponent();
    }

    private static void OnDividerPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CompareImageControl)d).UpdateClip();

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateClip();

    private void UpdateClip()
    {
        double width = RootGrid.ActualWidth;
        double height = RootGrid.ActualHeight;
        double dividerX = width * Math.Clamp(DividerPosition, 0.0, 1.0);

        AfterClip.Rect = new Rect(0, 0, dividerX, height);
        DividerLine.Height = height;
        DividerLine.Margin = new Thickness(dividerX, 0, 0, 0);
        DividerThumb.Margin = new Thickness(dividerX - 10, 0, 0, 0);
    }

    private void DividerThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double width = RootGrid.ActualWidth;
        if (width <= 0)
        {
            return;
        }
        DividerPosition = Math.Clamp(DividerPosition + e.HorizontalChange / width, 0.0, 1.0);
    }
}
