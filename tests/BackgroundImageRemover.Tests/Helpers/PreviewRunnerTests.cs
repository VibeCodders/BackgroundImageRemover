using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Helpers;

/// <summary>
/// Locks the shared preview contract extracted into <see cref="PreviewRunner"/>: the readiness
/// guards, the missing-strategy skip, the debounced scheduling, the cancellation lifecycle and
/// the result/status setters must behave identically for both hosts (inline document editor and
/// Background Remover tool tab).
/// </summary>
public sealed class PreviewRunnerTests
{
    [Fact]
    public void RunPreviewAsync_WhenPreviewNull_DoesNotRunStrategy()
    {
        RunOnSta(() =>
        {
            var harness = new Harness { HasPreview = false };
            using var runner = harness.CreateRunner();

            runner.RunPreviewAsync().GetAwaiter().GetResult();

            Assert.Equal(0, harness.Strategy.PreviewCalls);
            Assert.Null(harness.Result);
            Assert.Null(harness.Status);
            Assert.Equal(0, harness.Completed);
        });
    }

    [Fact]
    public void RunPreviewAsync_WhenStrategyMissing_DoesNotRun()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            using var runner = harness.CreateRunner(registerStrategy: false);

            runner.RunPreviewAsync().GetAwaiter().GetResult();

