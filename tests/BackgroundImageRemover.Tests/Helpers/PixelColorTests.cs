using BackgroundImageRemover.Helpers;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

public class PixelColorTests
{
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(127.4, 127)]
    [InlineData(127.6, 128)]
    [InlineData(255.0, 255)]
    public void ClampByte_RoundsToNearestByte(double value, byte expected)
    {
        Assert.Equal(expected, PixelColor.ClampByte(value));
    }

    [Theory]
    [InlineData(-10.0, 0)]
    [InlineData(-0.4, 0)]
    [InlineData(255.4, 255)]
    [InlineData(1000.0, 255)]
    public void ClampByte_ClampsIntoByteRange(double value, byte expected)
    {
        Assert.Equal(expected, PixelColor.ClampByte(value));
    }

    [Theory]
    [InlineData(0, 255, 0.0, 0)]
    [InlineData(0, 255, 1.0, 255)]
    [InlineData(0, 255, 0.5, 128)] // 127.5 rounds to 128
    [InlineData(100, 200, 0.5, 150)]
    [InlineData(100, 100, 0.9, 100)]
    public void BlendByte_InterpolatesBetweenEndpoints(byte from, byte to, double t, byte expected)
    {
        Assert.Equal(expected, PixelColor.BlendByte(from, to, t));
    }

    [Fact]
    public void BlendByte_ClampsOvershootBeyondRange()
    {
        // t outside [0,1] extrapolates past the endpoints; the result must stay a byte.
        Assert.Equal(0, PixelColor.BlendByte(100, 50, 10.0));
        Assert.Equal(255, PixelColor.BlendByte(100, 200, 10.0));
    }

    [Fact]
    public void Blend_InterpolatesEveryChannel()
    {
        var from = new Vec3b(10, 100, 200);   // B, G, R
        var to = new Vec3b(110, 200, 20);

        var mid = PixelColor.Blend(from, to, 0.5);

        Assert.Equal(60, mid.Item0);
        Assert.Equal(150, mid.Item1);
        Assert.Equal(110, mid.Item2);
    }

    [Fact]
    public void Blend_EndpointsReturnInputs()
    {
        var from = new Vec3b(40, 80, 120);
        var to = new Vec3b(200, 160, 90);

        Assert.Equal(from, PixelColor.Blend(from, to, 0.0));
        Assert.Equal(to, PixelColor.Blend(from, to, 1.0));
    }
}
