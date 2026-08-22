using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class ShapeToolDefinition : EditorToolDefinitionBase
{
    public ShapeToolDefinition() : base(EditorTool.Shape)
    {
    }

    public override string DisplayName => "Shape";
    public override string Category => "Drawing";
    public override int Order => 2;
    public override string IconResourceKey => "ShapeIcon";
    public override string ToolTip => "Draw a rectangle, ellipse, line or arrow";
    public override char? Shortcut => null;

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new ShapeToolSessionViewModel(shell, doc);
}
