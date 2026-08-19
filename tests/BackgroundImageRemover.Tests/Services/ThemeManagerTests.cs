using BackgroundImageRemover.Services.Settings;

namespace BackgroundImageRemover.Tests.Services;

public class ThemeManagerTests
{
    [Theory]
    [InlineData(AppTheme.System, AppTheme.Light, true, false)]   // Windows light, effective light -> nothing to do
    [InlineData(AppTheme.System, AppTheme.Light, false, true)]   // Windows switched to dark -> reapply
    [InlineData(AppTheme.System, AppTheme.Dark, true, true)]     // Windows switched to light -> reapply
    [InlineData(AppTheme.System, AppTheme.Dark, false, false)]   // Windows dark, effective dark -> nothing to do
    [InlineData(AppTheme.Light, AppTheme.Light, false, false)]   // explicit theme: never follow Windows
    [InlineData(AppTheme.Light, AppTheme.Dark, true, false)]
    [InlineData(AppTheme.Dark, AppTheme.Light, false, false)]
    public void SystemThemeChanged_OnlyTracksWindowsForSystemSetting(
        AppTheme currentSetting, AppTheme lastEffective, bool windowsUsesLight, bool expected)
    {
        Assert.Equal(expected, ThemeManager.SystemThemeChanged(currentSetting, lastEffective, windowsUsesLight));
    }
}
