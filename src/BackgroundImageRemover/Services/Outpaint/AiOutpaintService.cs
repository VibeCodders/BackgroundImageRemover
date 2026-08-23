using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Onnx;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Outpaint;

/// <summary>
/// Orchestrates Uncrop's AI fill mode: expands the canvas, builds the inpaint mask over the new
/// border area, runs LaMa (fixed 512×512 input) and returns the filled canvas. The original
/// pixels are preserved pixel-exact: for canvases that fit in 512×512 the exported graph already
/// composites them back, and for larger canvases the downscaled run is re-upscaled and the crisp
/// original is blended back over its region with a feathered seam.
/// </summary>
public sealed class AiOutpaintService : IAiOutpaintService
{
    private const int ModelSize = LamaInpaintEngine.ModelSize;

    private readonly ILamaInpaintEngine _engine;
    private readonly IUncropFillService _fillService;

    public AiOutpaintService(ILamaInpaintEngine engine, IUncropFillService fillService)
    {
        _engine = engine;
        _fillService = fillService;
    }

    public async Task<Mat> OutpaintAsync(Mat sourceBgr, CanvasPadding padding, LamaModelVariant model, bool useGpu, IProgress<ModelDownloadProgress>? downloadProgress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceBgr);
        ct.ThrowIfCancellationRequested();

        await _engine.EnsureReadyAsync(model, useGpu, downloadProgress, ct);
        ct.ThrowIfCancellationRequested();

        using var canvas = _fillService.ExpandCanvas(sourceBgr, padding, out var newAreaMask);
        try
        {
            // Scale the working pair to fit the model's fixed square input, then pad to the
            // square with reflected content (context only; the mask stays 0 outside the canvas).
            double scale = Math.Min(1.0, (double)ModelSize / Math.Max(canvas.Width, canvas.Height));
            var workSize = scale < 1.0
                ? new Size(Math.Max(1, (int)Math.Round(canvas.Width * scale)), Math.Max(1, (int)Math.Round(canvas.Height * scale)))
                : canvas.Size();

            using var workImage = scale < 1.0
                ? ResizeTo(canvas, workSize, InterpolationFlags.Area)
                : canvas.Clone();
            using var workMask = scale < 1.0
                ? ResizeTo(newAreaMask, workSize, InterpolationFlags.Nearest)
                : newAreaMask.Clone();

            // A touch of dilation pushes the synthesized seam a couple of pixels into the
            // original content so the boundary never shows a hard line.
            using (var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3)))
            {
                Cv2.Dilate(workMask, workMask, kernel, iterations: 2);
            }

            using var squareImage = new Mat(ModelSize, ModelSize, MatType.CV_8UC3);
            using var squareMask = new Mat(ModelSize, ModelSize, MatType.CV_8UC1, Scalar.All(0));
            Cv2.CopyMakeBorder(workImage, squareImage, 0, ModelSize - workSize.Height, 0, ModelSize - workSize.Width,
                BorderTypes.Reflect101);
            using (var maskRoi = new Mat(squareMask, new Rect(0, 0, workSize.Width, workSize.Height)))
            {
                workMask.CopyTo(maskRoi);
            }

            using var inpainted = _engine.Inpaint(squareImage, squareMask);

            if (scale < 1.0)
            {
                // The model output's original region holds the downscaled (then re-upscaled)
                // content: blend the crisp full-resolution original back over it, feathered by
                // the (already dilated) new-area mask so the seam is invisible.
                using var upscaled = new Mat();
                Cv2.Resize(inpainted, upscaled, canvas.Size(), interpolation: InterpolationFlags.Linear);
                using var softMask = new Mat();
                Cv2.GaussianBlur(newAreaMask, softMask, new Size(5, 5), 0);
                return canvas.BlendByMask(upscaled, softMask);
            }

            // Fits in the model input: the exported graph already returned the original pixels
            // untouched in the unmasked region, so cropping the square output is final.
            using var cropped = new Mat(inpainted, new Rect(0, 0, workSize.Width, workSize.Height));
            return cropped.Clone();
        }
        finally
        {
            newAreaMask.Dispose();
        }
    }

    private static Mat ResizeTo(Mat src, Size size, InterpolationFlags flags)
    {
        var dst = new Mat();
        Cv2.Resize(src, dst, size, interpolation: flags);
        return dst;
    }
}
