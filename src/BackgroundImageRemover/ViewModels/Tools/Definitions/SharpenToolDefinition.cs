using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class SharpenToolDefinition : EditorToolDefinitionBase
{
    public SharpenToolDefinition() : base(EditorTool.Sharpen)
    {
    }

    public override string DisplayName => "Sharpen";
    public override string Category => "Color & Adjust";
    public override int Order => 4;
    public override string IconResourceKey => "SharpenIcon";
    public override string ToolTip => "Sharpen (Z)";
    public override char? Shortcut => 'Z';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new SharpenToolSessionViewModel(shell, doc);
}
