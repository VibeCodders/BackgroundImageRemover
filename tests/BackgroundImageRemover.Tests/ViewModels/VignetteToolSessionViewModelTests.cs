using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using BackgroundImageRemover.Tests.Helpers;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Pins the debounced, off-UI-thread live preview shared by the single-effect tools
/// (<see cref="PreviewToolSessionViewModelBase"/>): a slider change must NOT re-render the
/// full-resolution preview synchronously on the calling thread — it coalesces through the
/// shared debounce and the effect runs off the UI thread, with the result applied on the
/// dispatcher afterwards.
/// </summary>
public sealed class VignetteToolSessionViewModelTests
{
    private const int Width = 40;
    private const int Height = 40;

    [Fact]
    public void SliderChange_IsDebouncedAndUpdatesPreviewAsync()
    {
        RunOnSta(() =>
        {
            using var vm = CreateVm();
            var original = vm.ResultBitmap;
            Assert.NotNull(original);

            vm.Strength = 1.0;
            Assert.Same(original, vm.ResultBitmap); // debounce: not applied synchronously

            PumpDispatcher(TimeSpan.FromMilliseconds(600));

            Assert.True(vm.IsDirty);
            Assert.NotSame(original, vm.ResultBitmap);
            // The vignette darkens the corners; the source is a solid (10,20,30) image.
            Assert.NotEqual(new Vec4b(10, 20, 30, 255), PreviewPixel(vm, 0, 0));
        });
    }

    [Fact]
    public void Reset_RestoresDefaultsAndRefreshesSynchronously()
    {
        RunOnSta(() =>
        {
            using var vm = CreateVm();
            vm.Strength = 1.0;
            PumpDispatcher(TimeSpan.FromMilliseconds(600));
            var strongCorner = PreviewPixel(vm, 0, 0);

            // Reset refreshes synchronously (no debounce): the corner visibly lightens because
            // the strength drops from 1.0 back to the 0.3 default. (The default is still an
            // active effect for this tool, so IsDirty stays true — unchanged behavior.)
            vm.ResetCommand.Execute(null);
            var defaultCorner = PreviewPixel(vm, 0, 0);
            Assert.NotEqual(strongCorner, defaultCorner);

            // The property sets inside Reset arm the debounce again; the pending run must
            // re-render the same default state (idempotent), not a stale strong vignette.
            PumpDispatcher(TimeSpan.FromMilliseconds(600));
            Assert.Equal(defaultCorner, PreviewPixel(vm, 0, 0));
        });
    }

    // ---- helpers ----

    private static VignetteToolSessionViewModel CreateVm()
    {
        var (doc, shell) = TestDoubles.CreateDocumentAndShell(Width, Height);
        doc.LoadImageAsync("photo.jpg").GetAwaiter().GetResult();
        return new VignetteToolSessionViewModel(shell, doc);
    }

    private static Vec4b PreviewPixel(VignetteToolSessionViewModel vm, int x, int y)
    {
        using var bgra = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(vm.ResultBitmap!);
        return bgra.Get<Vec4b>(y, x);
    }

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
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var stop = new DispatcherTimer { Interval = duration };
        stop.Tick += (_, _) =>
        {
            stop.Stop();
            frame.Continue = false;
        };
        stop.Start();
        Dispatcher.PushFrame(frame);
    }
}
