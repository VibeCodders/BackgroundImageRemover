using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using BackgroundImageRemover.Tests.Helpers;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Pins the debounced, off-UI-thread live preview of the Adjustments tool: slider changes must
/// coalesce into a single full-resolution run (not one synchronous UI-thread pass per tick),
/// the result must reflect the final slider values, and resetting to identity must restore the
/// original bitmap.
/// </summary>
public sealed class AdjustmentsToolSessionViewModelTests
{
    private const int Width = 40;
    private const int Height = 40;

    [Fact]
    public void SliderChange_AfterDebounce_UpdatesPreviewOffUiThread()
    {
        RunOnSta(() =>
        {
            using var vm = CreateVm();
            var original = vm.ResultBitmap;
            Assert.NotNull(original);

            vm.AdjBrightness = 50;
            Assert.Same(original, vm.ResultBitmap); // debounce: not applied synchronously

            WaitUntil(() => vm.IsDirty && !ReferenceEquals(original, vm.ResultBitmap));

            Assert.True(vm.IsDirty);
            Assert.NotSame(original, vm.ResultBitmap);
            // source solid (10,20,30) + brightness 50 -> (60,70,80), opaque alpha
            Assert.Equal(new Vec4b(60, 70, 80, 255), PreviewPixel(vm, 0, 0));
        });
    }

    [Fact]
    public void RapidSliderChanges_CoalesceIntoFinalValue()
    {
        RunOnSta(() =>
        {
            using var vm = CreateVm();

            // Several changes before the debounce fires: only the last value matters.
            vm.AdjBrightness = 10;
            vm.AdjBrightness = 40;
            vm.AdjBrightness = 70;
            WaitUntil(() => vm.IsDirty);

            Assert.True(vm.IsDirty);
            Assert.Equal(new Vec4b(80, 90, 100, 255), PreviewPixel(vm, 0, 0));
        });
    }

    [Fact]
    public void ResetToIdentity_RestoresOriginalBitmap()
    {
        RunOnSta(() =>
        {
            using var vm = CreateVm();
            var original = vm.ResultBitmap;

            vm.AdjBrightness = 50;
            WaitUntil(() => !ReferenceEquals(original, vm.ResultBitmap));
            Assert.NotSame(original, vm.ResultBitmap);

            vm.AdjBrightness = 0; // identity again
            WaitUntil(() => ReferenceEquals(original, vm.ResultBitmap));

            Assert.False(vm.IsDirty);
            Assert.Same(original, vm.ResultBitmap);
        });
    }

    // ---- helpers ----

    private static AdjustmentsToolSessionViewModel CreateVm()
    {
        var (doc, shell) = TestDoubles.CreateDocumentAndShell(Width, Height);
        doc.LoadImageAsync("photo.jpg").GetAwaiter().GetResult();
        return new AdjustmentsToolSessionViewModel(shell, doc, new FakeFileLogService());
    }

    private static Vec4b PreviewPixel(AdjustmentsToolSessionViewModel vm, int x, int y)
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
    /// passes. Polling (rather than a fixed sleep) keeps the test robust when the parallel test
    /// runner starves the debounce/worker threads.
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
