using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class MagicWandRemovalStrategyTests
{
    // A bright, uniform background surrounding a dark, sharply-bordered subject blob.
    private static Mat MakeSubjectImage()
    {
        var bgr = new Mat(200, 200, MatType.CV_8UC3, new Scalar(210, 210, 210));
        using var roi = new Mat(bgr, new Rect(80, 80, 40, 40));
        roi.SetTo(new Scalar(30, 30, 30));
        return bgr;
    }

    [Fact]
    public async Task ClickOnBackground_RemovesConnectedBackground_KeepsSubject()
    {
        var strategy = new MagicWandRemovalStrategy();
        using var bgr = MakeSubjectImage();
        var context = new StrategyContext
        {
            MagicWandSeed = new Point(0, 0),
            MagicWandTolerance = 40,
            DecontaminateEdges = false
        };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        var split = Cv2.Split(result.Bgra);
        try
        {
            Assert.InRange(split[3].At<byte>(1, 1), byte.MinValue, 127);      // background -> transparent
            Assert.InRange(split[3].At<byte>(100, 100), 128, byte.MaxValue);  // subject -> opaque
        }
        finally
        {
            foreach (var ch in split) ch.Dispose();
        }
    }

    [Fact]
    public async Task ClickOnSubject_RemovesSubject_KeepsBackground()
    {
        var strategy = new MagicWandRemovalStrategy();
        using var bgr = MakeSubjectImage();
        var context = new StrategyContext
        {
            MagicWandSeed = new Point(100, 100),
            MagicWandTolerance = 40,
            DecontaminateEdges = false
        };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        var split = Cv2.Split(result.Bgra);
        try
        {
            Assert.InRange(split[3].At<byte>(100, 100), byte.MinValue, 127);  // subject -> transparent
            Assert.InRange(split[3].At<byte>(1, 1), 128, byte.MaxValue);      // background -> opaque
        }
        finally
        {
            foreach (var ch in split) ch.Dispose();
        }
    }

    [Fact]
    public async Task OutOfBoundsSeed_DoesNotCrash_KeepsWholeImageOpaque()
    {
        var strategy = new MagicWandRemovalStrategy();
        using var bgr = MakeSubjectImage();
        var context = new StrategyContext
        {
            MagicWandSeed = new Point(-50, -50),
            MagicWandTolerance = 40,
            DecontaminateEdges = false
        };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        // The guard bails out with the untouched all-255 mask: nothing is removed.
        Assert.InRange(result.Bgra.At<Vec4b>(5, 5).Item3, 128, byte.MaxValue);
        Assert.InRange(result.Bgra.At<Vec4b>(100, 100).Item3, 128, byte.MaxValue);
    }

    [Fact]
    public async Task SeedBeyondImageBounds_DoesNotCrash_KeepsWholeImageOpaque()
    {
        var strategy = new MagicWandRemovalStrategy();
        using var bgr = MakeSubjectImage();
        var context = new StrategyContext
        {
            MagicWandSeed = new Point(5000, 5000),
            MagicWandTolerance = 40,
            DecontaminateEdges = false
        };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        Assert.InRange(result.Bgra.At<Vec4b>(5, 5).Item3, 128, byte.MaxValue);
        Assert.InRange(result.Bgra.At<Vec4b>(100, 100).Item3, 128, byte.MaxValue);
    }

    [Fact]
    public async Task NoSeed_DoesNotCrash_KeepsWholeImageOpaque()
    {
        var strategy = new MagicWandRemovalStrategy();
        using var bgr = MakeSubjectImage();
        var context = new StrategyContext { MagicWandSeed = null, DecontaminateEdges = false };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        Assert.InRange(result.Bgra.At<Vec4b>(5, 5).Item3, 128, byte.MaxValue);
    }
}

public class StrategyBasePostProcessingTests
{
    // A deterministic strategy whose mask is a hard left/right split, so the shared
    // invert/feather post-processing can be observed in isolation.
    private sealed class HalfMaskStrategy : StrategyBase
    {
        public override StrategyKind Kind => StrategyKind.Otsu;

        protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct)
        {
            var mask = new Mat(bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
            using var right = new Mat(mask, new Rect(bgr.Width / 2, 0, bgr.Width - bgr.Width / 2, bgr.Height));
            right.SetTo(Scalar.All(255));
            return mask;
        }
    }

    [Fact]
    public async Task InvertMask_FlipsForegroundAndBackground()
    {
        var strategy = new HalfMaskStrategy();
        using var bgr = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(128));
        var context = new StrategyContext { InvertMask = true, DecontaminateEdges = false };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        var split = Cv2.Split(result.Bgra);
        try
        {
            Assert.InRange(split[3].At<byte>(50, 25), 128, byte.MaxValue);  // left was transparent
            Assert.InRange(split[3].At<byte>(50, 75), byte.MinValue, 127);  // right was opaque
        }
        finally
        {
            foreach (var ch in split) ch.Dispose();
        }
    }

    [Fact]
    public async Task MaskFeatherPixels_SoftensTheHardMaskEdge()
    {
        var strategy = new HalfMaskStrategy();
        using var bgr = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(128));
        var context = new StrategyContext { MaskFeatherPixels = 6, DecontaminateEdges = false };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        var alpha = result.Bgra.ExtractAlphaChannel();
        try
        {
            bool hasIntermediate = false;
            for (int y = 0; y < alpha.Height && !hasIntermediate; y++)
            {
                for (int x = 0; x < alpha.Width; x++)
                {
                    byte v = alpha.At<byte>(y, x);
                    if (v > 0 && v < 255)
                    {
                        hasIntermediate = true;
                        break;
                    }
                }
            }
            Assert.True(hasIntermediate);
        }
        finally
        {
            alpha.Dispose();
        }
    }
}
