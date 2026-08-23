using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.Services.Editing;

public class PerspectiveServiceTests : ServiceTestBase
{
    private static Mat MakeUniform(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    // ------------------------------------------------------------------ Correct

    [Fact]
    public void Correct_IdentityQuad_ReturnsResizedButRecognizableImage()
    {
        using var input = CreateTestInputWithRectangle(40, 40, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 10, 10, 10, 10);
        var (tl, tr, br, bl) = PerspectiveService.DefaultQuad(input.Size());

        using var result = PerspectiveService.Correct(input, tl, tr, br, bl, 40, 40);

        Assert.Equal(new Size(40, 40), result.Size());
        // Identity quad -> output should closely match input.
        Assert.Equal(input.Get<Vec3b>(20, 20), result.Get<Vec3b>(20, 20));
    }

    [Fact]
    public void Correct_OutputSizeDiffersFromInput_ProducesRequestedSize()
    {
        using var input = MakeUniform(40, 40, new Scalar(50, 100, 150));
        var (tl, tr, br, bl) = PerspectiveService.DefaultQuad(input.Size());

        using var result = PerspectiveService.Correct(input, tl, tr, br, bl, 100, 60);

        Assert.Equal(new Size(100, 60), result.Size());
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(60, 0)]
    [InlineData(-5, 60)]
    [InlineData(60, -5)]
    public void Correct_NonPositiveOutputDimensions_ClampedToOne(int width, int height)
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));
        var (tl, tr, br, bl) = PerspectiveService.DefaultQuad(input.Size());

        using var result = PerspectiveService.Correct(input, tl, tr, br, bl, width, height);

        Assert.True(result.Width >= 1);
        Assert.True(result.Height >= 1);
    }

    [Fact]
    public void Correct_SkewedQuad_ChangesPixelsRelativeToIdentity()
    {
        using var input = CreateTestInputWithRectangle(40, 40, new Scalar(10, 20, 30), new Scalar(220, 220, 220), 5, 5, 20, 20);

        using var identity = PerspectiveService.Correct(
            input, new Point2f(0, 0), new Point2f(39, 0), new Point2f(39, 39), new Point2f(0, 39), 40, 40);

        // A skewed quad (top edge narrowed) should produce a different result than identity.
        using var skewed = PerspectiveService.Correct(
            input, new Point2f(10, 0), new Point2f(29, 0), new Point2f(39, 39), new Point2f(0, 39), 40, 40);

        AssertResultsDiffer(identity, skewed);
    }

    [Fact]
    public void Correct_CollinearSourcePoints_DoesNotThrow()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        // All four points collinear (on a horizontal line) -> degenerate/singular transform.
        using var result = PerspectiveService.Correct(
            input, new Point2f(0, 0), new Point2f(5, 0), new Point2f(10, 0), new Point2f(15, 0), 20, 20);

        Assert.Equal(new Size(20, 20), result.Size());
    }

    [Fact]
    public void Correct_CoincidentSourcePoints_DoesNotThrow()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        // All four points identical -> fully degenerate quad.
        var p = new Point2f(5, 5);
        using var result = PerspectiveService.Correct(input, p, p, p, p, 20, 20);

        Assert.Equal(new Size(20, 20), result.Size());
    }

    [Fact]
    public void Correct_SourcePointsOutsideImageBounds_DoesNotThrow()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));

        using var result = PerspectiveService.Correct(
            input,
            new Point2f(-100, -100), new Point2f(500, -100),
            new Point2f(500, 500), new Point2f(-100, 500),
            20, 20);

        Assert.Equal(new Size(20, 20), result.Size());
    }

    [Fact]
    public void Correct_OneByOneOutput_DoesNotThrow()
    {
        using var input = MakeUniform(20, 20, new Scalar(50, 100, 150));
        var (tl, tr, br, bl) = PerspectiveService.DefaultQuad(input.Size());

        using var result = PerspectiveService.Correct(input, tl, tr, br, bl, 1, 1);

        Assert.Equal(new Size(1, 1), result.Size());
    }

    [Fact]
    public void Correct_OnePixelInput_DoesNotThrow()
    {
        using var input = MakeUniform(1, 1, new Scalar(50, 100, 150));
        var (tl, tr, br, bl) = PerspectiveService.DefaultQuad(input.Size());

        using var result = PerspectiveService.Correct(input, tl, tr, br, bl, 10, 10);

        Assert.Equal(new Size(10, 10), result.Size());
    }

    // ------------------------------------------------------------------ DefaultQuad

    [Fact]
    public void DefaultQuad_ReturnsFourCorners()
    {
        var (tl, tr, br, bl) = PerspectiveService.DefaultQuad(new Size(30, 20));

        Assert.Equal(new Point2f(0, 0), tl);
        Assert.Equal(new Point2f(29, 0), tr);
        Assert.Equal(new Point2f(29, 19), br);
        Assert.Equal(new Point2f(0, 19), bl);
    }

    [Fact]
    public void DefaultQuad_OnePixelImage_ReturnsAllZeroCorners()
    {
        var (tl, tr, br, bl) = PerspectiveService.DefaultQuad(new Size(1, 1));

        Assert.Equal(new Point2f(0, 0), tl);
        Assert.Equal(new Point2f(0, 0), tr);
        Assert.Equal(new Point2f(0, 0), br);
        Assert.Equal(new Point2f(0, 0), bl);
    }
}
