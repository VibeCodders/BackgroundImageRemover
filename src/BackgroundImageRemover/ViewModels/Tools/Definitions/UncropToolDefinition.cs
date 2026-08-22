using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Outpaint;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class UncropToolDefinition : EditorToolDefinitionBase
{
    private readonly IUncropFillService _uncropFillService;
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;
    private readonly IImageExportService _imageExporter;
    private readonly IFileLogService _log;

    public UncropToolDefinition(
        IUncropFillService uncropFillService, IDialogService dialogs, IImageLoaderService imageLoader,
        IImageExportService imageExporter, IFileLogService log)
        : base(EditorTool.Uncrop)
    {
        _uncropFillService = uncropFillService;
        _dialogs = dialogs;
        _imageLoader = imageLoader;
        _imageExporter = imageExporter;
        _log = log;
    }

    public override string DisplayName => "Uncrop / Expand";
    public override string Category => "Transform";
    public override int Order => 4;
    public override string IconResourceKey => "UncropIcon";
    public override string ToolTip => "Uncrop / Expand (U)";
    public override char? Shortcut => 'U';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new UncropToolSessionViewModel(shell, doc, _uncropFillService, _dialogs, _imageLoader, _imageExporter, _log);
}
