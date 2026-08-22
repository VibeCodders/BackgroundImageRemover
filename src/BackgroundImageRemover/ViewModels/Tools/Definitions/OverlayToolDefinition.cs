using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class OverlayToolDefinition : EditorToolDefinitionBase
{
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;

    public OverlayToolDefinition(IDialogService dialogs, IImageLoaderService imageLoader) : base(EditorTool.Overlay)
    {
        _dialogs = dialogs;
        _imageLoader = imageLoader;
    }

    public override string DisplayName => "Overlay";
    public override string Category => "Composite";
    public override int Order => 1;
    public override string IconResourceKey => "OverlayIcon";
    public override string ToolTip => "Overlay (O)";
    public override char? Shortcut => 'O';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new OverlayToolSessionViewModel(shell, doc, _dialogs, _imageLoader);
}
