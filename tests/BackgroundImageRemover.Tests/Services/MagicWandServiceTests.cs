using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class MagicWandServiceTests
{
    private static Mat MakeTwoRegionImage()
    {
        // Left half solid blue, right half solid red -- two flood-fillable regions.
        var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(255, 0, 0));
        using var right = new Mat(bgr, new Rect(5, 0, 5, 10));
        right.SetTo(new Scalar(0, 0, 255));
        return bgr;
    }

    [Fact]
    public void Apply_Add_SetsOnlyTheFloodedRegionToOpaque()
    {
        using var bgr = MakeTwoRegionImage();
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));

        MagicWandService.Apply(bgr, alpha, new Point(2, 5), tolerance: 10, add: true);

        Assert.Equal(255, alpha.At<byte>(5, 2)); // inside seeded (left/blue) region
        Assert.Equal(0, alpha.At<byte>(5, 7));   // outside, in the red region
    }

    [Fact]
    public void Apply_Remove_SetsOnlyTheFloodedRegionToTransparent()
    {
        using var bgr = MakeTwoRegionImage();
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(255));

        MagicWandService.Apply(bgr, alpha, new Point(7, 5), tolerance: 10, add: false);

        Assert.Equal(0, alpha.At<byte>(5, 7));     // inside seeded (right/red) region
        Assert.Equal(255, alpha.At<byte>(5, 2));   // outside, in the blue region, untouched
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, -1)]
    [InlineData(10, 5)]
    [InlineData(5, 10)]
    public void Apply_WithSeedOutsideImageBounds_DoesNothing(int x, int y)
    {
        using var bgr = MakeTwoRegionImage();
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));

        MagicWandService.Apply(bgr, alpha, new Point(x, y), tolerance: 10, add: true);

        Assert.Equal(0, Cv2.CountNonZero(alpha));
    }

    [Fact]
    public void Apply_DoesNotCrossIntoDissimilarColorBeyondTolerance()
    {
        using var bgr = MakeTwoRegionImage();
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(0));

        // Blue (255,0,0) vs red (0,0,255) differ by 255 per channel -- well beyond a small tolerance.
        MagicWandService.Apply(bgr, alpha, new Point(2, 5), tolerance: 10, add: true);

        // Exactly the left half (50 px) should have been selected, not the whole image.
        Assert.Equal(50, Cv2.CountNonZero(alpha));
    }
}
