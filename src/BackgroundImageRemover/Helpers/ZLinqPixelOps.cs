using System;
using System.Numerics;
using ZLinq.Simd;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Vectorized (SIMD) per-pixel transforms over flat float spans, built on ZLinq's
/// <c>VectorizedUpdate</c> (Cysharp/ZLinq, the sibling of the already-used SimdLinq).
///
/// Every transform is supplied twice: a <see cref="Vector{T}"/> lambda for the SIMD bulk
/// and a scalar lambda for the tail, and ZLinq picks the vector path only when the span
/// is long enough and the hardware supports it. The two lambdas must be semantically
/// identical — for the transforms below they are bit-identical, because float division,
/// floor and clamping are all exactly specified in IEEE 754.
///
/// ZLinq deliberately restricts <c>Select</c>/<c>Zip</c>/<c>Update</c> to same-size
/// element types, so these helpers operate in place on float buffers; the subsequent
/// float→byte cast stays a plain loop (conversion to a smaller type is not vectorizable
/// through ZLinq's pipeline).
/// </summary>
public static class ZLinqPixelOps
{
    /// <summary>
    /// In-place: <c>v = clamp((v - min) / range * 255, 0, 255)</c> — the mask rescale used
    /// when turning ONNX saliency outputs into 0-255 byte masks.
    /// </summary>
    public static void NormalizeMaskToByteRange(Span<float> values, float min, float range)
    {
        var minV = new Vector<float>(min);
        var rangeV = new Vector<float>(range);
        var scaleV = new Vector<float>(255f);
        var zeroV = Vector<float>.Zero;
        values.VectorizedUpdate<float>(
            vec => Vector.Clamp((vec - minV) / rangeV * scaleV, zeroV, scaleV),
            v => Math.Clamp((v - min) / range * 255f, 0f, 255f));
    }

    /// <summary>
    /// In-place: <c>v = v &gt; 0 ? 1 : 0</c> — the threshold applied to SAM decoder outputs
    /// before they are converted to 0/255 mask bytes.
    /// </summary>
    public static void ThresholdToUnit(Span<float> values)
    {
        values.VectorizedUpdate<float>(
            vec => Vector.ConditionalSelect(Vector.GreaterThan(vec, Vector<float>.Zero), Vector<float>.One, Vector<float>.Zero),
            v => v > 0 ? 1f : 0f);
    }

    /// <summary>
    /// In-place: <c>v = v % 180; if (v &lt; 0) v += 180</c> — OpenCV's hue wrap. The vector
    /// path computes the remainder as <c>v - 180 * floor(v / 180)</c>, which is bit-identical
    /// to the scalar <c>%</c> for non-negative inputs; the caller guarantees the positive
    /// domain (<c>h + shift + 360</c> ∈ [270, 630)), matching the previous <c>shiftPositive</c>
    /// buffer.
    /// </summary>
    public static void WrapHue180(Span<float> values)
    {
        var c180 = new Vector<float>(180f);
        values.VectorizedUpdate<float>(
            vec => vec - c180 * Vector.Floor(vec / c180),
            v =>
            {
                float m = v % 180f;
                return m < 0 ? m + 180f : m;
            });
    }
}
