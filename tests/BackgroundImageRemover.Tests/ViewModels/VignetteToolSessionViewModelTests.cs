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

            // Wait (polling the dispatcher) until the debounced off-thread run has rendered the
            // darkened corner, instead of a fixed sleep that flakes when the CI runner is loaded.
            WaitUntil(() => !ReferenceEquals(original, vm.ResultBitmap)
                && PreviewPixel(vm, 0, 0) != new Vec4b(10, 20, 30, 255));

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
            // The default 0.3 strength is already an active effect (corners darkened), so wait
            // for the corner to CHANGE after strength 1.0 is applied through the debounce.
            var initialCorner = PreviewPixel(vm, 0, 0);
            vm.Strength = 1.0;
            WaitUntil(() => PreviewPixel(vm, 0, 0) != initialCorner); // strong vignette applied
            var strongCorner = PreviewPixel(vm, 0, 0);

            // Reset refreshes synchronously (no debounce): the corner visibly lightens because
            // the strength drops from 1.0 back to the 0.3 default. (The default is still an
            // active effect for this tool, so IsDirty stays true — unchanged behavior.)
            vm.ResetCommand.Execute(null);
            var defaultCorner = PreviewPixel(vm, 0, 0);
            var afterResetBitmap = vm.ResultBitmap;
            Assert.NotEqual(strongCorner, defaultCorner);

            // The property sets inside Reset arm the debounce again; the pending run must
            // re-render the same default state (idempotent) as a NEW bitmap instance — not a
            // stale strong vignette. Wait for that new instance (the debounce run assigns a
            // fresh BitmapSource even for identical pixels), then pin the pixels.
            WaitUntil(() => !ReferenceEquals(vm.ResultBitmap, afterResetBitmap)
                && PreviewPixel(vm, 0, 0) == defaultCorner);
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

    /// <summary>
    /// Pumps the STA dispatcher until <paramref name="condition"/> holds or a generous deadline
    /// passes. Polling (rather than a fixed sleep) keeps the test robust when the CI runner is
    /// loaded and starves the debounce/worker threads.
    /// </summary>
    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }
            PumpDispatcher(TimeSpan.FromMilliseconds(25));
        }
        Assert.Fail($"Condition not met within 10s: {condition.Method.Name}");
    }
}
