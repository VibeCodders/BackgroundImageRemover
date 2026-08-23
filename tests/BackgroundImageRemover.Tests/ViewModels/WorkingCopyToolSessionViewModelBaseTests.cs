using BackgroundImageRemover.Tests.Helpers;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Pins the shared template hosted by <see cref="WorkingCopyToolSessionViewModelBase"/>
/// (used by Heal and Retouch): the preview is rebuilt from the independent BGR working copy,
/// and Apply pushes that working-copy result back into the parent document.
/// </summary>
public class WorkingCopyToolSessionViewModelBaseTests
{
    private const int Width = 40;
    private const int Height = 40;

    [Fact]
    public async Task Constructor_RefreshesPreviewFromWorkingCopy()
    {
        using var vm = await CreateToolAsync();

        Assert.NotNull(vm.ResultBitmap);
        // Solid-color source BGR (10,20,30) shows up as BGRA with opaque alpha.
        Assert.Equal(new Vec4b(10, 20, 30, 255), PreviewPixel(vm, 0, 0));
    }

    [Fact]
    public async Task RefreshResult_RebuildsPreviewFromWorkingCopy()
    {
        using var vm = await CreateToolAsync();

        // Mutating the working copy does not touch the preview until RefreshResult runs.
        vm.SetWorkingPixel(3, 5, new Vec3b(200, 100, 50));
        Assert.Equal(new Vec4b(10, 20, 30, 255), PreviewPixel(vm, 3, 5));

        vm.PublicRefresh();
        Assert.Equal(new Vec4b(200, 100, 50, 255), PreviewPixel(vm, 3, 5));
    }

    [Fact]
    public async Task BuildResult_ReflectsWorkingCopy_NotSource()
    {
        using var vm = await CreateToolAsync();
        vm.SetWorkingPixel(0, 0, new Vec3b(200, 100, 50));

        using var result = vm.PublicBuildResult();
        Assert.Equal(new Vec3b(200, 100, 50), result.Get<Vec3b>(0, 0));

        // The parent document source is untouched by the working-copy edit.
        using var snapshot = vm.ParentDocument.CreateCurrentStateSnapshot();
        Assert.Equal(new Vec3b(10, 20, 30), snapshot.FullBgr.Get<Vec3b>(0, 0));
    }

    [Fact]
    public async Task ApplyAsync_PushesWorkingCopyResultToParent()
    {
        var (doc, shell) = await CreateDocumentAsync();
        var vm = new TestWorkingCopyTool(shell, doc);
        vm.SetWorkingPixel(0, 0, new Vec3b(200, 100, 50));

        await vm.ApplyAsync();

        Assert.True(doc.HasWorkingResult);
        Assert.Equal(Width, doc.ImageWidth);
        Assert.Equal(Height, doc.ImageHeight);
        Assert.Contains(doc.EditSteps, s => s.Name == "Test" && !s.IsUndone);

        // The applied result carries the working-copy edit.
        using var snapshot = doc.CreateCurrentStateSnapshot();
        Assert.Equal(new Vec3b(200, 100, 50), snapshot.FullBgr.Get<Vec3b>(0, 0));
    }

    [Fact]
    public async Task Dispose_DoesNotThrow()
    {
        using var vm = await CreateToolAsync();
        vm.Dispose(); // disposes the working copy and the source/alpha state without throwing
    }

    // ---- helpers ----

    private static async Task<TestWorkingCopyTool> CreateToolAsync()
    {
        var (doc, shell) = await CreateDocumentAsync();
        return new TestWorkingCopyTool(shell, doc);
    }

    private static async Task<(DocumentViewModel Doc, FakeShell Shell)> CreateDocumentAsync()
    {
        var (doc, shell) = TestDoubles.CreateDocumentAndShell(Width, Height);
        await doc.LoadImageAsync("photo.jpg");
        return (doc, shell);
    }

    private static Vec4b PreviewPixel(TestWorkingCopyTool vm, int x, int y)
    {
        using var bgra = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(vm.ResultBitmap!);
        return bgra.Get<Vec4b>(y, x);
    }

    /// <summary>
    /// Concrete working-copy tool whose result is the working copy itself: any edit made to
    /// <c>_workingBgr</c> must show up in both the preview and the applied result, mirroring
    /// Heal/Retouch (where <see cref="WorkingCopyToolSessionViewModelBase.BuildResult"/> chains
    /// effect operations on top of the same working copy).
    /// </summary>
    private sealed class TestWorkingCopyTool : WorkingCopyToolSessionViewModelBase
    {
        public TestWorkingCopyTool(ShellViewModel shell, DocumentViewModel parentDocument)
            : base(shell, parentDocument)
        {
            InitSourceAlpha();
            _workingBgr = CloneWorkingBgr();
            RefreshResult();
        }

        public override string ToolBadge => "Test";
        public override string AccentColor => "#000000";

        protected override Mat BuildResult() => _workingBgr!.Clone();

        public override Task ApplyAsync()
        {
            ApplyAndClose(BuildResult(), "Test");
            return Task.CompletedTask;
        }

        public void SetWorkingPixel(int x, int y, Vec3b color) => _workingBgr!.Set(y, x, color);

        public Mat PublicBuildResult() => BuildResult();
        public void PublicRefresh() => RefreshResult();
    }
}
