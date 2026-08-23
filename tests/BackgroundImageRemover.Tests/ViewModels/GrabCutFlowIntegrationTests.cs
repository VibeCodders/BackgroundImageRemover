using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Batch;
using BackgroundImageRemover.Services.Dialogs;
using BackgroundImageRemover.Services.ImageIo;
using BackgroundImageRemover.Services.Logging;
using BackgroundImageRemover.Services.Onnx;
using BackgroundImageRemover.Services.Outpaint;
using BackgroundImageRemover.Services.Preview;
using BackgroundImageRemover.Services.Projects;
using BackgroundImageRemover.Services.Sam;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Services.Strategies;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

using BackgroundImageRemover.Tests.Helpers;
namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// End-to-end GrabCut flow through the real ViewModels: open the Background Remover tool
/// tab, pick GrabCut, draw a rectangle, scribble over the subject, run the preview and
/// apply. Pins the scribble-lifetime fix: the full-res scribble copies must stay alive for
/// the whole background run, and an in-flight preview must not touch the manager's live
/// Mats after the UI thread clears them (previously "Cannot access a disposed object").
/// Runs on a dedicated STA thread with a pumped dispatcher, mirroring the real app.
/// </summary>
public class GrabCutFlowIntegrationTests
{
    // A dark background with a bright, sharply-bordered rectangle "subject" -- the same scene
    // the strategy unit tests use, so GrabCut converges deterministically at this resolution.
    private const int ImageWidth = 200;
    private const int ImageHeight = 150;

    [Fact]
    public void FullFlow_RectScribblePreviewAndApply_AppliesToTheParentDocument()
        => RunOnSta(async () =>
        {
            var grabCut = new GrabCutStrategy();
            var doc = CreateDocument(grabCut);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { grabCut }, grabCut);
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");
            doc.OpenToolTab(EditorTool.RemoveBackground);
            var session = Assert.IsType<BackgroundRemoverToolSessionViewModel>(doc.ActiveToolSession);

            // Pick GrabCut: the left preview must switch to rectangle-drawing mode.
            session.SelectedStrategy = StrategyKind.GrabCut;
            Assert.Equal(InteractionMode.DrawRect, session.OriginalMode);

            // Draw the rectangle: the debounced preview must produce a cutout on its own.
            session.GrabCut.SelectedRect = new Rect(30, 20, 140, 110);
            PumpUntil(() => session.ResultBitmap is not null, TimeSpan.FromSeconds(5));
            Assert.NotNull(session.ResultBitmap);
            Assert.DoesNotContain("failed", session.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);

            // Add a foreground scribble over the subject, then refresh the preview directly
            // (the "Update preview" button path) -- both inputs must feed the same run.
            session.OriginalMode = InteractionMode.ScribbleForeground;
            session.OnOriginalStrokeStart(new WpfPoint(100, 75));
            session.OnOriginalStrokeMove(new WpfPoint(110, 75));
            session.OnOriginalStrokeEnd();
            Assert.True(session.GrabCut.HasScribbles);

            await session.RefineGrabCutPreviewCommand.ExecuteAsync(null);
            Assert.NotNull(session.ResultBitmap);
            Assert.DoesNotContain("failed", session.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);

            // Apply: the full-res run consumes the resized scribble copies. Regression: the
            // copies used to be disposed before the run even started (the old "using var
            // inside an if" scope), which threw "Apply failed: Cannot access a disposed
            // object" on every apply with scribbles.
            await session.ApplyCommand.ExecuteAsync(null);

            Assert.True(doc.HasWorkingResult);
            Assert.Null(doc.ActiveToolSession); // the tool tab closed itself on success
            Assert.DoesNotContain("Apply failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.Contains(doc.EditSteps, s => s.Name.Contains("Remove Background"));

            doc.Dispose();
        });

