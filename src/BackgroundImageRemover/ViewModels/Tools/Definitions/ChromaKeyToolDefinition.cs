using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class ChromaKeyToolDefinition : StrategyToolDefinitionBase
{
    public ChromaKeyToolDefinition(
        IDownscaleService downscaler, IDialogService dialogs, IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies, OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy, SamStrategy samStrategy)
        : base(StrategyKind.ChromaKey, downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy)
    {
    }

    public override int Order => 3;
    public override string IconResourceKey => "ChromaKeyIcon";
    public override string DisplayName => "Chroma Key";
    public override string ToolTip => "Chroma Key (solid color backdrop)";
}
