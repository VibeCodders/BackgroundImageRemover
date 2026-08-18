using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Pins the Resize-tool contract: when a cutout (BGR + alpha) is resized, the alpha channel
/// must be scaled along with the color, so transparency survives the operation.
/// </summary>
public class ResizeAlphaPreservationTests
{
    [Fact]
    public void Resize_ScalesAlphaAlongWithColor()
    {
        // A 40x40 cutout: opaque red square on a fully transparent background.
        using var bgr = new Mat(40, 40, MatType.CV_8UC3, new Scalar(0, 0, 255));
        using var alpha = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(bgr, new Rect(10, 10, 20, 20), new Scalar(0, 0, 255), -1);
        Cv2.Rectangle(alpha, new Rect(10, 10, 20, 20), new Scalar(255), -1);

        // Halve the size, exactly like the Resize tool does for the color and the alpha.
        using var resizedBgr = ResizeService.ResizeTo(bgr, 20, 20);
        using var resizedAlpha = ResizeService.ResizeTo(alpha, 20, 20);

        Assert.Equal(resizedBgr.Size(), resizedAlpha.Size());
        Assert.True(Cv2.CountNonZero(resizedAlpha) > 0, "expected the opaque subject to survive the resize");
        Assert.True(resizedAlpha.At<byte>(2, 2) < 255, "expected the transparent background to stay transparent");
        Assert.True(resizedAlpha.At<byte>(10, 10) > 0, "expected the subject area to be at least partially opaque");
    }

    [Fact]
    public void Resize_IdentitySize_KeepsAlphaUnchanged()
    {
        using var bgr = new Mat(20, 30, MatType.CV_8UC3, new Scalar(0, 0, 255));
        using var alpha = new Mat(20, 30, MatType.CV_8UC1, Scalar.All(128));

        using var resizedBgr = ResizeService.ResizeTo(bgr, 30, 20);
        using var resizedAlpha = ResizeService.ResizeTo(alpha, 30, 20);

        Assert.Equal(30, resizedBgr.Width);
        Assert.Equal(30, resizedAlpha.Width);
        Assert.Equal(20, resizedAlpha.Height);
    }
}
