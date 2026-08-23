using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BackgroundImageRemover.Views.Controls;

/// <summary>
/// The modal tool-session top bar (tool badge + document title + Cancel/Apply buttons), which
/// used to be duplicated verbatim across every tool session view. Title, CancelCommand and
/// ApplyCommand bind to the session view model; only the badge text and accent color vary.
/// </summary>
public partial class ToolSessionHeader : UserControl
{
    public static readonly DependencyProperty BadgeProperty =
        DependencyProperty.Register(nameof(Badge), typeof(string), typeof(ToolSessionHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(ToolSessionHeader),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(88, 101, 242))));

    public string Badge
    {
        get => (string)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public Brush Accent
    {
        get => (Brush)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public ToolSessionHeader()
    {
        InitializeComponent();
    }
}
