using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class HealToolDefinition : EditorToolDefinitionBase
{
    public HealToolDefinition() : base(EditorTool.Heal)
    {
    }

    public override string DisplayName => "Heal";
    public override string Category => "Paint & Retouch";
    public override int Order => 1;
    public override string IconResourceKey => "HealIcon";
    public override string ToolTip => "Heal (H)";
    public override char? Shortcut => 'H';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new HealToolSessionViewModel(shell, doc);
}
