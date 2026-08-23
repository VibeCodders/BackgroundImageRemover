using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class DodgeBurnService
{
    public static Mat DodgeBurnRegion(Mat bgr, Mat mask, bool dodge, double strength)
    {
        strength = EditingGuard.ClampStrength(strength);
        using var adjusted = DodgeBurnHelper.ApplyToAllChannels(bgr, dodge, strength);
        return adjusted.BlendByMask(adjusted, mask);
    }

    public static Mat DodgeBurnAll(Mat bgr, bool dodge, double strength)
    {
        strength = EditingGuard.ClampStrength(strength);
        return DodgeBurnHelper.ApplyToAllChannels(bgr, dodge, strength);
    }
}
