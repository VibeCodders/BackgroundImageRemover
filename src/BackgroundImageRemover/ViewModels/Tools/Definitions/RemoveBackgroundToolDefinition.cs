using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Strategies;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

/// <summary>
/// The plain "Remove Background" dispatch target (opened e.g. from a menu, without picking a
/// specific strategy first) -- defaults to ChromaKey, matching the previous switch-based
/// behavior. Not shown in the palette: users pick one of the <see cref="StrategyToolDefinitionBase"/>
/// icons instead, GIMP-style.
/// </summary>
public sealed class RemoveBackgroundToolDefinition : EditorToolDefinitionBase
{
    private readonly IDownscaleService _downscaler;
    private readonly IDialogService _dialogs;
    private readonly IFileLogService _log;
    private readonly IEnumerable<IBackgroundRemovalStrategy> _strategies;
    private readonly OnnxStrategy _onnxStrategy;
    private readonly GrabCutStrategy _grabCutStrategy;
    private readonly SamStrategy _samStrategy;

    public RemoveBackgroundToolDefinition(
        IDownscaleService downscaler, IDialogService dialogs, IFileLogService log,
        IEnumerable<IBackgroundRemovalStrategy> strategies, OnnxStrategy onnxStrategy,
        GrabCutStrategy grabCutStrategy, SamStrategy samStrategy)
        : base(EditorTool.RemoveBackground)
    {
        _downscaler = downscaler;
        _dialogs = dialogs;
        _log = log;
        _strategies = strategies;
        _onnxStrategy = onnxStrategy;
        _grabCutStrategy = grabCutStrategy;
        _samStrategy = samStrategy;
    }

    public override string DisplayName => "Remove Background";
    public override string Category => "Background Removal";
    public override int Order => -1;
    public override string IconResourceKey => "ChromaKeyIcon";
    public override string ToolTip => "Remove Background";
    public override char? Shortcut => null;
    public override bool ShowInPalette => false;

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new BackgroundRemoverToolSessionViewModel(
            shell, doc, _downscaler, _dialogs, _log, _strategies, _onnxStrategy, _grabCutStrategy, _samStrategy,
            StrategyKind.ChromaKey);
}
