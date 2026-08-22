using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class FloodFillToolDefinition : StrategyToolDefinitionBase
{
    public FloodFillToolDefinition(
        IDownscaleService downscaler, IDialogService dialogs, IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies, OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy, SamStrategy samStrategy)
        : base(StrategyKind.FloodFill, downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy)
    {
    }

    public override int Order => 5;
    public override string IconResourceKey => "FloodFillIcon";
    public override string DisplayName => "Flood Fill";
    public override string ToolTip => "Flood Fill (from border)";
}
