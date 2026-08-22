using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class RetouchToolDefinition : EditorToolDefinitionBase
{
    public RetouchToolDefinition() : base(EditorTool.Retouch)
    {
    }

    public override string DisplayName => "Retouch & Brush";
    public override string Category => "Paint & Retouch";
    public override int Order => 0;
    public override string IconResourceKey => "RetouchIcon";
    public override string ToolTip => "Retouch & Brush (B)";
    public override char? Shortcut => 'B';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new RetouchToolSessionViewModel(shell, doc);
}
