using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class VignetteToolDefinition : EditorToolDefinitionBase
{
    public VignetteToolDefinition() : base(EditorTool.Vignette)
    {
    }

    public override string DisplayName => "Vignette";
    public override string Category => "Color & Adjust";
    public override int Order => 5;
    public override string IconResourceKey => "VignetteIcon";
    public override string ToolTip => "Vignette (V)";
    public override char? Shortcut => 'V';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new VignetteToolSessionViewModel(shell, doc);
}
