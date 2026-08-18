using System.ComponentModel;
using System.Globalization;
using System.Windows;
using BackgroundImageRemover.Services.Settings;

namespace BackgroundImageRemover.Services.Localization;

/// <summary>
/// Lightweight key-based localization for the app chrome (menus, titles). Labels are looked up
/// by key in the current language's dictionary, falling back to the key itself when missing.
/// Exposed as a singleton so XAML can bind via <c>{Binding [Key], Source={x:Static services:LocalizationService.Instance}}</c>
/// and the labels update live when the language changes.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["File"] = "_File",
        ["Edit"] = "_Edit",
        ["Help"] = "_Help",
        ["OpenImage"] = "_Open image...",
        ["OpenNewTab"] = "Open in _new tab...",
        ["OpenProject"] = "Open _project...",
        ["DuplicateTab"] = "_Duplicate tab",
        ["ShowInExplorer"] = "Show _in Explorer",
        ["SaveProject"] = "_Save project",
        ["SaveProjectAs"] = "Save project _as...",
        ["Recent"] = "_Recent",
        ["RecentProjects"] = "Recent _projects",
        ["ClearRecentFiles"] = "Clear recent files",
        ["ClearRecentProjects"] = "Clear recent projects",
        ["Undo"] = "_Undo",
        ["Redo"] = "_Redo",
        ["PasteImage"] = "_Paste image",
        ["CopyCutout"] = "_Copy cutout",
        ["CopyFilePath"] = "Copy _file path",
        ["ExportPng"] = "_Export PNG (no crop)",
        ["ExportJpg"] = "Export _JPG (no crop)",
        ["Preferences"] = "_Preferences...",
        ["About"] = "_About Background Image Remover",
        ["PreferencesTitle"] = "Preferences"
    };

    private static readonly IReadOnlyDictionary<string, string> Italian = new Dictionary<string, string>
    {
        ["File"] = "_File",
        ["Edit"] = "_Modifica",
        ["Help"] = "_Aiuto",
        ["OpenImage"] = "_Apri immagine...",
        ["OpenNewTab"] = "Apri in una _nuova scheda...",
        ["OpenProject"] = "_Apri progetto...",
        ["DuplicateTab"] = "_Duplica scheda",
        ["ShowInExplorer"] = "Mostra in E_splorer",
        ["SaveProject"] = "_Salva progetto",
        ["SaveProjectAs"] = "Salva progetto _con nome...",
        ["Recent"] = "_Recenti",
        ["RecentProjects"] = "Progetti _recenti",
        ["ClearRecentFiles"] = "Cancella file recenti",
        ["ClearRecentProjects"] = "Cancella progetti recenti",
        ["Undo"] = "_Annulla",
        ["Redo"] = "_Ripeti",
        ["PasteImage"] = "_Incolla immagine",
        ["CopyCutout"] = "_Copia ritaglio",
        ["CopyFilePath"] = "Copia _percorso file",
        ["ExportPng"] = "_Esporta PNG (senza crop)",
        ["ExportJpg"] = "Esporta _JPG (senza crop)",
        ["Preferences"] = "_Preferenze...",
        ["About"] = "_Informazioni su Background Image Remover",
        ["PreferencesTitle"] = "Preferenze"
    };

    private AppLanguage _language = AppLanguage.System;

    /// <summary>Current language (System resolves to the OS display language at read time).</summary>
    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Language"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        }
    }

    public string this[string key]
    {
        get
        {
            var dict = ResolveDictionary();
            return dict.TryGetValue(key, out var value) ? value : key;
        }
    }

    public string TitleSuffix => "Background Image Remover";

    private IReadOnlyDictionary<string, string> ResolveDictionary()
    {
        var language = _language == AppLanguage.System
            ? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? AppLanguage.Italian : AppLanguage.English)
            : _language;
        return language == AppLanguage.Italian ? Italian : English;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
