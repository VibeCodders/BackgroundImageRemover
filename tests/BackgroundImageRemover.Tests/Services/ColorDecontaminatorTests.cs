using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class ColorDecontaminatorTests
{
    [Fact]
    public void Decontaminate_KnownBackground_SuppressesSpillOnEdgePixel()
    {
        // A semi-transparent edge pixel with a green cast (chroma key): the key alpha is a soft
        // key, not true coverage, so the fix is to neutralize the dominant green channel rather
        // than recover a pure foreground color.
        using var bgra = new Mat(1, 1, MatType.CV_8UC4);
        bgra.Set(0, 0, new Vec4b(0, 127, 127, 128)); // BGR = (0,127,127), alpha = 128

        ColorDecontaminator.Decontaminate(bgra, new Vec3b(0, 255, 0)); // green key

        var px = bgra.At<Vec4b>(0, 0);
        Assert.Equal(128, px.Item3);        // alpha untouched
        Assert.InRange(px.Item1, 90, 100);  // dominant green pulled toward the average of R/B
        Assert.Equal(127, px.Item2);        // red channel left untouched
        Assert.Equal(0, px.Item0);          // blue channel left untouched
    }

    [Fact]
    public void Decontaminate_LocalEstimate_RemovesSpillNearTransparentBackground()
    {
        // 40x40: left half is a transparent green background, right half is a semi-transparent
        // red-over-green edge. The background color must be estimated from the transparent side.
        using var bgra = new Mat(40, 40, MatType.CV_8UC4);
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                if (x < 20)
                {
                    bgra.Set(y, x, new Vec4b(0, 255, 0, 0));      // transparent green background
                }
                else
                {
                    bgra.Set(y, x, new Vec4b(0, 127, 127, 128));  // semi-transparent blended edge
                }
            }
        }

        ColorDecontaminator.Decontaminate(bgra, null);

        // A pixel just inside the edge still has transparent neighbors within the estimation kernel.
        var px = bgra.At<Vec4b>(20, 21);
        Assert.InRange(px.Item1, 0, 8);      // green spill largely removed
        Assert.InRange(px.Item2, 240, 255);  // red foreground recovered
    }

    [Fact]
    public void Decontaminate_ConfigurableRadius_IsHonored()
    {
        // Same setup as the local-estimate test: a transparent green background on the left,
        // semi-transparent blended edge pixels on the right. A tiny radius still sees the
        // adjacent transparent background, so the spill is removed.
        using var bgra = new Mat(20, 20, MatType.CV_8UC4);
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                bgra.Set(y, x, x < 10
                    ? new Vec4b(0, 255, 0, 0)
                    : new Vec4b(0, 127, 127, 128));
            }
        }

        ColorDecontaminator.Decontaminate(bgra, null, estimateRadius: 3);

        var px = bgra.At<Vec4b>(10, 10);
        Assert.InRange(px.Item1, 0, 8);      // green spill removed with the small radius too
        Assert.InRange(px.Item2, 240, 255);  // red foreground recovered
    }

    [Fact]
    public void Decontaminate_LeavesOpaqueAndTransparentPixelsUntouched()
    {
        using var bgra = new Mat(2, 1, MatType.CV_8UC4);
        bgra.Set(0, 0, new Vec4b(10, 20, 30, 255)); // opaque foreground
        bgra.Set(0, 1, new Vec4b(0, 255, 0, 0));    // fully transparent background

        ColorDecontaminator.Decontaminate(bgra, new Vec3b(0, 255, 0));

        Assert.Equal(new Vec4b(10, 20, 30, 255), bgra.At<Vec4b>(0, 0));
        Assert.Equal(new Vec4b(0, 255, 0, 0), bgra.At<Vec4b>(0, 1));
    }
}
