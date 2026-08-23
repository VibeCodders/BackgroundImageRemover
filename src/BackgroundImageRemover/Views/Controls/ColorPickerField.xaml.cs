using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BackgroundImageRemover.Views.Controls;

/// <summary>
/// Compact label + color swatch + "Choose color..." button + popup color picker, reusing the
/// pattern that was previously duplicated (with a per-color VM toggle and a code-behind click
/// handler) across every tool that picks colors. The popup is self-managed: it opens on the
/// button click and closes on outside click (StaysOpen=False), so no VM state is needed.
/// </summary>
public partial class ColorPickerField : UserControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(ColorPickerField),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPickerField),
            new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ColorPickerField()
    {
        InitializeComponent();
    }

    private void ChooseColorButton_Click(object sender, RoutedEventArgs e)
        => ColorPopup.IsOpen = !ColorPopup.IsOpen;
}
