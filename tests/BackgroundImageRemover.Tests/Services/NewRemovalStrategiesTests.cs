using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class FloodFillStrategyTests
{
    // A bright background surrounding a dark, sharply-bordered subject blob.
    private static Mat MakeSubjectImage()
    {
        var bgr = new Mat(200, 200, MatType.CV_8UC3, new Scalar(210, 210, 210));
        using var roi = new Mat(bgr, new Rect(80, 80, 40, 40));
        roi.SetTo(new Scalar(30, 30, 30));
        return bgr;
    }

    [Fact]
    public async Task RunFullAsync_RemovesConnectedBackground_KeepsInteriorSubject()
    {
        var strategy = new FloodFillStrategy();
        using var bgr = MakeSubjectImage();
        var context = new StrategyContext { FloodFillTolerance = 40, DecontaminateEdges = false };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        var split = Cv2.Split(result.Bgra);
        try
        {
            Assert.InRange(split[3].At<byte>(1, 1), byte.MinValue, 127);      // background corner -> transparent
            Assert.InRange(split[3].At<byte>(100, 100), 128, byte.MaxValue);  // subject center -> opaque
        }
        finally
        {
            foreach (var ch in split) ch.Dispose();
        }
    }
}

public class KMeansStrategyTests
{
    private static Mat MakeSubjectImage()
    {
        var bgr = new Mat(200, 200, MatType.CV_8UC3, new Scalar(210, 210, 210));
        using var roi = new Mat(bgr, new Rect(80, 80, 40, 40));
        roi.SetTo(new Scalar(30, 30, 30));
        return bgr;
    }

    [Fact]
    public async Task RunFullAsync_DiscardsBorderTouchingCluster_KeepsSubject()
    {
        var strategy = new KMeansStrategy();
        using var bgr = MakeSubjectImage();
        var context = new StrategyContext { KMeansClusters = 2, DecontaminateEdges = false };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        var split = Cv2.Split(result.Bgra);
        try
        {
            Assert.InRange(split[3].At<byte>(1, 1), byte.MinValue, 127);
            Assert.InRange(split[3].At<byte>(100, 100), 128, byte.MaxValue);
        }
        finally
        {
            foreach (var ch in split) ch.Dispose();
        }
    }
}

public class OtsuStrategyTests
{
    private static Mat MakeSubjectImage()
    {
        var bgr = new Mat(200, 200, MatType.CV_8UC3, new Scalar(230, 230, 230));
        using var roi = new Mat(bgr, new Rect(80, 80, 40, 40));
        roi.SetTo(new Scalar(30, 30, 30));
        return bgr;
    }

    [Fact]
    public async Task RunFullAsync_SeparatesBrightBackgroundFromDarkSubject()
    {
        var strategy = new OtsuStrategy();
        using var bgr = MakeSubjectImage();
        var context = new StrategyContext { DecontaminateEdges = false };

        using var result = await strategy.RunFullAsync(bgr, context, CancellationToken.None);

        var split = Cv2.Split(result.Bgra);
        try
        {
            Assert.InRange(split[3].At<byte>(1, 1), byte.MinValue, 127);
            Assert.InRange(split[3].At<byte>(100, 100), 128, byte.MaxValue);
        }
        finally
        {
            foreach (var ch in split) ch.Dispose();
        }
    }
}
