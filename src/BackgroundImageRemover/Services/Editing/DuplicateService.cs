using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Produces an independent deep copy of an image Mat so the caller can safely mutate the
/// result without sharing pixel memory with the source.
/// </summary>
public static class DuplicateService
{
    /// <summary>
    /// Returns a full deep copy of <paramref name="input"/> (same size, type and pixel data,
    /// but a separate allocation). A null or empty input is returned as an empty Mat.
    /// </summary>
    public static Mat Duplicate(Mat input)
    {
        if (input is null || input.Empty())
        {
            return input.CloneOrEmpty();
        }

        return input.Clone();
    }
}
