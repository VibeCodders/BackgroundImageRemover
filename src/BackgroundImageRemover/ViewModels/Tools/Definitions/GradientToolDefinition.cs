using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class GradientToolDefinition : EditorToolDefinitionBase
{
    public GradientToolDefinition() : base(EditorTool.Gradient)
    {
    }

    public override string DisplayName => "Gradient";
    public override string Category => "Drawing";
    public override int Order => 1;
    public override string IconResourceKey => "GradientIcon";
    public override string ToolTip => "Overlay a linear or radial gradient";
    public override char? Shortcut => null;

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new GradientToolSessionViewModel(shell, doc);
}
