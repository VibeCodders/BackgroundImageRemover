using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Edge-case tests for the classical background-removal strategies: degenerate image sizes
/// (1×1, tiny), uniform images, subject/background separation and cooperative cancellation.
/// A strategy that throws on a 1×1 or tiny image crashes the tool when the user works on a
/// very small crop, so these pin that the pipelines stay defensive.
/// </summary>
public class ClassicalStrategiesEdgeCaseTests
{
    // Bright, uniform background around a dark, sharply-bordered subject blob (mirrors the
    // fixture used by the MagicWand tests).
    private static Mat MakeSubjectImage()
    {
        var bgr = new Mat(200, 200, MatType.CV_8UC3, new Scalar(210, 210, 210));
        using var roi = new Mat(bgr, new Rect(80, 80, 40, 40));
        roi.SetTo(new Scalar(30, 30, 30));
        return bgr;
    }

    private static Mat ExtractAlpha(RemovalResult result)
    {
        var split = Cv2.Split(result.Bgra);
        try
        {
            return split[3].Clone();
        }
        finally
        {
            foreach (var ch in split) ch.Dispose();
        }
    }

    private static StrategyContext BasicContext() => new() { DecontaminateEdges = false };

    // ------------------------------------------------------------------ FloodFill

    public class FloodFillStrategyEdgeCases
    {
        [Fact]
        public async Task SubjectInCenter_KeptOpaque_BackgroundTransparent()
        {
            var strategy = new FloodFillStrategy();
            using var bgr = MakeSubjectImage();

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);
            using var alpha = ExtractAlpha(result);

            Assert.InRange(alpha.At<byte>(5, 5), byte.MinValue, 127);        // corner: background
            Assert.InRange(alpha.At<byte>(100, 100), 128, byte.MaxValue);    // center: subject
        }

        [Fact]
        public async Task UniformImage_EverythingIsBackground_FullyTransparent()
        {
            var strategy = new FloodFillStrategy();
            using var bgr = new Mat(40, 40, MatType.CV_8UC3, new Scalar(150, 150, 150));

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);
            using var alpha = ExtractAlpha(result);

