using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class RedEyeToolDefinition : EditorToolDefinitionBase
{
    public RedEyeToolDefinition() : base(EditorTool.RedEye)
    {
    }

    public override string DisplayName => "Red Eye";
    public override string Category => "Retouch";
    public override int Order => 8;
    public override string IconResourceKey => "RedEyeIcon";
    public override string ToolTip => "Remove red eyes (R)";
    public override char? Shortcut => 'R';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new RedEyeToolSessionViewModel(shell, doc);
}
