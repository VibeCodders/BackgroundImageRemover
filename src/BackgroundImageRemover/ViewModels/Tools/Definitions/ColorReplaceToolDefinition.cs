using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class ColorReplaceToolDefinition : EditorToolDefinitionBase
{
    public ColorReplaceToolDefinition() : base(EditorTool.ColorReplace)
    {
    }

    public override string DisplayName => "Color Replace";
    public override string Category => "Paint & Retouch";
    public override int Order => 12;
    public override string IconResourceKey => "ColorReplaceIcon";
    public override string ToolTip => "Replace a target color with another color";
    public override char? Shortcut => null;

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new ColorReplaceToolSessionViewModel(shell, doc);
}
