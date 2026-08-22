using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class OnnxToolDefinition : StrategyToolDefinitionBase
{
    public OnnxToolDefinition(
        IDownscaleService downscaler, IDialogService dialogs, IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies, OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy, SamStrategy samStrategy)
        : base(StrategyKind.Onnx, downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy)
    {
    }

    public override int Order => 0;
    public override string IconResourceKey => "OnnxIcon";
    public override string DisplayName => "AI Background Removal";
    public override string ToolTip => "AI Background Removal (ONNX)";
}
