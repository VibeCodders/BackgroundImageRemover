using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BackgroundImageRemover.Services.Settings;

/// <summary>
/// Swaps the merged theme dictionary (LightTheme/DarkTheme) on the application resources.
/// The theme affects the app chrome (window, menu bar, toolbars, panels, status bar) via
/// DynamicResource bindings to the "Theme.*" keys.
/// </summary>
public static class ThemeManager
{
    private static DispatcherTimer? _watchTimer;
    private static AppTheme _currentSetting = AppTheme.System;
    private static AppTheme _lastEffective = AppTheme.System;

    /// <summary>Applies the requested theme. System resolves to the Windows light/dark preference.</summary>
    public static void Apply(AppTheme theme)
    {
        _currentSetting = theme;

        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var effective = theme == AppTheme.System
            ? (SystemUsesLightTheme() ? AppTheme.Light : AppTheme.Dark)
            : theme;
        _lastEffective = effective;

        string file = effective == AppTheme.Dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        var dictionary = new ResourceDictionary { Source = new Uri(file, UriKind.Relative) };

        var merged = app.Resources.MergedDictionaries;
        int existing = -1;
        for (int i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source is { } source && IsThemeDictionary(source.OriginalString))
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

    /// <summary>
    /// Starts watching the Windows light/dark preference so that, when the app theme is
    /// "Follow Windows" (System), a change in Windows is picked up live without restarting.
    /// Windows broadcasts WM_SETTINGCHANGE on theme toggles (surfaced here via
    /// <see cref="SystemEvents.UserPreferenceChanged"/>); a slow poll acts as a fallback in
    /// case that event is not raised in a given environment.
    /// </summary>
    public static void StartWatching()
    {
        if (_watchTimer is not null)
        {
            return; // already watching
        }

        try
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
        catch
        {
            // No message pump (e.g. headless tests): the polling fallback still covers us.
        }

        _watchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _watchTimer.Tick += OnWatchTick;
        _watchTimer.Start();
    }

    public static void StopWatching()
    {
        try
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
        catch
        {
        }

        if (_watchTimer is not null)
        {
            _watchTimer.Tick -= OnWatchTick;
            _watchTimer.Stop();
            _watchTimer = null;
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // SystemEvents raises on its own thread; re-check on the UI thread.
        Application.Current?.Dispatcher.InvokeAsync(ReapplyIfSystemThemeChanged);
    }

    private static void OnWatchTick(object? sender, EventArgs e) => ReapplyIfSystemThemeChanged();

    private static void ReapplyIfSystemThemeChanged()
    {
        if (!SystemThemeChanged(_currentSetting, _lastEffective, SystemUsesLightTheme()))
        {
            return;
        }

        Apply(_currentSetting);
    }

    /// <summary>
    /// True when the app is set to "Follow Windows" and the effective theme differs from the one
    /// currently applied, i.e. Windows just switched light/dark. Exposed internally for tests.
    /// </summary>
    internal static bool SystemThemeChanged(AppTheme currentSetting, AppTheme lastEffective, bool windowsUsesLight)
        => currentSetting == AppTheme.System
           && (windowsUsesLight ? AppTheme.Light : AppTheme.Dark) != lastEffective;

    /// <summary>True for the app's own theme dictionaries (and not e.g. the shared ControlStyles dictionary).</summary>
    private static bool IsThemeDictionary(string path)
        => path.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when Windows uses the light theme for apps (registry AppsUseLightTheme).</summary>
    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch
        {
            return true; // assume light when the preference cannot be read
        }
    }
}
