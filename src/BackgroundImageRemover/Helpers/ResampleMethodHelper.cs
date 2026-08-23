using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Utility for converting ResampleMethod to OpenCV InterpolationFlags.
/// Eliminates the duplicated conversion logic that was previously copy-pasted
/// across ResizeService and PerspectiveToolSessionViewModel.
/// </summary>
public static class ResampleMethodHelper
{
    /// <summary>
    /// Converts a ResampleMethod to the corresponding OpenCV InterpolationFlags.
    /// </summary>
    /// <param name="method">The resampling method.</param>
    /// <returns>The corresponding InterpolationFlags.</returns>
    public static InterpolationFlags ToInterpolationFlags(ResampleMethod method)
    {
        return method switch
        {
            ResampleMethod.Nearest => InterpolationFlags.Nearest,
            ResampleMethod.Linear => InterpolationFlags.Linear,
            ResampleMethod.Cubic => InterpolationFlags.Cubic,
            _ => InterpolationFlags.Lanczos4
        };
    }
}
