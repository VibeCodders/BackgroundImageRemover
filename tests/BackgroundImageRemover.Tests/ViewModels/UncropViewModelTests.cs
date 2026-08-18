using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Outpaint;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.ViewModels;

public class UncropViewModelTests
{
    private sealed class DummyUncropFillService : IUncropFillService
    {
        public Mat ExpandCanvas(Mat sourceBgr, CanvasPadding padding, out Mat newAreaMask)
        {
            newAreaMask = new Mat(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC1, Scalar.All(0));
            return new Mat(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));
        }

        public Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, bool preFillEdgeAverage = false, CancellationToken ct = default)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101, int blurRadius = 0, double fadeOpacity = 1.0, CancellationToken ct = default)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred, Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, int smoothRadius = 0, CancellationToken ct = default)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillWrap(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillZoomBlur(Mat sourceBgr, CanvasPadding padding, int blurRadius = 25, double zoomScale = 1.25, int blendMargin = 0, CancellationToken ct = default)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillEdgeGradient(Mat sourceBgr, CanvasPadding padding, UncropGradientMode gradientMode = UncropGradientMode.PerEdgeSplay, Scalar? customEndColor = null, double noiseAmount = 0, CancellationToken ct = default)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillPatchSynthesis(Mat sourceBgr, CanvasPadding padding, int patchSize = 32, int blendOverlap = 8, int blendMargin = 0, CancellationToken ct = default)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));
    }

    private sealed class DummyDialogService : IDialogService
    {
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
        public string? ShowOpenFolderDialog(string title) => null;
        public string? ShowOpenImageDialog() => null;
        public string? ShowOpenProjectDialog() => null;
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG") => null;
        public string? ShowSaveProjectDialog(string? suggestedFileName) => null;
    }

    private sealed class DummyImageLoaderService : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(path, new Mat(100, 100, MatType.CV_8UC3, Scalar.All(128))));
    }

    private sealed class DummyImageExportService : IImageExportService
    {
        public Task ExportPngAsync(Mat imageBgra, string destinationPath, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class DummyFileLogService : IFileLogService
    {
        public void Debug(string message) { }
        public void Error(string message, Exception? ex = null) { }
        public void Info(string message) { }
        public void Warn(string message) { }
    }

    private static UncropViewModel CreateViewModel() =>
        new(new DummyUncropFillService(),
            new DummyDialogService(),
            new DummyImageLoaderService(),
            new DummyImageExportService(),
            new DummyFileLogService());

    [Fact]
    public async Task LoadAsync_SetsImageLoadedAndTitles()
    {
        using var vm = CreateViewModel();
        Assert.False(vm.IsImageLoaded);
        Assert.Equal("Uncrop", vm.Title);

        await vm.LoadAsync("test_photo.jpg");

        Assert.True(vm.IsImageLoaded);
        Assert.Contains("test_photo.jpg", vm.Title);
        Assert.Contains("test_photo.jpg", vm.TabTitle);
        Assert.Contains("test_photo.jpg", vm.WindowTitle);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task ApplyFillAsync_MarksDirtyAndUpdatesPreview()
    {
        using var vm = CreateViewModel();
        await vm.LoadAsync("test_photo.jpg");
        vm.Padding = new CanvasPadding(10, 10, 10, 10);

        Assert.False(vm.IsDirty);
        await vm.ApplyFillCommand.ExecuteAsync(null);

        Assert.True(vm.IsDirty);
        Assert.EndsWith("*", vm.TabTitle);
        Assert.NotNull(vm.PreviewResult);
        Assert.False(vm.CanUndo); // first fill has no prior result to undo to

        // Applying a second fill pushes the first result to edit history
        vm.Padding = new CanvasPadding(20, 20, 20, 20);
        await vm.ApplyFillCommand.ExecuteAsync(null);
        Assert.True(vm.CanUndo);
    }

    [Theory]
    [InlineData(UncropFillMode.Mirror)]
    [InlineData(UncropFillMode.Inpaint)]
    [InlineData(UncropFillMode.SolidColor)]
    [InlineData(UncropFillMode.Replicate)]
    [InlineData(UncropFillMode.Wrap)]
    [InlineData(UncropFillMode.ZoomBlur)]
    [InlineData(UncropFillMode.EdgeGradient)]
    [InlineData(UncropFillMode.PatchSynthesis)]
    public async Task ApplyFillAsync_SupportsAllFillModes(UncropFillMode fillMode)
    {
        using var vm = CreateViewModel();
        await vm.LoadAsync("test_photo.jpg");
        vm.Padding = new CanvasPadding(15, 15, 15, 15);
        vm.SelectedFillMode = fillMode;
        vm.InpaintRadius = 10;
        vm.BlendMargin = 4;
        vm.InpaintPreFillEdgeAverage = true;
        vm.SelectedMirrorType = UncropMirrorType.Reflect;
        vm.MirrorBlurRadius = 10;
        vm.MirrorFadeOpacity = 0.8;
        vm.SelectedColorSource = UncropColorSource.CustomColor;
        vm.BlurRadius = 21;
        vm.ReplicateSmoothRadius = 5;
        vm.ZoomBlurRadius = 30;
        vm.ZoomScale = 1.4;
        vm.SelectedGradientMode = UncropGradientMode.FourCorners;
        vm.GradientNoiseAmount = 0.02;
        vm.PatchSize = 24;
        vm.PatchBlendOverlap = 6;

        Assert.True(vm.ApplyFillCommand.CanExecute(null));
        await vm.ApplyFillCommand.ExecuteAsync(null);

        Assert.True(vm.IsDirty);
        Assert.NotNull(vm.PreviewResult);
    }

    [Fact]
    public async Task CancelFillCommand_CancelsRunningOperation()
    {
        var delayFillService = new DelayUncropFillService();
        using var vm = new UncropViewModel(
            delayFillService,
            new DummyDialogService(),
            new DummyImageLoaderService(),
            new DummyImageExportService(),
            new DummyFileLogService());

        await vm.LoadAsync("test_photo.jpg");
        vm.Padding = new CanvasPadding(10, 10, 10, 10);

        var fillTask = vm.ApplyFillCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.True(vm.CancelFillCommand.CanExecute(null));

        vm.CancelFillCommand.Execute(null);
        await fillTask;

        Assert.False(vm.IsBusy);
        Assert.Equal("Fill operation cancelled.", vm.StatusMessage);
    }

    private sealed class DelayUncropFillService : IUncropFillService
    {
        public Mat ExpandCanvas(Mat sourceBgr, CanvasPadding padding, out Mat newAreaMask)
        {
            newAreaMask = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));
            return new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));
        }

        public Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101, int blurRadius = 0, double fadeOpacity = 1.0, CancellationToken ct = default)
        {
            ct.WaitHandle.WaitOne(500);
            ct.ThrowIfCancellationRequested();
            return new Mat(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));
        }

        public Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, bool preFillEdgeAverage = false, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred, Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, int smoothRadius = 0, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Mat FillWrap(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Mat FillZoomBlur(Mat sourceBgr, CanvasPadding padding, int blurRadius = 25, double zoomScale = 1.25, int blendMargin = 0, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Mat FillEdgeGradient(Mat sourceBgr, CanvasPadding padding, UncropGradientMode gradientMode = UncropGradientMode.PerEdgeSplay, Scalar? customEndColor = null, double noiseAmount = 0, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Mat FillPatchSynthesis(Mat sourceBgr, CanvasPadding padding, int patchSize = 32, int blendOverlap = 8, int blendMargin = 0, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
