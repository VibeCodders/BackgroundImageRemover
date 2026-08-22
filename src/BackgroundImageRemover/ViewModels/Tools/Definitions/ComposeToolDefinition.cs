using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;

namespace BackgroundImageRemover.ViewModels.Tools.Definitions;

public sealed class ComposeToolDefinition : EditorToolDefinitionBase
{
    private readonly IDialogService _dialogs;
    private readonly IImageLoaderService _imageLoader;

    public ComposeToolDefinition(IDialogService dialogs, IImageLoaderService imageLoader) : base(EditorTool.Compose)
    {
        _dialogs = dialogs;
        _imageLoader = imageLoader;
    }

    public override string DisplayName => "Compose";
    public override string Category => "Composite";
    public override int Order => 0;
    public override string IconResourceKey => "ComposeIcon";
    public override string ToolTip => "Compose (C)";
    public override char? Shortcut => 'C';

    public override IToolSessionTab OpenSession(ShellViewModel shell, DocumentViewModel doc)
        => new ComposeToolSessionViewModel(shell, doc, _dialogs, _imageLoader);
}
