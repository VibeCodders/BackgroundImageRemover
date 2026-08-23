using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.ViewModels.Tools;

/// <summary>
/// Base for the background-removal strategies: each is its own icon in the palette
/// (GIMP-style), but all of them open the same <see cref="BackgroundRemoverToolSessionViewModel"/>
/// pre-selected to their own <see cref="StrategyKind"/>.
/// </summary>
public abstract class StrategyToolDefinitionBase : IToolDefinition
{
    private readonly IDownscaleService _downscaler;
    private readonly IDialogService _dialogs;
    private readonly IFileLogService _log;
    private readonly IEnumerable<IBackgroundRemovalStrategy> _strategies;
    private readonly OnnxStrategy _onnxStrategy;
    private readonly GrabCutStrategy _grabCutStrategy;
    private readonly SamStrategy _samStrategy;

    protected StrategyToolDefinitionBase(
        StrategyKind strategy,
        IDownscaleService downscaler,
        IDialogService dialogs,
        IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies,
        OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy,
        SamStrategy samStrategy)
    {
        Strategy = strategy;
        _downscaler = downscaler;
        _dialogs = dialogs;
        _log = log;
        _strategies = strategies;
        _onnxStrategy = onnxStrategy;
        _grabCutStrategy = grabCutStrategy;
        _samStrategy = samStrategy;
    }

    public StrategyKind Strategy { get; }
    public string Id => $"Strategy.{Strategy}";
    public string Category => "Background Removal";
    public abstract int Order { get; }
    public abstract string IconResourceKey { get; }
    public abstract string ToolTip { get; }
    public abstract string DisplayName { get; }
    public virtual char? Shortcut => null;
    public bool ShowInPalette => true;
    public bool OpensInlineOnSelect => false;

    public bool IsActive(EditorTool activeTool, StrategyKind selectedStrategy)
        => activeTool == EditorTool.RemoveBackground && selectedStrategy == Strategy;

    public void Select(DocumentViewModel doc)
    {
        doc.ActiveTool = EditorTool.RemoveBackground;
        doc.SelectedStrategy = Strategy;
    }

    public void RequestOpen(DocumentViewModel doc) => doc.OpenBackgroundRemovalToolCommand.Execute(Strategy);

    public IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new BackgroundRemoverToolSessionViewModel(
            shell, doc, _downscaler, _dialogs, _log, _strategies, _onnxStrategy, _grabCutStrategy, _samStrategy, Strategy);
}
