using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BackgroundImageRemover.Views.Controls;

public partial class SimpleColorPickerControl : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(SimpleColorPickerControl),
            new PropertyMetadata(Colors.White, OnSelectedColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private bool _suppressEvents;

    public SimpleColorPickerControl()
    {
        InitializeComponent();
        SyncSlidersFromColor();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SimpleColorPickerControl)d).SyncSlidersFromColor();

    private void SyncSlidersFromColor()
    {
        _suppressEvents = true;
        RedSlider.Value = SelectedColor.R;
        GreenSlider.Value = SelectedColor.G;
        BlueSlider.Value = SelectedColor.B;
        _suppressEvents = false;
        UpdateSwatch();
    }

    private void ChannelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents)
        {
            return;
        }
        SelectedColor = Color.FromRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
        UpdateSwatch();
    }

    private void UpdateSwatch() => Swatch.Fill = new SolidColorBrush(SelectedColor);
}
