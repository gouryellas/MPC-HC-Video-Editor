using System.Reflection;
using System.Windows;
using MpcHcVideoEditor.Services;
using MpcHcVideoEditor.Views;

namespace MpcHcVideoEditor;

public partial class App : Application
{
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!TryClaimSingleInstance())
        {
            // The running copy has been signalled to come to the front.
            // base.OnStartup is what creates the StartupUri window, so
            // skipping it here is what keeps this launch from ever showing
            // one — see the well-known WPF pattern this follows: the
            // framework checks for a shutdown request between OnStartup
            // returning and creating that window.
            Shutdown();
            return;
        }

        // Before base.OnStartup, which is what creates the first window: the
        // theme's brushes have to be in Application.Resources before anything
        // is measured, or the window paints in whatever the defaults were and
        // then visibly repaints.
        // The operation colours are recomputed whenever the palette moves: two
        // of the four can follow the theme, and the settings dialog repaints
        // live while someone is trying themes out.
        ThemeService.Changed += _ => ButtonColors.Reapply();
        ThemeService.Changed += _ => SeekbarAppearance.Reapply();

        try
        {
            var settings = new SettingsService().Current;
            var themeKey = settings.FollowSystemTheme ? SystemTheme.ResolveThemeKey() : settings.ThemeKey;
            ThemeService.ApplyFromKey(themeKey);
            ButtonColors.Apply(settings.ButtonColors, ThemeService.Current);
            SeekbarAppearance.Apply(settings, ThemeService.Current);
        }
        catch
        {
            // An unreadable settings file must not stop the app starting; the
            // default palette is already in place.
            ThemeService.Apply(ThemePalette.Graphite);
            ButtonColors.Apply(null, ThemeService.Current);
            SeekbarAppearance.Apply(null, ThemeService.Current);
        }

        // Before any window can load a video and look for its bookmarks. A
        // program folder that arrived empty gets its collection back from the
        // %APPDATA% mirror here, and anything the mirror missed is pushed to it
        // — see BookmarkService.SyncWithBackup for why that order matters.
        BookmarkService.SyncWithBackup();

        // Re-reads settings rather than closing over the instance above: that
        // one is a throwaway for startup, while this fires for as long as the
        // app runs and has to see whatever the Settings dialog last saved —
        // including "Follow system theme" being turned on or off since.
        // Dispatched to the UI thread because SystemEvents does not promise
        // one, and applying a theme touches Application.Resources.
        SystemTheme.Watch();
        SystemTheme.Changed += () => Dispatcher.Invoke(() =>
        {
            if (new SettingsService().Current.FollowSystemTheme)
                ThemeService.ApplyFromKey(SystemTheme.ResolveThemeKey());
        });

        // Windows sometimes sets "menu drop alignment" so popups open to the left.
        // Force normal left-to-right menu alignment.
        try
        {
            if (SystemParameters.MenuDropAlignment)
            {
                var field = typeof(SystemParameters)
                    .GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
                field?.SetValue(null, false);
            }
        }
        catch
        {
            // ignore if runtime layout differs
        }

        base.OnStartup(e);
    }

    /// <summary>
    /// Enforces the "only one instance" setting. Reads settings.json directly
    /// through its own <see cref="SettingsService"/> rather than waiting for
    /// <see cref="ViewModels.MainViewModel"/>'s copy, because this decision
    /// has to be made before the main window — and with it, that ViewModel —
    /// exists at all.
    /// </summary>
    /// <returns>
    /// False only when another copy of this exact install already holds the
    /// lock and this launch must exit without ever creating a window. True
    /// in every other case, including "multiple instances are allowed" and
    /// "the lock could not be checked" — a guard that fails closed would let
    /// a broken mutex turn into a broken application.
    /// </returns>
    private bool TryClaimSingleInstance()
    {
        try
        {
            var settings = new SettingsService();
            if (settings.Current.AllowMultipleInstances) return true;

            _singleInstance = new SingleInstanceService();
            if (_singleInstance.IsFirstInstance)
            {
                _singleInstance.WakeRequested += () =>
                {
                    if (MainWindow is MainWindow mw) mw.BringToFront();
                };
                return true;
            }

            _singleInstance.SignalRunningInstance();
            _singleInstance.Dispose();
            _singleInstance = null;
            return false;
        }
        catch
        {
            // See the return-value doc above — never let this guard itself
            // keep the app from starting.
            return true;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
