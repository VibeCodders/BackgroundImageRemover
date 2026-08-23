using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class DodgeBurnService
{
    public static Mat DodgeBurnRegion(Mat bgr, Mat mask, bool dodge, double strength)
    {
        strength = EditingGuard.ClampStrength(strength);
        using var adjusted = DodgeBurnHelper.ApplyToAllChannels(bgr, dodge, strength);
        // Blend the adjusted image back over the ORIGINAL by the mask, so pixels outside the
        // painted region stay untouched. Blending the adjusted image with itself (the previous
        // implementation) silently ignored the mask.
        return bgr.BlendByMask(adjusted, mask);
    }

    public static Mat DodgeBurnAll(Mat bgr, bool dodge, double strength)
    {
        strength = EditingGuard.ClampStrength(strength);
        return DodgeBurnHelper.ApplyToAllChannels(bgr, dodge, strength);
    }
}