            Assert.Equal(0, alpha.At<byte>(20, 20));
            Assert.Equal(0, alpha.At<byte>(0, 0));
        }

        [Fact]
        public async Task OneByOneImage_DoesNotCrash()
        {
            var strategy = new FloodFillStrategy();
            using var bgr = new Mat(1, 1, MatType.CV_8UC3, new Scalar(120, 120, 120));

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);

            Assert.Equal(1, result.Bgra.Width);
            Assert.Equal(1, result.Bgra.Height);
        }

        [Fact]
        public async Task TinyTwoByTwoImage_DoesNotCrash()
        {
            var strategy = new FloodFillStrategy();
            using var bgr = new Mat(2, 2, MatType.CV_8UC3, new Scalar(100, 100, 100));

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);

            Assert.Equal(2, result.Bgra.Width);
            Assert.Equal(2, result.Bgra.Height);
        }

        [Fact]
        public async Task PreCanceledToken_ThrowsOperationCanceled()
        {
            var strategy = new FloodFillStrategy();
            using var bgr = MakeSubjectImage();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => strategy.RunFullAsync(bgr, BasicContext(), cts.Token));
        }
    }

    // ------------------------------------------------------------------ KMeans

    public class KMeansStrategyEdgeCases
    {
        private static StrategyContext KMeansContext() => new() { KMeansClusters = 2, DecontaminateEdges = false };

        [Fact]
        public async Task BorderCluster_RemovedAsBackground_SubjectKeptOpaque()
        {
            var strategy = new KMeansStrategy();
            using var bgr = MakeSubjectImage();

            using var result = await strategy.RunFullAsync(bgr, KMeansContext(), CancellationToken.None);
            using var alpha = ExtractAlpha(result);

            Assert.InRange(alpha.At<byte>(5, 5), byte.MinValue, 127);        // border color cluster: background
            Assert.InRange(alpha.At<byte>(100, 100), 128, byte.MaxValue);    // subject cluster: foreground
        }

        [Fact]
        public async Task OneByOneImage_DoesNotCrash()
        {
            var strategy = new KMeansStrategy();
            using var bgr = new Mat(1, 1, MatType.CV_8UC3, new Scalar(120, 120, 120));

            using var result = await strategy.RunFullAsync(bgr, KMeansContext(), CancellationToken.None);

            Assert.Equal(1, result.Bgra.Width);
            Assert.Equal(1, result.Bgra.Height);
        }

        [Fact]
        public async Task UniformImage_DoesNotCrash()
        {
            var strategy = new KMeansStrategy();
            using var bgr = new Mat(30, 30, MatType.CV_8UC3, new Scalar(90, 90, 90));

            using var result = await strategy.RunFullAsync(bgr, KMeansContext(), CancellationToken.None);

            Assert.Equal(bgr.Size(), result.Bgra.Size());
        }

        [Fact]
        public async Task PreCanceledToken_ThrowsOperationCanceled()
        {
            var strategy = new KMeansStrategy();
            using var bgr = MakeSubjectImage();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => strategy.RunFullAsync(bgr, KMeansContext(), cts.Token));
        }
    }

    // ------------------------------------------------------------------ Otsu

    public class OtsuStrategyEdgeCases
    {
        [Fact]
        public async Task DarkSubjectOnBrightBackground_SubjectKeptOpaque()
        {
            var strategy = new OtsuStrategy();
            using var bgr = MakeSubjectImage();

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);
            using var alpha = ExtractAlpha(result);

            Assert.InRange(alpha.At<byte>(100, 100), 128, byte.MaxValue);    // dark subject
            Assert.InRange(alpha.At<byte>(5, 5), byte.MinValue, 127);        // bright border background
        }

        [Fact]
        public async Task OneByOneImage_DoesNotCrash()
        {
            var strategy = new OtsuStrategy();
            using var bgr = new Mat(1, 1, MatType.CV_8UC3, new Scalar(120, 120, 120));

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);

            Assert.Equal(1, result.Bgra.Width);
            Assert.Equal(1, result.Bgra.Height);
        }

        [Fact]
        public async Task UniformImage_DoesNotCrash()
        {
            var strategy = new OtsuStrategy();
            using var bgr = new Mat(30, 30, MatType.CV_8UC3, new Scalar(128, 128, 128));

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);

            Assert.Equal(bgr.Size(), result.Bgra.Size());
        }

        [Fact]
        public async Task PreCanceledToken_ThrowsOperationCanceled()
        {
            var strategy = new OtsuStrategy();
            using var bgr = MakeSubjectImage();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => strategy.RunFullAsync(bgr, BasicContext(), cts.Token));
        }
    }

    // ------------------------------------------------------------------ ChromaKey

    public class ChromaKeyStrategyEdgeCases
    {
        private static Mat MakeGreenScreenImage()
        {
            var bgr = new Mat(120, 120, MatType.CV_8UC3, new Scalar(0, 255, 0)); // green background
            using var roi = new Mat(bgr, new Rect(40, 40, 40, 40));
            roi.SetTo(new Scalar(0, 0, 255)); // red subject in the middle
            return bgr;
        }

        [Fact]
        public async Task GreenScreen_Removed_SubjectKeptOpaque()
        {
            var strategy = new ChromaKeyStrategy();
            using var bgr = MakeGreenScreenImage();

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);
            using var alpha = ExtractAlpha(result);

            Assert.InRange(alpha.At<byte>(5, 5), byte.MinValue, 127);        // green border: transparent
            Assert.InRange(alpha.At<byte>(60, 60), 128, byte.MaxValue);      // red subject: opaque
        }

        [Fact]
        public async Task ExplicitKeyColor_IsUsedInsteadOfDetection()
        {
            var strategy = new ChromaKeyStrategy();
            using var bgr = MakeGreenScreenImage();
            var context = new StrategyContext
            {
                ChromaKeyColor = new Vec3b(0, 255, 0),
                DecontaminateEdges = false
            };

            using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);
            using var alpha = ExtractAlpha(result);

            Assert.InRange(alpha.At<byte>(5, 5), byte.MinValue, 127);
            Assert.InRange(alpha.At<byte>(60, 60), 128, byte.MaxValue);
        }

        [Fact]
        public async Task OneByOneImage_DoesNotCrash()
        {
            var strategy = new ChromaKeyStrategy();
            using var bgr = new Mat(1, 1, MatType.CV_8UC3, new Scalar(0, 255, 0));

            using var result = await strategy.RunFullAsync(bgr, BasicContext(), CancellationToken.None);

            Assert.Equal(1, result.Bgra.Width);
            Assert.Equal(1, result.Bgra.Height);
        }

        [Fact]
        public async Task PreCanceledToken_ThrowsOperationCanceled()
        {
            var strategy = new ChromaKeyStrategy();
            using var bgr = MakeGreenScreenImage();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => strategy.RunFullAsync(bgr, BasicContext(), cts.Token));
        }
    }
}
