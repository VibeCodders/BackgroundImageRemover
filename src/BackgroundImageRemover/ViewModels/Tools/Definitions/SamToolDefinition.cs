using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class SamToolDefinition : StrategyToolDefinitionBase
{
    public SamToolDefinition(
        IDownscaleService downscaler, IDialogService dialogs, IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies, OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy, SamStrategy samStrategy)
        : base(StrategyKind.Sam, downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy)
    {
    }

    public override int Order => 1;
    public override string IconResourceKey => "SamIcon";
    public override string DisplayName => "Segment Anything";
    public override string ToolTip => "Segment Anything (click to select)";
}
