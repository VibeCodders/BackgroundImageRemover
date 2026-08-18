using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Editing;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class EditingOperationsTests
{
    [Fact]
    public void Grayscale_EqualizesChannels_AtFullIntensity()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(0, 0, 255)); // pure red

        using var result = FilterService.Apply(input, FilterKind.Grayscale, intensity: 1.0);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(px.Item0, px.Item1);
        Assert.Equal(px.Item1, px.Item2);
    }

    [Fact]
    public void Invert_FlipsChannelValues()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using var result = FilterService.Apply(input, FilterKind.Invert, intensity: 1.0);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(245, px.Item0);
        Assert.Equal(235, px.Item1);
        Assert.Equal(225, px.Item2);
    }

    [Fact]
    public void Posterize_QuantizesChannelValues()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(130, 130, 130));

        // 4 levels -> bucket of 64 -> 130 maps to 128.
        using var result = FilterService.Apply(input, FilterKind.Posterize, intensity: 1.0, posterizeLevels: 4);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(128, px.Item0);
        Assert.Equal(128, px.Item1);
        Assert.Equal(128, px.Item2);
    }

    [Fact]
    public void IntensityZero_ReturnsTheOriginal()
    {
        using var input = new Mat(1, 1, MatType.CV_8UC3, new Scalar(0, 0, 255));

        using var result = FilterService.Apply(input, FilterKind.Grayscale, intensity: 0.0);

        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(0, px.Item0);
        Assert.Equal(0, px.Item1);
        Assert.Equal(255, px.Item2);
    }

    [Fact]
    public void FlipHorizontal_SwapsLeftAndRight()
    {
        using var input = new Mat(1, 2, MatType.CV_8UC3);
        input.Set(0, 0, new Vec3b(0, 0, 255));    // left red
        input.Set(0, 1, new Vec3b(255, 0, 0));    // right blue

        using var result = TransformService.FlipHorizontal(input);

        Assert.Equal(255, result.At<Vec3b>(0, 0).Item0); // now blue on the left
        Assert.Equal(255, result.At<Vec3b>(0, 1).Item2); // and red on the right
    }

    [Fact]
    public void Rotate90Clockwise_SwapsDimensions()
    {
        using var input = new Mat(2, 3, MatType.CV_8UC3);

        using var result = TransformService.Rotate90Clockwise(input);

        Assert.Equal(2, result.Width);
        Assert.Equal(3, result.Height);
    }

    [Fact]
    public void Resize_ScalesByFactor()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC3);

        using var result = TransformService.Resize(input, 0.5);

        Assert.Equal(5, result.Width);
        Assert.Equal(5, result.Height);
    }

    [Fact]
    public void AddBorder_ExpandsCanvasAndFillsBorder()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.AddBorder(input, thickness: 2, new Vec3b(0, 0, 255));

        Assert.Equal(14, result.Width);
        Assert.Equal(14, result.Height);

        var corner = result.At<Vec4b>(0, 0);
        Assert.Equal(0, corner.Item0);
        Assert.Equal(0, corner.Item1);
        Assert.Equal(255, corner.Item2); // red border
        Assert.Equal(255, corner.Item3);

        var center = result.At<Vec4b>(7, 7);
        Assert.Equal(255, center.Item0); // original white content
        Assert.Equal(255, center.Item1);
        Assert.Equal(255, center.Item2);
    }

    [Fact]
    public void RoundCorners_TransparentizesCorners()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.RoundCorners(input, radius: 4);

        Assert.Equal(0, result.At<Vec4b>(0, 0).Item3); // corner alpha cleared
        Assert.Equal(255, result.At<Vec4b>(5, 5).Item3); // center stays opaque
    }

    [Fact]
    public void AddPadding_ExpandsCanvasTransparent()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.AddPadding(input, top: 3, right: 3, bottom: 3, left: 3);

        Assert.Equal(16, result.Width);
        Assert.Equal(16, result.Height);
        Assert.Equal(0, result.At<Vec4b>(0, 0).Item3); // new corner transparent
        Assert.Equal(255, result.At<Vec4b>(3, 3).Item3); // original content opaque
    }

    [Fact]
    public void AddInnerBorder_DrawsAccentLine()
    {
        using var input = new Mat(20, 20, MatType.CV_8UC4, new Scalar(100, 100, 100, 255));

        using var result = FrameService.AddInnerBorder(input, thickness: 2, new Vec3b(0, 0, 255), opacity: 1.0);

        Assert.Equal(input.Size(), result.Size());
        Assert.True(result.At<Vec4b>(0, 0).Item2 > 200); // red accent on the edge
        Assert.Equal(100, result.At<Vec4b>(10, 10).Item2); // center untouched
    }

    [Fact]
    public void AddOuterShadow_PadsCanvas()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(0, 0, 0, 255));

        using var result = FrameService.AddOuterShadow(input, offset: 5, blur: 0, opacity: 1.0);

        Assert.Equal(22, result.Width);
        Assert.Equal(22, result.Height);
    }

    [Fact]
    public void AddPaddingWithColor_FillsPaddingWithMatColor()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.AddPaddingWithColor(input, top: 2, right: 2, bottom: 2, left: 2, new Vec3b(0, 0, 255));

        var corner = result.At<Vec4b>(0, 0);
        Assert.Equal(0, corner.Item0);
        Assert.Equal(0, corner.Item1);
        Assert.Equal(255, corner.Item2); // red mat
        Assert.Equal(255, corner.Item3);
    }

    [Fact]
    public void AddBorder_Opacity_MakesBorderSemiTransparent()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.AddBorder(input, thickness: 2, new Vec3b(0, 0, 255), opacity: 0.5);

        Assert.Equal(14, result.Width);
        Assert.True(result.At<Vec4b>(0, 0).Item3 is > 120 and < 135); // ~127
    }

    [Fact]
    public void AddPartialBorder_OnlyAddsRequestedSides()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(255, 255, 255, 255));

        using var result = FrameService.AddPartialBorder(
            input, thickness: 2, new Vec3b(0, 0, 255), opacity: 1.0, top: true, right: false, bottom: true, left: false);

        Assert.Equal(10, result.Width);   // no left/right bars
        Assert.Equal(14, result.Height);  // top + bottom bars
        var top = result.At<Vec4b>(0, 5);
        Assert.Equal(255, top.Item2);
        Assert.Equal(255, top.Item3);
    }

    [Fact]
    public void AddGradientBorder_InterpolatesBetweenCornerColors()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(100, 100, 100, 255));

        using var result = FrameService.AddGradientBorder(
            input, thickness: 2, new Vec3b(0, 0, 255), new Vec3b(255, 0, 0));

        Assert.Equal(14, result.Width);
        Assert.Equal(14, result.Height);
        var topLeft = result.At<Vec4b>(0, 0);
        Assert.Equal(255, topLeft.Item2); // red start
        var bottomRight = result.At<Vec4b>(13, 13);
        Assert.Equal(255, bottomRight.Item0); // blue end
        var center = result.At<Vec4b>(7, 7);
        Assert.Equal(100, center.Item0); // image preserved
    }

    [Fact]
    public void AddBevel_LightsTopEdgeAndShadowsBottomEdge()
    {
        using var input = new Mat(20, 20, MatType.CV_8UC4, new Scalar(100, 100, 100, 255));

        using var result = FrameService.AddBevel(
            input, thickness: 3, new Vec3b(255, 255, 255), new Vec3b(0, 0, 0), opacity: 1.0);

        Assert.Equal(input.Size(), result.Size());
        var top = result.At<Vec4b>(0, 10);
        Assert.True(top.Item0 > 150); // brightened
        var bottom = result.At<Vec4b>(19, 10);
        Assert.True(bottom.Item0 < 60); // darkened
    }

    [Fact]
    public void AddPolaroidBar_ExpandsBottomWithBarColor()
    {
        using var input = new Mat(10, 10, MatType.CV_8UC4, new Scalar(100, 100, 100, 255));

        using var result = FrameService.AddPolaroidBar(input, height: 5, new Vec3b(255, 255, 255));

        Assert.Equal(10, result.Width);
        Assert.Equal(15, result.Height);
        var bar = result.At<Vec4b>(12, 5);
        Assert.Equal(255, bar.Item0);
        Assert.Equal(255, bar.Item1);
        Assert.Equal(255, bar.Item2);
        var image = result.At<Vec4b>(2, 5);
        Assert.Equal(100, image.Item0);
    }

    [Fact]
    public void AddVignette_DarkensCornersTowardColor()
    {
        using var input = new Mat(20, 20, MatType.CV_8UC4, new Scalar(200, 200, 200, 255));

        using var result = FrameService.AddVignette(input, strength: 1.0, new Vec3b(0, 0, 0));

        var corner = result.At<Vec4b>(0, 0);
        var center = result.At<Vec4b>(10, 10);
        Assert.True(corner.Item0 < center.Item0, $"corner {corner.Item0} should be darker than center {center.Item0}");
    }

    [Fact]
    public void TextOverlay_BlankText_LeavesImageUnchanged()
    {
        using var input = new Mat(40, 40, MatType.CV_8UC3, new Scalar(10, 20, 30));

        using var result = TextOverlayService.Render(input, "", TextAnchor.Center, 40, new Vec3b(255, 255, 255), 1.0, 10);

        Assert.Equal(40, result.Width);
        Assert.Equal(40, result.Height);
        var px = result.At<Vec3b>(0, 0);
        Assert.Equal(10, px.Item0);
        Assert.Equal(20, px.Item1);
        Assert.Equal(30, px.Item2);
    }

    [Fact]
    public void TextOverlay_WithText_ModifiesOnlyTheTargetRegion()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));

        using var result = TextOverlayService.Render(input, "TEST", TextAnchor.BottomRight, 48, new Vec3b(255, 255, 255), 1.0, 10);

        Assert.Equal(input.Size(), result.Size());

        // Some pixels in the bottom-right were painted white.
        using var diff = new Mat();
        Cv2.Absdiff(input, result, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(gray) > 0);

        // The top-left corner (far from the watermark) is untouched.
        var corner = result.At<Vec3b>(0, 0);
        Assert.Equal(0, corner.Item0);
        Assert.Equal(0, corner.Item1);
        Assert.Equal(0, corner.Item2);
    }

    [Fact]
    public void TextOverlay_BackgroundPlate_AddsPlateColor()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(10, 10, 10));

        using var result = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "TEST",
            Anchor = TextAnchor.Center,
            FontSize = 48,
            Color = new Vec3b(255, 255, 255),
            Opacity = 1.0,
            BackgroundPlate = true,
            PlateColor = new Vec3b(0, 0, 255),
            PlateOpacity = 1.0,
            PlatePadding = 12
        });

        Assert.True(CountPixelsWhere(result, p => p.Item2 > 200 && p.Item0 < 100 && p.Item1 < 100) > 0);
    }

    [Fact]
    public void TextOverlay_Outline_AddsOutlineColor()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(10, 10, 10));

        using var result = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "TEST",
            Anchor = TextAnchor.Center,
            FontSize = 48,
            Color = new Vec3b(255, 255, 255),
            Opacity = 1.0,
            OutlineThickness = 3,
            OutlineColor = new Vec3b(0, 255, 0)
        });

        Assert.True(CountPixelsWhere(result, p => p.Item1 > 200 && p.Item0 < 100 && p.Item2 < 100) > 0);
    }

    [Fact]
    public void TextOverlay_Rotation_PreservesCanvasSize()
    {
        using var input = new Mat(100, 100, MatType.CV_8UC3, new Scalar(10, 10, 10));

        using var result = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "TEST",
            Anchor = TextAnchor.Center,
            FontSize = 48,
            Rotation = 45
        });

        Assert.Equal(input.Size(), result.Size());
    }

    [Fact]
    public void TextOverlay_Multiline_RendersBothLines()
    {
        using var input = new Mat(120, 120, MatType.CV_8UC3, Scalar.All(0));

        using var result = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "LINE1\nLINE2",
            Anchor = TextAnchor.Center,
            FontSize = 40,
            LineSpacing = 16
        });

        Assert.Equal(input.Size(), result.Size());
        Assert.True(CountPixelsWhere(result, p => p.Item0 > 200 && p.Item1 > 200 && p.Item2 > 200) > 0);
    }

    [Fact]
    public void TextOverlay_LetterSpacing_WidensTheText()
    {
        using var input = new Mat(200, 200, MatType.CV_8UC3, Scalar.All(0));

        using var tight = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "TEXT",
            Anchor = TextAnchor.Center,
            FontSize = 40,
            LetterSpacing = 0
        });
        using var spaced = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "TEXT",
            Anchor = TextAnchor.Center,
            FontSize = 40,
            LetterSpacing = 30
        });

        Assert.True(ChangedBounds(input, spaced).Width > ChangedBounds(input, tight).Width);
    }

    [Fact]
    public void TextOverlay_AutoFitWidth_ScalesDownLongText()
    {
        using var input = new Mat(120, 120, MatType.CV_8UC3, Scalar.All(0));

        using var natural = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "WWWWWWWWWWWW",
            Anchor = TextAnchor.Center,
            FontSize = 80,
            AutoFitWidth = false
        });
        using var fitted = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "WWWWWWWWWWWW",
            Anchor = TextAnchor.Center,
            FontSize = 80,
            AutoFitWidth = true
        });

        Assert.True(ChangedBounds(input, fitted).Height < ChangedBounds(input, natural).Height);
    }

    [Fact]
    public void TextOverlay_ShadowColor_TintsTheShadow()
    {
        using var input = new Mat(150, 150, MatType.CV_8UC3, Scalar.All(0));

        using var result = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "TEST",
            Anchor = TextAnchor.Center,
            FontSize = 48,
            Color = new Vec3b(255, 255, 255),
            ShadowOffset = 6,
            ShadowOpacity = 1.0,
            ShadowColor = new Vec3b(0, 0, 255)
        });

        int redPixels = CountPixelsWhere(result, p => p.Item2 > 200 && p.Item0 < 100 && p.Item1 < 100);
        Assert.True(redPixels > 0, $"expected red shadow pixels, found {redPixels}");
    }

    [Fact]
    public void TextOverlay_ShadowBlur_PreservesCanvasSize()
    {
        using var input = new Mat(150, 150, MatType.CV_8UC3, Scalar.All(0));

        using var result = TextOverlayService.Render(input, new TextOverlayOptions
        {
            Text = "TEST",
            Anchor = TextAnchor.Center,
            FontSize = 48,
            ShadowOffset = 6,
            ShadowBlur = 4
        });

        Assert.Equal(input.Size(), result.Size());
    }

    private static Rect ChangedBounds(Mat before, Mat after)
    {
        using var diff = new Mat();
        Cv2.Absdiff(before, after, diff);
        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        using var mask = new Mat();
        Cv2.Threshold(gray, mask, 10, 255, ThresholdTypes.Binary);
        using var nonZero = new Mat();
        Cv2.FindNonZero(mask, nonZero);
        return nonZero.Rows == 0 ? new Rect() : Cv2.BoundingRect(nonZero);
    }

    private static int CountPixelsWhere(Mat bgr, Func<Vec3b, bool> predicate)
    {
        int count = 0;
        for (int y = 0; y < bgr.Height; y++)
        {
            for (int x = 0; x < bgr.Width; x++)
            {
                if (predicate(bgr.At<Vec3b>(y, x)))
                {
                    count++;
                }
            }
        }
        return count;
    }

    [Fact]
    public void Neon_ProducesEdgeGlow()
    {
        using var input = new Mat(21, 21, MatType.CV_8UC3, Scalar.All(0));
        using (var block = new Mat(input, new Rect(8, 8, 5, 5)))
        {
            block.SetTo(new Scalar(200, 200, 200));
        }

        using var result = FilterService.Apply(input, FilterKind.Neon, intensity: 1.0);

        Assert.Equal(input.Size(), result.Size());
        using var gray = new Mat();
        Cv2.CvtColor(result, gray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.CountNonZero(gray) > 0);
    }

    [Fact]
    public void Hdr_PreservesSizeAndType()
    {
        using var input = new Mat(30, 30, MatType.CV_8UC3, new Scalar(120, 90, 60));

        using var result = FilterService.Apply(input, FilterKind.Hdr, intensity: 1.0);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Pencil_PreservesSizeAndType()
    {
        using var input = new Mat(30, 30, MatType.CV_8UC3, new Scalar(120, 90, 60));

        using var result = FilterService.Apply(input, FilterKind.Pencil, intensity: 1.0);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Dreamy_PreservesSizeAndType()
    {
        using var input = new Mat(30, 30, MatType.CV_8UC3, new Scalar(120, 90, 60));

        using var result = FilterService.Apply(input, FilterKind.Dreamy, intensity: 1.0);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Cartoon_PreservesSizeAndType()
    {
        using var input = new Mat(30, 30, MatType.CV_8UC3, new Scalar(120, 90, 60));

        using var result = FilterService.Apply(input, FilterKind.Cartoon, intensity: 1.0);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }

    [Fact]
    public void Vivid_IncreasesColorSpread()
    {
        using var input = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 150, 200));

        using var result = FilterService.Apply(input, FilterKind.Vivid, intensity: 1.0);

        var before = input.At<Vec3b>(0, 0);
        var after = result.At<Vec3b>(0, 0);
        int beforeSpread = Math.Max(before.Item0, Math.Max(before.Item1, before.Item2)) - Math.Min(before.Item0, Math.Min(before.Item1, before.Item2));
        int afterSpread = Math.Max(after.Item0, Math.Max(after.Item1, after.Item2)) - Math.Min(after.Item0, Math.Min(after.Item1, after.Item2));
        Assert.True(afterSpread > beforeSpread);
    }

    [Fact]
    public void Noir_ProducesGrayscale()
    {
        using var input = new Mat(5, 5, MatType.CV_8UC3, new Scalar(0, 0, 255)); // pure red

        using var result = FilterService.Apply(input, FilterKind.Noir, intensity: 1.0);

        var px = result.At<Vec3b>(0, 0);
        Assert.True(Math.Abs(px.Item0 - px.Item2) <= 1);
    }

    [Fact]
    public void Warm_IncreasesRedChannel()
    {
        using var input = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = FilterService.Apply(input, FilterKind.Warm, intensity: 1.0);

        Assert.True(result.At<Vec3b>(0, 0).Item2 > 100);
    }

    [Fact]
    public void Cool_IncreasesBlueChannel()
    {
        using var input = new Mat(5, 5, MatType.CV_8UC3, new Scalar(100, 100, 100));

        using var result = FilterService.Apply(input, FilterKind.Cool, intensity: 1.0);

        Assert.True(result.At<Vec3b>(0, 0).Item0 > 100);
    }

    [Fact]
    public void Vintage_PreservesSizeAndType()
    {
        using var input = new Mat(20, 20, MatType.CV_8UC3, new Scalar(120, 90, 60));

        using var result = FilterService.Apply(input, FilterKind.Vintage, intensity: 1.0);

        Assert.Equal(input.Size(), result.Size());
        Assert.Equal(input.Type(), result.Type());
    }
}
