using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using BackgroundImageRemover.Helpers;
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
using WpfPoint = System.Windows.Point;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Verifies that exporting after a GrabCut produces the same cutout the user saw in the
/// preview. The full-res export must seed from the preview's raw label mask (nearest-neighbor
/// upscale, no re-segmentation), so the exported alpha is just a higher-resolution version of
/// the preview alpha -- compared here pixel-for-pixel after downsampling back to preview size.
/// </summary>
public class GrabCutExportConsistencyTests
{
    // 1200x900 full image: the preview downscales to 800x600 (ScaleFactor 1.5), so the export
    // takes the seeding path (full is larger than the preview).
    private const int FullWidth = 1200;
    private const int FullHeight = 900;
    private const int PreviewWidth = 800;
    private const int PreviewHeight = 600;

    [Fact]
    public void Export_FromTheMainEditor_WithRectAndScribbles_ProducesTheCutout()
        => RunOnSta(async () =>
        {
            var recording = new RecordingGrabCutStrategy();
            var exporter = new CapturingImageExportService();
            var doc = CreateDocument(recording, exporter, new PngPathDialogService("out.png"));

            await doc.LoadImageAsync("subject.png");

            // Rectangle first, then GrabCut: the debounce collapses into one preview with both.
            doc.GrabCut.SelectedRect = new Rect(200, 133, 400, 300); // preview coords (full ÷ 1.5)
            doc.SelectedStrategy = StrategyKind.GrabCut;
            Assert.Equal(InteractionMode.DrawRect, doc.OriginalMode);

            // Foreground scribble over the subject via the main editor's scribble API.
            doc.OriginalMode = InteractionMode.ScribbleForeground;
            doc.OnOriginalStrokeStart(new WpfPoint(400, 300));
            doc.OnOriginalStrokeMove(new WpfPoint(420, 300));
            doc.OnOriginalStrokeEnd();
            Assert.True(doc.GrabCut.HasScribbles);

            // Run the preview directly (the "refine selection" path). The debounce armed by
            // the strategy change may supersede it, so pump until a preview actually lands.
            await doc.RefineGrabCutPreviewCommand.ExecuteAsync(null);
            PumpUntil(() => doc.ResultBitmap is not null, TimeSpan.FromSeconds(10));
            Assert.NotNull(doc.ResultBitmap);
            Assert.DoesNotContain("failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);

            // Export PNG: the full-res run must consume the resized scribble copies (the path
            // whose copies used to be disposed before the run even started).
            await doc.ExportCommand.ExecuteAsync(null);

            Assert.Contains("Exported", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.True(doc.HasWorkingResult);
            Assert.Equal("out.png", doc.LastExportedFilePath);

            // The resized full-res scribble copies reached the strategy run.
            Assert.NotNull(recording.FullRunForegroundScribble);
            Assert.Equal(new Size(FullWidth, FullHeight), recording.FullRunForegroundScribble!.Size());

            // The export is the full-size cutout: the subject is foreground, the rest is gone.
            Assert.NotNull(exporter.CapturedBgra);
            Assert.Equal(FullWidth, exporter.CapturedBgra!.Width);
            Assert.Equal(FullHeight, exporter.CapturedBgra.Height);
            using var exportedAlpha = exporter.CapturedBgra!.ExtractAlphaChannel();
            double fgFraction = ForegroundFraction(exportedAlpha);
            Assert.InRange(fgFraction, 0.1, 0.5); // subject ≈ 25% of the canvas

            recording.PreviewAlpha?.Dispose();
            recording.FullAlpha?.Dispose();
            recording.FullRunForegroundScribble?.Dispose();
            recording.FullRunBackgroundScribble?.Dispose();
            doc.Dispose();
        });

    [Fact]
    public void Export_AfterGrabCutPreview_MatchesThePreviewPixelForPixel()
        => RunOnSta(async () =>
        {
            var recording = new RecordingGrabCutStrategy();
            var exporter = new CapturingImageExportService();
            var doc = CreateDocument(recording, exporter, new PngPathDialogService("out.png"));

            await doc.LoadImageAsync("subject.png");

            // Draw the rectangle in PREVIEW coordinates (full rect 300,200,600,450 divided by
            // ScaleFactor 1.5), then pick GrabCut: the debounce collapses both into one preview
            // run with rect + strategy set.
            doc.GrabCut.SelectedRect = new Rect(200, 133, 400, 300);
            doc.SelectedStrategy = StrategyKind.GrabCut;
            PumpUntil(() => doc.ResultBitmap is not null, TimeSpan.FromSeconds(10));
            Assert.NotNull(recording.PreviewAlpha);
            Assert.Equal(new Size(PreviewWidth, PreviewHeight), recording.PreviewAlpha!.Size());

            // Capture the preview's raw label mask before the export run replaces it.
            Assert.NotNull(recording.Inner.LastLabelMask);
            using var previewLabels = recording.Inner.LastLabelMask!.Clone();

            await doc.ExportCommand.ExecuteAsync(null);

            Assert.DoesNotContain("failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.True(doc.HasWorkingResult);
            Assert.NotNull(recording.FullAlpha);
            Assert.NotNull(exporter.CapturedBgra);

            // 1) The full-res raw label mask must be a bit-exact nearest-neighbor upscale of the
            //    preview's: same segmentation, just sharper. This is the mask-seeding contract.
            var fullLabels = recording.Inner.LastLabelMask!;
            Assert.Equal(new Size(FullWidth, FullHeight), fullLabels.Size());
            using (var upscaled = new Mat())
            using (var diff = new Mat())
            {
                Cv2.Resize(previewLabels, upscaled, fullLabels.Size(), interpolation: InterpolationFlags.Nearest);
                Cv2.Compare(fullLabels, upscaled, diff, CmpTypes.NE);
                Assert.Equal(0, Cv2.CountNonZero(diff));
            }

            // 2) The exported PNG alpha must be bit-identical to the full-res run's alpha (the
            //    export pipeline must not reshape the mask between strategy and file).
            using var exportedAlpha = exporter.CapturedBgra!.ExtractAlphaChannel();
            using (var diff = new Mat())
            {
                Cv2.Compare(exportedAlpha, recording.FullAlpha!, diff, CmpTypes.NE);
                Assert.Equal(0, Cv2.CountNonZero(diff));
            }

            // 3) The exported alpha, downsampled back to preview size, must match the preview
            //    alpha almost pixel-for-pixel (small tolerance: the export feathers with a
            //    proportionally larger kernel and the downsample uses area interpolation).
            using (var down = new Mat())
            using (var diff = new Mat())
            using (var big = new Mat())
            {
                Cv2.Resize(recording.FullAlpha!, down, previewLabels.Size(), interpolation: InterpolationFlags.Area);
                Cv2.Absdiff(down, recording.PreviewAlpha!, diff);
                double meanAbsDiff = diff.Mean().Val0;

                Cv2.Threshold(diff, big, 32, 255, ThresholdTypes.Binary);
                double bigDiffFraction = Cv2.CountNonZero(big) / (double)(PreviewWidth * PreviewHeight);

                // Measured with seeding: MAE ~0.16 alpha levels, ~0.16% of pixels differ by
                // >32 levels (only the feathered edge, where the export's proportionally
                // larger kernel blurs slightly differently). If the seeding were removed, the
                // from-scratch full-res re-segmentation diverges on this soft-edged subject
                // (the label check above trips on 14 pixels) -- thresholds give 3x margin.
                Assert.True(meanAbsDiff < 0.5, $"mean absolute alpha difference vs preview: {meanAbsDiff:F2}");
                Assert.True(bigDiffFraction < 0.005, $"fraction of pixels differing by >32 alpha levels: {bigDiffFraction:P2}");
            }

            recording.PreviewAlpha?.Dispose();
            recording.FullAlpha?.Dispose();
            doc.Dispose();
        });

    /// <summary>Runs the async body on a dedicated STA thread, pumping the dispatcher while the
    /// body is incomplete so DispatcherTimers (the debounce) actually fire.</summary>
    private static void RunOnSta(Func<Task> body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var task = body();
            while (!task.IsCompleted)
            {
                PumpFrame(TimeSpan.FromMilliseconds(10));
            }
            try
            {
                task.GetAwaiter().GetResult();
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

    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
        {
            PumpFrame(TimeSpan.FromMilliseconds(20));
        }
    }

    private static void PumpFrame(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Delegates to a real GrabCutStrategy and records the alpha of every preview and
    /// full-res run so the test can compare them pixel-for-pixel.</summary>
    private sealed class RecordingGrabCutStrategy : IBackgroundRemovalStrategy
    {
        public GrabCutStrategy Inner { get; } = new();

        public Mat? PreviewAlpha { get; private set; }
        public Mat? FullAlpha { get; private set; }
        public Mat? FullRunForegroundScribble { get; private set; }
        public Mat? FullRunBackgroundScribble { get; private set; }

        public StrategyKind Kind => StrategyKind.GrabCut;

        public async Task<RemovalResult> RunPreviewAsync(Mat bgr, StrategyContext context, CancellationToken ct)
        {
            var result = await Inner.RunPreviewAsync(bgr, context, ct);
            PreviewAlpha?.Dispose();
            PreviewAlpha = result.Bgra.ExtractAlphaChannel();
            return result;
        }

        public async Task<RemovalResult> RunFullAsync(Mat bgr, StrategyContext context, CancellationToken ct)
        {
            // Record the scribble copies the caller handed to the full-res run (they must be
            // the full-size resized copies, alive for the whole run).
            FullRunForegroundScribble?.Dispose();
            FullRunBackgroundScribble?.Dispose();
            FullRunForegroundScribble = context.GrabCutForegroundScribble?.Clone();
            FullRunBackgroundScribble = context.GrabCutBackgroundScribble?.Clone();

            var result = await Inner.RunFullAsync(bgr, context, ct);
            FullAlpha?.Dispose();
            FullAlpha = result.Bgra.ExtractAlphaChannel();
            return result;
        }
    }

    /// <summary>Fraction of pixels whose alpha is above the 127 threshold.</summary>
    private static double ForegroundFraction(Mat alpha)
    {
        using var binary = new Mat();
        Cv2.Threshold(alpha, binary, 127, 255, ThresholdTypes.Binary);
        return Cv2.CountNonZero(binary) / (double)(alpha.Width * alpha.Height);
    }

    // ---- fakes ----

    private static DocumentViewModel CreateDocument(
        IBackgroundRemovalStrategy strategy,
        IImageExportService exporter,
        IDialogService dialogs)
    {
        var log = new FakeFileLogService();
        return new DocumentViewModel(
            new TestImageLoader(FullWidth, FullHeight, new Scalar(20, 20, 20), draw: bgr =>
            {
                using var roi = new Mat(bgr, new Rect(300, 200, 600, 450));
                roi.SetTo(new Scalar(220, 210, 200));
                // Soft (anti-aliased) subject edge: at preview resolution the ramp is sampled
                // coarsely, at full resolution finely -- exactly the case where a from-scratch
                // re-segmentation settles on a visibly different boundary than the preview (the
                // seed exists precisely so the export is a refinement of what the user saw, not a
                // fresh guess).
                Cv2.GaussianBlur(bgr, bgr, new Size(31, 31), 0);
            }),
            exporter,
            new DownscaleService(), // the real downscaler: 1200x900 -> 800x600 preview, ScaleFactor 1.5
            dialogs,
            new FakeBatchProcessingService(),
            new FakeSettingsService(),
            new FakeProjectService(),
            log,
            new[] { strategy },
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), log)),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService());
    }

    private sealed class PngPathDialogService : IDialogService
    {
        private readonly string _path;
        public PngPathDialogService(string path) => _path = path;

        public string? ShowOpenImageDialog() => null;
        public string? ShowSavePngDialog(string? suggestedFileName, string title = "Export PNG", string? initialDirectory = null) => _path;
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

    private sealed class CapturingImageExportService : IImageExportService
    {
        public Mat? CapturedBgra { get; private set; }

        public Task ExportPngAsync(Mat imageBgra, string destinationPath, CancellationToken ct = default)
        {
            CapturedBgra?.Dispose();
            CapturedBgra = imageBgra.Clone();
            return Task.CompletedTask;
        }

        public Task ExportJpgAsync(Mat bgr, string destinationPath, int quality = 95, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task ExportWebpAsync(Mat bgra, string destinationPath, int quality = 90, CancellationToken ct = default)
            => Task.CompletedTask;
    }

}
