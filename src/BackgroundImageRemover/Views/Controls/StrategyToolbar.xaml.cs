using System.Windows.Controls;
using System.Windows.Input;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.ViewModels.Tools;

namespace BackgroundImageRemover.Views.Controls;

public partial class StrategyToolbar : UserControl
{
    public StrategyToolbar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// GIMP-style tool selection: left click only selects/highlights the tool (IToolDefinition.Select
    /// -- no session opened). Middle click actually opens the tool's dedicated tab
    /// (IToolDefinition.RequestOpen), so a session is only spun up when the user asks for it.
    /// </summary>
    private void ToolButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not RadioButton { Tag: IToolDefinition definition } button) return;
        if (DataContext is not DocumentViewModel vm) return;

        switch (e.ChangedButton)
        {
            case MouseButton.Left:
                definition.Select(vm);
                break;
            case MouseButton.Middle:
                button.IsChecked = true;
                definition.RequestOpen(vm);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
