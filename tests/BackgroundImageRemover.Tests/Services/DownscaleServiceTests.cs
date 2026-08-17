using BackgroundImageRemover.Services.Preview;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class DownscaleServiceTests
{
    [Fact]
    public void CreatePreview_SmallerThanMaxDim_ReturnsUnscaledCopy()
    {
        var service = new DownscaleService();
        using var full = new Mat(300, 400, MatType.CV_8UC3, Scalar.All(50));

        using var preview = service.CreatePreview(full, maxDim: 800);

        Assert.Equal(400, preview.Bgr.Width);
        Assert.Equal(300, preview.Bgr.Height);
        Assert.Equal(1.0, preview.ScaleFactor);
    }

    [Fact]
    public void CreatePreview_ReturnsAClone_NotTheOriginalMat()
    {
        var service = new DownscaleService();
        using var full = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(50));

        using var preview = service.CreatePreview(full, maxDim: 800);
        full.SetTo(Scalar.All(200));

        Assert.Equal(50, preview.Bgr.At<Vec3b>(0, 0).Item0);
    }

    [Fact]
    public void CreatePreview_LargerThanMaxDim_ScalesDownPreservingAspectRatio()
    {
        var service = new DownscaleService();
        using var full = new Mat(1000, 2000, MatType.CV_8UC3, Scalar.All(50)); // 2:1 landscape

        using var preview = service.CreatePreview(full, maxDim: 800);

        Assert.Equal(800, preview.Bgr.Width);
        Assert.Equal(400, preview.Bgr.Height);
    }

    [Fact]
    public void CreatePreview_ScaleFactor_MapsPreviewCoordinatesBackToFullImageCoordinates()
    {
        var service = new DownscaleService();
        using var full = new Mat(1000, 2000, MatType.CV_8UC3, Scalar.All(50));

        using var preview = service.CreatePreview(full, maxDim: 800);

        // preview width 800 * scaleFactor should reconstruct the full width (2000).
        Assert.Equal(2000, preview.Bgr.Width * preview.ScaleFactor, precision: 3);
    }

    [Fact]
    public void CreatePreview_AtExactlyMaxDim_ReturnsUnscaledCopy()
    {
        var service = new DownscaleService();
        using var full = new Mat(800, 600, MatType.CV_8UC3, Scalar.All(50));

        using var preview = service.CreatePreview(full, maxDim: 800);

        Assert.Equal(600, preview.Bgr.Width);
        Assert.Equal(800, preview.Bgr.Height);
        Assert.Equal(1.0, preview.ScaleFactor);
    }
}
