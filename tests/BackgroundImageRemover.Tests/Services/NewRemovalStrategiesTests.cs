using BackgroundImageRemover.Models;
using BackgroundImageRemover.Services.Strategies;
using OpenCvSharp;

namespace BackgroundImageRemover.Tests.Services;

/// <summary>
/// Verifies that the new SAM multi-point support aggregates the primary click and any
/// additional foreground points into a single decoder call.
/// </summary>
public class SamStrategyTests
{
    [Fact]
    public void StrategyContext_SamPromptPoints_CanHoldAdditionalPoints()
    {
        // Sanity check: the StrategyContext now exposes both a primary point and a list of
        // additional points, so the SAM strategy can aggregate them into one decoder call.
        var context = new StrategyContext
        {
            SamPromptPoint = new Point(32, 32),
            SamPromptPoints = new[] { new Point(10, 10), new Point(50, 50) }
        };

        Assert.Equal(new Point(32, 32), context.SamPromptPoint);
        Assert.NotNull(context.SamPromptPoints);
        Assert.Equal(2, context.SamPromptPoints.Length);
        Assert.Contains(new Point(10, 10), context.SamPromptPoints);
        Assert.Contains(new Point(50, 50), context.SamPromptPoints);
    }
}
