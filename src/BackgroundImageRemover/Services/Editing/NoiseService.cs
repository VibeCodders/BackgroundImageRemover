using BackgroundImageRemover.Helpers;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

public static class NoiseService
{
    public static Mat AddGaussianNoise(Mat input, double strength)
    {
        if (input is null || input.Empty() || strength <= 0)
        {
            return EditingGuard.ReturnCloneIfNullOrEmpty(input);
        }

        using var noise = new Mat(input.Size(), input.Type());
        Cv2.Randn(noise, 0, strength);

        using var scaled = new Mat();
        noise.ConvertTo(scaled, input.Type(), strength);

        var result = new Mat();
        Cv2.Add(input, scaled, result);
        Cv2.Threshold(result, result, 255, 255, ThresholdTypes.Tozero);
        Cv2.Threshold(result, result, 0, 0, ThresholdTypes.TozeroInv);
        return result;
    }

    public static Mat AddSaltPepperNoise(Mat input, double strength)
    {
        if (input is null || input.Empty() || strength <= 0)
        {
            return EditingGuard.ReturnCloneIfNullOrEmpty(input);
        }

        var result = input.Clone();
        var rng = new Random();
        int channels = input.Channels();

        double saltProb = strength / 2.0;
        double pepperProb = strength / 2.0;

        if (channels == 1)
        {
            byte[] data = PixelLoop.GetData<byte>(result);
            for (int i = 0; i < data.Length; i++)
            {
                double r = rng.NextDouble();
                if (r < saltProb)
                {
                    data[i] = 255;
                }
                else if (r < saltProb + pepperProb)
                {
                    data[i] = 0;
                }
            }
            PixelLoop.SetData(result, data);
        }
        else
        {
            Vec3b[] data = PixelLoop.GetData<Vec3b>(result);
            for (int i = 0; i < data.Length; i++)
            {
                double r = rng.NextDouble();
                if (r < saltProb)
                {
                    data[i] = new Vec3b(255, 255, 255);
                }
                else if (r < saltProb + pepperProb)
                {
                    data[i] = new Vec3b(0, 0, 0);
                }
            }
            PixelLoop.SetData(result, data);
        }

        return result;
    }
}
