using BackgroundImageRemover.Models;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Outpaint;

/// <summary>
/// Service implementing various algorithmic canvas expansion (uncrop/outpaint) techniques.
/// </summary>
public sealed partial class UncropFillService : IUncropFillService
{
    public Mat ExpandCanvas(Mat sourceBgr, CanvasPadding padding, out Mat newAreaMask)
    {
        var expanded = new Mat();
        Cv2.CopyMakeBorder(sourceBgr, expanded, padding.Top, padding.Bottom, padding.Left, padding.Right,
            BorderTypes.Constant, Scalar.All(0));

        var mask = new Mat(expanded.Size(), MatType.CV_8UC1, Scalar.All(255));
        using (var innerRoi = new Mat(mask, new Rect(padding.Left, padding.Top, sourceBgr.Width, sourceBgr.Height)))
        {
            innerRoi.SetTo(Scalar.All(0));
        }

        newAreaMask = mask;
        return expanded;
    }
}
