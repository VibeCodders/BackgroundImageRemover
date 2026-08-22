using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class ResizeToolDefinition : EditorToolDefinitionBase
{
    public ResizeToolDefinition() : base(EditorTool.Resize)
    {
    }

    public override string DisplayName => "Resize";
    public override string Category => "Transform";
    public override int Order => 1;
    public override string IconResourceKey => "ResizeIcon";
    public override string ToolTip => "Resize (S)";
    public override char? Shortcut => 'S';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new ResizeToolSessionViewModel(shell, doc);
}