            Assert.Equal(0, harness.Strategy.PreviewCalls);
            Assert.Null(harness.Result);
            Assert.Null(harness.Status);
        });
    }

    [Fact]
    public void RunPreviewAsync_WhenNotReady_SkipsRun()
    {
        RunOnSta(() =>
        {
            var harness = new Harness { Ready = _ => false };
            using var runner = harness.CreateRunner();

            runner.RunPreviewAsync().GetAwaiter().GetResult();

            Assert.Equal(0, harness.Strategy.PreviewCalls);
            Assert.Null(harness.Result);
            Assert.Null(harness.Status);
            Assert.Equal(0, harness.Completed);
        });
    }

    [Fact]
    public void RunPreviewAsync_Success_RendersResultAndInvokesCompletedHook()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            using var runner = harness.CreateRunner();

            runner.RunPreviewAsync().GetAwaiter().GetResult();

            Assert.Equal(1, harness.Strategy.PreviewCalls);
            Assert.NotNull(harness.Result);
            Assert.Equal(4, harness.Result!.PixelWidth);
            Assert.Equal(4, harness.Result.PixelHeight);
            Assert.Equal(PixelFormats.Bgra32, harness.Result.Format);
            Assert.Equal(1, harness.Completed);
            Assert.Null(harness.Status);
        });
    }

    [Fact]
    public void RunPreviewAsync_StrategyThrows_ReportsPreviewFailed()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            harness.Strategy.OnPreview = (_, _, _) => throw new InvalidOperationException("boom");
            using var runner = harness.CreateRunner();

            runner.RunPreviewAsync().GetAwaiter().GetResult();

            Assert.Equal(1, harness.Strategy.PreviewCalls);
            Assert.Null(harness.Result);
            Assert.Equal("Preview failed: boom", harness.Status);
            Assert.Equal(0, harness.Completed);
        });
    }

    [Fact]
    public void RunPreviewAsync_OperationCanceled_NoResultNoStatus()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            harness.Strategy.OnPreview = (_, _, ct) => throw new OperationCanceledException(ct);
            using var runner = harness.CreateRunner();

            runner.RunPreviewAsync().GetAwaiter().GetResult();

            Assert.Equal(1, harness.Strategy.PreviewCalls);
            Assert.Null(harness.Result);
            Assert.Null(harness.Status);
            Assert.Equal(0, harness.Completed);
        });
    }

    [Fact]
    public void RunPreviewAsync_SupersedesInFlightRun()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Strategy.OnPreview = async (_, _, ct) =>
            {
                firstStarted.SetResult(true);
                await Task.Delay(Timeout.Infinite, ct); // throws OperationCanceledException when superseded
                throw new OperationCanceledException(ct); // unreachable, keeps the lambda well-formed
            };
            using var runner = harness.CreateRunner();

            var run1 = runner.RunPreviewAsync();
            firstStarted.Task.GetAwaiter().GetResult(); // strategy 1 is now suspended inside its await
            harness.Strategy.OnPreview = null; // run 2 takes the default fast path
            var run2 = runner.RunPreviewAsync(); // cancels the in-flight run 1

            run1.GetAwaiter().GetResult();
            run2.GetAwaiter().GetResult();

            Assert.Equal(2, harness.Strategy.PreviewCalls);
            // Only the second (winning) run reaches the result setters.
            Assert.NotNull(harness.Result);
            Assert.Equal(1, harness.Completed);
            Assert.Null(harness.Status);
        });
    }

    [Fact]
    public void CancelInFlight_CancelsTheRunningStrategy()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            harness.Strategy.OnPreview = async (_, _, ct) =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    harness.Strategy.TokenCancelled = true;
                    throw;
                }
                throw new InvalidOperationException("unreachable");
            };
            using var runner = harness.CreateRunner();

            var run = runner.RunPreviewAsync();
            runner.CancelInFlight();

            run.GetAwaiter().GetResult();

            Assert.True(harness.Strategy.TokenCancelled);
            Assert.Null(harness.Result);
            Assert.Null(harness.Status);
        });
    }

    [Fact]
    public void RequestPreviewDebounced_CoalescesRapidRequests_IntoSingleRun()
    {
        RunOnSta(() =>
        {
            var harness = new Harness();
            using var runner = harness.CreateRunner();

            for (var i = 0; i < 5; i++)
            {
                runner.RequestPreviewDebounced();
            }
            // Wait until the coalesced run finishes (polling, not a fixed sleep: the CI runner
            // can starve the debounce timer/worker and 500 ms is not a guarantee).
            WaitUntil(() => harness.Completed >= 1);

            Assert.Equal(1, harness.Strategy.PreviewCalls);
            Assert.NotNull(harness.Result);
            Assert.Equal(1, harness.Completed);
        });
    }

    [Fact]
    public void RequestPreviewDebounced_WhenGateBlocked_DoesNotSchedule()
    {
        RunOnSta(() =>
        {
            var harness = new Harness { Gate = () => false };
            using var runner = harness.CreateRunner();

            runner.RequestPreviewDebounced();
            PumpDispatcher(TimeSpan.FromMilliseconds(500)); // negative assertion: just let the debounce window pass

            Assert.Equal(0, harness.Strategy.PreviewCalls);
            Assert.Null(harness.Result);
            Assert.Equal(0, harness.Completed);
        });
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

    /// <summary>Runs a test body on an STA thread: the runner renders WPF BitmapSources and its
    /// debounce timer needs a dispatcher, both of which require an STA thread.</summary>
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

    /// <summary>Pumps the current thread's dispatcher for a duration so DispatcherTimers can tick.</summary>
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

    /// <summary>Host fakes wired into a <see cref="PreviewRunner"/>: captures the rendered result,
    /// the status message and the completed-hook count, and controls the readiness/gate behavior.</summary>
    private sealed class Harness
    {
        public readonly FakeStrategy Strategy = new();
        public readonly ScribbleManager Scribbles = new();
        public readonly PreviewImage Preview = new(
            new Mat(4, 4, MatType.CV_8UC3, new Scalar(10, 20, 30)), 1.0);

        public bool HasPreview = true;
        public Func<StrategyKind, bool>? Ready;
        public Func<bool>? Gate;

        public BitmapSource? Result;
        public string? Status;
        public int Completed;

        public PreviewRunner CreateRunner(bool registerStrategy = true)
        {
            var strategies = new Dictionary<StrategyKind, IBackgroundRemovalStrategy>();
            if (registerStrategy)
            {
                strategies[StrategyKind.GrabCut] = Strategy;
            }
            return new PreviewRunner(
                () => HasPreview ? Preview : null,
                strategies,
                () => StrategyKind.GrabCut,
                Ready ?? (_ => true),
                () => Scribbles,
                (_, _) => new StrategyContext(),
                bitmap => Result = bitmap,
                message => Status = message,
                () => Completed++,
                Gate);
        }
    }

    private sealed class FakeStrategy : IBackgroundRemovalStrategy
    {
        /// <summary>Per-call override; null uses the default fast path (opaque 4x4 BGRA result).</summary>
        public Func<Mat, StrategyContext, CancellationToken, Task<RemovalResult>>? OnPreview;

        public StrategyKind Kind => StrategyKind.GrabCut;
        public int PreviewCalls;
        public int FullCalls;
        public bool TokenCancelled;

        public Task<RemovalResult> RunPreviewAsync(Mat previewBgr, StrategyContext context, CancellationToken ct)
        {
            PreviewCalls++;
            return OnPreview is not null
                ? OnPreview(previewBgr, context, ct)
                : Task.FromResult(new RemovalResult(
                    new Mat(previewBgr.Size(), MatType.CV_8UC4, Scalar.All(255)), 1.0));
        }

        public Task<RemovalResult> RunFullAsync(Mat fullBgr, StrategyContext context, CancellationToken ct)
        {
            FullCalls++;
            return Task.FromResult(new RemovalResult(
                new Mat(fullBgr.Size(), MatType.CV_8UC4, Scalar.All(255)), 1.0));
        }
    }
}
