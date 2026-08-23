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
using BackgroundImageRemover.Tests.Helpers;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Pins the shared tool-session helpers extracted during the ViewModel dedup: the BGRA
/// split-and-apply lifecycle (<see cref="ToolSessionViewModelBase.ApplyBgra"/> and
/// <c>ApplyBgraAndClose</c>), the working-copy ownership helpers on
/// <see cref="BgraToolSessionViewModelBase"/> (<c>ReplaceWorkingBgra</c> / <c>ResetWorkingBgra</c>)
/// and the source-clone helper (<c>CloneSourceWorkingBgr</c>).
/// </summary>
public class ToolSessionBaseHelpersTests
{
    private const int Width = 40;
    private const int Height = 40;

    [Fact]
    public async Task ApplyBgra_SplitsBgraAppliesToParent_AndLeavesTabOpen()
    {
        var (doc, shell) = await CreateToolAsync();
        using var vm = new TestBgraTool(shell, doc);

        using var bgra = new Mat(Height, Width, MatType.CV_8UC4, new Scalar(60, 120, 180, 128));
        vm.PublicApplyBgra(bgra, "Test");

        // Both split Mats land in the parent document as an undoable edit.
        Assert.True(doc.HasWorkingResult);
        Assert.Contains(doc.EditSteps, s => s.Name == "Test" && !s.IsUndone);

        // The BGR content comes from the BGRA's own pixels...
        using var snapshot = doc.CreateCurrentStateSnapshot();
        Assert.Equal(new Vec3b(60, 120, 180), snapshot.FullBgr.Get<Vec3b>(0, 0));
        // ...and the alpha comes from the BGRA's alpha channel, not the document's (opaque) one.
        Assert.NotNull(snapshot.FullAlpha);
        Assert.Equal(128, snapshot.FullAlpha!.Get<byte>(0, 0));

        // ApplyBgra alone must not close the tab.
        Assert.Null(shell.LastClosedTab);
        Assert.Equal(0, shell.CloseCount);
    }

    [Fact]
    public async Task ApplyBgraAndClose_SplitsAppliesAndClosesTab()
    {
        var (doc, shell) = await CreateToolAsync();
        using var vm = new TestBgraTool(shell, doc);

        using var bgra = new Mat(Height, Width, MatType.CV_8UC4, new Scalar(60, 120, 180, 128));
        vm.PublicApplyBgraAndClose(bgra, "Test");

        Assert.True(doc.HasWorkingResult);
        Assert.Same(vm, shell.LastClosedTab);
        Assert.Equal(1, shell.CloseCount);

        using var snapshot = doc.CreateCurrentStateSnapshot();
        Assert.Equal(new Vec3b(60, 120, 180), snapshot.FullBgr.Get<Vec3b>(0, 0));
        Assert.Equal(128, snapshot.FullAlpha!.Get<byte>(0, 0));
    }

    [Fact]
    public async Task ReplaceWorkingBgra_TakesOwnershipAndDisposesPrevious()
    {
        var (doc, shell) = await CreateToolAsync();
        using var vm = new TestBgraTool(shell, doc);
        var previous = vm.PublicWorkingBgra!;

        var replacement = new Mat(Height, Width, MatType.CV_8UC4, new Scalar(5, 25, 45, 255));
        vm.PublicReplaceWorkingBgra(replacement);

        // The same instance becomes the working copy (no defensive clone)...
        Assert.Same(replacement, vm.PublicWorkingBgra);
        // ...the previous copy is released by the helper...
        Assert.True(previous.IsDisposed);
        // ...and the VM owns the replacement until its own Dispose.
        Assert.False(replacement.IsDisposed);
    }

    [Fact]
    public async Task ResetWorkingBgra_RestoresPristineSourceFusion()
    {
        var (doc, shell) = await CreateToolAsync();
        using var vm = new TestBgraTool(shell, doc);

        var before = vm.PublicWorkingBgra!;
        before.Set(0, 0, new Vec4b(200, 100, 50, 255)); // simulate a warp/transform edit

        vm.PublicResetWorkingBgra();

        var after = vm.PublicWorkingBgra!;
        Assert.NotSame(before, after);
        Assert.True(before.IsDisposed);
        // Back to the pristine source BGR (10,20,30) fused with the opaque working alpha.
        Assert.Equal(new Vec4b(10, 20, 30, 255), after.Get<Vec4b>(0, 0));
    }

