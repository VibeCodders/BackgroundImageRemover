using BackgroundImageRemover.Tests.Helpers;
using BackgroundImageRemover.ViewModels;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;
using Xunit;

namespace BackgroundImageRemover.Tests.ViewModels;

/// <summary>
/// Pins the unified mask-tool semantics hosted by <see cref="MaskToolSessionViewModelBase"/>:
/// the effect is applied inside the painted mask while the rest of the image stays untouched,
/// WholeImage applies it everywhere, and with no flags the image is returned unchanged.
/// </summary>
public class MaskToolPaintModeTests
{
    private const int Width = 40;
    private const int Height = 40;

    [Fact]
    public async Task NoFlags_ReturnsUnchangedClone()
    {
        using var vm = await CreateToolAsync();
        using var src = CreateSource();
        using var result = vm.PublicBuildResult(src);

        ServiceTestHelper.AssertNoChange(src, result);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task PaintMode_WithoutPaintedMask_ReturnsUnchangedClone()
    {
        using var vm = await CreateToolAsync();
        vm.PaintMode = true;

        using var src = CreateSource();
        using var result = vm.PublicBuildResult(src);

        ServiceTestHelper.AssertNoChange(src, result);
    }

    [Fact]
    public async Task WholeImage_AppliesEffectToEveryPixel()
    {
        using var vm = await CreateToolAsync();
        vm.WholeImage = true;

        using var src = CreateSource();
        using var result = vm.PublicBuildResult(src);

        // The test tool inverts the image: every pixel must be flipped.
        Assert.Equal(new Vec3b(245, 235, 225), result.Get<Vec3b>(0, 0));
        Assert.Equal(new Vec3b(245, 235, 225), result.Get<Vec3b>(Height - 1, Width - 1));
        ServiceTestHelper.AssertChangesPixels(src, result);
    }

    [Fact]
    public async Task PaintMode_AppliesEffectInsideMask_LeavesOutsideUntouched()
    {
        using var vm = await CreateToolAsync();

        // Paint a filled circle of radius 3 centered at (10, 10).
        vm.OnStrokeStart(new WpfPoint(10, 10), 3);
        vm.OnStrokeEnd();
        Assert.True(vm.HasPaintedMask);

        vm.PaintMode = true;
        vm.WholeImage = false;

        using var src = CreateSource();
        using var result = vm.PublicBuildResult(src);

        // Inside the painted circle the effect is applied (image inverted).
        Assert.Equal(new Vec3b(245, 235, 225), result.Get<Vec3b>(10, 10));
        // Far outside the painted circle the original pixel is untouched.
        Assert.Equal(new Vec3b(10, 20, 30), result.Get<Vec3b>(35, 35));
    }

    [Fact]
    public async Task Stroke_ClearsAndRepaintsMask()
    {
        using var vm = await CreateToolAsync();

        vm.OnStrokeStart(new WpfPoint(5, 5), 2);
        vm.OnStrokeEnd();
        Assert.True(vm.HasPaintedMask);

        // The Reset command clears the mask and disables whole-image / paint mode.
        vm.ResetCommand.Execute(null);

        Assert.False(vm.HasPaintedMask);
        Assert.False(vm.WholeImage);
        Assert.False(vm.PaintMode);

        using var src = CreateSource();
        using var result = vm.PublicBuildResult(src);
        ServiceTestHelper.AssertNoChange(src, result);
    }

    [Fact]
    public async Task RefreshResult_SetsIsDirtyOnlyWhenEffectActive()
    {
        using var vm = await CreateToolAsync();

        vm.PublicRefresh(); // nothing painted, no flags
        Assert.False(vm.IsDirty);

        vm.OnStrokeStart(new WpfPoint(10, 10), 3);
        vm.OnStrokeEnd();
        vm.PaintMode = true;
        vm.PublicRefresh(); // painted mask + paint mode -> effect active
        Assert.True(vm.IsDirty);

        vm.WholeImage = true;
        vm.PublicRefresh(); // whole-image also counts as effect active
        Assert.True(vm.IsDirty);
    }

    // ---- helpers ----

    private static async Task<TestMaskTool> CreateToolAsync()
    {
        var (doc, shell) = TestDoubles.CreateDocumentAndShell(Width, Height);
        await doc.LoadImageAsync("photo.jpg");
        return new TestMaskTool(shell, doc);
    }

    private static Mat CreateSource() => new(Height, Width, MatType.CV_8UC3, new Scalar(10, 20, 30));

    /// <summary>
    /// Concrete mask tool whose effect is a deterministic bitwise invert, exposing the
    /// protected <see cref="MaskToolSessionViewModelBase.BuildResult"/> /
    /// <see cref="MaskToolSessionViewModelBase.RefreshResult"/> for assertions.
    /// </summary>
    private sealed class TestMaskTool : MaskToolSessionViewModelBase
    {
        public TestMaskTool(ShellViewModel shell, DocumentViewModel parentDocument)
            : base(shell, parentDocument)
        {
            InitMask();
        }

        public override string ToolBadge => "Test";
        public override string AccentColor => "#000000";
        protected override string OperationName => "Test";

        protected override Mat ApplyEffect(Mat src)
        {
            var result = new Mat();
            Cv2.BitwiseNot(src, result);
            return result;
        }

        public Mat PublicBuildResult(Mat src) => BuildResult(src);
        public void PublicRefresh() => RefreshResult();
    }
}
