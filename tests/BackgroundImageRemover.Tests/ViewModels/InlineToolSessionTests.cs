using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.ViewModels;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Left-click (GIMP/Photoshop-style) tool activation: opens the tool session inline in the
/// current document tab instead of a separate tab. See <see cref="DocumentViewModel.InlineToolSession"/>,
/// <see cref="ShellViewModel.OpenToolInline"/> and <see cref="ShellViewModel.CloseTabDirect"/>.
/// </summary>
public class InlineToolSessionTests
{
    private sealed class UnusedDialogService : IDialogService
    {
        public string? ShowOpenImageDialog() => throw new NotImplementedException();
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG", string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG", string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowSaveWebpDialog(string? suggestedFileName, string title = "Export WebP", string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => throw new NotImplementedException();
        public string? ShowOpenProjectDialog() => throw new NotImplementedException();
        public string? ShowSaveProjectDialog(string? suggestedFileName) => throw new NotImplementedException();
        public BackgroundImageRemover.Models.BatchExportOptions? ShowBatchOptionsDialog() => null;
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => throw new NotImplementedException();
        public void ShowPreferencesDialog() { }
        public bool ConfirmRestoreRecovery(int documentCount) => false;
    }

    private sealed class FakeImageLoaderService : BackgroundImageRemover.Services.ImageIo.IImageLoaderService
    {
        public Task<Models.LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new Models.LoadedImage(path, new OpenCvSharp.Mat(4, 4, OpenCvSharp.MatType.CV_8UC3)));

        public Task<Models.LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new Models.LoadedImage(sourceName, new OpenCvSharp.Mat(4, 4, OpenCvSharp.MatType.CV_8UC3)));

