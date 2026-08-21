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
}
