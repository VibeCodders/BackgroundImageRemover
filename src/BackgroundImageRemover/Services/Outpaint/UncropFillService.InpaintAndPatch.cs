using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Outpaint;

public sealed partial class UncropFillService
{
    public Mat FillInpaint(
        Mat sourceBgr,
        CanvasPadding padding,
        UncropInpaintMethod method,
        double inpaintRadius = 5,
        int blendMargin = 0,
        bool preFillEdgeAverage = false,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        int totalW = sourceBgr.Width + padding.Left + padding.Right;
        int totalH = sourceBgr.Height + padding.Top + padding.Bottom;

        // Step 1: build the expanded canvas from a plausible prior. OpenCV inpainting alone
        // smears when the unknown region is large, so the new area starts as a mirrored
        // continuation of the image (or the sampled edge color when requested). Inpainting is
        // then only used to reconcile the seam, keeping the outer texture crisp.
        using var expanded = new Mat();
        if (preFillEdgeAverage)
        {
            Cv2.CopyMakeBorder(sourceBgr, expanded, padding.Top, padding.Bottom, padding.Left, padding.Right,
                BorderTypes.Constant, Scalar.All(0));
            FillBorderRegions(expanded, padding, sourceBgr.Size(), SampleEdgeAverageColor(sourceBgr));
        }
        else
        {
            Cv2.CopyMakeBorder(sourceBgr, expanded, padding.Top, padding.Bottom, padding.Left, padding.Right,
                BorderTypes.Reflect101);
        }

        using var newAreaMask = new Mat(totalH, totalW, MatType.CV_8UC1, Scalar.All(255));
        using (var innerRoi = new Mat(newAreaMask, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height)))
        {
            innerRoi.SetTo(Scalar.All(0));
        }

        ct.ThrowIfCancellationRequested();

        var cvMethod = method == UncropInpaintMethod.Telea ? InpaintTypes.Telea : InpaintTypes.NS;
        double radius = Math.Max(1.0, Math.Min(100.0, inpaintRadius));

        // Step 2: restrict inpainting to a band hugging the interior edge. The mirrored region
        // beyond that band already looks like content, so it is preserved instead of smeared.
        using var interior = new Mat(totalH, totalW, MatType.CV_8UC1, Scalar.All(0));
        using (var innerRoi = new Mat(interior, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height)))
        {
            innerRoi.SetTo(Scalar.All(255));
        }

        using var nonInterior = new Mat();
        Cv2.BitwiseNot(interior, nonInterior);

        int bandWidth = Math.Max(1, (int)Math.Ceiling(radius) * 2);
        using var distToInterior = new Mat();
        Cv2.DistanceTransform(nonInterior, distToInterior, DistanceTypes.L2, DistanceTransformMasks.Mask3);
        using var seamBand = new Mat();
        Cv2.InRange(distToInterior, new Scalar(0.5), new Scalar(bandWidth), seamBand);
        Cv2.BitwiseAnd(seamBand, newAreaMask, seamBand);

        var result = new Mat();
        Cv2.Inpaint(expanded, seamBand, result, inpaintRadius: radius, cvMethod);

        ct.ThrowIfCancellationRequested();

        // Step 3: restore the untouched original interior, feathering the seam when requested.
        if (blendMargin <= 0)
        {
            using var interiorRoi = new Mat(result, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
            sourceBgr.CopyTo(interiorRoi);
        }
        else
        {
            BlendInteriorWithFeather(result, sourceBgr, padding, blendMargin, ct);
        }

        ct.ThrowIfCancellationRequested();
        return result;
    }

    public Mat FillPatchSynthesis(
        Mat sourceBgr,
        CanvasPadding padding,
        int patchSize = 32,
        int blendOverlap = 8,
        int blendMargin = 0,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        patchSize = Math.Max(8, Math.Min(patchSize, Math.Min(sourceBgr.Width, sourceBgr.Height)));
        blendOverlap = Math.Max(2, Math.Min(blendOverlap, patchSize / 2));

        // Start with edge replicate as baseline
        using var baseReplicate = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, baseReplicate, padding.Top, padding.Bottom, padding.Left, padding.Right, BorderTypes.Replicate);

        int totalW = sourceBgr.Width + padding.Left + padding.Right;
        int totalH = sourceBgr.Height + padding.Top + padding.Bottom;

        var result = baseReplicate.Clone();

        int step = patchSize - blendOverlap;
        var rand = new Random(42);

        // Fill non-interior blocks by sampling random patches from near the inner border
        for (int y = 0; y < totalH - patchSize; y += step)
        {
            if (y % 16 == 0) ct.ThrowIfCancellationRequested();
            for (int x = 0; x < totalW - patchSize; x += step)
            {
                // If completely inside interior, skip
                if (x >= padding.Left && x + patchSize <= padding.Left + sourceBgr.Width &&
                    y >= padding.Top && y + patchSize <= padding.Top + sourceBgr.Height)
                {
                    continue;
                }

                // Sample patch from source interior near the closest edge
                int sampleX = rand.Next(0, Math.Max(1, sourceBgr.Width - patchSize));
                int sampleY = rand.Next(0, Math.Max(1, sourceBgr.Height - patchSize));

                // Prefer sampling from outer border region of source
                if (x < padding.Left) sampleX = rand.Next(0, Math.Min(sourceBgr.Width - patchSize, Math.Max(1, sourceBgr.Width / 3)));
                else if (x >= padding.Left + sourceBgr.Width) sampleX = rand.Next(Math.Max(0, sourceBgr.Width * 2 / 3 - patchSize), Math.Max(1, sourceBgr.Width - patchSize));

                if (y < padding.Top) sampleY = rand.Next(0, Math.Min(sourceBgr.Height - patchSize, Math.Max(1, sourceBgr.Height / 3)));
                else if (y >= padding.Top + sourceBgr.Height) sampleY = rand.Next(Math.Max(0, sourceBgr.Height * 2 / 3 - patchSize), Math.Max(1, sourceBgr.Height - patchSize));

                using var patch = new Mat(sourceBgr, new Rect(sampleX, sampleY, patchSize, patchSize));
                using var destRoi = new Mat(result, new Rect(x, y, patchSize, patchSize));

                // Feathered blend into destination ROI
                Cv2.AddWeighted(patch, 0.7, destRoi, 0.3, 0, destRoi);
            }
        }

        ct.ThrowIfCancellationRequested();

        // Restore interior
        if (blendMargin <= 0)
        {
            using var interiorRoi = new Mat(result, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height));
            sourceBgr.CopyTo(interiorRoi);
        }
        else
        {
            BlendInteriorWithFeather(result, sourceBgr, padding, blendMargin, ct);
        }

        return result;
    }
}