        public Task<Models.LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new Models.LoadedImage(sourceName, new OpenCvSharp.Mat(4, 4, OpenCvSharp.MatType.CV_8UC3)));
    }

    private static ShellViewModel CreateShell()
    {
        var settings = new FakeSettingsService();
        var log = new FakeFileLogService();
        var modelCache = new FakeModelCacheService();
        var onnxEngine = new BackgroundImageRemover.Services.Onnx.OnnxInferenceEngine(modelCache, log);
        var samEngine = new BackgroundImageRemover.Services.Sam.SamInferenceEngine(modelCache);
        var onnxStrategy = new BackgroundImageRemover.Services.Strategies.OnnxStrategy(onnxEngine);
        var grabCutStrategy = new BackgroundImageRemover.Services.Strategies.GrabCutStrategy();
        var samStrategy = new BackgroundImageRemover.Services.Strategies.SamStrategy(samEngine);
        var uncropFillService = new FakeUncropFillService();
        var imageLoader = new FakeImageLoaderService();
        var imageExporter = new FakeImageExportService();
        var downscaler = new FakeDownscaleService();

        Func<DocumentViewModel> docFactory = () => new DocumentViewModel(
            imageLoader,
            imageExporter,
            downscaler,
            new UnusedDialogService(),
            new FakeBatchProcessingService(),
            settings,
            new FakeProjectService(),
            log,
            Array.Empty<BackgroundImageRemover.Services.Strategies.IBackgroundRemovalStrategy>(),
            onnxStrategy,
            grabCutStrategy,
            samStrategy,
            uncropFillService);

        return new ShellViewModel(
            docFactory,
            () => throw new NotImplementedException("Uncrop factory not needed for this test"),
            new UnusedDialogService(),
            settings,
            downscaler,
            log,
            Array.Empty<BackgroundImageRemover.Services.Strategies.IBackgroundRemovalStrategy>(),
            onnxStrategy,
            grabCutStrategy,
            samStrategy,
            uncropFillService,
            imageLoader,
            imageExporter);
    }

    private static async Task<(ShellViewModel Shell, DocumentViewModel Doc)> CreateShellWithOpenDocumentAsync()
    {
        var shell = CreateShell();
        await shell.OpenInNewTabAsync("photo.png");
        var doc = Assert.IsType<DocumentViewModel>(Assert.Single(shell.Documents));
        return (shell, doc);
    }

    [Fact]
    public async Task Select_OnGenericTool_OpensItInlineWithoutAddingATab()
    {
        var (shell, doc) = await CreateShellWithOpenDocumentAsync();
        var sketch = shell.ToolDefinitions.Single(t => t.Id == "Tool.Sketch");

        sketch.Select(doc);

        Assert.IsType<SketchToolSessionViewModel>(doc.InlineToolSession);
        Assert.True(doc.HasInlineToolSession);
        Assert.Single(shell.Documents); // no new tab
        Assert.Same(doc, shell.SelectedDocument); // selection unchanged
    }

    [Theory]
    [InlineData("Tool.Uncrop")]
    [InlineData("Tool.Retouch")]
    [InlineData("Tool.Adjustments")]
    public async Task Select_OnSpecialBespokeInlineTool_DoesNotPopulateInlineToolSession(string id)
    {
        var (shell, doc) = await CreateShellWithOpenDocumentAsync();
        var special = shell.ToolDefinitions.Single(t => t.Id == id);

        special.Select(doc);

        Assert.Null(doc.InlineToolSession);
        Assert.False(doc.HasInlineToolSession);
    }

    [Fact]
    public async Task Select_OnBackgroundRemovalStrategyIcon_DoesNotPopulateInlineToolSession()
    {
        var (shell, doc) = await CreateShellWithOpenDocumentAsync();
        var onnx = shell.ToolDefinitions.Single(t => t.Id == "Strategy.Onnx");

        onnx.Select(doc);

        Assert.Null(doc.InlineToolSession);
        Assert.Equal(BackgroundImageRemover.Models.EditorTool.RemoveBackground, doc.ActiveTool);
    }

    [Fact]
    public async Task ApplyOrCancel_OnInlineSession_ClearsInlineToolSessionAndLeavesDocumentsUntouched()
    {
        var (shell, doc) = await CreateShellWithOpenDocumentAsync();
        var sketch = shell.ToolDefinitions.Single(t => t.Id == "Tool.Sketch");
        sketch.Select(doc);
        var session = Assert.IsType<SketchToolSessionViewModel>(doc.InlineToolSession);

        session.Cancel();

        Assert.Null(doc.InlineToolSession);
        Assert.False(doc.HasInlineToolSession);
        Assert.Single(shell.Documents); // still just the parent document
        Assert.Same(doc, shell.SelectedDocument);
    }

    [Fact]
    public async Task Select_OnADifferentGenericTool_ReplacesThePreviousInlineSession()
    {
        var (shell, doc) = await CreateShellWithOpenDocumentAsync();
        var sketch = shell.ToolDefinitions.Single(t => t.Id == "Tool.Sketch");
        var blur = shell.ToolDefinitions.Single(t => t.Id == "Tool.Blur");

        sketch.Select(doc);
        var first = doc.InlineToolSession;
        Assert.IsType<SketchToolSessionViewModel>(first);

        blur.Select(doc);

        Assert.NotSame(first, doc.InlineToolSession);
        Assert.IsType<BlurToolSessionViewModel>(doc.InlineToolSession);
        Assert.Single(shell.Documents); // switching tools inline never touches the tab list
    }

    [Fact]
    public async Task RequestOpen_OnGenericTool_StillOpensASeparateTab_RegressionGuard()
    {
        var (shell, doc) = await CreateShellWithOpenDocumentAsync();
        var sketch = shell.ToolDefinitions.Single(t => t.Id == "Tool.Sketch");

        sketch.RequestOpen(doc);

        Assert.Equal(2, shell.Documents.Count);
        Assert.IsType<SketchToolSessionViewModel>(doc.ActiveToolSession);
        Assert.Same(doc.ActiveToolSession, shell.SelectedDocument);
        Assert.Null(doc.InlineToolSession); // middle-click path must not touch inline state
    }
}
