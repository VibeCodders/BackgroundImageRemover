using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class PerspectiveToolDefinition : EditorToolDefinitionBase
{
    public PerspectiveToolDefinition() : base(EditorTool.Perspective)
    {
    }

    public override string DisplayName => "Perspective";
    public override string Category => "Transform";
    public override int Order => 3;
    public override string IconResourceKey => "PerspectiveIcon";
    public override string ToolTip => "Perspective (P)";
    public override char? Shortcut => 'P';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new PerspectiveToolSessionViewModel(shell, doc);
}
