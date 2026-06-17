using System;
using System.Globalization;
using System.Windows;

namespace AxiomateInstaller.Services;

public enum UiLang
{
    Zh,
    En
}

/// <summary>
/// Loads / swaps the UI string resource dictionary. Strings are looked up
/// from XAML via DynamicResource, so swapping the dictionary updates UI live.
/// Code-behind goes through Strings.Get(key).
/// </summary>
public static class Strings
{
    private static UiLang _current = UiLang.En;
    private static ResourceDictionary? _activeDict;

    public static UiLang Current => _current;

    public static UiLang DetectFromCulture()
    {
        try
        {
            string name = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return string.Equals(name, "zh", StringComparison.OrdinalIgnoreCase) ? UiLang.Zh : UiLang.En;
        }
        catch
        {
            return UiLang.En;
        }
    }

    /// <summary>Switch UI language. Safe to call before App is fully constructed (no-op then).</summary>
    public static void Apply(UiLang lang)
    {
        _current = lang;
        var app = Application.Current;
        if (app is null) return;

        string uri = lang == UiLang.Zh
            ? "pack://application:,,,/Resources/Strings.zh.xaml"
            : "pack://application:,,,/Resources/Strings.en.xaml";

        var newDict = new ResourceDictionary { Source = new Uri(uri, UriKind.Absolute) };

        if (_activeDict != null)
            app.Resources.MergedDictionaries.Remove(_activeDict);
        app.Resources.MergedDictionaries.Add(newDict);
        _activeDict = newDict;
    }

    /// <summary>Lookup a string key from the active dictionary. Falls back to the key itself.</summary>
    public static string Get(string key)
    {
        var app = Application.Current;
        if (app is null) return key;
        return app.TryFindResource(key) is string s ? s : key;
    }

    /// <summary>Format a string from the active dictionary using the given args.</summary>
    public static string Format(string key, params object?[] args)
    {
        string fmt = Get(key);
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }
}
