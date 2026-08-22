using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class FiltersToolDefinition : EditorToolDefinitionBase
{
    public FiltersToolDefinition() : base(EditorTool.Filters)
    {
    }

    public override string DisplayName => "Filters";
    public override string Category => "Filters & FX";
    public override int Order => 0;
    public override string IconResourceKey => "FiltersIcon";
    public override string ToolTip => "Filters (F)";
    public override char? Shortcut => 'F';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new FiltersToolSessionViewModel(shell, doc);
}