    [Fact]
    public async Task CloneSourceWorkingBgr_CapturesIndependentClone()
    {
        var (doc, shell) = await CreateToolAsync();
        using var vm = new TestBgraTool(shell, doc);

        using var clone = vm.PublicCloneSourceWorkingBgr();
        // The helper initialized the snapshot + working alpha and cloned the full-resolution BGR.
        Assert.True(vm.PublicEnsureSourceAlpha());
        Assert.Equal(new Vec3b(10, 20, 30), clone.Get<Vec3b>(0, 0));

        // The clone is independent: mutating it never touches the parent document.
        clone.Set(0, 0, new Vec3b(200, 100, 50));
        using var snapshot = doc.CreateCurrentStateSnapshot();
        Assert.Equal(new Vec3b(10, 20, 30), snapshot.FullBgr.Get<Vec3b>(0, 0));
    }

    // ---- helpers ----

    private static async Task<(DocumentViewModel Doc, RecordingShell Shell)> CreateToolAsync()
    {
        var loader = new TestImageLoader(Width, Height);
        var log = new FakeFileLogService();
        var dialogs = new FakeDialogService();
        var settings = new FakeSettingsService();
        var downscaler = new FakeDownscaleService();
        var exporter = new FakeImageExportService();
        var modelCache = new FakeModelCacheService();

        var shell = new RecordingShell(loader, dialogs, settings, downscaler, log, exporter, modelCache);
        var doc = new DocumentViewModel(
            loader,
            exporter,
            downscaler,
            dialogs,
            new FakeBatchProcessingService(),
            settings,
            new FakeProjectService(),
            log,
            Array.Empty<IBackgroundRemovalStrategy>(),
            new OnnxStrategy(new OnnxInferenceEngine(modelCache, log)),
            new GrabCutStrategy(),
            new SamStrategy(new SamInferenceEngine(modelCache)),
            new FakeUncropFillService());

        await doc.LoadImageAsync("photo.jpg");
        return (doc, shell);
    }

    /// <summary>
    /// Concrete BGRA tool that exposes the protected lifecycle helpers, mirroring the
    /// Transform/Liquify/Rotate/Crop tools' <c>InitWorkingBgra</c> startup.
    /// </summary>
    private sealed class TestBgraTool : BgraToolSessionViewModelBase
    {
        public TestBgraTool(ShellViewModel shell, DocumentViewModel parentDocument)
            : base(shell, parentDocument)
        {
            InitWorkingBgra();
        }

        public override string ToolBadge => "Test";
        public override string AccentColor => "#000000";

        public override Task ApplyAsync() => Task.CompletedTask;

        public Mat? PublicWorkingBgra => WorkingBgra;
        public void PublicReplaceWorkingBgra(Mat newBgra) => ReplaceWorkingBgra(newBgra);
        public void PublicResetWorkingBgra() => ResetWorkingBgra();
        public Mat PublicCloneSourceWorkingBgr() => CloneSourceWorkingBgr();
        public bool PublicEnsureSourceAlpha() => EnsureSourceAlpha();
        public void PublicApplyBgra(Mat bgra, string operationName) => ApplyBgra(bgra, operationName);
        public void PublicApplyBgraAndClose(Mat bgra, string operationName) => ApplyBgraAndClose(bgra, operationName);
    }

    /// <summary>Shell that records every tab closed via <c>CloseTabDirect</c>.</summary>
    private sealed class RecordingShell : FakeShell
    {
        public IToolSessionTab? LastClosedTab { get; private set; }
        public int CloseCount { get; private set; }

        public RecordingShell(
            IImageLoaderService imageLoader,
            IDialogService dialogs,
            ISettingsService settings,
            IDownscaleService downscaler,
            IFileLogService log,
            IImageExportService imageExporter,
            IModelCacheService modelCache)
            : base(imageLoader, dialogs, settings, downscaler, log, imageExporter, modelCache)
        {
        }

        public override void CloseTabDirect(IToolSessionTab toolTab)
        {
            LastClosedTab = toolTab;
            CloseCount++;
            base.CloseTabDirect(toolTab);
        }
    }
}
