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

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Shared test doubles (fake shell, fake services, sized image loader and document factory)
/// so view-model tests do not each duplicate the same ~200 lines of private fakes.
/// </summary>
public static class TestDoubles
{
    /// <summary>
    /// Builds a <see cref="DocumentViewModel"/> and a no-op <see cref="FakeShell"/> backed by a
    /// solid-color image of the given size. The returned shell's <c>CloseTabDirect</c> is a no-op.
    /// </summary>
    public static (DocumentViewModel Doc, FakeShell Shell) CreateDocumentAndShell(int width, int height)
    {
        var loader = new TestImageLoader(width, height);
        var log = new FakeFileLogService();
        var dialogs = new FakeDialogService();
        var settings = new FakeSettingsService();
        var downscaler = new FakeDownscaleService();
        var exporter = new FakeImageExportService();
        var modelCache = new FakeModelCacheService();

        var shell = new FakeShell(
            loader,
            dialogs,
            settings,
            downscaler,
            log,
            exporter,
            modelCache);

        var doc = new DocumentViewModel(
            loader,
            exporter,
            downscaler,
            dialogs,
            new FakeBatchProcessingService(),
            settings,
            new FakeProjectService(),
            log,
            Array.Empty<IBackgroundRemovalStrategy>(),
            new OnnxStrategy(new OnnxInferenceEngine(modelCache, log)),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(modelCache)),
            new FakeUncropFillService());

        return (doc, shell);
    }
}

/// <summary>
/// Loads a configurable synthetic image: a solid-color background of a given size, an optional
/// alpha channel (uniform fill value; null = no alpha) and an optional shape-painting callback
/// (subject rectangle, blur, ...) applied to the BGR pixels. Replaces the per-file
/// Alpha/Plain/Subject image loaders in the view-model tests.
/// </summary>
public sealed class TestImageLoader : IImageLoaderService
{
    private readonly int _width;
    private readonly int _height;
    private readonly Scalar _background;
    private readonly byte? _alphaValue;
    private readonly Action<Mat>? _draw;

    /// <param name="background">Solid BGR fill color of the image; null uses the default BGR(10,20,30).</param>
    /// <param name="alphaValue">When set, the image carries a single-channel alpha filled with this
    /// value (0 = fully transparent cutout); when null, the image has no alpha channel.</param>
    /// <param name="draw">Optional callback that paints shapes (subject rectangle, blur, ...) into the
    /// BGR Mat right after creation. Invoked on a fresh Mat for every load.</param>
    public TestImageLoader(int width, int height, Scalar? background = null, byte? alphaValue = null, Action<Mat>? draw = null)
    {
        _width = width;
        _height = height;
        _background = background ?? new Scalar(10, 20, 30);
        _alphaValue = alphaValue;
        _draw = draw;
    }

    private LoadedImage Create(string name) => new(name, CreateBgr(), CreateAlpha());

    private Mat CreateBgr()
    {
        var bgr = new Mat(_height, _width, MatType.CV_8UC3, _background);
        _draw?.Invoke(bgr);
        return bgr;
    }

    private Mat? CreateAlpha() => _alphaValue is { } value
        ? new Mat(_height, _width, MatType.CV_8UC1, new Scalar(value))
        : null;

    public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default) => Task.FromResult(Create(path));
    public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default) => Task.FromResult(Create(sourceName));
    public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png") => Task.FromResult(Create(sourceName));
}

/// <summary>Shell with a no-op <c>CloseTabDirect</c> for tool-session tests.</summary>
public sealed class FakeShell : ShellViewModel
{
    public FakeShell(
        IImageLoaderService imageLoader,
        IDialogService dialogs,
        ISettingsService settings,
        IDownscaleService downscaler,
        IFileLogService log,
        IImageExportService imageExporter,
        IModelCacheService modelCache)
        : base(
            () => throw new NotImplementedException(),
            () => throw new NotImplementedException(),
            dialogs,
            settings,
            downscaler,
            log,
            Array.Empty<IBackgroundRemovalStrategy>(),
            new OnnxStrategy(new OnnxInferenceEngine(modelCache, log)),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(modelCache)),
            new FakeUncropFillService(),
            imageLoader,
            imageExporter)
    {
    }

    public override void CloseTabDirect(IToolSessionTab toolTab)
    {
        // No-op: the test does not need real tab lifecycle management.
    }
}

public sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Current { get; } = new();
    public void Save() { }
    public void AddRecentFile(string path) => Current.RecentFiles.Insert(0, path);
    public void AddRecentProject(string path) => Current.RecentProjects.Insert(0, path);
    public void ClearRecentFiles() => Current.RecentFiles.Clear();
    public void ClearRecentProjects() => Current.RecentProjects.Clear();
}

/// <summary>Dialog fake with virtual members so test-specific dialog variants can derive from it.</summary>
public class FakeDialogService : IDialogService
{
    public virtual string? ShowOpenImageDialog() => null;
    public virtual string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG", string? initialDirectory = null) => null;
    public virtual string? ShowSaveJpgDialog(string? suggestedFileName, string title = "Export JPEG", string? initialDirectory = null) => null;
    public virtual string? ShowSaveWebpDialog(string? suggestedFileName, string title = "Export WebP", string? initialDirectory = null) => null;
    public virtual string? ShowOpenFolderDialog(string title, string? initialDirectory = null) => null;
    public virtual string? ShowOpenProjectDialog() => null;
    public virtual string? ShowSaveProjectDialog(string? suggestedFileName) => null;
    public virtual BatchExportOptions? ShowBatchOptionsDialog() => null;
    public virtual CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
    public virtual void ShowPreferencesDialog() { }
    public virtual bool ConfirmRestoreRecovery(int documentCount) => false;
}

public sealed class FakeImageExportService : IImageExportService
{
    public Task ExportPngAsync(Mat imageBgra, string destinationPath, CancellationToken ct = default) => Task.CompletedTask;
    public Task ExportJpgAsync(Mat bgr, string destinationPath, int quality = 95, CancellationToken ct = default) => Task.CompletedTask;
    public Task ExportWebpAsync(Mat bgra, string destinationPath, int quality = 90, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class FakeDownscaleService : IDownscaleService
{
    public PreviewImage CreatePreview(Mat full, int maxDim = 800) => new(full.Clone(), 1.0);
}

public sealed class FakeBatchProcessingService : IBatchProcessingService
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

public sealed class FakeProjectService : IProjectService
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

public sealed class FakeFileLogService : IFileLogService
{
    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message, Exception? ex = null) { }
}

public sealed class FakeModelCacheService : IModelCacheService
{
    public string CachedModelPath(OnnxModelKind kind) => "";
    public bool IsModelCached(OnnxModelKind kind) => true;
    public Task<string> EnsureModelAvailableAsync(OnnxModelKind kind, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
        => Task.FromResult("");
    public Task<string> EnsureNamedFileAvailableAsync(string fileName, string url, IProgress<ModelDownloadProgress>? progress, CancellationToken ct)
        => Task.FromResult("");
    public bool IsNamedFileCached(string fileName) => true;
}

/// <summary>Uncrop-fill fake with virtual members so test-specific variants can derive from it.</summary>
public class FakeUncropFillService : IUncropFillService
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
