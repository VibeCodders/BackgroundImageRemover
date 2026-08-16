using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class ChromaKeyStrategyTests
{
    [Fact]
    public void DetectDominantBorderColor_ReturnsBorderColor_WhenBorderIsUniform()
    {
        using var image = new Mat(100, 100, MatType.CV_8UC3, new Scalar(0, 255, 0)); // green background
        using var subjectRoi = new Mat(image, new Rect(30, 30, 40, 40));
        subjectRoi.SetTo(new Scalar(0, 0, 255)); // red subject in the middle, away from the border

        var detected = ChromaKeyStrategy.DetectDominantBorderColor(image);

        Assert.Equal(0, detected.Item0);
        Assert.Equal(255, detected.Item1);
        Assert.Equal(0, detected.Item2);
    }

    [Fact]
    public void DetectDominantBorderColor_IgnoresSmallCenterVariation()
    {
        using var image = new Mat(200, 200, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var centerRoi = new Mat(image, new Rect(90, 90, 20, 20));
        centerRoi.SetTo(new Scalar(200, 200, 200));

        var detected = ChromaKeyStrategy.DetectDominantBorderColor(image);

        Assert.Equal(10, detected.Item0);
        Assert.Equal(20, detected.Item1);
        Assert.Equal(30, detected.Item2);
    }
}
