using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class LevelsToolDefinition : EditorToolDefinitionBase
{
    public LevelsToolDefinition() : base(EditorTool.Levels)
    {
    }

    public override string DisplayName => "Levels";
    public override string Category => "Color & Adjust";
    public override int Order => 1;
    public override string IconResourceKey => "LevelsIcon";
    public override string ToolTip => "Levels (L)";
    public override char? Shortcut => 'L';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new LevelsToolSessionViewModel(shell, doc);
}
