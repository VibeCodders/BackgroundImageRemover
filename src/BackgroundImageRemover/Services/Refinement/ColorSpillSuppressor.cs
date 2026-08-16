using OpenCvSharp;

namespace BackgroundImageRemover.Services.Refinement;

/// <summary>
/// Neutralizes the background color's cast on semi-transparent edge pixels of a chroma-keyed
/// cutout (e.g. a greenish rim around hair over a green background). Only touches pixels whose
/// alpha is in the soft edge band; fully opaque/transparent pixels are left untouched.
/// </summary>
public static class ColorSpillSuppressor
{
    public static void Suppress(Mat bgra, Vec3b keyColor)
    {
        int dominant = keyColor.Item0 >= keyColor.Item1 && keyColor.Item0 >= keyColor.Item2 ? 0
            : keyColor.Item1 >= keyColor.Item2 ? 1 : 2;

        bgra.GetArray(out Vec4b[] pixels);
        for (int i = 0; i < pixels.Length; i++)
        {
            var px = pixels[i];
            if (px.Item3 == 0 || px.Item3 == 255)
            {
                continue;
            }

            double edgeWeight = 1.0 - px.Item3 / 255.0;
            byte b = px.Item0, g = px.Item1, r = px.Item2;

            double othersAvg = dominant switch
            {
                0 => (g + r) / 2.0,
                1 => (b + r) / 2.0,
                _ => (b + g) / 2.0
            };

            switch (dominant)
            {
                case 0 when b > othersAvg:
                    b = (byte)Math.Clamp(b - (b - othersAvg) * edgeWeight, 0, 255);
                    break;
                case 1 when g > othersAvg:
                    g = (byte)Math.Clamp(g - (g - othersAvg) * edgeWeight, 0, 255);
                    break;
                case 2 when r > othersAvg:
                    r = (byte)Math.Clamp(r - (r - othersAvg) * edgeWeight, 0, 255);
                    break;
            }

            pixels[i] = new Vec4b(b, g, r, px.Item3);
        }
        bgra.SetArray(pixels);
    }
}
