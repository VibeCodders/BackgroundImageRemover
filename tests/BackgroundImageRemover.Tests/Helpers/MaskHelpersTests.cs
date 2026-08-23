using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Tests for the shared strategy mask post-processing (<see cref="MaskHelpers"/>): the
/// feather blur that Otsu/KMeans/MagicWand/FloodFill/EdgeContour/GrabCut all repeated, and
/// the keep-largest-filled-region contour step shared by Otsu and EdgeContour.
/// </summary>
public class MaskHelpersTests
{
    // ------------------------------------------------------------------ Feather

    [Fact]
    public void Feather_ReturnsNewBlurredMat_SameSizeAndType()
    {
        using var mask = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(0));
        using var roi = new Mat(mask, new Rect(10, 10, 20, 20));
        roi.SetTo(new Scalar(255));
        var size = mask.Size();

        // Feather takes ownership of its input (it disposes it), so pass a clone and keep the
        // original for assertions.
        var feathered = MaskHelpers.Feather(mask.Clone(), kernelSize: 5);

        Assert.Equal(size, feathered.Size());
        Assert.Equal(MatType.CV_8UC1, feathered.Type());
        // Edges are softened: the border of the white block is no longer a hard 0/255 step.
        Assert.InRange(feathered.At<byte>(10, 10), 1, 254);
        // The core stays near-opaque.
        Assert.InRange(feathered.At<byte>(20, 20), 200, 255);

        feathered.Dispose();
    }

    [Fact]
    public void Feather_LargerKernel_BlursMore()
    {
        using var mask = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(0));
        using var roi = new Mat(mask, new Rect(10, 10, 20, 20));
        roi.SetTo(new Scalar(255));

        using var smallKernel = MaskHelpers.Feather(mask.Clone(), kernelSize: 3);
        using var largeKernel = MaskHelpers.Feather(mask.Clone(), kernelSize: 11);

        // A bigger kernel spreads the white further into the black: a point just outside the
        // block (8,8) stays ~black with a small kernel but picks up white with a large one.
        Assert.True(largeKernel.At<byte>(8, 8) > smallKernel.At<byte>(8, 8));
    }

    // ------------------------------------------------------------------ KeepLargestFilledRegion

    [Fact]
    public void KeepLargestFilledRegion_KeepsOnlyTheLargestComponent()
    {
        using var binary = new Mat(50, 50, MatType.CV_8UC1, Scalar.All(0));
        using (var big = new Mat(binary, new Rect(5, 5, 20, 20)))
        {
            big.SetTo(new Scalar(255));
        }
        using (var small = new Mat(binary, new Rect(40, 40, 5, 5)))
        {
            small.SetTo(new Scalar(255));
        }

        using var result = MaskHelpers.KeepLargestFilledRegion(binary);

        Assert.Equal(255, result.At<byte>(15, 15)); // inside the big block
        Assert.Equal(0, result.At<byte>(42, 42));   // the small block is dropped
        Assert.Equal(0, result.At<byte>(49, 49));   // corner stays black
    }

    [Fact]
    public void KeepLargestFilledRegion_FillsHolesInsideTheRegion()
    {
        // A ring (white border, black center): filling the outer contour closes the hole.
        using var binary = new Mat(50, 50, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(binary, new Rect(10, 10, 30, 30), new Scalar(255), thickness: 2);

        using var result = MaskHelpers.KeepLargestFilledRegion(binary);

        Assert.Equal(255, result.At<byte>(25, 25)); // hole is now filled
        Assert.Equal(255, result.At<byte>(10, 25)); // ring border kept
        Assert.Equal(0, result.At<byte>(5, 5));     // outside stays black
    }

    [Fact]
    public void KeepLargestFilledRegion_NoContours_ReturnsAllBlack()
    {
        using var binary = new Mat(30, 30, MatType.CV_8UC1, Scalar.All(0));

        using var result = MaskHelpers.KeepLargestFilledRegion(binary);

        Assert.Equal(0, Cv2.CountNonZero(result));
        Assert.Equal(binary.Size(), result.Size());
    }

    [Fact]
    public void KeepLargestFilledRegion_LeavesInputUntouched()
    {
        using var binary = new Mat(50, 50, MatType.CV_8UC1, Scalar.All(0));
        using (var big = new Mat(binary, new Rect(5, 5, 20, 20)))
        {
            big.SetTo(new Scalar(255));
        }

        using var result = MaskHelpers.KeepLargestFilledRegion(binary);

        Assert.Equal(255, binary.At<byte>(15, 15)); // input unchanged
        Assert.Equal(255, result.At<byte>(15, 15));
    }

    // ------------------------------------------------------------------ FloodFillBorderMask

    [Fact]
    public void FloodFillBorderMask_UniformImage_FloodsEverything()
    {
        using var bgr = new Mat(40, 40, MatType.CV_8UC3, new Scalar(120, 120, 120));
        var seeds = new[] { new Point(0, 0), new Point(39, 39) };

        using var mask = MaskHelpers.FloodFillBorderMask(bgr, seeds, tolerance: 20);

        Assert.Equal(255, mask.At<byte>(20, 20));
        Assert.Equal(255, mask.At<byte>(0, 0));
    }

    [Fact]
    public void FloodFillBorderMask_SubjectInCenter_FloodsOnlyTheBackground()
    {
        using var bgr = new Mat(60, 60, MatType.CV_8UC3, new Scalar(210, 210, 210));
        using (var subject = new Mat(bgr, new Rect(25, 25, 10, 10)))
        {
            subject.SetTo(new Scalar(30, 30, 30));
        }

        using var mask = MaskHelpers.FloodFillBorderMask(bgr, new[] { new Point(0, 0) }, tolerance: 20);

        Assert.Equal(255, mask.At<byte>(5, 5));    // border: background
        Assert.Equal(0, mask.At<byte>(30, 30));    // subject center: not flooded
    }

    [Fact]
    public void FloodFillBorderMask_OneByOneImage_DoesNotCrash()
    {
        using var bgr = new Mat(1, 1, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var mask = MaskHelpers.FloodFillBorderMask(bgr, new[] { new Point(0, 0) }, tolerance: 20);

        Assert.Equal(new Size(1, 1), mask.Size());
        Assert.Equal(255, mask.At<byte>(0, 0));
    }
}
