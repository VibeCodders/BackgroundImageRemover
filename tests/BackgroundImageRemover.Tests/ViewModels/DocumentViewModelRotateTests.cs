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

using BackgroundImageRemover.Tests.Helpers;
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
        var doc = CreateDocument(loader: new TestImageLoader(Width, Height, alphaValue: 0));
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
            loader ?? new TestImageLoader(Width, Height),
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

}
