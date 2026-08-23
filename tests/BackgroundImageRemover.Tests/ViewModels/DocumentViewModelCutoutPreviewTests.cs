using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BackgroundImageRemover.Tests.Helpers;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Locks the cutout-aware preview fallback coherence: whenever the document rebuilds a preview
/// whose source is a cutout working pair, <see cref="DocumentViewModel.PreviewBitmap"/> must carry
/// the alpha (Bgra32 with the transparency preserved) instead of a flat BGR render. The two flows
/// covered are the duplicate-tab flow and a size-changing edit (crop/resize/transform/frame/...).
/// Each has an opaque negative control pinning the flat Bgr24 path, so a regression back to
/// <c>ToBitmapSource()</c> fails the cutout tests while the opaque ones stay green.
/// </summary>
public sealed class DocumentViewModelCutoutPreviewTests
{
    private const int Width = 8;
    private const int Height = 8;
    private const int NewWidth = 12;
    private const int NewHeight = 12;

    [Fact]
    public void DuplicateTabFlow_WithCutoutWorkingPair_PreviewPreservesAlpha()
    {
        RunOnSta(() =>
        {
            var doc = CreateDocument(Width, Height);
            doc.LoadImageAsync("photo.jpg").GetAwaiter().GetResult();
            using (var bgr = new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30)))
            using (var alpha = CreateCutoutAlpha(Height, Width, 4, 4))
            {
                doc.ApplyToolResult(bgr.Clone(), alpha.Clone(), "Cutout");
            }

            // ShellViewModel.DuplicateTabAsync runs exactly these two calls: snapshot the current
            // working pair, then load it into a fresh document.
            var snapshot = doc.CreateCurrentStateSnapshot();
            var copy = CreateDocument(1, 1);
            copy.LoadFromSnapshotAsync(snapshot, doc.Title + " (copy)").GetAwaiter().GetResult();

            var bitmap = copy.PreviewBitmap!;
            Assert.Equal(PixelFormats.Bgra32, bitmap.Format);
            Assert.Equal((byte)0, SampleAlpha(bitmap, 4, 4));
            Assert.Equal((byte)255, SampleAlpha(bitmap, 0, 0));

            copy.Dispose();
            doc.Dispose();
        });
    }

    [Fact]
    public void DuplicateTabFlow_WithOpaqueWorkingPair_PreviewStaysPlain()
    {
        RunOnSta(() =>
        {
            var doc = CreateDocument(Width, Height);
            doc.LoadImageAsync("photo.jpg").GetAwaiter().GetResult();
            using (var bgr = new Mat(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30)))
            using (var alpha = new Mat(Height, Width, MatType.CV_8UC1, Scalar.All(255)))
            {
                doc.ApplyToolResult(bgr.Clone(), alpha.Clone(), "Opaque");
            }

            var snapshot = doc.CreateCurrentStateSnapshot();
            var copy = CreateDocument(1, 1);
            copy.LoadFromSnapshotAsync(snapshot, doc.Title + " (copy)").GetAwaiter().GetResult();

            // A uniformly opaque working pair is not meaningful transparency: the fallback stays flat.
            Assert.Equal(PixelFormats.Bgr24, copy.PreviewBitmap!.Format);

            copy.Dispose();
            doc.Dispose();
        });
    }

    [Fact]
    public void SizeChangingEdit_WithCutoutWorkingPair_PreviewPreservesAlpha()
    {
        RunOnSta(() =>
        {
            var doc = CreateDocument(Width, Height);
            doc.LoadImageAsync("photo.jpg").GetAwaiter().GetResult();
            using (var bgr = new Mat(NewHeight, NewWidth, MatType.CV_8UC3, new Scalar(10, 20, 30)))
            using (var alpha = CreateCutoutAlpha(NewHeight, NewWidth, 6, 6))
            {
                doc.ApplyToolResult(bgr.Clone(), alpha.Clone(), "Resize");
            }

            // EnsureLoadedImageMatchesWorkingSize rebuilt the loaded image and preview from the
            // working pair: the fallback must carry the cutout's alpha, not a flat BGR render.
            var bitmap = doc.PreviewBitmap!;
            Assert.Equal(NewWidth, doc.ImageWidth); // proves the size-change path actually ran
            Assert.Equal(PixelFormats.Bgra32, bitmap.Format);
            Assert.Equal((byte)0, SampleAlpha(bitmap, 6, 6));
            Assert.Equal((byte)255, SampleAlpha(bitmap, 0, 0));

            doc.Dispose();
        });
    }

    [Fact]
    public void SizeChangingEdit_WithOpaqueWorkingPair_PreviewStaysPlain()
    {
        RunOnSta(() =>
        {
            var doc = CreateDocument(Width, Height);
            doc.LoadImageAsync("photo.jpg").GetAwaiter().GetResult();
            using (var bgr = new Mat(NewHeight, NewWidth, MatType.CV_8UC3, new Scalar(10, 20, 30)))
            using (var alpha = new Mat(NewHeight, NewWidth, MatType.CV_8UC1, Scalar.All(255)))
            {
                doc.ApplyToolResult(bgr.Clone(), alpha.Clone(), "Resize");
            }

            Assert.Equal(NewWidth, doc.ImageWidth); // proves the size-change path actually ran
            Assert.Equal(PixelFormats.Bgr24, doc.PreviewBitmap!.Format);

            doc.Dispose();
        });
    }

    /// <summary>An otherwise opaque alpha with a fully transparent 2x2 block at the given row/col.</summary>
    private static Mat CreateCutoutAlpha(int height, int width, int blockRow, int blockCol)
    {
        var alpha = new Mat(height, width, MatType.CV_8UC1, Scalar.All(255));
        alpha.Set(blockRow, blockCol, (byte)0);
        alpha.Set(blockRow, blockCol + 1, (byte)0);
        alpha.Set(blockRow + 1, blockCol, (byte)0);
        alpha.Set(blockRow + 1, blockCol + 1, (byte)0);
        return alpha;
    }

    private static DocumentViewModel CreateDocument(int width, int height)
    {
        var (doc, _) = TestDoubles.CreateDocumentAndShell(width, height);
        return doc;
    }

    /// <summary>Copies the alpha byte of one bitmap pixel. Must run on the same thread that
    /// created the bitmap (WPF bitmaps are thread-affine).</summary>
    private static byte SampleAlpha(BitmapSource bitmap, int x, int y)
    {
        var buffer = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), buffer, 4, 0);
        return buffer[3];
    }

    /// <summary>Runs a test body on an STA thread: the document builds WPF BitmapSources, and
    /// creation and sampling must share the thread.</summary>
    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA test thread timed out");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
