using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

public sealed class MatExtensionsTests
{
    [Fact]
    public void ToBgra_FromBgr_ProducesFourChannels()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var bgra = bgr.ToBgra();

        Assert.Equal(4, bgra.Channels());
        Assert.Equal(10, bgra.Width);
        Assert.Equal(10, bgra.Height);
    }

    [Fact]
    public void ToBgr_FromBgra_ProducesThreeChannels()
    {
        using var bgra = new Mat(10, 10, MatType.CV_8UC4, new Scalar(10, 20, 30, 255));
        using var bgr = bgra.ToBgr();

        Assert.Equal(3, bgr.Channels());
        Assert.Equal(10, bgr.Width);
        Assert.Equal(10, bgr.Height);
    }

    [Fact]
    public void SetAlphaChannel_And_ExtractAlphaChannel_WorkAccurately()
    {
        using var bgra = new Mat(5, 5, MatType.CV_8UC4, new Scalar(0, 0, 0, 255));
        using var alpha = new Mat(5, 5, MatType.CV_8UC1, new Scalar(128));

        bgra.SetAlphaChannel(alpha);

        using var extracted = bgra.ExtractAlphaChannel();
        Assert.Equal(1, extracted.Channels());
        Assert.Equal(128, extracted.At<byte>(0, 0));
    }

    [Fact]
    public void CloneSafe_HandlesNullGracefully()
    {
        Mat? nullMat = null;
        Assert.Null(nullMat.CloneSafe());
    }

    [Fact]
    public void BlendByMask_BlackMask_ReturnsOriginal()
    {
        using var original = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var modified = new Mat(5, 5, MatType.CV_8UC3, new Scalar(200, 200, 200));
        using var mask = new Mat(5, 5, MatType.CV_8UC1, Scalar.All(0));

        using var result = original.BlendByMask(modified, mask);

        Assert.Equal(100, result.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void BlendByMask_WhiteMask_ReturnsModified()
    {
        using var original = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var modified = new Mat(5, 5, MatType.CV_8UC3, new Scalar(200, 200, 200));
        using var mask = new Mat(5, 5, MatType.CV_8UC1, Scalar.All(255));

        using var result = original.BlendByMask(modified, mask);

        Assert.Equal(200, result.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void BlendByMask_50PercentMask_ReturnsMidpoint()
    {
        using var original = new Mat(5, 5, MatType.CV_8UC3, new Scalar(0, 0, 0));
        using var modified = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));
        using var mask = new Mat(5, 5, MatType.CV_8UC1, Scalar.All(128));

        using var result = original.BlendByMask(modified, mask);

        byte mid = result.At<Vec3b>(0, 0).Item0;
        Assert.InRange(mid, (byte)48, (byte)52);
    }

    [Fact]
    public void GetWorkingAlpha_WithAlpha_ReturnsClone()
    {
        var alpha = new Mat(10, 10, MatType.CV_8UC1, new Scalar(128));
        var image = new LoadedImage("test", new Mat(10, 10, MatType.CV_8UC3), alpha);
        var result = image.GetWorkingAlpha();

        Assert.Equal(128, result.At<byte>(0, 0));
    }

    [Fact]
    public void GetWorkingAlpha_NullAlpha_ReturnsOpaque()
    {
        var bgr = new Mat(10, 10, MatType.CV_8UC3);
        var image = new LoadedImage("test", bgr, null);
        var result = image.GetWorkingAlpha();

        Assert.Equal(255, result.At<byte>(0, 0));
    }

    [Fact]
    public void ToResultBitmap_NullBgr_ReturnsNull()
    {
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, new Scalar(255));

        Assert.Null(((Mat?)null).ToResultBitmap(alpha));
    }

    [Fact]
    public void ToResultBitmap_NullAlpha_ReturnsNull()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 20, 30));

        Assert.Null(bgr.ToResultBitmap(null));
    }

    [Fact]
    public void ToResultBitmap_ValidPair_PreservesAlpha()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var alpha = new Mat(10, 10, MatType.CV_8UC1, new Scalar(128));

        Assert.Equal((byte)128, SampleAlphaOnSta(() => bgr.ToResultBitmap(alpha), 0, 0));
    }

    [Fact]
    public void ToPreviewBitmap_OpaqueAlpha_RendersPlain()
    {
        // A uniformly opaque alpha is not meaningful transparency: the plain BGR path runs,
        // which produces a 3-channel bitmap (opaque by construction, no alpha channel).
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var opaqueAlpha = new Mat(20, 20, MatType.CV_8UC1, Scalar.All(255));

        Assert.Equal(PixelFormats.Bgr24, FormatOnSta(() => bgr.ToPreviewBitmap(opaqueAlpha)));
    }

    [Fact]
    public void ToPreviewBitmap_MeaningfulAlpha_PreservesTransparency()
    {
        // Preview 10x10 built from a 20x20 alpha with a fully transparent 2x2 block in the
        // center: the preview pixel (5,5) is the area-average of that block and must be 0,
        // while the corner pixel stays opaque.
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var alpha = new Mat(20, 20, MatType.CV_8UC1, Scalar.All(255));
        alpha.Set(10, 10, (byte)0);
        alpha.Set(10, 11, (byte)0);
        alpha.Set(11, 10, (byte)0);
        alpha.Set(11, 11, (byte)0);

        Assert.Equal((byte)255, SampleAlphaOnSta(() => bgr.ToPreviewBitmap(alpha), 0, 0));
        Assert.Equal((byte)0, SampleAlphaOnSta(() => bgr.ToPreviewBitmap(alpha), 5, 5));
    }

    [Fact]
    public void ToPreviewBitmap_NullAlpha_RendersPlain()
    {
        using var bgr = new Mat(10, 10, MatType.CV_8UC3, new Scalar(10, 20, 30));

        Assert.Equal(PixelFormats.Bgr24, FormatOnSta(() => bgr.ToPreviewBitmap(null)));
    }

    /// <summary>Creates a BitmapSource on an STA thread and samples the alpha byte of one pixel.
    /// WPF bitmaps are thread-affine, so creation and sampling must share the thread.</summary>
    private static byte SampleAlphaOnSta(Func<BitmapSource?> create, int x, int y)
    {
        byte alpha = 0;
        var thread = new Thread(() =>
        {
            var bitmap = create()!;
            var buffer = new byte[4];
            bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), buffer, 4, 0);
            alpha = buffer[3];
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA bitmap sample thread timed out");
        return alpha;
    }

    /// <summary>Creates a BitmapSource on an STA thread and returns its pixel format.
    /// WPF bitmaps are thread-affine, so creation must happen on the STA thread.</summary>
    private static PixelFormat FormatOnSta(Func<BitmapSource?> create)
    {
        PixelFormat format = PixelFormats.Default;
        var thread = new Thread(() =>
        {
            var bitmap = create()!;
            format = bitmap.Format;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA bitmap format thread timed out");
        return format;
    }
}
