using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Logging;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class AdjustmentsToolDefinition : EditorToolDefinitionBase
{
    private readonly IFileLogService _log;

    public AdjustmentsToolDefinition(IFileLogService log) : base(EditorTool.Adjustments)
    {
        _log = log;
    }

    public override string DisplayName => "Adjustments";
    public override string Category => "Color & Adjust";
    public override int Order => 0;
    public override string IconResourceKey => "AdjustmentsIcon";
    public override string ToolTip => "Adjustments (A)";
    public override char? Shortcut => 'A';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new AdjustmentsToolSessionViewModel(shell, doc, _log);
}
