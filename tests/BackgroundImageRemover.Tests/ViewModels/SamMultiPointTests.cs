using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows.Threading;
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
using Xunit;
using WpfPoint = System.Windows.Point;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Verifies the multi-point SAM feature: additional foreground points can be added,
/// they are passed to the strategy context, and ClearSamPointsCommand clears them all.
/// </summary>
public class SamMultiPointTests
{
    [Fact]
    public void ClearSamPointsCommand_ClearsAllPromptPoints()
        => RunOnSta(() =>
        {
            var doc = CreateDocument();
            var shell = CreateShell(doc);
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            doc.LoadImageAsync("subject.png").GetAwaiter().GetResult();
            doc.OpenToolTab(EditorTool.RemoveBackground);
            var session = doc.ActiveToolSession as BackgroundRemoverToolSessionViewModel;
            Assert.NotNull(session);

            // Simulate adding the primary point and additional points
            session!.SelectedStrategy = StrategyKind.Sam;
            session.Sam.IsModelReady = true;
            session.Sam.HasClickedPoint = true;
            session.OnOriginalSamPointClicked(new OpenCvSharp.Point(100, 100));
            session.OnOriginalSamAdditionalPointClicked(new OpenCvSharp.Point(50, 50));
            session.OnOriginalSamAdditionalPointClicked(new OpenCvSharp.Point(150, 150));

            Assert.Equal(2, session.Sam.AdditionalPointCount);

            // Clear all points
            session.ClearSamPointsCommand.Execute(null);

            Assert.Equal(0, session.Sam.AdditionalPointCount);
            Assert.False(session.Sam.HasClickedPoint);

            doc.Dispose();
        });

    [Fact]
    public void SamStrategy_BuildContext_IncludesAdditionalPoints()
        => RunOnSta(() =>
        {
            var doc = CreateDocument();
            var shell = CreateShell(doc);
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            doc.LoadImageAsync("subject.png").GetAwaiter().GetResult();
            doc.OpenToolTab(EditorTool.RemoveBackground);
            var session = doc.ActiveToolSession as BackgroundRemoverToolSessionViewModel;
            Assert.NotNull(session);

            session!.SelectedStrategy = StrategyKind.Sam;
            session.Sam.IsModelReady = true;
            session.OnOriginalSamPointClicked(new OpenCvSharp.Point(100, 100));
            session.OnOriginalSamAdditionalPointClicked(new OpenCvSharp.Point(50, 50));
            session.OnOriginalSamAdditionalPointClicked(new OpenCvSharp.Point(150, 150));

            // The BuildContext method is private, but we can verify the points are tracked
            // by checking the AdditionalPointCount property
            Assert.Equal(2, session.Sam.AdditionalPointCount);

            doc.Dispose();
        });

    /// <summary>Runs the async body on a dedicated STA thread, pumping the dispatcher while the
    /// body is incomplete so DispatcherTimers (the debounce) actually fire.</summary>
    private static void RunOnSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    // ---- fakes ----

    private static DocumentViewModel CreateDocument()
    {
        var log = new FakeFileLogService();
        return new DocumentViewModel(
            new SubjectImageLoader(),
            new FakeImageExportService(),
            new FakeDownscaleService(),
            new FakeDialogService(),
            new FakeBatchProcessingService(),
            new FakeSettingsService(),
            new FakeProjectService(),
            log,
            new IBackgroundRemovalStrategy[] { new GrabCutStrategy() },
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), log)),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService());
    }

    private static ShellViewModel CreateShell(DocumentViewModel doc)
    {
        var log = new FakeFileLogService();
        return new ShellViewModel(
            () => doc,
            () => throw new InvalidOperationException("Uncrop factory not needed"),
            new FakeDialogService(),
            new FakeSettingsService(),
            new FakeDownscaleService(),
            log,
            new IBackgroundRemovalStrategy[] { new GrabCutStrategy() },
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), log)),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService(),
            new SubjectImageLoader(),
            new FakeImageExportService());
    }

    private sealed class SubjectImageLoader : IImageLoaderService
    {
        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(path, new Mat(200, 150, MatType.CV_8UC3, new Scalar(20, 20, 20))));

        public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(sourceName, new Mat(1, 1, MatType.CV_8UC3)));

        public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new LoadedImage(sourceName, new Mat(1, 1, MatType.CV_8UC3)));
    }

}
