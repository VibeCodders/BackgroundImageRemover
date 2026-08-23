using BackgroundImageRemover.Helpers;
using Xunit;

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Locks the ZLinq SIMD helpers to the exact scalar math they replace: the vector path and
/// the scalar tail must produce bit-identical values, including spans shorter than a vector
/// (which exercise the scalar lambda only).
/// </summary>
public class ZLinqPixelOpsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(257)]
    public void NormalizeMaskToByteRange_MatchesScalarMath(int length)
    {
        var rng = new Random(1234);
        var values = new float[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        float min = -0.4f;
        float range = 1.6f;

        var expected = new float[length];
        for (int i = 0; i < length; i++)
        {
            expected[i] = Math.Clamp((values[i] - min) / range * 255f, 0f, 255f);
        }

        ZLinqPixelOps.NormalizeMaskToByteRange(values, min, range);

        Assert.Equal(expected, values);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(257)]
    public void ThresholdToUnit_MatchesScalarThreshold(int length)
    {
        var rng = new Random(4321);
        var values = new float[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        var expected = new float[length];
        for (int i = 0; i < length; i++)
        {
            expected[i] = values[i] > 0 ? 1f : 0f;
        }

        ZLinqPixelOps.ThresholdToUnit(values);

        Assert.Equal(expected, values);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(257)]
    public void WrapHue180_MatchesScalarModulo(int length)
    {
        var rng = new Random(999);
        var values = new float[length];
        for (int i = 0; i < length; i++)
        {
            // The hue-wrap input domain: h + shift + 360 ∈ [270, 630).
            values[i] = (float)(rng.NextDouble() * 360.0 + 270.0);
        }

        var expected = new float[length];
        for (int i = 0; i < length; i++)
        {
            float m = values[i] % 180f;
            if (m < 0)
            {
                m += 180f;
            }

            expected[i] = m;
        }

        ZLinqPixelOps.WrapHue180(values);

        Assert.Equal(expected, values);
    }

    [Fact]
    public void WrapHue180_NegativeValues_FallBackToScalarGuard()
    {
        // The scalar tail keeps the legacy `if (v < 0) v += 180` guard even though the
        // caller only feeds the positive domain. Keep the span below every possible
        // Vector<float>.Count so only the scalar lambda runs, regardless of hardware.
        var values = new[] { -200f, -90f, 540f };
        var expected = new[] { 160f, 90f, 0f };

        ZLinqPixelOps.WrapHue180(values);

        Assert.Equal(expected, values);
    }
}
