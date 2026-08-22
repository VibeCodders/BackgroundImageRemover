using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class EdgeContourToolDefinition : StrategyToolDefinitionBase
{
    public EdgeContourToolDefinition(
        IDownscaleService downscaler, IDialogService dialogs, IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies, OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy, SamStrategy samStrategy)
        : base(StrategyKind.EdgeContour, downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy)
    {
    }

    public override int Order => 9;
    public override string IconResourceKey => "EdgeContourIcon";
    public override string DisplayName => "Edge / Contour";
    public override string ToolTip => "Edge / Contour (Canny outline + largest region)";
}
