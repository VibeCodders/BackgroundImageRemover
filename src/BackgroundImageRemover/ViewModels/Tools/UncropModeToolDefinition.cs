using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;

namespace BackgroundImageRemover.ViewModels.Tools;

/// <summary>
/// A toolbar entry for one specific Uncrop fill mode (Mirror, Inpaint, Solid Color, Replicate,
/// Wrap, Zoom Blur, Edge Gradient, Patch Synthesis). It opens the same Uncrop tool session as the
/// general "Uncrop / Expand" entry but with <see cref="UncropOptionsViewModel.SelectedFillMode"/>
/// pre-selected, and a left-click also switches the document's inline Uncrop panel to that fill
/// mode so the on-canvas flow matches the clicked icon.
/// </summary>
public sealed class UncropModeToolDefinition : EditorToolDefinitionBase
{
    private readonly string _displayName;
    private readonly int _order;
    private readonly string _iconResourceKey;
    private readonly string _toolTip;
    private readonly IUncropFillService _fillService;
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;
    private readonly IFileLogService _log;
    private readonly IAiOutpaintService? _aiOutpaintService;

    public UncropModeToolDefinition(
        EditorTool tool,
        UncropFillMode fillMode,
        string displayName,
        int order,
        string iconResourceKey,
        string toolTip,
        IUncropFillService fillService,
        IDialogService dialogs,
        IImageLoaderService imageLoader,
        IImageExportService imageExporter,
        IFileLogService log,
        IAiOutpaintService? aiOutpaintService = null)
        : base(tool)
    {
        FillMode = fillMode;
        _displayName = displayName;
        _order = order;
        _iconResourceKey = iconResourceKey;
        _toolTip = toolTip;
        _fillService = fillService;
        _dialogs = dialogs;
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _log = log;
        _aiOutpaintService = aiOutpaintService;
    }

    /// <summary>The uncrop fill method this toolbar entry stands for.</summary>
    public UncropFillMode FillMode { get; }

    public override string DisplayName => _displayName;
    public override string Category => "Uncrop";
    public override int Order => _order;
    public override string IconResourceKey => _iconResourceKey;
    public override string ToolTip => _toolTip;
    public override char? Shortcut => null;

    /// <summary>Like the general Uncrop entry: the inline canvas panel is driven by
    /// <see cref="DocumentViewModel.UncropOptions"/> directly, not by an inline session tab.</summary>
    public override bool OpensInlineOnSelect => false;

    public override void Select(DocumentViewModel doc)
    {
        // The inline Uncrop panel (shown when the variant is active) must default to the clicked
        // fill method, not whatever the last operation left behind.
        doc.UncropOptions.SelectedFillMode = FillMode;
        base.Select(doc);
    }

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new UncropToolSessionViewModel(shell, doc, _fillService, _dialogs, _imageLoader, _imageExporter, _log, FillMode, _aiOutpaintService);
}
