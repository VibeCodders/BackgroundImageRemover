using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Flips an image along the requested axis (or both) using OpenCv's <see cref="Cv2.Flip"/>.
/// </summary>
public static class FlipService
{
    /// <summary>
    /// Flips <paramref name="input"/> according to <paramref name="mode"/> and returns a new Mat.
    /// A null or empty input is returned as an empty Mat.
    /// </summary>
    public static Mat Flip(Mat input, ImageFlipMode mode)
    {
        if (input is null || input.Empty())
        {
            return new Mat();
        }

        var flipCode = mode switch
        {
            ImageFlipMode.Horizontal => FlipMode.Y,
            ImageFlipMode.Vertical => FlipMode.X,
            ImageFlipMode.Both => FlipMode.XY,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        var result = new Mat();
        Cv2.Flip(input, result, flipCode);
        return result;
    }
}
