using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class RotateToolDefinition : EditorToolDefinitionBase
{
    public RotateToolDefinition() : base(EditorTool.Rotate)
    {
    }

    public override string DisplayName => "Rotate";
    public override string Category => "Transform";
    public override int Order => 2;
    public override string IconResourceKey => "RotateIcon";
    public override string ToolTip => "Rotate";
    public override char? Shortcut => null;

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new RotateToolSessionViewModel(shell, doc);
}
