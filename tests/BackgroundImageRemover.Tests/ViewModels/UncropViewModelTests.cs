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

        public Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred, Scalar? customColor = null, int blurRadius = 0)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillReplicate(Mat sourceBgr, CanvasPadding padding)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));

        public Mat FillWrap(Mat sourceBgr, CanvasPadding padding)
            => new(sourceBgr.Height + padding.Top + padding.Bottom, sourceBgr.Width + padding.Left + padding.Right, MatType.CV_8UC3, Scalar.All(255));
    }

    private sealed class DummyDialogService : IDialogService
    {
        public CloseDocumentResult ConfirmCloseDocument(string documentName) => CloseDocumentResult.Discard;
        public (NewProjectType? Type, bool OpenImageImmediately) ShowNewProjectDialog() => (null, false);
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
    public async Task ApplyFillAsync_SupportsAllFillModes(UncropFillMode fillMode)
    {
        using var vm = CreateViewModel();
        await vm.LoadAsync("test_photo.jpg");
        vm.Padding = new CanvasPadding(15, 15, 15, 15);
        vm.SelectedFillMode = fillMode;
        vm.InpaintRadius = 10;
        vm.BlendMargin = 4;
        vm.SelectedMirrorType = UncropMirrorType.Reflect;
        vm.SelectedColorSource = UncropColorSource.CustomColor;
        vm.BlurRadius = 21;

        Assert.True(vm.ApplyFillCommand.CanExecute(null));
        await vm.ApplyFillCommand.ExecuteAsync(null);

        Assert.True(vm.IsDirty);
        Assert.NotNull(vm.PreviewResult);
    }
}
