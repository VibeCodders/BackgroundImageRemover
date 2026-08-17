using System.IO;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// End-to-end export test: build a synthetic green-screen photo, remove the background with
/// the real GrabCut strategy (a matte whose feathered alpha approximates the true subject
/// coverage), export the cutout to a PNG, reload it, and verify the semi-transparent edge
/// pixels no longer carry the background color.
/// </summary>
public class ExportPipelineTests
{
    [Fact]
    public async Task Export_EdgePixelsAreFreeOfBackgroundColor()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cutout_e2e_{Guid.NewGuid():N}.png");
        try
        {
            using var source = BuildGreenScreenImage();

            // Raw cutout (no decontamination): the feathered edges keep the green background
            // mixed into their RGB, so green dominates red.
            var rawContext = Context(decontaminate: false);
            using var raw = await new GrabCutStrategy().RunFullAsync(source, rawContext, CancellationToken.None);
            var (_, rawGreen, rawRed) = EdgeChannelMeansOfBgra(raw.Bgra);
            Assert.True(rawGreen > rawRed, "test image must actually contain background spill in its edges");

            // Decontaminated cutout, exported through the real PNG pipeline and read back.
            var context = Context(decontaminate: true);
            using var result = await new GrabCutStrategy().RunFullAsync(source, context, CancellationToken.None);

            var exporter = new ImageExportService();
            await exporter.ExportPngAsync(result.Bgra, path);

            var loader = new ImageLoaderService();
            using var exported = await loader.LoadAsync(path);

            var (edgeCount, meanGreen, meanRed) = EdgeChannelMeans(exported.FullBgr, exported.FullAlpha);

            Assert.True(edgeCount > 100, $"expected a substantial edge band, found {edgeCount} pixels");
            Assert.True(
                meanRed > meanGreen,
                $"expected the recovered edge to be dominated by the subject (red) instead of the background (green): red = {meanRed:F1}, green = {meanGreen:F1}");
            Assert.True(
                meanGreen < rawGreen * 0.6,
                $"expected the background green to be substantially removed: raw green = {rawGreen:F1}, exported green = {meanGreen:F1}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static StrategyContext Context(bool decontaminate) => new()
    {
        // Tightly bound the subject circle so GrabCut's feathered edge lands on the
        // red/green boundary (not on the pure green margin around it).
        GrabCutRect = new Rect(60, 60, 180, 180),
        GrabCutIterations = 3,
        DecontaminateEdges = decontaminate
    };

    /// <summary>
    /// Green background with a red filled circle whose edge is blurred, so the boundary has
    /// genuinely semi-transparent pixels (a blend of subject and background colors).
    /// </summary>
    private static Mat BuildGreenScreenImage()
    {
        var image = new Mat(300, 300, MatType.CV_8UC3, new Scalar(0, 255, 0)); // green background
        Cv2.Circle(image, new Point(150, 150), 90, new Scalar(0, 0, 255), -1, LineTypes.AntiAlias); // red subject
        Cv2.GaussianBlur(image, image, new Size(15, 15), 0); // soften the edge into a real blend
        return image;
    }

    private static (int Count, double MeanGreen, double MeanRed) EdgeChannelMeans(Mat bgr, Mat? alpha)
    {
        Assert.NotNull(alpha);

        long sumGreen = 0;
        long sumRed = 0;
        int count = 0;

        for (int y = 0; y < bgr.Height; y++)
        {
            for (int x = 0; x < bgr.Width; x++)
            {
                byte a = alpha!.At<byte>(y, x);
                if (a < 20 || a > 235)
                {
                    continue; // only the semi-transparent edge band
                }
                var px = bgr.At<Vec3b>(y, x);
                sumGreen += px.Item1;
                sumRed += px.Item2;
                count++;
            }
        }

        return count == 0
            ? (0, double.NaN, double.NaN)
            : (count, sumGreen / (double)count, sumRed / (double)count);
    }

    private static (int Count, double MeanGreen, double MeanRed) EdgeChannelMeansOfBgra(Mat bgra)
    {
        var channels = Cv2.Split(bgra);
        try
        {
            using var bgr = new Mat();
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, bgr);
            return EdgeChannelMeans(bgr, channels[3]);
        }
        finally
        {
            foreach (var c in channels)
            {
                c.Dispose();
            }
        }
    }
}
