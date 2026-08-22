using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class EmojiToolDefinition : EditorToolDefinitionBase
{
    public EmojiToolDefinition() : base(EditorTool.Emoji)
    {
    }

    public override string DisplayName => "Emoji";
    public override string Category => "Text & Decor";
    public override int Order => 1;
    public override string IconResourceKey => "EmojiIcon";
    public override string ToolTip => "Emoji (Y)";
    public override char? Shortcut => 'Y';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new EmojiToolSessionViewModel(shell, doc);
}
