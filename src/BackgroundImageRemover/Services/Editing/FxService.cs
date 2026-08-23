using System.Threading.Tasks;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Services.Compositing;
using OpenCvSharp;

namespace BackgroundImageRemover.Services.Editing;

/// <summary>Cinematic/optical effects for the FX tool: bloom, glow, light leaks, chromatic aberration, bokeh.</summary>
public static class FxService
{
    /// <summary>Soft glow: a blurred copy screen-blended over the original, scaled by strength (0..1).</summary>
    public static Mat Glow(Mat bgr, double strength, double sigma = 12)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (bgr is null || strength <= 1e-4)
        {
            return EditingGuard.ReturnCloneIfNull(bgr);
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(bgr, blurred, new Size(0, 0), sigma, sigma);
        return ScreenBlend(bgr, blurred, strength);
    }

    /// <summary>Highlight bloom: blurs only the bright areas and screen-blends them back.</summary>
    public static Mat Bloom(Mat bgr, double strength, double threshold = 200)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (bgr is null || strength <= 1e-4)
        {
            return EditingGuard.ReturnCloneIfNull(bgr);
        }

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        using var mask = new Mat();
        Cv2.Threshold(gray, mask, threshold, 255, ThresholdTypes.Binary);

        using var bright = new Mat(bgr.Size(), bgr.Type(), Scalar.All(0));
        bgr.CopyTo(bright, mask);
        using var blurred = new Mat();
        Cv2.GaussianBlur(bright, blurred, new Size(0, 0), 10, 10);
        return ScreenBlend(bgr, blurred, strength);
    }

    /// <summary>Warm light leak from the top-left corner, scaled by strength (0..1).</summary>
    public static Mat LightLeak(Mat bgr, double strength, Vec3b? color = null)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        if (bgr is null || strength <= 1e-4)
        {
            return EditingGuard.ReturnCloneIfNull(bgr);
        }

        var c = color ?? new Vec3b(80, 120, 255); // warm orange-red in BGR
        var result = bgr.Clone();
        float maxDist = MathF.Sqrt(bgr.Width * bgr.Width + bgr.Height * bgr.Height);
        int cols = result.Cols;
        int rows = result.Rows;
        unsafe
        {
            byte* resPtr = (byte*)result.DataPointer;
            long resStep = result.Step();
            Parallel.For(0, rows, y =>
            {
                var row = new Span<Vec3b>((Vec3b*)(resPtr + y * resStep), cols);
                for (int x = 0; x < cols; x++)
                {
                    float d = MathF.Sqrt(x * x + y * y) / maxDist;
                    float falloff = MathF.Pow(1.0f - d, 2.0f);
                    float amount = (float)strength * falloff;
                    ref var px = ref row[x];
                    px.Item0 = (byte)Math.Min(255, px.Item0 + c.Item0 * amount);
                    px.Item1 = (byte)Math.Min(255, px.Item1 + c.Item1 * amount);
                    px.Item2 = (byte)Math.Min(255, px.Item2 + c.Item2 * amount);
                }
            });
        }
        return result;
    }

    /// <summary>Radial chromatic aberration: shifts the red channel outward and the blue channel inward.</summary>
    public static Mat ChromaticAberration(Mat bgr, double strength)
    {
        strength = Math.Clamp(strength, 0.0, 2.0);
        if (bgr is null || Math.Abs(strength) < 1e-4)
        {
            return EditingGuard.ReturnCloneIfNull(bgr);
        }

        Mat r, g, b;
        using (var split = ChannelSplit.Of(bgr)) // B, G, R
        {
            float cx = bgr.Width / 2.0f;
            float cy = bgr.Height / 2.0f;
            float maxR = MathF.Sqrt(cx * cx + cy * cy);

            r = RemapRadially(split[2], cx, cy, maxR, (float)strength);
            b = RemapRadially(split[0], cx, cy, maxR, -(float)strength);
            g = split[1].Clone();
        }

        using (r)
        using (g)
        using (b)
        {
            var result = new Mat();
            Cv2.Merge(new[] { b, g, r }, result);
            return result;
        }
    }

    /// <summary>Out-of-focus bokeh discs scattered over the image, screen-blended.</summary>
    public static Mat Bokeh(Mat bgr, int count, double size)
    {
        count = Math.Clamp(count, 0, 200);
        size = Math.Max(1, size);
        if (bgr is null || count == 0)
        {
            return EditingGuard.ReturnCloneIfNull(bgr);
        }

        using var overlay = new Mat(bgr.Size(), bgr.Type(), Scalar.All(0));
        var rand = new Random(1234);
        for (int i = 0; i < count; i++)
        {
            int cx = rand.Next(bgr.Width);
            int cy = rand.Next(bgr.Height);
            int radius = rand.Next((int)Math.Max(1, size * 0.3), (int)Math.Max(2, size));
            byte bright = (byte)rand.Next(120, 240);
            Cv2.Circle(overlay, new Point(cx, cy), radius, new Scalar(bright, bright, bright), -1, LineTypes.AntiAlias);
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(overlay, blurred, new Size(0, 0), 3, 3);
        return ScreenBlend(bgr, blurred, 1.0);
    }

    private static Mat RemapRadially(Mat channel, float cx, float cy, float maxR, float strength)
    {
        using var mapX = new Mat(channel.Size(), MatType.CV_32FC1);
        using var mapY = new Mat(channel.Size(), MatType.CV_32FC1);
        int w = channel.Width;
        int h = channel.Height;
        unsafe
        {
            byte* xPtr = (byte*)mapX.DataPointer;
            byte* yPtr = (byte*)mapY.DataPointer;
            long xStep = mapX.Step();
            long yStep = mapY.Step();
            Parallel.For(0, h, y =>
            {
                var mapXRow = new Span<float>((float*)(xPtr + y * xStep), w);
                var mapYRow = new Span<float>((float*)(yPtr + y * yStep), w);
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float norm = maxR > 0 ? MathF.Sqrt(dx * dx + dy * dy) / maxR : 0;
                    float scale = 1.0f + strength * 0.06f * norm;
                    mapXRow[x] = cx + dx * scale;
                    mapYRow[x] = cy + dy * scale;
                }
            });
        }

        var result = new Mat();
        Cv2.Remap(channel, result, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        return result;
    }

    /// <summary>screen(a,b) = 255 - (255-a)*(255-b)/255, blended toward b by <paramref name="t"/>.
    /// Single parallel pass over the native buffers (the previous version materialized ~6
    /// intermediate CV_32FC3 Mats per call).</summary>
    private static Mat ScreenBlend(Mat baseBgr, Mat blendBgr, double t)
    {
        int rows = baseBgr.Rows;
        int cols = baseBgr.Cols;
        float f = (float)t;
        var result = new Mat(baseBgr.Size(), MatType.CV_8UC3);
        unsafe
        {
            byte* aPtr = (byte*)baseBgr.DataPointer;
            byte* bPtr = (byte*)blendBgr.DataPointer;
            byte* dstPtr = (byte*)result.DataPointer;
            long aStep = baseBgr.Step();
            long bStep = blendBgr.Step();
            long dstStep = result.Step();
            Parallel.For(0, rows, y =>
            {
                var aRow = new Span<Vec3b>((Vec3b*)(aPtr + y * aStep), cols);
                var bRow = new Span<Vec3b>((Vec3b*)(bPtr + y * bStep), cols);
                var dstRow = new Span<Vec3b>((Vec3b*)(dstPtr + y * dstStep), cols);
                for (int x = 0; x < cols; x++)
                {
                    var a = aRow[x];
                    var b = bRow[x];
                    dstRow[x] = new Vec3b(
                        ScreenByte(a.Item0, b.Item0, f),
                        ScreenByte(a.Item1, b.Item1, f),
                        ScreenByte(a.Item2, b.Item2, f));
                }
            });
        }
        return result;
    }

    private static byte ScreenByte(byte a, byte b, float t)
    {
        float screen = 255f - (255f - a) * (255f - b) / 255f;
        float v = a * (1f - t) + screen * t;
        return (byte)Math.Clamp(Math.Round(v, MidpointRounding.AwayFromZero), 0, 255);
    }
}
