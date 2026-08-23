using System.Windows;
using System.Windows.Controls;

namespace BackgroundImageRemover.Views.Controls;

/// <summary>
/// Shared tool-session layout: the bottom status bar, the right-hand settings panel
/// (title + optional description + content + reset button) and the preview area.
/// Replaces the Border/ScrollViewer/StatusBar chrome duplicated across every tool view.
/// </summary>
public partial class ToolSessionPanel : UserControl
{
    public ToolSessionPanel()
    {
        InitializeComponent();
    }

    /// <summary>Panel title shown above the settings content.</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ToolSessionPanel), new PropertyMetadata(null));

    /// <summary>Optional one-line description under the title (hidden when null/empty).</summary>
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(ToolSessionPanel), new PropertyMetadata(null));

    /// <summary>Width of the right-hand settings panel (default 300).</summary>
    public double PanelWidth
    {
        get => (double)GetValue(PanelWidthProperty);
        set => SetValue(PanelWidthProperty, value);
    }

    public static readonly DependencyProperty PanelWidthProperty =
        DependencyProperty.Register(nameof(PanelWidth), typeof(double), typeof(ToolSessionPanel), new PropertyMetadata(300.0));

    /// <summary>Settings content (sliders, check boxes, buttons...) placed inside the scrollable panel.</summary>
    public object? Panel
    {
        get => GetValue(PanelProperty);
        set => SetValue(PanelProperty, value);
    }

    public static readonly DependencyProperty PanelProperty =
        DependencyProperty.Register(nameof(Panel), typeof(object), typeof(ToolSessionPanel), new PropertyMetadata(null));

    /// <summary>Preview area content, shown in the space left of the settings panel.</summary>
    public object? Preview
    {
        get => GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }

    public static readonly DependencyProperty PreviewProperty =
        DependencyProperty.Register(nameof(Preview), typeof(object), typeof(ToolSessionPanel), new PropertyMetadata(null));

    /// <summary>When true the shared "↺ Reset" button is shown at the bottom of the panel.</summary>
    public bool ShowReset
    {
        get => (bool)GetValue(ShowResetProperty);
        set => SetValue(ShowResetProperty, value);
    }

    public static readonly DependencyProperty ShowResetProperty =
        DependencyProperty.Register(nameof(ShowReset), typeof(bool), typeof(ToolSessionPanel), new PropertyMetadata(true));

    /// <summary>Label of the shared reset button (default "↺ Reset").</summary>
    public string ResetLabel
    {
        get => (string)GetValue(ResetLabelProperty);
        set => SetValue(ResetLabelProperty, value);
    }

    public static readonly DependencyProperty ResetLabelProperty =
        DependencyProperty.Register(nameof(ResetLabel), typeof(string), typeof(ToolSessionPanel), new PropertyMetadata("↺ Reset"));

    /// <summary>
    /// When false the built-in status bar is hidden; tools with custom status content
    /// (e.g. busy indicators) render their own StatusBar in the view instead.
    /// </summary>
    public bool ShowStatusBar
    {
        get => (bool)GetValue(ShowStatusBarProperty);
        set => SetValue(ShowStatusBarProperty, value);
    }

    public static readonly DependencyProperty ShowStatusBarProperty =
        DependencyProperty.Register(nameof(ShowStatusBar), typeof(bool), typeof(ToolSessionPanel), new PropertyMetadata(true));
}
