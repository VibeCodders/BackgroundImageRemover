using System.Runtime.ExceptionServices;
using System.Threading;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.Tests.Helpers;
using BackgroundImageRemover.ViewModels;
using BackgroundImageRemover.ViewModels.Tools;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// The per-fill-mode Uncrop toolbar entries (UncropMirror, UncropInpaint, ...): each is
/// registered as an <see cref="UncropModeToolDefinition"/> in its own "Uncrop" toolbar category,
/// left-click pre-sets the document's inline fill mode, and middle-click opens the Uncrop tool
/// session with that fill mode selected.
/// </summary>
public class UncropModeToolDefinitionTests
{
    private static readonly (string Id, EditorTool Tool, UncropFillMode FillMode)[] Variants =
    [
        ("Tool.UncropMirror", EditorTool.UncropMirror, UncropFillMode.Mirror),
        ("Tool.UncropInpaint", EditorTool.UncropInpaint, UncropFillMode.Inpaint),
        ("Tool.UncropSolidColor", EditorTool.UncropSolidColor, UncropFillMode.SolidColor),
        ("Tool.UncropReplicate", EditorTool.UncropReplicate, UncropFillMode.Replicate),
        ("Tool.UncropWrap", EditorTool.UncropWrap, UncropFillMode.Wrap),
        ("Tool.UncropZoomBlur", EditorTool.UncropZoomBlur, UncropFillMode.ZoomBlur),
        ("Tool.UncropEdgeGradient", EditorTool.UncropEdgeGradient, UncropFillMode.EdgeGradient),
        ("Tool.UncropPatchSynthesis", EditorTool.UncropPatchSynthesis, UncropFillMode.PatchSynthesis)
    ];

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

    private sealed class FakeImageLoaderService : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(path, new OpenCvSharp.Mat(4, 4, OpenCvSharp.MatType.CV_8UC3)));

        public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(sourceName, new OpenCvSharp.Mat(4, 4, OpenCvSharp.MatType.CV_8UC3)));

        public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new LoadedImage(sourceName, new OpenCvSharp.Mat(4, 4, OpenCvSharp.MatType.CV_8UC3)));
    }

    private static ShellViewModel CreateShell()
    {
        var settings = new FakeSettingsService();
        var log = new FakeFileLogService();
        var modelCache = new FakeModelCacheService();
        var onnxEngine = new OnnxInferenceEngine(modelCache, log);
        var samEngine = new SamInferenceEngine(modelCache);
        var onnxStrategy = new OnnxStrategy(onnxEngine);
        var grabCutStrategy = new GrabCutStrategy();
        var samStrategy = new SamStrategy(samEngine);
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
            Array.Empty<IBackgroundRemovalStrategy>(),
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
            Array.Empty<IBackgroundRemovalStrategy>(),
            onnxStrategy,
            grabCutStrategy,
            samStrategy,
            uncropFillService,
            imageLoader,
            imageExporter);
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [Fact]
    public void AllUncropFillModes_AreRegistered_AsUncropCategoryToolbarEntries()
    {
        var shell = CreateShell();

        // The general entry moved into the same category as its per-mode siblings.
        Assert.Equal("Uncrop", shell.ToolDefinitions.Single(t => t.Id == "Tool.Uncrop").Category);

        foreach (var (id, tool, fillMode) in Variants)
        {
            var definition = Assert.IsType<UncropModeToolDefinition>(shell.ToolDefinitions.Single(t => t.Id == id));
            Assert.Equal("Uncrop", definition.Category);
            Assert.Equal(fillMode, definition.FillMode);
            Assert.False(definition.OpensInlineOnSelect);
            Assert.True(definition.ShowInPalette);
            Assert.NotEmpty(definition.IconResourceKey);
            Assert.True(definition.IconResourceKey.StartsWith("Uncrop", StringComparison.Ordinal));

            // Radio-button semantics: exactly the clicked variant is active; the other uncrop
            // variants and unrelated tools are not.
            Assert.True(definition.IsActive(tool, StrategyKind.ChromaKey));
            Assert.False(definition.IsActive(EditorTool.Uncrop, StrategyKind.ChromaKey));
            Assert.False(definition.IsActive(EditorTool.Transform, StrategyKind.ChromaKey));
        }
    }

    [Fact]
    public void Select_SetsTheDocumentFillModeAndActiveTool_WithoutOpeningASession()
    {
        RunOnSta(() =>
        {
            var shell = CreateShell();
            shell.OpenInNewTabAsync("photo.png").GetAwaiter().GetResult();
            var doc = Assert.IsType<DocumentViewModel>(Assert.Single(shell.Documents));
            var zoomBlur = Assert.IsType<UncropModeToolDefinition>(shell.ToolDefinitions.Single(t => t.Id == "Tool.UncropZoomBlur"));

            // Pick a different fill mode first so the preset actually changes something.
            doc.UncropOptions.SelectedFillMode = UncropFillMode.SolidColor;
            zoomBlur.Select(doc);

            Assert.Equal(EditorTool.UncropZoomBlur, doc.ActiveTool);
            Assert.Equal(UncropFillMode.ZoomBlur, doc.UncropOptions.SelectedFillMode);
            Assert.Null(doc.InlineToolSession); // bespoke inline panel, no inline session tab
            Assert.Null(doc.ActiveToolSession); // left-click never opens a tab
        });
    }

    [Fact]
    public void RequestOpen_OpensASessionTab_PresetToTheFillMode()
    {
        RunOnSta(() =>
        {
            var shell = CreateShell();
            shell.OpenInNewTabAsync("photo.png").GetAwaiter().GetResult();
            var doc = Assert.IsType<DocumentViewModel>(Assert.Single(shell.Documents));
            var inpaint = Assert.IsType<UncropModeToolDefinition>(shell.ToolDefinitions.Single(t => t.Id == "Tool.UncropInpaint"));

            inpaint.RequestOpen(doc);

            var session = Assert.IsType<UncropToolSessionViewModel>(doc.ActiveToolSession);
            Assert.Equal(UncropFillMode.Inpaint, session.Options.SelectedFillMode);
            Assert.True(session.IsImageLoaded);
            Assert.Equal(2, shell.Documents.Count); // session opened as a separate tab
        });
    }
}
