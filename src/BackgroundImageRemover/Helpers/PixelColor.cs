using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Per-channel color math shared by the per-pixel editing services: byte clamping and the
/// "blend toward a target" formula <c>from + (to - from) * t</c> that Duotone, ColorReplace
/// and Thermal each re-rolled with per-channel Math.Round / Math.Clamp boilerplate.
/// </summary>
public static class PixelColor
{
    /// <summary>Rounds and clamps a double into the byte range [0, 255].</summary>
    public static byte ClampByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value), 0, 255);
    }

    /// <summary>Linearly interpolates between two bytes: <c>from + (to - from) * t</c>, rounded and clamped.</summary>
    public static byte BlendByte(byte from, byte to, double t)
    {
        return ClampByte(from + (to - from) * t);
    }

    /// <summary>Linearly interpolates each channel between two BGR colors.</summary>
    public static Vec3b Blend(Vec3b from, Vec3b to, double t)
    {
        return new Vec3b(
            BlendByte(from[0], to[0], t),
            BlendByte(from[1], to[1], t),
            BlendByte(from[2], to[2], t));
    }

    /// <summary>
    /// Blends two channel values with explicit weights: <c>first * firstWeight + second * secondWeight</c>
    /// (the weights normally sum to 1), rounded away from zero and clamped to [0, 255]. Unifies the
    /// private <c>BlendByte</c> helpers that the mask-blend (<see cref="MatExtensions.BlendByMask"/>),
    /// alpha-composite and Uncrop fill passes each re-rolled. The second channel is a float because
    /// some callers blend from CV_32F sources (values still on the 0..255 scale).
    /// </summary>
    public static byte BlendWeighted(byte first, float firstWeight, float second, float secondWeight)
    {
        float v = first * firstWeight + second * secondWeight;
        return (byte)Math.Clamp(Math.Round(v, MidpointRounding.AwayFromZero), 0, 255);
    }
}
