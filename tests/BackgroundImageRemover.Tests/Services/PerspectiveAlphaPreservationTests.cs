using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Pins the Perspective-tool contract: when a cutout (BGR + alpha) is perspective-corrected,
/// the alpha channel must go through the same warp as the color, so the subject's transparency
/// survives the operation (a regression where Apply replaced the alpha with an opaque rectangle).
/// </summary>
public class PerspectiveAlphaPreservationTests
{
    [Fact]
    public void Correct_WarpsAlphaAlongWithColor()
    {
        // A 60x60 cutout: opaque red square on a fully transparent background.
        using var bgr = new Mat(60, 60, MatType.CV_8UC3, new Scalar(0, 0, 255));
        using var alpha = new Mat(60, 60, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(alpha, new Rect(15, 15, 30, 30), new Scalar(255), -1);

        // A keystone warp: pull the top corners inward, mirroring what the tool does.
        var quad = (
            new Point2f(10, 0),
            new Point2f(50, 0),
            new Point2f(59, 59),
            new Point2f(0, 59));

        using var warpedBgr = PerspectiveService.Correct(
            bgr, quad.Item1, quad.Item2, quad.Item3, quad.Item4, 60, 60, InterpolationFlags.Linear);
        using var warpedAlpha = PerspectiveService.Correct(
            alpha, quad.Item1, quad.Item2, quad.Item3, quad.Item4, 60, 60, InterpolationFlags.Linear);

        Assert.Equal(warpedBgr.Size(), warpedAlpha.Size());
        Assert.True(Cv2.CountNonZero(warpedAlpha) > 0, "expected the opaque subject to survive the warp");
        Assert.True(warpedAlpha.At<byte>(0, 30) < 255, "expected the warped top edge to stay transparent");
        Assert.True(warpedAlpha.At<byte>(30, 30) > 0, "expected the subject center to remain at least partially opaque");
    }

    [Fact]
    public void Correct_IdentityQuad_KeepsAlphaShape()
    {
        using var bgr = new Mat(40, 50, MatType.CV_8UC3, new Scalar(0, 0, 255));
        using var alpha = new Mat(40, 50, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Circle(alpha, new Point(20, 25), 12, new Scalar(255), -1);

        var quad = PerspectiveService.DefaultQuad(new Size(50, 40));

        using var warpedAlpha = PerspectiveService.Correct(
            alpha, quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft, 50, 40, InterpolationFlags.Linear);

        Assert.Equal(new Size(50, 40), warpedAlpha.Size());
        Assert.True(Cv2.CountNonZero(warpedAlpha) > 100, "expected the circular subject to survive an identity warp");
    }
}
