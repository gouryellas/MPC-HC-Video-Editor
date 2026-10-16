using Microsoft.Win32;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Reads whether Windows itself is set to light or dark app mode, and says so
/// again whenever that changes while the program is running.
/// </summary>
/// <remarks>
/// There is no public API for this — <c>AppsUseLightTheme</c> under
/// Personalize is the same undocumented registry value every theme-aware
/// Windows app reads, absent entirely on a system that has never opened the
/// Personalization page, which is why a missing value reads as light rather
/// than throwing.
/// </remarks>
public static class SystemTheme
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    /// <summary>
    /// Raised when Windows' own light/dark setting changes. Only fires while
    /// at least one caller has asked to be told — see <see cref="Watch"/>.
    /// </summary>
    public static event Action? Changed;

    private static bool _watching;

    /// <summary>True if Windows apps are currently set to the light theme.</summary>
    public static bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            var value = key?.GetValue(ValueName);
            // 1 (or the key/value being absent) is light; 0 is dark — this is
            // read the same way across every Windows version that has the key.
            return value is not int i || i != 0;
        }
        catch
        {
            // A registry read should never be what takes the app down; light
            // is the safer guess since it is also what a value this new
            // Windows build does not have yet would mean.
            return true;
        }
    }

    /// <summary>The theme key that matches Windows' current setting.</summary>
    public static string ResolveThemeKey() =>
        IsLight() ? ThemePalette.Daylight.Key : ThemePalette.Graphite.Key;

    /// <summary>
    /// Starts listening for Windows theme changes, if nothing already is.
    /// Safe to call repeatedly — only the first caller actually subscribes.
    /// </summary>
    /// <remarks>
    /// <see cref="SystemEvents.UserPreferenceChanged"/> fires for a lot more
    /// than the theme — wallpaper, accent colour, taskbar settings — all
    /// folded into <see cref="UserPreferenceCategory.General"/> with no finer
    /// category to ask for instead, so every caller just re-reads the value
    /// and compares it themselves rather than trusting that this fired for
    /// the reason it thinks it did.
    /// </remarks>
    public static void Watch()
    {
        if (_watching) return;
        _watching = true;

        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General) Changed?.Invoke();
        };
    }
}
