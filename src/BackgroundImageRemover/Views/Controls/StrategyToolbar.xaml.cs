using System.Windows.Controls;
using System.Windows.Input;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views.Controls;

public partial class StrategyToolbar : UserControl
{
    public StrategyToolbar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// GIMP-style tool selection: left click only selects/highlights the tool (via the
    /// RadioButton's own IsChecked-&gt;ActiveTool two-way binding). Middle click actually opens
    /// the tool's dedicated tab, so a session is only spun up when the user asks for it.
    /// </summary>
    private void ToolButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        if (sender is not RadioButton { Tag: not null } button) return;
        if (DataContext is not DocumentViewModel vm) return;

        button.IsChecked = true;

        switch (button.Tag)
        {
            case EditorTool tool:
                vm.OpenToolTabCommand.Execute(tool);
                break;
            case StrategyKind strategy:
                vm.OpenBackgroundRemovalToolCommand.Execute(strategy);
                break;
        }

        e.Handled = true;
    }
}
