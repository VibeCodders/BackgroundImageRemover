using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Projects;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Pins the quick-rotate feature (toolbar ↺/↻): the whole document — working result, source
/// image and preview — rotates together, dimensions in the status bar stay in sync, and the
/// operation is undoable like any other edit.
/// </summary>
public class DocumentViewModelRotateTests
{
    // The loader produces a 6-wide × 4-tall image so rotation visibly swaps the dimensions.
    private const int Width = 6;
    private const int Height = 4;

    [Fact]
    public async Task Rotate90Cw_SwapsDimensionsAndUpdatesStatusBar()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");
        Assert.Equal($"{Width} × {Height}", doc.ImageDimensions);

        doc.Rotate90CwCommand.Execute(null);

        Assert.Equal(Height, doc.ImageWidth);
        Assert.Equal(Width, doc.ImageHeight);
        Assert.Equal($"{Height} × {Width}", doc.ImageDimensions);
        Assert.Contains("Rotated", doc.StatusMessage ?? "");
        Assert.Equal($"{Height} × {Width}", doc.LoadedImageForUncrop!.FullBgr.Size().Width + " × " + doc.LoadedImageForUncrop.FullBgr.Size().Height);
    }

    [Fact]
    public async Task Rotate90Ccw_SwapsDimensionsTheOtherWay()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        doc.Rotate90CcwCommand.Execute(null);

        Assert.Equal(Height, doc.ImageWidth);
        Assert.Equal(Width, doc.ImageHeight);
    }

    [Fact]
    public async Task Rotate_WithWorkingResult_RotatesWorkingAndSourceTogether()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");
        using var bgr = new Mat(Height, Width, MatType.CV_8UC3, new Scalar(255, 0, 0));
        using var alpha = new Mat(Height, Width, MatType.CV_8UC1, new Scalar(255));
        doc.ApplyToolResult(bgr.Clone(), alpha.Clone(), "Test edit");
        Assert.True(doc.HasWorkingResult);

        doc.RotateDocument(clockwise: true);

        Assert.True(doc.HasWorkingResult, "rotation must keep the working result");
        Assert.NotNull(doc.ResultBitmap);
        Assert.Equal(Height, doc.ImageWidth);
        Assert.Equal(Width, doc.ImageHeight);
        Assert.Equal(Height, doc.LoadedImageForUncrop!.FullBgr.Width);
        Assert.Equal(Width, doc.LoadedImageForUncrop.FullBgr.Height);
    }

    [Fact]
    public async Task Rotate_PreservesSourceAlphaChannel()
    {
        var doc = CreateDocument(loader: new AlphaImageLoader());
        await doc.LoadImageAsync("cutout.png");
        Assert.NotNull(doc.LoadedImageForUncrop!.FullAlpha);

        doc.RotateDocument(clockwise: true);

        Assert.NotNull(doc.LoadedImageForUncrop!.FullAlpha);
        Assert.Equal(Height, doc.LoadedImageForUncrop.FullAlpha!.Width);
        Assert.Equal(Width, doc.LoadedImageForUncrop.FullAlpha.Height);
    }

    [Fact]
    public async Task Rotate_IsUndoableAndRestoresDimensions()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        doc.Rotate90CwCommand.Execute(null);
        Assert.True(doc.UndoCommand.CanExecute(null));
        Assert.Equal($"{Height} × {Width}", doc.ImageDimensions);

        doc.UndoCommand.Execute(null);

        Assert.Equal($"{Width} × {Height}", doc.ImageDimensions);
        Assert.Equal(Width, doc.LoadedImageForUncrop!.FullBgr.Width);

        doc.RedoCommand.Execute(null);

        Assert.Equal($"{Height} × {Width}", doc.ImageDimensions);
    }

    [Fact]
    public void Rotate_CommandsAreDisabledWithoutAnImage()
    {
        var doc = CreateDocument();

        Assert.False(doc.Rotate90CwCommand.CanExecute(null));
        Assert.False(doc.Rotate90CcwCommand.CanExecute(null));
    }

    [Fact]
    public async Task Rotate_ClearsScribblesAndInteractionSeeds()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        // Scribble over the subject.
        doc.OriginalMode = InteractionMode.ScribbleForeground;
        doc.OnOriginalStrokeStart(new System.Windows.Point(3, 2));
        doc.OnOriginalStrokeEnd();
        Assert.True(doc.ScribbleManager.HasScribbles);

        // Plant a magic-wand seed.
        doc.SelectedStrategy = StrategyKind.MagicWand;
        doc.OnOriginalWandClicked(new Point(2, 2));
        Assert.True(doc.MagicWand.HasClickedPoint);

        doc.Rotate90CwCommand.Execute(null);

        // The old coordinate space is gone: seeds must be dropped, not painted over wrong pixels.
        Assert.False(doc.ScribbleManager.HasScribbles);
        Assert.False(doc.GrabCut.HasScribbles);
        Assert.False(doc.MagicWand.HasClickedPoint);
    }

    /// <summary>Regression: the status bar showed stale dimensions after a size-changing edit
    /// (crop/resize/transform) because EnsureLoadedImageMatchesWorkingSize rebuilt the source
    /// image and preview without refreshing ImageWidth/ImageHeight.</summary>
    [Fact]
    public async Task ApplyToolResult_WithDifferentSize_UpdatesStatusBarDimensions()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");
        Assert.Equal($"{Width} × {Height}", doc.ImageDimensions);

        // A 4-wide × 6-tall result (e.g. a rotate/resize/crop tool applying back).
        using var bgr = new Mat(6, 4, MatType.CV_8UC3, new Scalar(255, 0, 0));
        using var alpha = new Mat(6, 4, MatType.CV_8UC1, new Scalar(255));
        doc.ApplyToolResult(bgr.Clone(), alpha.Clone(), "Resize");

        Assert.Equal(4, doc.ImageWidth);
        Assert.Equal(6, doc.ImageHeight);
        Assert.Equal("4 × 6", doc.ImageDimensions);
    }

    [Fact]
    public async Task Undo_AfterSizeChangingEdit_RestoresDimensions()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        using var bgr = new Mat(6, 4, MatType.CV_8UC3, new Scalar(255, 0, 0));
        using var alpha = new Mat(6, 4, MatType.CV_8UC1, new Scalar(255));
        doc.ApplyToolResult(bgr.Clone(), alpha.Clone(), "Resize");
        Assert.Equal("4 × 6", doc.ImageDimensions);

        doc.UndoCommand.Execute(null);

        Assert.Equal("6 × 4", doc.ImageDimensions);
        Assert.Equal(Width, doc.LoadedImageForUncrop!.FullBgr.Width);
    }

    /// <summary>Regression: ApplyUncropAsync expanded the canvas but never refreshed the status-bar dimensions.</summary>
    [Fact]
    public async Task ApplyUncrop_ExpandingCanvas_UpdatesStatusBarDimensions()
    {
        var doc = CreateDocument(uncrop: new PaddingAwareUncropFillService());
        await doc.LoadImageAsync("photo.jpg");

        doc.UncropOptions.Padding = new CanvasPadding(10, 0, 0, 0);
        Assert.True(doc.ApplyUncropCommand.CanExecute(null));

        await doc.ApplyUncropCommand.ExecuteAsync(null);

        Assert.Equal(Width + 10, doc.ImageWidth);
        Assert.Equal(Height, doc.ImageHeight);
        Assert.Equal($"{Width + 10} × {Height}", doc.ImageDimensions);
    }

    // ---- fakes (same pattern as the other ViewModel test files) ----

    private static DocumentViewModel CreateDocument(
        IImageLoaderService? loader = null,
        IUncropFillService? uncrop = null)
    {
        var log = new FakeFileLogService();
        return new DocumentViewModel(
            loader ?? new PlainImageLoader(),
            new FakeImageExportService(),
            new FakeDownscaleService(),
            new FakeDialogService(),
            new FakeBatchProcessingService(),
            new FakeSettingsService(),
            new FakeProjectService(),
            log,
            Array.Empty<IBackgroundRemovalStrategy>(),
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), log)),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            uncrop ?? new FakeUncropFillService());
    }

    /// <summary>Returns a 6×4 opaque image, so 90° rotation visibly swaps the dimensions.</summary>
    private sealed class PlainImageLoader : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(path, new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30))));

        public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(sourceName, new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30))));

        public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new LoadedImage(sourceName, new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30))));
    }

    /// <summary>Returns a 6×4 cutout with a fully transparent alpha channel.</summary>
    private sealed class AlphaImageLoader : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
        {
            var bgr = new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30));
            var alpha = new Mat(Height, Width, MatType.CV_8UC1, new Scalar(0));
            return Task.FromResult(new LoadedImage(path, bgr, alpha));
        }

        public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
        {
            var bgr = new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30));
            var alpha = new Mat(Height, Width, MatType.CV_8UC1, new Scalar(0));
            return Task.FromResult(new LoadedImage(sourceName, bgr, alpha));
        }

        public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
        {
            var bgr = new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30));
            var alpha = new Mat(Height, Width, MatType.CV_8UC1, new Scalar(0));
            return Task.FromResult(new LoadedImage(sourceName, bgr, alpha));
        }
    }

    /// <summary>Mirror fill returns a mat of the exact padded size, so the test can assert the
    /// status bar shows the real expanded dimensions.</summary>
    private sealed class PaddingAwareUncropFillService : FakeUncropFillService
    {
        public override Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101, int blurRadius = 0, double fadeOpacity = 1.0, CancellationToken ct = default)
            => new Mat(
                sourceBgr.Height + padding.Top + padding.Bottom,
                sourceBgr.Width + padding.Left + padding.Right,
                MatType.CV_8UC3,
                Scalar.All(100));
    }

    private class FakeUncropFillService : IUncropFillService
    {
        public virtual Mat ExpandCanvas(Mat sourceBgr, CanvasPadding padding, out Mat newAreaMask)
        {
            newAreaMask = new Mat(1, 1, MatType.CV_8UC1);
            return new Mat(1, 1, MatType.CV_8UC3);
        }
        public virtual Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, bool preFillEdgeAverage = false, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public virtual Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101, int blurRadius = 0, double fadeOpacity = 1.0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public virtual Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred, Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public virtual Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, int smoothRadius = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public virtual Mat FillWrap(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public virtual Mat FillZoomBlur(Mat sourceBgr, CanvasPadding padding, int blurRadius = 25, double zoomScale = 1.25, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public virtual Mat FillEdgeGradient(Mat sourceBgr, CanvasPadding padding, UncropGradientMode gradientMode = UncropGradientMode.PerEdgeSplay, Scalar? customEndColor = null, double noiseAmount = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public virtual Mat FillPatchSynthesis(Mat sourceBgr, CanvasPadding padding, int patchSize = 32, int blendOverlap = 8, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public void Save() { }
        public void AddRecentFile(string path) { }
        public void AddRecentProject(string path) { }
        public void ClearRecentFiles() { }
        public void ClearRecentProjects() { }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? ShowOpenImageDialog() => null;
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG", string? initialDirectory = null) => null;
        public string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG", string? initialDirectory = null) => null;
        public string? ShowSaveWebpDialog(string? suggestedFileName, string title = "Export WebP", string? initialDirectory = null) => null;
        public string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => null;
        public string? ShowOpenProjectDialog() => null;
        public string? ShowSaveProjectDialog(string? suggestedFileName) => null;
        public BatchExportOptions? ShowBatchOptionsDialog() => null;
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
        public void ShowPreferencesDialog() { }
        public bool ConfirmRestoreRecovery(int documentCount) => false;
    }

    private sealed class FakeImageExportService : IImageExportService
    {
        public Task ExportPngAsync(Mat imageBgra, string destinationPath, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportJpgAsync(Mat bgr, string destinationPath, int quality = 95, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportWebpAsync(Mat bgra, string destinationPath, int quality = 90, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDownscaleService : IDownscaleService
    {
        public PreviewImage CreatePreview(Mat full, int maxDim = 800) => new(full.Clone(), 1.0);
    }

    private sealed class FakeBatchProcessingService : IBatchProcessingService
    {
        public Task RunAsync(
            IReadOnlyList<string> inputFiles,
            IBackgroundRemovalStrategy strategy,
            StrategyContext context,
            string outputFolder,
            IProgress<BatchProgress>? progress,
            CancellationToken ct,
            BatchExportOptions? exportOptions = null)
            => Task.CompletedTask;
    }

    private sealed class FakeProjectService : IProjectService
    {
        public Task SaveAsync(string path, Mat originalBgr, Mat? originalAlpha, Mat? workingBgr, Mat? workingAlpha, ProjectDocument settings, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<LoadedProject> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedProject
            {
                Settings = new ProjectDocument(),
                OriginalBgr = new Mat(1, 1, MatType.CV_8UC3)
            });
    }

    private sealed class FakeFileLogService : IFileLogService
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    private sealed class FakeModelCacheService : IModelCacheService
    {
        public string CachedModelPath(OnnxModelKind kind) => "";
        public bool IsModelCached(OnnxModelKind kind) => true;
        public Task<string> EnsureModelAvailableAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
            => Task.FromResult("");
        public Task<string> EnsureNamedFileAvailableAsync(string fileName, string url, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
            => Task.FromResult("");
        public bool IsNamedFileCached(string fileName) => true;
    }
}
