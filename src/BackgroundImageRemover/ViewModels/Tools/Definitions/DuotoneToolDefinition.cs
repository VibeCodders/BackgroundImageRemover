using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class DuotoneToolDefinition : EditorToolDefinitionBase
{
    public DuotoneToolDefinition() : base(EditorTool.Duotone)
    {
    }

    public override string DisplayName => "Duotone";
    public override string Category => "Color & Adjust";
    public override int Order => 6;
    public override string IconResourceKey => "DuotoneIcon";
    public override string ToolTip => "Map brightness to a two-color palette";
    public override char? Shortcut => null;

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new DuotoneToolSessionViewModel(shell, doc);
}
