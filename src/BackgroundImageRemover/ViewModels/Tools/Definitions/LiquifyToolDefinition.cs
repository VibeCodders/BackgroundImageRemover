using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class LiquifyToolDefinition : EditorToolDefinitionBase
{
    public LiquifyToolDefinition() : base(EditorTool.Liquify)
    {
    }

    public override string DisplayName => "Liquify";
    public override string Category => "Paint & Retouch";
    public override int Order => 2;
    public override string IconResourceKey => "LiquifyIcon";
    public override string ToolTip => "Liquify (J)";
    public override char? Shortcut => 'J';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new LiquifyToolSessionViewModel(shell, doc);
}
