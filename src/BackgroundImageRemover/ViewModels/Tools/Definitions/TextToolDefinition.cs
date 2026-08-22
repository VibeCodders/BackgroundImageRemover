using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class TextToolDefinition : EditorToolDefinitionBase
{
    public TextToolDefinition() : base(EditorTool.Text)
    {
    }

    public override string DisplayName => "Text";
    public override string Category => "Text & Decor";
    public override int Order => 0;
    public override string IconResourceKey => "TextIcon";
    public override string ToolTip => "Text (X)";
    public override char? Shortcut => 'X';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new TextToolSessionViewModel(shell, doc);
}
