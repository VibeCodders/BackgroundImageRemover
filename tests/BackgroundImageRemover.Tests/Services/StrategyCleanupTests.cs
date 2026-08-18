using BackgroundImageRemover.Helpers;
using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

public class StrategyCleanupTests
{
    private sealed class FixedMaskStrategy : StrategyBase
    {
        private readonly Mat _mask;
        public FixedMaskStrategy(Mat mask) => _mask = mask.Clone();
        public override StrategyKind Kind => StrategyKind.Otsu;
        protected override Mat ComputeMask(Mat bgr, StrategyContext context, CancellationToken ct) => _mask.Clone();
    }

    private static Mat RunMask(Mat mask, StrategyContext context)
    {
        using var bgr = new Mat(mask.Size(), MatType.CV_8UC3, new Scalar(0, 0, 0));
        var strategy = new FixedMaskStrategy(mask);
        using var result = strategy.RunFullAsync(bgr, context, CancellationToken.None).GetAwaiter().GetResult();
        return result.Bgra.ExtractAlphaChannel();
    }

    [Fact]
    public void Despeckle_RemovesIsolatedForegroundSpeck()
    {
        using var mask = new Mat(21, 21, MatType.CV_8UC1, Scalar.All(0));
        using (var block = new Mat(mask, new Rect(6, 6, 10, 10)))
        {
            block.SetTo(Scalar.All(255));
        }
        mask.Set(0, 0, (byte)255); // isolated speck

        using var alpha = RunMask(mask, new StrategyContext { DespeckleKernelSize = 5, DecontaminateEdges = false });

        Assert.Equal(0, alpha.Get<byte>(0, 0)); // speck removed
        Assert.Equal(255, alpha.Get<byte>(10, 10)); // large block preserved
    }

    [Fact]
    public void FillHoles_FillsBackgroundHole()
    {
        using var mask = new Mat(21, 21, MatType.CV_8UC1, Scalar.All(255));
        mask.Set(10, 10, (byte)0); // a background hole

        using var alpha = RunMask(mask, new StrategyContext { FillHolesKernelSize = 5, DecontaminateEdges = false });

        Assert.Equal(255, alpha.Get<byte>(10, 10)); // hole filled
    }

    [Fact]
    public void KeepLargestComponent_DropsStrayIslands()
    {
        using var mask = new Mat(21, 21, MatType.CV_8UC1, Scalar.All(0));
        using (var big = new Mat(mask, new Rect(5, 5, 8, 8)))
        {
            big.SetTo(Scalar.All(255));
        }
        using (var small = new Mat(mask, new Rect(15, 15, 2, 2)))
        {
            small.SetTo(Scalar.All(255));
        }

        using var alpha = RunMask(mask, new StrategyContext { KeepLargestComponent = true, DecontaminateEdges = false });

        Assert.Equal(255, alpha.Get<byte>(9, 9)); // largest component preserved
        Assert.Equal(0, alpha.Get<byte>(16, 16)); // stray island removed
    }

    [Fact]
    public void SmoothEdges_PreservesSolidForeground()
    {
        using var mask = new Mat(21, 21, MatType.CV_8UC1, Scalar.All(255));

        using var alpha = RunMask(mask, new StrategyContext { SmoothEdgesKernelSize = 3, DecontaminateEdges = false });

        Assert.Equal(255, alpha.Get<byte>(10, 10));
    }
}
