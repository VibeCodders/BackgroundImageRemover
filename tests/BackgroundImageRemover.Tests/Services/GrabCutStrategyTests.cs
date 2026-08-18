using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class GrabCutStrategyTests
{
    // A dark background with a bright, sharply-bordered rectangle "subject" -- easy for
    // GrabCut to converge on consistently regardless of resolution.
    private static Mat MakeSubjectImage(int width, int height, Rect subjectRect)
    {
        var bgr = new Mat(height, width, MatType.CV_8UC3, Scalar.All(20));
        using var roi = new Mat(bgr, subjectRect);
        roi.SetTo(new Scalar(220, 210, 200));
        return bgr;
    }

    private static double ForegroundFraction(Mat alpha)
    {
        return Cv2.CountNonZero(ThresholdOpaque(alpha)) / (double)(alpha.Width * alpha.Height);
    }

    private static Mat ThresholdOpaque(Mat alpha)
    {
        var thresholded = new Mat();
        Cv2.Threshold(alpha, thresholded, 127, 255, ThresholdTypes.Binary);
        return thresholded;
    }

    [Fact]
    public async Task RunFullAsync_WithoutAPriorPreview_FallsBackToInitWithRect_AndSucceeds()
    {
        var strategy = new GrabCutStrategy();
        using var full = MakeSubjectImage(200, 150, new Rect(40, 30, 120, 90));
        var context = new StrategyContext { GrabCutRect = new Rect(30, 20, 140, 110), DecontaminateEdges = false };

        using var result = await strategy.RunFullAsync(full, context, CancellationToken.None);

        Assert.Equal(200, result.Bgra.Width);
        Assert.Equal(150, result.Bgra.Height);
        Assert.NotNull(strategy.LastLabelMask);
        Assert.Equal(full.Size(), strategy.LastLabelMask!.Size());
    }

    [Fact]
    public async Task RunFullAsync_AfterAPreviewRun_SeedsFromTheUpscaledPreviewMask_AndAgreesOnSubjectLocation()
    {
        // Same scene at two resolutions, with the same relative rect -- mirrors how the app
        // downscales for preview then reprocesses at full resolution on export.
        var strategy = new GrabCutStrategy();
        using var preview = MakeSubjectImage(100, 75, new Rect(20, 15, 60, 45));
        using var full = MakeSubjectImage(200, 150, new Rect(40, 30, 120, 90));

        var previewContext = new StrategyContext { GrabCutRect = new Rect(15, 10, 70, 55), DecontaminateEdges = false };
        using var previewResult = await strategy.RunPreviewAsync(preview, previewContext, CancellationToken.None);

        var fullContext = new StrategyContext { GrabCutRect = new Rect(30, 20, 140, 110), DecontaminateEdges = false };
        using var fullResult = await strategy.RunFullAsync(full, fullContext, CancellationToken.None);

        Assert.Equal(full.Size(), strategy.LastLabelMask!.Size());

        // Preview and full-res foreground coverage (as a fraction of image area) should be
        // close: seeding the full run from the preview's mask keeps them from diverging, unlike
        // two independent from-scratch GrabCut runs at different resolutions.
        var previewChannels = Cv2.Split(previewResult.Bgra);
        var fullChannels = Cv2.Split(fullResult.Bgra);
        try
        {
            double previewFraction = ForegroundFraction(previewChannels[3]);
            double fullFraction = ForegroundFraction(fullChannels[3]);

            Assert.InRange(fullFraction - previewFraction, -0.05, 0.05);
        }
        finally
        {
            foreach (var c in previewChannels) c.Dispose();
            foreach (var c in fullChannels) c.Dispose();
        }
    }

    [Fact]
    public async Task RunPreviewAsync_AfterAPriorFullRun_StillReinitializesFromTheRect()
    {
        // A later preview call is always at preview (smaller) resolution, so it must never seed
        // from a stale, larger full-res mask left behind by an earlier export.
        var strategy = new GrabCutStrategy();
        using var full = MakeSubjectImage(200, 150, new Rect(40, 30, 120, 90));
        using var fullResult = await strategy.RunFullAsync(
            full, new StrategyContext { GrabCutRect = new Rect(30, 20, 140, 110), DecontaminateEdges = false }, CancellationToken.None);
        fullResult.Dispose();

        using var preview = MakeSubjectImage(100, 75, new Rect(20, 15, 60, 45));
        using var previewResult = await strategy.RunPreviewAsync(
            preview, new StrategyContext { GrabCutRect = new Rect(15, 10, 70, 55), DecontaminateEdges = false }, CancellationToken.None);

        Assert.Equal(preview.Size(), strategy.LastLabelMask!.Size());
        Assert.Equal(100, previewResult.Bgra.Width);
        Assert.Equal(75, previewResult.Bgra.Height);
    }

    [Fact]
    public async Task RunFullAsync_WithoutARectOrScribbles_DefaultsToFullImageAndSucceeds()
    {
        var strategy = new GrabCutStrategy();
        using var full = MakeSubjectImage(100, 100, new Rect(10, 10, 50, 50));
        var context = new StrategyContext { GrabCutRect = null, DecontaminateEdges = false };

        using var result = await strategy.RunFullAsync(full, context, CancellationToken.None);

        Assert.Equal(100, result.Bgra.Width);
        Assert.Equal(100, result.Bgra.Height);
        Assert.NotNull(strategy.LastLabelMask);
    }

    [Fact]
    public async Task RunFullAsync_WithoutARect_UsingOnlyScribbles_SegmentsTheSubject()
    {
        // The rectangle is optional: a foreground scribble inside the subject plus a background
        // scribble outside it should be enough to seed a plausible segmentation on its own.
        var strategy = new GrabCutStrategy();
        using var full = MakeSubjectImage(200, 150, new Rect(40, 30, 120, 90));
        using var foreground = new Mat(full.Size(), MatType.CV_8UC1, Scalar.All(0));
        using var background = new Mat(full.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(foreground, new Rect(80, 60, 20, 20), Scalar.All(255), thickness: -1);
        Cv2.Rectangle(background, new Rect(5, 5, 20, 20), Scalar.All(255), thickness: -1);

        var context = new StrategyContext
        {
            GrabCutRect = null,
            GrabCutForegroundScribble = foreground,
            GrabCutBackgroundScribble = background,
            DecontaminateEdges = false
        };

        using var result = await strategy.RunFullAsync(full, context, CancellationToken.None);

        Assert.NotNull(strategy.LastLabelMask);
        var split = Cv2.Split(result.Bgra);
        try
        {
            double fraction = ForegroundFraction(split[3]);
            Assert.InRange(fraction, 0.05, 0.95);
        }
        finally
        {
            foreach (var c in split) c.Dispose();
        }
    }
}
