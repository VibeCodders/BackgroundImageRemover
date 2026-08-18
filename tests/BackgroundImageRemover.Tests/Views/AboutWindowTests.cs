using BackgroundImageRemover.Views;

namespace BackgroundImageRemover.Tests.Views;

public class AboutWindowTests
{
    [Fact]
    public void DisplayVersion_ReadsTheRealProductVersion()
    {
        var version = AboutWindow.DisplayVersion;

        // The assembly version is pinned and never bumped, so the About dialog must read the
        // <Version> property instead (e.g. "1.22.0"), otherwise it would show a stale number.
        Assert.Matches(@"^\d+\.\d+\.\d+", version);
        Assert.False(version.Contains('+'), $"expected the SourceLink hash to be stripped, got {version}");
    }
}
