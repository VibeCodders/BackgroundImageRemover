using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Refinement;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class BrushEditorTests
{
    [Fact]
    public void StampSegment_Restore_SetsCenterPixelToOpaque()
    {
        using var alpha = new Mat(50, 50, MatType.CV_8UC1, Scalar.All(0));

        BrushEditor.StampSegment(alpha, new Point2f(25, 25), new Point2f(25, 25), radius: 10, hardness: 0.5, BrushMode.Restore);

        Assert.Equal(255, alpha.Get<byte>(25, 25));
    }

    [Fact]
    public void StampSegment_Erase_SetsCenterPixelToTransparent()
    {
        using var alpha = new Mat(50, 50, MatType.CV_8UC1, Scalar.All(255));

        BrushEditor.StampSegment(alpha, new Point2f(25, 25), new Point2f(25, 25), radius: 10, hardness: 0.5, BrushMode.Erase);

        Assert.Equal(0, alpha.Get<byte>(25, 25));
    }

    [Fact]
    public void StampSegment_DoesNotAffectPixelsFarOutsideRadius()
    {
        using var alpha = new Mat(50, 50, MatType.CV_8UC1, Scalar.All(0));

        BrushEditor.StampSegment(alpha, new Point2f(5, 5), new Point2f(5, 5), radius: 3, hardness: 0.5, BrushMode.Restore);

        Assert.Equal(0, alpha.Get<byte>(45, 45));
    }
}
