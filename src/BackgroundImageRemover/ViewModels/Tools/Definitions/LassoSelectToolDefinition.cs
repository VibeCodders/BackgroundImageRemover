using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class LassoSelectToolDefinition : EditorToolDefinitionBase
{
    public LassoSelectToolDefinition() : base(EditorTool.LassoSelect)
    {
    }

    public override string DisplayName => "Lasso Select";
    public override string Category => "Selection";
    public override int Order => 1;
    public override string IconResourceKey => "LassoIcon";
    public override string ToolTip => "Lasso Select (freehand outline)";
    public override char? Shortcut => null;

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new LassoSelectToolSessionViewModel(shell, doc);
}
