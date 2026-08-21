using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>
/// Transposes an image (swaps rows with columns, i.e. mirrors across the main diagonal).
/// </summary>
public static class TransposeService
{
    /// <summary>
    /// Returns a transposed copy of <paramref name="input"/> (width and height are swapped).
    /// A null or empty input is returned as an empty Mat.
    /// </summary>
    public static Mat Transpose(Mat input)
    {
        if (input is null || input.Empty())
        {
            return new Mat();
        }

        var result = new Mat();
        Cv2.Transpose(input, result);
        return result;
    }
}
