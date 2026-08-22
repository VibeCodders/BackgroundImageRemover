using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class BlurToolDefinition : EditorToolDefinitionBase
{
    public BlurToolDefinition() : base(EditorTool.Blur)
    {
    }

    public override string DisplayName => "Blur";
    public override string Category => "Color & Adjust";
    public override int Order => 3;
    public override string IconResourceKey => "BlurIcon";
    public override string ToolTip => "Blur (W)";
    public override char? Shortcut => 'W';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new BlurToolSessionViewModel(shell, doc);
}
