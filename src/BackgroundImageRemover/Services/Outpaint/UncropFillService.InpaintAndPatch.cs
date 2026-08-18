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
        using var expanded = ExpandCanvas(sourceBgr, padding, out var mask);
        using (mask)
        {
            ct.ThrowIfCancellationRequested();
            if (preFillEdgeAverage)
            {
                var edgeColor = SampleEdgeAverageColor(sourceBgr);
                FillBorderRegions(expanded, padding, sourceBgr.Size(), edgeColor);
            }

            var result = new Mat();
            var cvMethod = method == UncropInpaintMethod.Telea ? InpaintMethod.Telea : InpaintMethod.NS;
            double radius = Math.Max(1.0, Math.Min(100.0, inpaintRadius));
            Cv2.Inpaint(expanded, mask, result, inpaintRadius: radius, cvMethod);

            ct.ThrowIfCancellationRequested();

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
