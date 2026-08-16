using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Snaps a rough alpha mask to the source image's edges using a guided filter (He et al.,
/// "Guided Image Filtering"), implemented directly with box filters so no extra native
/// dependency (e.g. ximgproc) is required. Cheap fast-matting alternative to closed-form
/// matting; noticeably improves soft edges like hair without a trimap.
/// </summary>
public static class AlphaMattingRefiner
{
    public static Mat Refine(Mat bgr, Mat roughAlpha, int radius = 8, double eps = 1e-3)
    {
        using var guide = new Mat();
        Cv2.CvtColor(bgr, guide, ColorConversionCodes.BGR2GRAY);
        guide.ConvertTo(guide, MatType.CV_32F, 1.0 / 255.0);

        using var p = new Mat();
        roughAlpha.ConvertTo(p, MatType.CV_32F, 1.0 / 255.0);

        var kernel = new Size(radius * 2 + 1, radius * 2 + 1);

        using var meanI = new Mat();
        Cv2.BoxFilter(guide, meanI, MatType.CV_32F, kernel);
        using var meanP = new Mat();
        Cv2.BoxFilter(p, meanP, MatType.CV_32F, kernel);

        using var ii = guide.Mul(guide).ToMat();
        using var ip = guide.Mul(p).ToMat();
        using var corrI = new Mat();
        Cv2.BoxFilter(ii, corrI, MatType.CV_32F, kernel);
        using var corrIp = new Mat();
        Cv2.BoxFilter(ip, corrIp, MatType.CV_32F, kernel);

        using var meanIsq = meanI.Mul(meanI).ToMat();
        using var varI = (corrI - meanIsq).ToMat();
        using var meanImeanP = meanI.Mul(meanP).ToMat();
        using var covIp = (corrIp - meanImeanP).ToMat();

        using var varIEps = (varI + new Scalar(eps)).ToMat();
        using var a = (covIp / varIEps).ToMat();
        using var aMeanI = a.Mul(meanI).ToMat();
        using var b = (meanP - aMeanI).ToMat();

        using var meanA = new Mat();
        Cv2.BoxFilter(a, meanA, MatType.CV_32F, kernel);
        using var meanB = new Mat();
        Cv2.BoxFilter(b, meanB, MatType.CV_32F, kernel);

        using var meanAI = meanA.Mul(guide).ToMat();
        using var q = (meanAI + meanB).ToMat();

        var result = new Mat();
        q.ConvertTo(result, MatType.CV_8U, 255.0);
        return result;
    }
}
