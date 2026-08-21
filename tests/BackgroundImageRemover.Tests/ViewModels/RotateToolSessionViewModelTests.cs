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
using BackgroundImageRemover.ViewModels.Tools;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Verifies the RotateToolSessionViewModel default state, live preview refresh and the
/// Apply flow that pushes the rotated result back into the parent document.
/// </summary>
public class RotateToolSessionViewModelTests
{
    // The loader produces a 6-wide × 4-tall image so 90° rotation visibly swaps the dimensions.
    private const int Width = 6;
    private const int Height = 4;

    [Fact]
    public async Task DefaultState_HasZeroAngleAndExpandOn()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        var vm = new RotateToolSessionViewModel(new FakeShell(), doc);

        Assert.Equal(0.0, vm.Angle);
        Assert.True(vm.Expand);
        Assert.NotNull(vm.ResultBitmap); // preview refreshed in ctor
        Assert.False(vm.IsDirty); // zero angle -> no pending edit
    }

    [Fact]
    public async Task ApplyCommand_WithAngle_PushesRotatedResultToParent()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        var vm = new RotateToolSessionViewModel(new FakeShell(), doc);

        vm.Angle = 90;
        vm.ApplyCommand.Execute(null);

        // The parent document must now hold a rotated working result (90° swaps dimensions).
        Assert.True(doc.HasWorkingResult);
        Assert.Equal(Height, doc.ImageWidth);  // original Height -> new Width
        Assert.Equal(Width, doc.ImageHeight); // original Width -> new Height
        Assert.Contains(doc.EditSteps, s => s.Name == "Rotate" && !s.IsUndone);
    }

    [Fact]
    public async Task ResetCommand_ClearsAngleAndRestoresPreview()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        var vm = new RotateToolSessionViewModel(new FakeShell(), doc);

        vm.Angle = 45;
        vm.Expand = false;
        Assert.True(vm.IsDirty);

        vm.ResetCommand.Execute(null);

        Assert.Equal(0.0, vm.Angle);
        Assert.True(vm.Expand);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task ChangingAngle_RefreshsPreview()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        var vm = new RotateToolSessionViewModel(new FakeShell(), doc);

        vm.Angle = 90;

        Assert.NotNull(vm.ResultBitmap);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task ApplyCommand_AtZeroAngle_DoesNotRecordEdit()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        var vm = new RotateToolSessionViewModel(new FakeShell(), doc);

        vm.ApplyCommand.Execute(null);

        Assert.False(doc.HasWorkingResult);
        Assert.Empty(doc.EditSteps);
    }

    [Fact]
    public async Task Dispose_DoesNotThrow()
    {
        var doc = CreateDocument();
        await doc.LoadImageAsync("photo.jpg");

        var vm = new RotateToolSessionViewModel(new FakeShell(), doc);

        vm.Dispose(); // should not throw
    }

    // ---- fakes ----

    private static DocumentViewModel CreateDocument()
    {
        var log = new FakeFileLogService();
        return new DocumentViewModel(
            new PlainImageLoader(),
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
            new FakeUncropFillService());
    }

    private sealed class FakeShell : ShellViewModel
    {
        public FakeShell()
            : base(
                () => throw new NotImplementedException(),
                () => throw new NotImplementedException(),
                new FakeDialogService(),
                new FakeSettingsService(),
                new FakeDownscaleService(),
                new FakeFileLogService(),
                Array.Empty<IBackgroundRemovalStrategy>(),
                new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), new FakeFileLogService())),
                new GrabCutStrategy(),
                new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
                new FakeUncropFillService(),
                new PlainImageLoader(),
                new FakeImageExportService())
        {
        }

        public override void CloseTabDirect(IToolSessionTab toolTab)
        {
            // No-op: the test does not need real tab lifecycle management.
        }
    }

    private sealed class PlainImageLoader : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(path, new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30))));

        public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(sourceName, new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30))));

        public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new LoadedImage(sourceName, new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30))));
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
        public void Error(string message, Exception? ex = null) { }
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

    private sealed class FakeUncropFillService : IUncropFillService
    {
        public Mat ExpandCanvas(Mat sourceBgr, CanvasPadding padding, out Mat newAreaMask)
        {
            newAreaMask = new Mat(1, 1, MatType.CV_8UC1);
            return new Mat(1, 1, MatType.CV_8UC3);
        }
        public Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, bool preFillEdgeAverage = false, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101, int blurRadius = 0, double fadeOpacity = 1.0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred, Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, int smoothRadius = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillWrap(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillZoomBlur(Mat sourceBgr, CanvasPadding padding, int blurRadius = 25, double zoomScale = 1.25, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillEdgeGradient(Mat sourceBgr, CanvasPadding padding, UncropGradientMode gradientMode = UncropGradientMode.PerEdgeSplay, Scalar? customEndColor = null, double noiseAmount = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
        public Mat FillPatchSynthesis(Mat sourceBgr, CanvasPadding padding, int patchSize = 32, int blendOverlap = 8, int blendMargin = 0, CancellationToken ct = default)
            => new(1, 1, MatType.CV_8UC3);
    }
}
