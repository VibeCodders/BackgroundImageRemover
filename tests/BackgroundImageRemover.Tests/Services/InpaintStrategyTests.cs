using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Pins the Inpaint strategy's contract: it floods the background from the image border and
/// must keep the subject opaque. Regression: the flood fill used a mask fill value of 0
/// (<c>(FloodFillFlags)(0 &lt;&lt; 8)</c>), so the background mask came out all-zero and the tool
/// returned a fully transparent image; and even with the fill fixed, the returned mask was
/// the background (not inverted), which would have kept the background and removed the subject.
/// </summary>
public class InpaintStrategyTests
{
    // Bright, uniform background around a dark, sharply-bordered subject blob (mirrors the
    // fixture used by the other classical strategy tests).
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

    private static StrategyContext Context() => new() { DecontaminateEdges = false };

    [Fact]
    public async Task BorderBackground_Removed_SubjectKeptOpaque()
    {
        var strategy = new InpaintStrategy();
        using var bgr = MakeSubjectImage();

        using var result = await strategy.RunFullAsync(bgr, Context(), CancellationToken.None);
        using var alpha = ExtractAlpha(result);

        Assert.InRange(alpha.At<byte>(100, 100), 128, byte.MaxValue);    // dark subject: opaque
        Assert.InRange(alpha.At<byte>(5, 5), byte.MinValue, 127);        // bright border: background
    }

    [Fact]
    public async Task UniformImage_EverythingIsBackground_FullyTransparent()
    {
        var strategy = new InpaintStrategy();
        using var bgr = new Mat(40, 40, MatType.CV_8UC3, new Scalar(150, 150, 150));

        using var result = await strategy.RunFullAsync(bgr, Context(), CancellationToken.None);
        using var alpha = ExtractAlpha(result);

        Assert.Equal(0, alpha.At<byte>(20, 20));
        Assert.Equal(0, alpha.At<byte>(0, 0));
    }

    [Fact]
    public async Task OneByOneImage_DoesNotCrash()
    {
        var strategy = new InpaintStrategy();
        using var bgr = new Mat(1, 1, MatType.CV_8UC3, new Scalar(120, 120, 120));

        using var result = await strategy.RunFullAsync(bgr, Context(), CancellationToken.None);

        Assert.Equal(1, result.Bgra.Width);
        Assert.Equal(1, result.Bgra.Height);
    }

    [Fact]
    public async Task PreCanceledToken_ThrowsOperationCanceled()
    {
        var strategy = new InpaintStrategy();
        using var bgr = MakeSubjectImage();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => strategy.RunFullAsync(bgr, Context(), cts.Token));
    }
}
