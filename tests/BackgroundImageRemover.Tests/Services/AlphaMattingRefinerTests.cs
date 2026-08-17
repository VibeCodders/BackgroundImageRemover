using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class AlphaMattingRefinerTests
{
    [Fact]
    public void Refine_OnUniformImage_LeavesAUniformOpaqueRegionOpaque()
    {
        using var bgr = new Mat(40, 40, MatType.CV_8UC3, Scalar.All(128));
        using var roughAlpha = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(255));

        using var refined = AlphaMattingRefiner.Refine(bgr, roughAlpha);

        // Far from any border effects, a uniform fully-opaque mask over a flat-color guide
        // image should stay fully opaque.
        Assert.InRange(refined.At<byte>(20, 20), 250, 255);
    }

    [Fact]
    public void Refine_OnUniformImage_LeavesAUniformTransparentRegionTransparent()
    {
        using var bgr = new Mat(40, 40, MatType.CV_8UC3, Scalar.All(128));
        using var roughAlpha = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(0));

        using var refined = AlphaMattingRefiner.Refine(bgr, roughAlpha);

        Assert.InRange(refined.At<byte>(20, 20), 0, 5);
    }

    [Fact]
    public void Refine_ReturnsSameSizeAndSingleChannel()
    {
        using var bgr = new Mat(30, 50, MatType.CV_8UC3, Scalar.All(100));
        using var roughAlpha = new Mat(30, 50, MatType.CV_8UC1, Scalar.All(200));

        using var refined = AlphaMattingRefiner.Refine(bgr, roughAlpha);

        Assert.Equal(bgr.Size(), refined.Size());
        Assert.Equal(1, refined.Channels());
        Assert.Equal(MatType.CV_8U, refined.Type());
    }

    [Fact]
    public void Refine_ProducesFiniteValues_AcrossAHardEdge()
    {
        // A step edge in both the guide image and the rough alpha is the scenario this filter
        // exists for; the division by (varI + eps) must never blow up into NaN/Inf.
        using var bgr = new Mat(40, 40, MatType.CV_8UC3, Scalar.All(0));
        using var brightHalf = new Mat(bgr, new Rect(20, 0, 20, 40));
        brightHalf.SetTo(Scalar.All(255));

        using var roughAlpha = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(0));
        using var opaqueHalf = new Mat(roughAlpha, new Rect(20, 0, 20, 40));
        opaqueHalf.SetTo(Scalar.All(255));

        using var refined = AlphaMattingRefiner.Refine(bgr, roughAlpha);

        for (int y = 0; y < refined.Height; y++)
        {
            for (int x = 0; x < refined.Width; x++)
            {
                byte v = refined.At<byte>(y, x);
                Assert.InRange(v, (byte)0, (byte)255);
            }
        }
    }
}
