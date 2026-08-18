using BackgroundImageRemover.Helpers;
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
}
