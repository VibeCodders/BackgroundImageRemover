using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Views.Controls;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfSize = System.Windows.Size;

namespace BackgroundImageRemover.Tests.Views;

public class ImagePreviewControlTests
{
    /// <summary>
    /// The Result pane must show a checkerboard behind the cutout's transparent pixels.
    /// This renders the actual control and verifies a fully transparent pixel shows the
    /// checkerboard tile (light gray), not black.
    /// </summary>
    [Fact]
    public void CheckerboardShowsThroughTransparentPixels()
    {
        var (sample, failure) = RenderAndSample(CreateBgraBitmapSource);
        Assert.Null(failure);
        Assert.NotNull(sample);

        // 40x40 bitmap scaled 3x fills the 120x120 control. Pixel (90, 60) is in the
        // transparent right half; behind it the checkerboard's bottom-right gray tile
        // quadrant (#E0E0E0) must show — never pure black.
        Assert.True(sample.Value.A > 0, $"transparent region rendered as fully transparent (nothing behind it): {sample}");
        Assert.True(sample.Value.R > 150 && sample.Value.G > 150 && sample.Value.B > 150,
            $"expected light checkerboard behind transparent pixels, got {sample}");
    }

    /// <summary>
    /// Same as above but through the app's real pipeline: a BGRA Mat with black RGB under
    /// the transparent half (like many cutout files), converted with OpenCvSharp's
    /// <c>ToBitmapSource()</c> extension used by the ViewModel.
    /// </summary>
    [Fact]
    public void CheckerboardShowsThroughOpenCvBitmapSource()
    {
        using var bgra = new Mat(40, 40, MatType.CV_8UC4);
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                bgra.Set(y, x, x < 20
                    ? new Vec4b(0, 0, 255, 255) // opaque magenta (B,G,R,A)
                    : new Vec4b(0, 0, 0, 0));   // fully transparent, black RGB underneath
            }
        }

        var (sample, failure) = RenderAndSample(() => bgra.ToBitmapSource());
        Assert.Null(failure);
        Assert.NotNull(sample);

        Assert.True(sample.Value.A > 0, $"transparent region rendered as fully transparent (nothing behind it): {sample}");
        Assert.True(sample.Value.R > 150 && sample.Value.G > 150 && sample.Value.B > 150,
            $"expected light checkerboard behind transparent pixels (alpha preserved by ToBitmapSource), got {sample}");
    }

    private static (Color? Sample, Exception? Failure) RenderAndSample(Func<BitmapSource> createBitmap)
    {
        Color? sample = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var bitmap = createBitmap();
                var control = new ImagePreviewControl
                {
                    ImageSource = bitmap,
                    Width = 120,
                    Height = 120
                };
                control.Measure(new WpfSize(120, 120));
                control.Arrange(new WpfRect(0, 0, 120, 120));
                control.UpdateLayout();

                var rtb = new RenderTargetBitmap(120, 120, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(control);
                sample = CopyPixelsToColor(rtb, 90, 60);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA render thread timed out");
        return (sample, failure);
    }

    private static BitmapSource CreateBgraBitmapSource()
    {
        // 40x40 BGRA bitmap: left half opaque red, right half fully transparent.
        const int size = 40;
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = (y * size + x) * 4;
                if (x < size / 2)
                {
                    pixels[i] = 0;       // B
                    pixels[i + 1] = 0;   // G
                    pixels[i + 2] = 255; // R
                    pixels[i + 3] = 255; // A
                }
                else
                {
                    pixels[i] = 0;
                    pixels[i + 1] = 0;
                    pixels[i + 2] = 0;
                    pixels[i + 3] = 0; // fully transparent
                }
            }
        }
        return BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
    }

    private static Color CopyPixelsToColor(BitmapSource source, int x, int y)
    {
        var buffer = new byte[4];
        source.CopyPixels(new Int32Rect(x, y, 1, 1), buffer, 4, 0);
        return Color.FromArgb(buffer[3], buffer[2], buffer[1], buffer[0]);
    }
}
