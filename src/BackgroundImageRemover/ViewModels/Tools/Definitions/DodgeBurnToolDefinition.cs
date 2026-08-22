using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class DodgeBurnToolDefinition : EditorToolDefinitionBase
{
    public DodgeBurnToolDefinition() : base(EditorTool.DodgeBurn)
    {
    }

    public override string DisplayName => "Dodge / Burn";
    public override string Category => "Color & Adjust";
    public override int Order => 5;
    public override string IconResourceKey => "DodgeBurnIcon";
    public override string ToolTip => "Dodge and Burn (B)";
    public override char? Shortcut => 'B';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new DodgeBurnToolSessionViewModel(shell, doc);
}
