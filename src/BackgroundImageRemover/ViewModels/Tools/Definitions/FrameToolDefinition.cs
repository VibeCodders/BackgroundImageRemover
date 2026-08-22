using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class FrameToolDefinition : EditorToolDefinitionBase
{
    public FrameToolDefinition() : base(EditorTool.Frame)
    {
    }

    public override string DisplayName => "Frame";
    public override string Category => "Composite";
    public override int Order => 2;
    public override string IconResourceKey => "FrameIcon";
    public override string ToolTip => "Frame (G)";
    public override char? Shortcut => 'G';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new FrameToolSessionViewModel(shell, doc);
}
