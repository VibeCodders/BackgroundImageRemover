using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.ViewModels.Tools;

/// <summary>
/// Data-driven <see cref="IToolDefinition"/> for the background-removal strategies: each strategy
/// is its own icon in the palette (GIMP-style), but all of them open the same
/// <see cref="BackgroundRemoverToolSessionViewModel"/> pre-selected to their
/// <see cref="StrategyKind"/>. Metadata is passed to the constructor instead of a per-strategy class.
/// </summary>
public sealed class StrategyToolDefinition : StrategyToolDefinitionBase
{
    private readonly int _order;
    private readonly string _iconResourceKey;
    private readonly string _displayName;
    private readonly string _toolTip;

    public StrategyToolDefinition(
        StrategyKind strategy,
        int order,
        string iconResourceKey,
        string displayName,
        string toolTip,
        IDownscaleService downscaler,
        IDialogService dialogs,
        IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies,
        OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy,
        SamStrategy samStrategy)
        : base(strategy, downscaler, dialogs, log, strategies, onnxStrategy, grabCutStrategy, samStrategy)
    {
        _order = order;
        _iconResourceKey = iconResourceKey;
        _displayName = displayName;
        _toolTip = toolTip;
    }

    public override int Order => _order;
    public override string IconResourceKey => _iconResourceKey;
    public override string DisplayName => _displayName;
    public override string ToolTip => _toolTip;
}
