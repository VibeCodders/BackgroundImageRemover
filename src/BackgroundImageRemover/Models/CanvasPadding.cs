namespace BackgroundImageRemover.Models;

/// <summary>
/// How far the Uncrop canvas extends beyond the original image on each side, in source-image
/// pixels. The single source of truth shared by the aspect-ratio presets, the drag handles, and
/// the numeric padding fields in the Uncrop window.
/// </summary>
public readonly record struct CanvasPadding(int Left, int Top, int Right, int Bottom)
{
    public static readonly CanvasPadding Zero = new(0, 0, 0, 0);

    public bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

    /// <summary>
    /// Computes centered padding required to adjust a source size to a target aspect ratio.
    /// </summary>
    public static CanvasPadding ComputeCentered(OpenCvSharp.Size sourceSize, double targetRatio)
    {
        if (sourceSize.Height <= 0 || sourceSize.Width <= 0 || targetRatio <= 0)
        {
            return Zero;
        }

        double currentRatio = (double)sourceSize.Width / sourceSize.Height;
        if (targetRatio > currentRatio)
        {
            int targetWidth = (int)Math.Round(sourceSize.Height * targetRatio);
            int extra = Math.Max(0, targetWidth - sourceSize.Width);
            int half = extra / 2;
            return new CanvasPadding(half, 0, extra - half, 0);
        }
        else
        {
            int targetHeight = (int)Math.Round(sourceSize.Width / targetRatio);
            int extra = Math.Max(0, targetHeight - sourceSize.Height);
            int half = extra / 2;
            return new CanvasPadding(0, half, 0, extra - half);
        }
    }
}

