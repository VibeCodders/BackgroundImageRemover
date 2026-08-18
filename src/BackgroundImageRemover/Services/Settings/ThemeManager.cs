using System.Windows;

namespace BackgroundImageRemover.Services.Settings;

/// <summary>
/// Swaps the merged theme dictionary (LightTheme/DarkTheme) on the application resources.
/// The theme affects the app chrome (window, menu bar, toolbars, panels, status bar) via
/// DynamicResource bindings to the "Theme.*" keys.
/// </summary>
public static class ThemeManager
{
    /// <summary>Applies the requested theme. System resolves to the Windows light/dark preference.</summary>
    public static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var effective = theme == AppTheme.System
            ? (SystemUsesLightTheme() ? AppTheme.Light : AppTheme.Dark)
            : theme;

        string file = effective == AppTheme.Dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        var dictionary = new ResourceDictionary { Source = new Uri(file, UriKind.Relative) };

        var merged = app.Resources.MergedDictionaries;
        int existing = -1;
        for (int i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source is { } source && source.OriginalString.Contains("Theme", StringComparison.OrdinalIgnoreCase))
            {
                existing = i;
                break;
            }
        }

        if (existing >= 0)
        {
            merged[existing] = dictionary;
        }
        else
        {
            merged.Add(dictionary);
        }
    }

    /// <summary>True when Windows uses the light theme for apps (registry AppsUseLightTheme).</summary>
    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch
        {
            return true; // assume light when the preference cannot be read
        }
    }
}
