using BackgroundImageRemover.Helpers;
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
        var expanded = ImageProcessingUtility.ExpandBorder(sourceBgr, padding, BorderTypes.Constant, Scalar.All(0));
        newAreaMask = ImageProcessingUtility.CreateNewAreaMask(expanded.Size(), padding, sourceBgr.Size());
        return expanded;
    }
}
