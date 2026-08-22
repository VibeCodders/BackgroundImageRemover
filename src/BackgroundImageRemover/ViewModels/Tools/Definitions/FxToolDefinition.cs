using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class FxToolDefinition : EditorToolDefinitionBase
{
    public FxToolDefinition() : base(EditorTool.Fx)
    {
    }

    public override string DisplayName => "FX";
    public override string Category => "Filters & FX";
    public override int Order => 1;
    public override string IconResourceKey => "FxIcon";
    public override string ToolTip => "FX (K)";
    public override char? Shortcut => 'K';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new FxToolSessionViewModel(shell, doc);
}
