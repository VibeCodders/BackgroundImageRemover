using System.Windows;
using BackgroundImageRemover.Services.Localization;
using BackgroundImageRemover.Services.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackgroundImageRemover.Views;

/// <summary>Bindable state of the preferences dialog.</summary>
public sealed partial class PreferencesViewModel : ObservableObject
{
    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private AppLanguage _language;

    [ObservableProperty]
    private bool _reopenLastSession;

    /// <summary>0 = System, 1 = English, 2 = Italiano (kept in sync with the ComboBox items).</summary>
    public int LanguageIndex
    {
        get => Language switch
        {
            AppLanguage.English => 1,
            AppLanguage.Italian => 2,
            _ => 0
        };
        set => Language = value switch
        {
            1 => AppLanguage.English,
            2 => AppLanguage.Italian,
            _ => AppLanguage.System
        };
    }

    public void LoadFrom(AppSettings settings)
    {
        Theme = settings.Theme;
        Language = settings.Language;
        ReopenLastSession = settings.ReopenLastSession;
        OnPropertyChanged(nameof(LanguageIndex));
    }

    public void SaveTo(AppSettings settings)
    {
        settings.Theme = Theme;
        settings.Language = Language;
        settings.ReopenLastSession = ReopenLastSession;
    }
}

/// <summary>Compact dialog for theme, language and startup behavior.</summary>
public sealed partial class PreferencesWindow : Window
{
    private readonly ISettingsService _settings;
    public PreferencesViewModel ViewModel { get; } = new();

    public PreferencesWindow(ISettingsService settings)
    {
        _settings = settings;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.LoadFrom(_settings.Current);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveTo(_settings.Current);
        _settings.Save();

        ThemeManager.Apply(_settings.Current.Theme);
        LocalizationService.Instance.Language = _settings.Current.Language;

        DialogResult = true;
    }
}