    [Fact]
    public void Preview_WithScribblesSurvivesAClearRacingMidRun()
        => RunOnSta(async () =>
        {
            var gated = new GatedGrabCutStrategy();
            var doc = CreateDocument(gated);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { gated }, new GrabCutStrategy());
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");
            doc.OpenToolTab(EditorTool.RemoveBackground);
            var session = Assert.IsType<BackgroundRemoverToolSessionViewModel>(doc.ActiveToolSession);

            session.SelectedStrategy = StrategyKind.GrabCut;
            session.GrabCut.SelectedRect = new Rect(30, 20, 140, 110);
            session.OriginalMode = InteractionMode.ScribbleForeground;
            session.OnOriginalStrokeStart(new WpfPoint(100, 75));
            session.OnOriginalStrokeEnd();
            Assert.True(session.GrabCut.HasScribbles);

            // Start a preview and hold it inside the strategy (the gate blocks before GrabCut
            // runs). The command's continuation needs the pumped dispatcher, so wait for the
            // gate with pumping.
            var previewTask = session.RefineGrabCutPreviewCommand.ExecuteAsync(null);
            PumpUntil(() => gated.Entered.IsSet, TimeSpan.FromSeconds(10));

            // The UI thread now clears the scribbles -- drawing a new rectangle does exactly
            // this (SelectedRect's handler calls ScribbleManager.Clear, disposing the live
            // Mats). The in-flight preview must keep using its own snapshot and complete.
            session.GrabCut.SelectedRect = new Rect(40, 30, 130, 100);
            gated.Proceed.Set();

            await previewTask;

            PumpUntil(() => session.ResultBitmap is not null, TimeSpan.FromSeconds(10));
            Assert.NotNull(session.ResultBitmap);
            Assert.DoesNotContain("failed", session.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);

            session.Dispose();
            doc.Dispose();
        });

    [Fact]
    public void Apply_WithANewRectangleDrawnMidRun_CompletesAndLeavesTheDocumentConsistent()
        => RunOnSta(async () =>
        {
            var gated = new ApplyGatedGrabCutStrategy();
            var doc = CreateDocument(gated);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { gated }, new GrabCutStrategy());
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");
            doc.OpenToolTab(EditorTool.RemoveBackground);
            var session = Assert.IsType<BackgroundRemoverToolSessionViewModel>(doc.ActiveToolSession);

            session.SelectedStrategy = StrategyKind.GrabCut;
            session.GrabCut.SelectedRect = new Rect(30, 20, 140, 110);
            session.OriginalMode = InteractionMode.ScribbleForeground;
            session.OnOriginalStrokeStart(new WpfPoint(100, 75));
            session.OnOriginalStrokeEnd();
            Assert.True(session.GrabCut.HasScribbles);

            // Start the apply and hold it inside the strategy (the gate blocks before GrabCut
            // runs full-res).
            var applyTask = session.ApplyCommand.ExecuteAsync(null);
            PumpUntil(() => gated.FullRunEntries >= 1, TimeSpan.FromSeconds(10));

            // The user draws a new rectangle while the apply is in flight: this clears
            // (disposes) the live scribbles and fires a debounced preview. The running apply
            // owns its own copies, so it must be unaffected, and the preview must complete.
            var previewBefore = session.ResultBitmap;
            session.GrabCut.SelectedRect = new Rect(40, 30, 130, 100);
            PumpUntil(
                () => session.ResultBitmap is not null && !ReferenceEquals(session.ResultBitmap, previewBefore),
                TimeSpan.FromSeconds(10));
            Assert.DoesNotContain("failed", session.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);

            // Let the apply finish: it completes with the selection it captured up front.
            gated.Proceed.Set();
            await applyTask;

            Assert.True(doc.HasWorkingResult);
            Assert.Null(doc.ActiveToolSession); // the tool tab closed itself on success
            Assert.Contains("Applied", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);

            doc.Dispose();
        });

    [Fact]
    public void Apply_SupersededByANewerApply_IsCancelledAndTheDocumentStaysConsistent()
        => RunOnSta(async () =>
        {
            var gated = new ApplyGatedGrabCutStrategy();
            var log = new RecordingFileLogService();
            var doc = CreateDocument(gated, log: log);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { gated }, new GrabCutStrategy(), log: log);
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");
            doc.OpenToolTab(EditorTool.RemoveBackground);
            var session = Assert.IsType<BackgroundRemoverToolSessionViewModel>(doc.ActiveToolSession);

            session.SelectedStrategy = StrategyKind.GrabCut;
            session.GrabCut.SelectedRect = new Rect(30, 20, 140, 110);

            var apply1 = session.ApplyCommand.ExecuteAsync(null);
            PumpUntil(() => gated.FullRunEntries >= 1, TimeSpan.FromSeconds(10));

            // The user draws a new rectangle and presses Apply again while the first run is
            // still held: the first apply is superseded (its cancellation token is cancelled),
            // the second one completes with the new selection.
            session.GrabCut.SelectedRect = new Rect(40, 30, 130, 100);
            var apply2 = session.ApplyCommand.ExecuteAsync(null);
            PumpUntil(() => gated.FullRunEntries >= 2, TimeSpan.FromSeconds(10));

            gated.Proceed.Set();
            await Task.WhenAll(apply1, apply2);

            // One apply was cancelled, one completed -- the document must not be left in a
            // half-applied state, and a cancellation must not surface as a failure.
            Assert.True(doc.HasWorkingResult);
            Assert.Null(doc.ActiveToolSession);
            Assert.Contains("Applied", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("failed", session.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(log.Errors, e => e.Contains("Failed to apply background removal"));

            doc.Dispose();
        });

    [Fact]
    public void Apply_WithOnlyARectangle_StillSucceeds()
        => RunOnSta(async () =>
        {
            var grabCut = new GrabCutStrategy();
            var doc = CreateDocument(grabCut);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { grabCut }, grabCut);
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");
            doc.OpenToolTab(EditorTool.RemoveBackground);
            var session = Assert.IsType<BackgroundRemoverToolSessionViewModel>(doc.ActiveToolSession);

            session.SelectedStrategy = StrategyKind.GrabCut;
            session.GrabCut.SelectedRect = new Rect(30, 20, 140, 110);

            await session.ApplyCommand.ExecuteAsync(null);

            Assert.True(doc.HasWorkingResult);
            Assert.DoesNotContain("Apply failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);

            doc.Dispose();
        });

    [Fact]
    public void UiCommands_AreDisabledWhileAFullResRunIsInFlight_AndReenabledAfter()
        => RunOnSta(async () =>
        {
            var gated = new ApplyGatedGrabCutStrategy();
            var doc = CreateDocument(gated);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { gated }, new GrabCutStrategy());
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");
            doc.SelectedStrategy = StrategyKind.GrabCut;
            doc.GrabCut.SelectedRect = new Rect(30, 20, 140, 110);

            // Draw scribbles too, so undo is genuinely available while busy: without the
            // busy gate the scribble-undo branch would answer true here (the ScribbleManager
            // has a stroke to undo) and let the user race the in-flight run.
            doc.OriginalMode = InteractionMode.ScribbleForeground;
            doc.OnOriginalStrokeStart(new WpfPoint(100, 75));
            doc.OnOriginalStrokeMove(new WpfPoint(110, 75));
            doc.OnOriginalStrokeEnd();
            Assert.True(doc.ScribbleManager.CanUndo);

            // Export runs the strategy at full resolution; the gate holds it mid-flight.
            var exportTask = doc.ExportCommand.ExecuteAsync(null);
            PumpUntil(() => gated.FullRunEntries >= 1, TimeSpan.FromSeconds(10));
            Assert.True(doc.IsBusy);

            // The UI can no longer generate the undo/open-while-busy conflict: every command
            // that would replace or mutate the live working state is disabled.
            Assert.False(doc.UndoCommand.CanExecute(null));
            Assert.False(doc.RedoCommand.CanExecute(null));
            Assert.False(doc.OpenFileCommand.CanExecute(null));
            Assert.False(doc.PasteFromClipboardCommand.CanExecute(null));
            Assert.False(doc.CanUndo);
            Assert.False(doc.CanRedo);

            // Let the run finish: the gate must release and the commands come back.
            gated.Proceed.Set();
            await exportTask;

            Assert.False(doc.IsBusy);
            Assert.True(doc.OpenFileCommand.CanExecute(null));
            Assert.True(doc.PasteFromClipboardCommand.CanExecute(null));

            // Undo re-enables once there is real history again (the run produced a working
            // result; a brush edit records it into the undo history).
            doc.OnResultStrokeStart(new WpfPoint(100, 75), 10);
            Assert.True(doc.UndoCommand.CanExecute(null));
            Assert.True(doc.CanUndo);

            doc.Dispose();
        });

    [Fact]
    public void SaveProject_WhileAnExportIsInFlight_IsDisabledAndTheDocumentStaysConsistent()
        => RunOnSta(async () =>
        {
            var gated = new ApplyGatedGrabCutStrategy();
            var doc = CreateDocument(gated);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { gated }, new GrabCutStrategy());
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");
            doc.SelectedStrategy = StrategyKind.GrabCut;
            doc.GrabCut.SelectedRect = new Rect(30, 20, 140, 110);

            var exportTask = doc.ExportCommand.ExecuteAsync(null);
            PumpUntil(() => gated.FullRunEntries >= 1, TimeSpan.FromSeconds(10));
            Assert.True(doc.IsBusy);

            // Saving while the export is in flight would read the live working Mats mid-run:
            // the gate keeps both save commands disabled.
            Assert.False(doc.SaveProjectCommand.CanExecute(null));
            Assert.False(doc.SaveProjectAsCommand.CanExecute(null));

            // Let the export finish: it completes, and saving becomes available again.
            gated.Proceed.Set();
            await exportTask;

            Assert.False(doc.IsBusy);
            Assert.True(doc.HasWorkingResult);
            Assert.DoesNotContain("failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.True(doc.SaveProjectCommand.CanExecute(null));

            doc.Dispose();
        });

    [Fact]
    public void SaveProject_WithLiveStateDisposedMidSave_CompletesUsingItsOwnCopies()
        => RunOnSta(async () =>
        {
            var saveService = new GatedRecordingProjectService();
            var doc = CreateDocument(projectService: saveService);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { new GrabCutStrategy() }, new GrabCutStrategy());
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");

            // Give the document a working result so the save has live BGR/alpha to persist
            // (ApplyToolResult takes ownership of these Mats).
            var bgr = new Mat(ImageHeight, ImageWidth, MatType.CV_8UC3, Scalar.All(120));
            var alpha = new Mat(ImageHeight, ImageWidth, MatType.CV_8UC1, Scalar.All(255));
            doc.ApplyToolResult(bgr, alpha, "Test edit");
            doc.ProjectPath = "test.ibrproj"; // skip the save dialog

            // Start the save and hold it inside the service (it encodes on a worker).
            var saveTask = doc.SaveProjectCommand.ExecuteAsync(null);
            PumpUntil(() => saveService.Entered.IsSet, TimeSpan.FromSeconds(10));
            Assert.True(doc.IsBusy); // the save now runs behind the busy gate
            Assert.False(doc.UndoCommand.CanExecute(null)); // ...so undo cannot race it

            // The user triggers undo / opens another image / closes the tab mid-save:
            // dispose the live working state. The in-flight save must keep using its own
            // copies -- previously it read the live Mats and hit the disposed ones.
            doc.Dispose();

            // While the save is still in flight, the Mats it received are independent copies
            // that survived the document's disposal (they are disposed only when the save
            // method exits).
            Assert.False(saveService.OriginalBgr!.IsDisposed);
            Assert.False(saveService.WorkingBgr!.IsDisposed);
            Assert.False(saveService.WorkingAlpha!.IsDisposed);
            Assert.Equal(ImageWidth * (long)ImageHeight, saveService.WorkingBgr.Total());

            saveService.Proceed.Set();
            await saveTask;
        });

    [Fact]
    public void Export_WithANewRectangleAndCtrlZMidRun_CompletesAndLeavesTheDocumentConsistent()
        => RunOnSta(async () =>
        {
            var gated = new ApplyGatedGrabCutStrategy();
            var doc = CreateDocument(gated);
            var shell = CreateShell(doc, new IBackgroundRemovalStrategy[] { gated }, new GrabCutStrategy());
            doc.SetShell(shell);
            shell.Documents.Add(doc);

            await doc.LoadImageAsync("subject.png");
            doc.SelectedStrategy = StrategyKind.GrabCut;
            doc.GrabCut.SelectedRect = new Rect(30, 20, 140, 110);
            doc.OriginalMode = InteractionMode.ScribbleForeground;
            doc.OnOriginalStrokeStart(new WpfPoint(100, 75));
            doc.OnOriginalStrokeEnd();
            Assert.True(doc.GrabCut.HasScribbles);

            // Start the full-res export and hold it inside the strategy (the gate blocks only
            // full-res runs, so the debounced preview below still runs through).
            var exportTask = doc.ExportCommand.ExecuteAsync(null);
            PumpUntil(() => gated.FullRunEntries >= 1, TimeSpan.FromSeconds(10));
            Assert.True(doc.IsBusy);

            // Ctrl+Z while the export is in flight is a no-op: the scribbles are still
            // undoable right now, so only the busy gate keeps Undo disabled -- without it the
            // command would answer true and let the user race the run.
            Assert.True(doc.ScribbleManager.CanUndo);
            Assert.False(doc.UndoCommand.CanExecute(null));
            Assert.False(doc.RedoCommand.CanExecute(null));

            // The user draws a new rectangle mid-export: this clears (disposes) the live
            // scribbles and fires a debounced preview. The in-flight export already captured
            // its own full-res scribble copies up front, so it must be unaffected, and the
            // preview must complete.
            var previewBefore = doc.ResultBitmap;
            doc.GrabCut.SelectedRect = new Rect(40, 30, 130, 100);
            PumpUntil(
                () => doc.ResultBitmap is not null && !ReferenceEquals(doc.ResultBitmap, previewBefore),
                TimeSpan.FromSeconds(10));
            Assert.DoesNotContain("failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);

            // Even a direct Execute call cannot reach the history: the scribbles were just
            // cleared and the export has not produced a working result yet.
            doc.UndoCommand.Execute(null);

            // Let the export finish: it completes with the selection it captured up front.
            gated.Proceed.Set();
            await exportTask;

            Assert.False(doc.IsBusy);
            Assert.True(doc.HasWorkingResult);
            Assert.DoesNotContain("Processing failed", doc.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("failed", doc.BusyMessage ?? "", StringComparison.OrdinalIgnoreCase);

            doc.Dispose();
        });

    /// <summary>Runs the async body on a dedicated STA thread, pumping the dispatcher while the
    /// body is incomplete so DispatcherTimers (the ViewModels' debounce) and dispatcher-marshaled
    /// continuations actually run -- the environment the app runs in.</summary>
    private static void RunOnSta(Func<Task> body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var task = body();
            while (!task.IsCompleted)
            {
                PumpFrame(TimeSpan.FromMilliseconds(10));
            }
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    /// <summary>Pumps the current thread's dispatcher until <paramref name="condition"/> is true
    /// (lets the debounce timer fire and its continuation run) or the timeout elapses.</summary>
    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
        {
            PumpFrame(TimeSpan.FromMilliseconds(20));
        }
    }

    private static void PumpFrame(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// Like <see cref="GatedGrabCutStrategy"/> but gates only full-res runs: previews pass
    /// through untouched, so a debounced preview can run while an apply is held mid-flight.
    /// </summary>
    private sealed class ApplyGatedGrabCutStrategy : IBackgroundRemovalStrategy
    {
        private readonly GrabCutStrategy _inner = new();
        private int _fullRunEntries;

        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Proceed { get; } = new(false);
        public int FullRunEntries => Volatile.Read(ref _fullRunEntries);

        public StrategyKind Kind => StrategyKind.GrabCut;

        public Task<RemovalResult> RunPreviewAsync(Mat bgr, StrategyContext context, CancellationToken ct)
            => _inner.RunPreviewAsync(bgr, context, ct);

        public Task<RemovalResult> RunFullAsync(Mat bgr, StrategyContext context, CancellationToken ct)
            => Task.Run(async () =>
            {
                Interlocked.Increment(ref _fullRunEntries);
                Entered.Set();
                Proceed.Wait(ct);
                return await _inner.RunFullAsync(bgr, context, ct);
            }, ct);
    }

    /// <summary>Captures the Mats a save hands over and holds the save at a gate, so a test
    /// can dispose the document's live state mid-save and prove the service received
    /// independent copies, not the live fields.</summary>
    private sealed class GatedRecordingProjectService : IProjectService
    {
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Proceed { get; } = new(false);

        public Mat? OriginalBgr { get; private set; }
        public Mat? OriginalAlpha { get; private set; }
        public Mat? WorkingBgr { get; private set; }
        public Mat? WorkingAlpha { get; private set; }

        public Task SaveAsync(
            string path,
            Mat originalBgr,
            Mat? originalAlpha,
            Mat? workingBgr,
            Mat? workingAlpha,
            ProjectDocument settings,
            CancellationToken ct = default)
            => Task.Run(() =>
            {
                OriginalBgr = originalBgr;
                OriginalAlpha = originalAlpha;
                WorkingBgr = workingBgr;
                WorkingAlpha = workingAlpha;
                Entered.Set();
                Proceed.Wait(ct);
            }, ct);

        public Task<LoadedProject> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedProject
            {
                Settings = new ProjectDocument(),
                OriginalBgr = new Mat(1, 1, MatType.CV_8UC3)
            });
    }

    /// <summary>Records Error calls so tests can assert that a cancelled apply was not
    /// reported as a failure.</summary>
    private sealed class RecordingFileLogService : IFileLogService
    {
        public List<string> Errors { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) => Errors.Add(message);
    }

    /// <summary>
    /// Wraps a real GrabCutStrategy with a gate so the test can pause a run mid-flight and
    /// race a UI-thread scribble clear against it. The actual computation still runs through
    /// the real strategy.
    /// </summary>
    private sealed class GatedGrabCutStrategy : IBackgroundRemovalStrategy
    {
        private readonly GrabCutStrategy _inner = new();

        /// <summary>Set as soon as a run reaches the gate (inside the background thread).</summary>
        public ManualResetEventSlim Entered { get; } = new(false);

        /// <summary>Set by the test to let the gated run proceed.</summary>
        public ManualResetEventSlim Proceed { get; } = new(false);

        public StrategyKind Kind => StrategyKind.GrabCut;

        public Task<RemovalResult> RunPreviewAsync(Mat bgr, StrategyContext context, CancellationToken ct)
            => RunGated(bgr, context, ct);

        public Task<RemovalResult> RunFullAsync(Mat bgr, StrategyContext context, CancellationToken ct)
            => RunGated(bgr, context, ct);

        private Task<RemovalResult> RunGated(Mat bgr, StrategyContext context, CancellationToken ct)
            => Task.Run(async () =>
            {
                Entered.Set();
                Proceed.Wait(ct);
                return await _inner.RunPreviewAsync(bgr, context, ct);
            }, ct);
    }

    // ---- fakes (same pattern as the other ViewModel test files) ----

    private static DocumentViewModel CreateDocument(
        IBackgroundRemovalStrategy? strategy = null,
        IFileLogService? log = null,
        IProjectService? projectService = null)
    {
        var grabCut = strategy as GrabCutStrategy ?? new GrabCutStrategy();
        log ??= new FakeFileLogService();
        return new DocumentViewModel(
            new SubjectImageLoader(),
            new FakeImageExportService(),
            new FakeDownscaleService(),
            new FakeDialogService(),
            new FakeBatchProcessingService(),
            new FakeSettingsService(),
            projectService ?? new FakeProjectService(),
            log,
            strategy is null ? new IBackgroundRemovalStrategy[] { grabCut } : new[] { strategy },
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), log)),
            grabCut,
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService());
    }

    private static ShellViewModel CreateShell(DocumentViewModel doc, IEnumerable<IBackgroundRemovalStrategy> strategies, GrabCutStrategy grabCut, IFileLogService? log = null)
    {
        var settings = new FakeSettingsService();
        log ??= new FakeFileLogService();
        return new ShellViewModel(
            () => doc,
            () => throw new InvalidOperationException("Uncrop factory not needed"),
            new FakeDialogService(),
            settings,
            new FakeDownscaleService(),
            log,
            strategies,
            new OnnxStrategy(new OnnxInferenceEngine(new FakeModelCacheService(), log)),
            grabCut,
            new SamStrategy(new SamInferenceEngine(new FakeModelCacheService())),
            new FakeUncropFillService(),
            new SubjectImageLoader(),
            new FakeImageExportService());
    }

    private sealed class SubjectImageLoader : IImageLoaderService
    {
        private static Mat MakeSubjectImage()
        {
            var bgr = new Mat(ImageHeight, ImageWidth, MatType.CV_8UC3, Scalar.All(20));
            using var roi = new Mat(bgr, new Rect(40, 30, 120, 90));
            roi.SetTo(new Scalar(220, 210, 200));
            return bgr;
        }

        public Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(path, MakeSubjectImage()));

        public Task<LoadedImage> LoadFromBytesAsync(byte[] imageBytes, string sourceName = "pasted_image.png", CancellationToken ct = default)
            => Task.FromResult(new LoadedImage(sourceName, new Mat(1, 1, MatType.CV_8UC3)));

        public Task<LoadedImage> LoadFromBitmapSourceAsync(System.Windows.Media.Imaging.BitmapSource bitmapSource, string sourceName = "clipboard_image.png")
            => Task.FromResult(new LoadedImage(sourceName, new Mat(1, 1, MatType.CV_8UC3)));
    }

}
