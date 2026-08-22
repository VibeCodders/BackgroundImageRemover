using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class CloneStampToolDefinition : EditorToolDefinitionBase
{
    public CloneStampToolDefinition() : base(EditorTool.CloneStamp)
    {
    }

    public override string DisplayName => "Clone Stamp";
    public override string Category => "Paint & Retouch";
    public override int Order => 7;
    public override string IconResourceKey => "CloneStampIcon";
    public override string ToolTip => "Clone Stamp (S)";
    public override char? Shortcut => 'S';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new CloneStampToolSessionViewModel(shell, doc);
}
