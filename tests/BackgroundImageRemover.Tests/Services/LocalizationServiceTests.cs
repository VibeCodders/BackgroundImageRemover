using BackgroundImageRemover.Services.Localization;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.Views;

namespace BackgroundImageRemover.Tests.Services;

public class LocalizationServiceTests
{
    [Fact]
    public void English_ReturnsMenuLabels()
    {
        LocalizationService.Instance.Language = AppLanguage.English;

        Assert.Equal("_File", LocalizationService.Instance["File"]);
        Assert.Equal("_Open image...", LocalizationService.Instance["OpenImage"]);
        Assert.Equal("Export _JPG (no crop)", LocalizationService.Instance["ExportJpg"]);
    }

    [Fact]
    public void Italian_ReturnsTranslatedLabels()
    {
        LocalizationService.Instance.Language = AppLanguage.Italian;

        Assert.Equal("_File", LocalizationService.Instance["File"]);
        Assert.Equal("_Apri immagine...", LocalizationService.Instance["OpenImage"]);
        Assert.Equal("_Preferenze...", LocalizationService.Instance["Preferences"]);
        Assert.Equal("_Annulla", LocalizationService.Instance["Undo"]);
    }

    [Fact]
    public void UnknownKey_FallsBackToKey()
    {
        LocalizationService.Instance.Language = AppLanguage.English;

        Assert.Equal("NoSuchKey", LocalizationService.Instance["NoSuchKey"]);
    }
}

public class PreferencesViewModelTests
{
    [Fact]
    public void NewSettings_DefaultToSystemThemeAndLanguage()
    {
        var settings = new AppSettings();
        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(AppLanguage.System, settings.Language);
        Assert.False(settings.ReopenLastSession);
    }

    [Fact]
    public void ViewModel_RoundTripsSettings()
    {
        var source = new AppSettings
        {
            Theme = AppTheme.Dark,
            Language = AppLanguage.Italian,
            ReopenLastSession = true
        };

        var vm = new PreferencesViewModel();
        vm.LoadFrom(source);

        Assert.Equal(AppTheme.Dark, vm.Theme);
        Assert.Equal(2, vm.LanguageIndex);
        Assert.True(vm.ReopenLastSession);

        var target = new AppSettings();
        vm.SaveTo(target);

        Assert.Equal(AppTheme.Dark, target.Theme);
        Assert.Equal(AppLanguage.Italian, target.Language);
        Assert.True(target.ReopenLastSession);
    }

    [Fact]
    public void LanguageIndex_MapsToAndFromLanguage()
    {
        var vm = new PreferencesViewModel { LanguageIndex = 1 };
        Assert.Equal(AppLanguage.English, vm.Language);

        vm.LanguageIndex = 2;
        Assert.Equal(AppLanguage.Italian, vm.Language);

        vm.LanguageIndex = 0;
        Assert.Equal(AppLanguage.System, vm.Language);
    }
}
