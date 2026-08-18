using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Outpaint;

/// <summary>
/// Classic (non-AI) algorithms that fill the area Uncrop adds beyond an image's original
/// borders. Not an <c>IBackgroundRemovalStrategy</c> — Uncrop is a standalone tool, not part of
/// the background-removal pipeline.
/// </summary>
public interface IUncropFillService
{
    /// <summary>
    /// Expands <paramref name="sourceBgr"/> to the padded canvas size with a black border, and
    /// produces a mask (255 = newly added area, 0 = original pixels). Shared prep step for the
    /// fill modes that need to know which pixels are new.
    /// </summary>
    Mat ExpandCanvas(Mat sourceBgr, CanvasPadding padding, out Mat newAreaMask);

    /// <summary>Extends the canvas by reflecting the image content outward from each edge.</summary>
    Mat FillMirror(Mat sourceBgr, CanvasPadding padding, UncropMirrorType mirrorType = UncropMirrorType.Reflect101, CancellationToken ct = default);

    /// <summary>Extends the canvas using OpenCV content-aware inpainting on the new border area.</summary>
    Mat FillInpaint(Mat sourceBgr, CanvasPadding padding, UncropInpaintMethod method, double inpaintRadius = 5, int blendMargin = 0, CancellationToken ct = default);

    /// <summary>Extends the canvas with a flat color (either sampled from image edges or custom), optionally
    /// replicated-and-blurred into a soft gradient instead of a hard flat fill.</summary>
    Mat FillSolidColor(Mat sourceBgr, CanvasPadding padding, bool blurred, Scalar? customColor = null, int blurRadius = 0, CancellationToken ct = default);

    /// <summary>Extends the canvas by clamping/replicating the edge pixels outward.</summary>
    Mat FillReplicate(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default);

    /// <summary>Extends the canvas by tiling/wrapping the image cyclically across borders.</summary>
    Mat FillWrap(Mat sourceBgr, CanvasPadding padding, CancellationToken ct = default);
}
