using System.Collections.Specialized;
using System.Globalization;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;
using MpcHcVideoEditor.ViewModels;

namespace MpcHcVideoEditor.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    // ------------------------------------------------------------------
    // Rearranging the toolbar
    // ------------------------------------------------------------------

    /// <summary>Where the left button went down, to measure a drag against.</summary>
    private Point _toolbarPressPoint;

    /// <summary>The item under the pointer when it went down, if any.</summary>
    private ToolbarItem? _toolbarPressed;

    /// <summary>The item being carried, once a press has become a drag.</summary>
    private ToolbarItem? _toolbarDragging;

    /// <summary>
    /// Notes which button a press landed on, without doing anything about it
    /// yet.
    /// </summary>
    /// <remarks>
    /// Every one of these is a click target, so a press cannot start a drag on
    /// its own — that would make Merge impossible to press. The drag only
    /// begins once the pointer has also moved, in
    /// <see cref="Toolbar_MouseMove"/>.
    /// </remarks>
    private void Toolbar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _toolbarPressPoint = e.GetPosition(this);
        _toolbarPressed = ItemUnder(e.OriginalSource as DependencyObject);
    }

    /// <summary>
    /// Turns a press that has travelled far enough into a drag, and reorders
    /// the row live while it is held.
    /// </summary>
    /// <remarks>
    /// The row rearranges under the pointer rather than showing an insertion
    /// line: the feedback is the thing itself, and there is no adorner to keep
    /// in step with a WrapPanel that may have wrapped to a second row.
    ///
    /// <see cref="SystemParameters.MinimumHorizontalDragDistance"/> is the
    /// threshold Windows uses everywhere else, so a press that wobbles by a
    /// pixel is still a click here as it is in File Explorer.
    /// </remarks>
    private void Toolbar_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _vm is null) return;
        if (_toolbarPressed is null) return;

        var at = e.GetPosition(this);

        if (_toolbarDragging is null)
        {
            if (Math.Abs(at.X - _toolbarPressPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(at.Y - _toolbarPressPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _toolbarDragging = _toolbarPressed;
            _toolbarDragging.IsDragging = true;
            ActionToolbar.CaptureMouse();
        }

        // Where the pointer is now, in the row's own terms.
        var over = ItemUnder(ActionToolbar.InputHitTest(e.GetPosition(ActionToolbar)) as DependencyObject);
        if (over is null || ReferenceEquals(over, _toolbarDragging)) return;

        // Within the row that holds it: a row lays itself out, so moving an
        // item between rows here would mean deciding which line the pointer is
        // over, which is the Customize dialog's job rather than a quick drag's.
        var row = _vm.RowContaining(_toolbarDragging);
        if (row is null || !row.Contains(over)) return;

        var from = row.IndexOf(_toolbarDragging);
        var to = row.IndexOf(over);
        if (from < 0 || to < 0) return;

        row.Move(from, to);
    }

    /// <summary>
    /// Ends a drag, saves the order, and swallows the click that would
    /// otherwise follow.
    /// </summary>
    /// <remarks>
    /// Handled on the window rather than the toolbar: a drag that ends with the
    /// pointer off the row still has to finish, and the mouse is captured here
    /// anyway. Marking the event handled is what stops the button underneath
    /// from firing its command at the end of a drag — dropping Split onto Merge
    /// must not also run a merge.
    /// </remarks>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_toolbarDragging is not null)
        {
            _toolbarDragging.IsDragging = false;
            _toolbarDragging = null;
            ActionToolbar.ReleaseMouseCapture();
            _vm?.RebuildOrderFromRows();
            e.Handled = true;
        }

        _toolbarPressed = null;
        base.OnPreviewMouseLeftButtonUp(e);
    }

    /// <summary>The toolbar item behind a hit-tested element, or null.</summary>
    private static ToolbarItem? ItemUnder(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: ToolbarItem item }) return item;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    public MainWindow()
    {
        InitializeComponent();

        // From the assembly, not from the XAML: a literal caption there is what
        // let the window still say 3.0 in the 3.0.1 release. See AppVersion.
        Title = $"MPC-HC Video Editor {AppVersion.Display}";

        // The window and tray icons are drawn from the palette rather than
        // loaded, so they follow the theme. Applied now for the starting theme
        // and again whenever it changes.
        IconRenderer.ApplyTo(this, ThemeService.Current);
        ThemeService.Changed += OnThemeChanged;

        Loaded += MainWindow_Loaded;
        Activated += (_, _) =>
        {
            _vm?.ResumePollTimer();

            // "Edit bookmarks" opens the CSV in an external editor, so coming
            // back to this window is the cue to pick up whatever was saved.
            _vm?.ReloadBookmarksFromDisk();
        };
        Deactivated += (_, _) => _vm?.PausePollTimer();

        StateChanged += MainWindow_StateChanged;

        // Once there is a laid-out window to measure. SizeChanged as well as
        // Loaded because the two things being measured are not fixed: the
        // toolbar row grows with the system font, and the side panel grows a
        // clip preview when a row is selected.
        SizeChanged += (_, _) => UpdateMinimumSize();

        // And once on the way in. SizeChanged does fire during the first
        // layout, but UpdateMinimumSize returns early until the window is
        // loaded — so with nothing else calling it, the measured minimum did
        // not take effect until the user resized the window by hand, which is
        // the one moment it was not needed.
        //
        // Then fit, because a minimum measured larger than the restored size
        // grows the window, and a grown window can reach past the screen edge.
        Loaded += (_, _) =>
        {
            UpdateMinimumSize();
            ScreenFit.Fit(this);
        };

        // Before the window is drawn, so it never appears at a size it is about
        // to be moved out of, and late enough to have a handle — which is what
        // ScreenFit needs to ask about the right monitor.
        SourceInitialized += (_, _) => RestorePlacement();
    }

    // ------------------------------------------------------------------
    // Placement
    // ------------------------------------------------------------------

    /// <summary>
    /// Puts the window back where it was, or fits the starting size to this
    /// screen on a first run.
    /// </summary>
    /// <remarks>
    /// The size in the XAML is 1100x780, which is taller than the usable height
    /// of a 1366x768 screen and of a 1080p one at 150% scaling — WPF would
    /// honour it regardless and hang the bottom of the window, status bar and
    /// all, below the edge of the screen. Nothing in WPF clamps a declared size
    /// to the display; <see cref="ScreenFit.Fit"/> is that step.
    ///
    /// A stored position is only used while it still lands on a monitor that is
    /// attached. Otherwise it is dropped and the window is centred as if it had
    /// never been saved, which is the right answer for a laptop that was docked
    /// to a second screen yesterday.
    /// </remarks>
    private void RestorePlacement()
    {
        var saved = _vm?.SavedWindowPlacement ?? (DataContext as MainViewModel)?.SavedWindowPlacement;

        if (saved is not null &&
            ScreenFit.IsOnScreen(saved.Left, saved.Top, saved.Width, saved.Height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;

            Left = saved.Left;
            Top = saved.Top;
            Width = saved.Width;
            Height = saved.Height;

            // Fit before maximizing, so the size restored by un-maximizing is
            // one that fits this screen rather than the last one.
            ScreenFit.Fit(this);

            if (saved.Maximized) WindowState = WindowState.Maximized;
            return;
        }

        ScreenFit.Fit(this);
    }

    /// <summary>
    /// Writes down where the window is, for the next run.
    /// </summary>
    /// <remarks>
    /// <see cref="Window.RestoreBounds"/> rather than the live properties: a
    /// maximized window reports the whole screen as its size, and restoring
    /// that would lose the size it had before — so un-maximizing after a
    /// restart would have nothing to go back to.
    /// </remarks>
    private void SavePlacement()
    {
        var vm = _vm ?? DataContext as MainViewModel;
        if (vm is null) return;

        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        // Minimized to the tray with nothing ever restored: there is no honest
        // size to record, so the last one stands.
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;

        vm.SaveWindowPlacement(new WindowPlacement(bounds.Left, bounds.Top,
                                                   bounds.Width, bounds.Height,
                                                   WindowState == WindowState.Maximized));
    }

    // ------------------------------------------------------------------
    // Minimum size
    // ------------------------------------------------------------------

    /// <summary>
    /// Holds the window to a size that keeps the toolbar on one row and the
    /// side panel's last line on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured rather than written down. A pair of literals in the XAML was
    /// what was there before, and literals do not know that the toolbar row is
    /// as wide as its buttons' text — which moves with the system font and
    /// again if a button is ever renamed — so the row wrapped to a second line
    /// well above the stated minimum.
    /// </para>
    /// <para>
    /// Both are summed from the children rather than read off the container.
    /// A <see cref="FrameworkElement"/> clamps its own DesiredSize to the room
    /// it was offered, so a panel that is already too short reports wanting
    /// exactly the height it has got — asking it is asking the wrong thing, and
    /// gives a minimum that only ever agrees with the current size. The
    /// children are measured against unbounded space in the direction that
    /// matters, so each one's DesiredSize is the honest figure: a WrapPanel's
    /// children are measured unwrapped, and a StackPanel's are measured with
    /// unbounded height. Collapsed children measure zero, which is right —
    /// the clip preview should only be accounted for while it is on screen.
    /// </para>
    /// <para>
    /// Both are capped to the work area. A minimum larger than the screen is
    /// one the user cannot satisfy, and WPF would grow the window to meet it.
    /// </para>
    /// </remarks>
    private void UpdateMinimumSize()
    {
        if (!IsLoaded) return;

        var work = SystemParameters.WorkArea;

        // The menu bar sets the floor, not the toolbar. The toolbar wraps when
        // it runs out of width — that is what ToolbarRowPanel is for — so
        // holding the window open wide enough for every button would be holding
        // it open for something that can look after itself. The menu cannot: its
        // words are the one row here with nowhere to go.
        if (HeaderMenu is { ActualWidth: > 0 })
        {
            double menu = 0;
            foreach (UIElement child in HeaderMenu.Items.OfType<UIElement>())
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                menu += child.DesiredSize.Width;
            }

            var chrome = ActualWidth - HeaderMenu.ActualWidth;
            MinWidth = Math.Min(menu + chrome, work.Width);

            // What the Customize dialog draws its preview at: build a row that
            // fits the narrowest the window can be, and it fits at every size.
            if (_vm is not null) _vm.ToolbarMinimumWidth = Math.Max(320, menu);
        }

        // Through the ScrollViewer the panel's content now sits in: the thing
        // worth measuring is the stack of lines, and a ScrollViewer asked for
        // its height would answer with whatever it was given, which is the
        // circular answer this method exists to avoid.
        var panelContent = SidePanel?.Child switch
        {
            ScrollViewer { Content: Panel scrolled } => scrolled,
            Panel direct => direct,
            _ => null
        };

        if (SidePanel is { ActualHeight: > 0 } && panelContent is { } stack)
        {
            double content = SidePanel.Padding.Top + SidePanel.Padding.Bottom;
            foreach (UIElement child in stack.Children)
                content += child.DesiredSize.Height;

            var chrome = ActualHeight - SidePanel.ActualHeight;
            MinHeight = Math.Min(content + chrome, work.Height);
        }
    }

    // ------------------------------------------------------------------
    // Alt
    // ------------------------------------------------------------------

    /// <summary>
    /// Whether this is Alt on its own, rather than Alt holding down another
    /// key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bare press reports <see cref="Key.System"/> with
    /// <see cref="KeyEventArgs.SystemKey"/> naming the Alt key itself. Every
    /// combination worth keeping names its other key there instead — Alt+F4 is
    /// <c>F4</c>, Alt+Space is <c>Space</c> — so testing for the Alt keys by
    /// name catches the bare press and nothing else. Alt+Tab never reaches an
    /// application at all.
    /// </para>
    /// </remarks>
    private static bool IsBareAlt(KeyEventArgs e) =>
        e.Key == Key.System && e.SystemKey is Key.LeftAlt or Key.RightAlt;

    /// <summary>
    /// Swallows a bare Alt so it cannot put the menu bar into menu mode.
    /// </summary>
    /// <remarks>
    /// The menus carry no access keys, so Alt has nothing left to reach — but
    /// Alt alone still moved focus to the menu bar and left the next keystroke
    /// going there rather than to the window. The timestamp hotkey is global
    /// and can be bound to anything, Alt included, and a binding that quietly
    /// activated the menu bar instead of recording a bookmark is the sort of
    /// thing a user would have to discover.
    ///
    /// Both directions are handled: the down stroke is what WPF arms menu mode
    /// on, the up stroke is what enters it.
    /// </remarks>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsBareAlt(e)) { e.Handled = true; return; }
        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc cref="OnPreviewKeyDown"/>
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (IsBareAlt(e)) { e.Handled = true; return; }
        base.OnPreviewKeyUp(e);
    }

    // ------------------------------------------------------------------
    // Run mode: application vs system tray
    // ------------------------------------------------------------------

    private TrayIconService? _tray;

    /// <summary>
    /// Repaints both icons for a new theme.
    /// </summary>
    /// <remarks>
    /// The tray icon only exists in tray mode, so it is updated conditionally —
    /// and it is the one that genuinely needs this, since it sits on the user's
    /// taskbar next to everything else all day.
    /// </remarks>
    private void OnThemeChanged(ThemePalette palette)
    {
        IconRenderer.ApplyTo(this, palette);
        _tray?.ApplyTheme(palette);
    }

    /// <summary>
    /// Set when the user has chosen Exit from the tray menu, so the close that
    /// follows is allowed through instead of being turned back into a hide.
    /// </summary>
    private bool _exitConfirmed;

    /// <summary>Whether the tray is currently the configured behavior.</summary>
    private bool InTrayMode => _vm?.RunMode == RunMode.SystemTray;

    /// <summary>
    /// Creates or removes the tray icon to match the current setting. Called at
    /// startup and again whenever Settings is saved.
    /// </summary>
    public void ApplyRunMode()
    {
        if (InTrayMode)
        {
            if (_tray == null)
            {
                _tray = new TrayIconService("MPC-HC Video Editor");
                _tray.ShowRequested += RestoreFromTray;
                _tray.ExitRequested += ExitFromTray;
            }

            _tray.Visible = true;
            return;
        }

        // Switching back to a plain application while hidden would strand the
        // window with no icon and no taskbar button to reach it by.
        if (!IsVisible) RestoreFromTray();

        _tray?.Dispose();
        _tray = null;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        _exitConfirmed = true;
        Close();
    }

    /// <summary>
    /// In tray mode, minimizing hides the window rather than parking it on the
    /// taskbar — otherwise it would be in both places at once.
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Told, not asked: the overlay hides this window while it is up, so by
        // the time the ViewModel wants to know whether it was minimized there
        // is nothing on screen to look at. Recorded on the way into the state
        // rather than read back out of it.
        //
        // Un-minimizing clears it, whichever way it happened — the taskbar
        // button, the tray icon, or a restore this program asked for — so the
        // next trip to the player starts from a window the user has out.
        if (_vm is not null) _vm.WindowMinimized = WindowState == WindowState.Minimized;

        if (WindowState == WindowState.Minimized && InTrayMode)
            Hide();
    }

    /// <summary>
    /// In tray mode, closing the window hides it instead. Exit is then only
    /// available from the tray icon's menu.
    /// </summary>
    /// <remarks>
    /// Cancelling the close is what keeps the process alive: the window is
    /// never actually closed, so <c>ShutdownMode.OnMainWindowClose</c> never
    /// fires. The balloon appears once per run, because a program that
    /// vanishes from the taskbar without exiting is worth explaining the first
    /// time and nagging about no further.
    /// </remarks>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Before the tray check below, which returns without closing. Exit from
        // the tray closes the window again later, so this runs twice there —
        // the second time with the same numbers, which costs one file write.
        SavePlacement();

        if (InTrayMode && !_exitConfirmed)
        {
            e.Cancel = true;
            Hide();

            if (!_explainedTrayOnce)
            {
                _explainedTrayOnce = true;
                _tray?.ShowMessage("Still running",
                    "MPC-HC Video Editor is in the notification area. Right-click its icon and choose Exit to close it.");
            }

            return;
        }

        base.OnClosing(e);
    }

    private bool _explainedTrayOnce;

    /// <summary>
    /// Tears down everything that outlives this window.
    /// </summary>
    /// <remarks>
    /// The process used to survive its own UI. Two reasons, both fixed here
    /// and in App.xaml:
    ///
    /// The overlay and the toast are shown once and thereafter only hidden, so
    /// both stayed open — hidden, but open — for the life of the app. Under
    /// the default <c>ShutdownMode.OnLastWindowClose</c> that meant closing
    /// this window was never the last close, and WPF kept a message loop
    /// running with nothing on screen. App.xaml now uses
    /// <c>OnMainWindowClose</c>, and they are closed explicitly here anyway.
    ///
    /// And <see cref="MainViewModel.Dispose"/> was never called by anything,
    /// so the low-level input hooks, the stall monitor's threads and any
    /// running ffmpeg carried on. Those threads are all background threads and
    /// would not block exit by themselves, but an orphaned ffmpeg.exe holding
    /// a file handle is its own kind of lingering.
    /// </remarks>
    protected override void OnClosed(EventArgs e)
    {
        // Close, not Hide: a hidden window is still an open one.
        try { _minimal?.Close(); } catch { /* going away regardless */ }
        _minimal = null;

        // Before the ViewModel, so the icon is out of the tray while the
        // dispatcher is still alive to remove it.
        try { _tray?.Dispose(); } catch { }
        _tray = null;

        try { _vm?.Dispose(); } catch { /* ditto */ }

        base.OnClosed(e);
    }

    // ------------------------------------------------------------------
    // Minimal / full view
    // ------------------------------------------------------------------

    private MinimalWindow? _minimal;

    /// <summary>
    /// Swaps between the full window and the click-through bookmark overlay.
    /// The overlay shares this window's DataContext, so it tracks the bookmark
    /// list with no extra plumbing.
    /// </summary>
    /// <param name="activate">
    /// Whether the restored window should also be brought to the front. True
    /// for the restores the user asked for — pressing X, a second launch, or
    /// leaving the player for this program. False when something else has the
    /// focus: the window still comes back, because the overlay is topmost and
    /// cannot be left floating over another application, but it comes back
    /// behind whatever the user moved to rather than over it.
    /// </param>
    private void SetMinimalView(bool minimal, bool activate)
    {
        if (minimal)
        {
            ShowOverlay();
            Hide();
            return;
        }

        _minimal?.Hide();

        // In tray mode, minimized means gone from the taskbar altogether — that
        // is what minimizing does there. A restore that is not meant to raise
        // the window must not undo it by showing the thing again, because the
        // user would get a taskbar button back for a window they had put in the
        // notification area. The tray icon is the way back to it.
        if (!activate && WindowState == WindowState.Minimized && InTrayMode) return;

        // Show() takes the focus on its own — ShowActivated is true by default
        // and applies to every Show, not only the first — so a restore that is
        // not meant to steal focus has to say so before showing, and put the
        // property back afterwards for the restores that are.
        //
        // And only when it would be granted. Showing with ShowActivated while
        // another process holds the foreground is a SetForegroundWindow that
        // Windows refuses, and a refusal flashes the taskbar button — the same
        // trap as in RaiseToFront, one call earlier.
        ShowActivated = activate && !ForegroundWindow.OwnedByAnotherApp();
        Show();
        ShowActivated = true;

        // Un-minimizing is itself a focus grab — Windows hands the foreground
        // to a window coming back from the taskbar — so a restore that must not
        // steal focus leaves a minimized window minimized. Nothing is lost by
        // that: it was put there on purpose, and the overlay has already come
        // down.
        if (activate) WindowState = WindowState.Normal;

        if (activate) RaiseToFront();
    }

    /// <summary>
    /// Puts this window in front of the others, and gives it focus if Windows
    /// will allow that. Does not leave it always-on-top.
    /// </summary>
    /// <remarks>
    /// <see cref="Window.Activate"/> on its own is not enough for the restore
    /// that follows the user leaving the player. It calls SetForegroundWindow,
    /// which Windows refuses to a process that does not already own the
    /// foreground — and at that moment the foreground belongs to whatever they
    /// just clicked. The refusal is silent: the taskbar button flashes and the
    /// window stays exactly where it was, behind everything.
    ///
    /// Z-order is not governed by that rule, so a momentary Topmost raises the
    /// window for real. Dropping it in the same breath leaves it at the top of
    /// the ordinary z-order rather than above everything for good: this window
    /// has no more claim on the foreground afterwards than any other, and
    /// clicking back to the player hands it straight over.
    ///
    /// Activate is still called, but only when the foreground does happen to be
    /// ours — then it costs nothing and the window gets focus rather than
    /// merely being visible. Asking for it when it will be refused is what made
    /// the taskbar button flash on the way back from the player: a refused
    /// SetForegroundWindow is not a no-op, it is Windows offering the user the
    /// attention the program was denied. There is nothing to ask for anyway,
    /// since the raise above has already done the part that works.
    /// </remarks>
    private void RaiseToFront()
    {
        Topmost = true;
        Topmost = false;

        // Asked for whenever it will not be refused: when the foreground is
        // ours already, and equally when nobody holds it. Withheld only from
        // an application the user actually switched to — that is the refusal
        // that flashes the taskbar button instead of raising anything.
        if (!ForegroundWindow.OwnedByAnotherApp()) Activate();
    }

    /// <summary>
    /// Creates the overlay on first use, applies its configured appearance, and
    /// shows it.
    /// </summary>
    /// <remarks>
    /// Appearance is re-applied on every show, not just on creation, so a change
    /// in Settings takes effect the next time the overlay appears rather than
    /// only after a restart. Positioning also has to be re-applied per show
    /// because it is sized to the monitor.
    /// </remarks>
    private void ShowOverlay()
    {
        if (_minimal == null)
        {
            _minimal = new MinimalWindow
            {
                DataContext = DataContext,

                // So the overlay lands in the corner of the monitor the video
                // is playing on, rather than the corner of the primary one.
                AnchorWindowProvider = () => _vm?.PlayerWindow ?? IntPtr.Zero
            };

            _minimal.Closed += (_, _) => _minimal = null;
        }

        // Before the Show, unlike the two below: this one decides whether the
        // window swallows clicks, and a frame of the wrong answer is a click
        // that went to the wrong place rather than a cosmetic flicker.
        _minimal.SetClickable(_vm?.OverlayClickable ?? false);

        _minimal.Show();
        _minimal.SetBackgroundOpacity(_vm?.OverlayOpacity ?? 1.0);
        _minimal.PositionInCorner(_vm?.OverlayCorner ?? OverlayCorner.TopRight);
    }

    /// <summary>
    /// Called when a second launch of this install was blocked by the "only
    /// one instance" setting and handed off to this one instead. Brings the
    /// app to the front regardless of whether it is minimized to the tray,
    /// hidden behind the minimal overlay, or just sitting in the background.
    /// </summary>
    /// <remarks>
    /// Routed through <see cref="MainViewModel.ShowFullViewCommand"/> rather
    /// than calling <c>Show</c>/<c>Activate</c> directly here: that command is
    /// already the full "come back to the full window" behavior — it also
    /// drops the minimal overlay and stops the view from following focus back
    /// to it, which a second launch asking to be seen should do too. The
    /// direct fallback only matters for the narrow race where the wake signal
    /// arrives before <see cref="MainWindow_Loaded"/> has wired up
    /// <see cref="_vm"/> — the window itself already exists by then (see
    /// <see cref="App.TryClaimSingleInstance"/>), so it still has something to
    /// act on.
    /// </remarks>
    public void BringToFront()
    {
        if (_vm != null)
        {
            _vm.ShowFullViewCommand.Execute(null);
            return;
        }

        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    // ------------------------------------------------------------------
    // Drag and drop
    // ------------------------------------------------------------------

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Accepts a dropped video, bookmark CSV, playlist or picture. Dispatch is
    /// by extension, matching File ▸ Open…
    /// </summary>
    /// <remarks>
    /// Every path is handed over, not just the first. A handful of pictures
    /// dropped together is one question about all of them; for everything else
    /// the ViewModel still takes the first and ignores the rest.
    /// </remarks>
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_vm == null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths) return;

        e.Handled = true;
        _vm.OpenDroppedFilesCommand.Execute(paths);
    }

    /// <summary>Guards <see cref="MainWindow_Loaded"/> against running twice.</summary>
    /// <remarks>
    /// A WPF Window raises Loaded again when it is hidden and shown, which
    /// this window now does constantly — the view follows focus, and in tray
    /// mode minimizing hides it too. Every subscription below would otherwise
    /// be added again on each restore, so menus would rebuild several times
    /// per change and handlers would fire in multiples.
    /// </remarks>
    private bool _wired;

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as MainViewModel;
        if (_vm == null) return;

        if (_wired) return;
        _wired = true;

        _vm.MinimalViewRequested += SetMinimalView;
        _vm.Job.Finished += PulseJobPanel;

        // Seed it, since StateChanged only fires on a change and the window may
        // already be in whatever state the saved placement put it.
        _vm.WindowMinimized = WindowState == WindowState.Minimized;

        // The tray icon belongs to the window, not the ViewModel, so the
        // ViewModel just says when the setting changed.
        _vm.RunModeChanged += ApplyRunMode;
        ApplyRunMode();

        _vm.Shortcuts.CollectionChanged += Shortcuts_CollectionChanged;


        // Quick save shortcuts live in the File menu and are a separate list.
        _vm.QuickSaveShortcuts.CollectionChanged += (_, _) => RefreshFileMenuShortcuts();

        // Subscribe to property changes (rename) on every entry that
        // already exists at startup. New entries added later are wired
        // up inside Shortcuts_CollectionChanged.
        foreach (var entry in _vm.Shortcuts)
            HookEntry(entry);

        // Recent videos: rebuild the File → Recent submenu whenever the
        // collection changes (new video played, entry removed, list cleared).
        _vm.RecentVideos.CollectionChanged += RecentVideos_CollectionChanged;

        // Suffixes: rebuild the Suffix menu whenever the collection
        // changes (add/remove/reorder). Also subscribe to property
        // changes on each entry so a rename refreshes the menu, and
        // to ActiveSuffixDisplay so the "Current:" label updates live.
        _vm.Suffixes.CollectionChanged += Suffixes_CollectionChanged;
        foreach (var suf in _vm.Suffixes)
            HookSuffixEntry(suf);
        _vm.PropertyChanged += Vm_PropertyChanged;

        // Playlists: rebuild the Playlist menu whenever the ViewModel
        // signals a change (playlist created/deleted, entry added/removed,
        // playlist folder changed). Built from disk each time.
        _vm.PlaylistsChanged += () => Dispatcher.BeginInvoke(RefreshPlaylistsMenu);

        // Timed individually so stalls.log attributes startup UI-thread time
        // to a specific menu rather than reporting an anonymous stall.
        _vm.TimeUiWork("RefreshShortcutsMenus", RefreshShortcutsMenus);
        _vm.TimeUiWork("RefreshRecentMenu", RefreshRecentMenu);
        _vm.TimeUiWork("RefreshSuffixMenu", RefreshSuffixMenu);
        _vm.TimeUiWork("RefreshPlaylistsMenu", RefreshPlaylistsMenu);

        // Deliberately not awaited: the window is finished loading either way,
        // and a slow or unreachable GitHub must not hold up startup. Guarded by
        // _wired above, so it runs once per launch rather than on every restore.
        _ = _vm.RunStartupUpdateCheckAsync();
    }

    private void Shortcuts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Wire up new entries so a later rename refreshes the menus;
        // unwire removed entries to avoid leaks.
        if (e.NewItems != null)
            foreach (ShortcutEntry entry in e.NewItems)
                HookEntry(entry);
        if (e.OldItems != null)
            foreach (ShortcutEntry entry in e.OldItems)
                UnhookEntry(entry);

        // A single full rebuild covers every action we care about:
        // Add / Remove / Move (drag-reorder) / Replace. The menus are
        // short, so rebuilding from scratch is cheap and avoids per-action
        // bookkeeping bugs. Rename is handled separately by the
        // PropertyChanged subscription below (so the File menu header
        // updates without needing a Replace action).
        RefreshShortcutsMenus();
    }

    private void HookEntry(ShortcutEntry entry)
        => entry.PropertyChanged += Entry_PropertyChanged;

    private void UnhookEntry(ShortcutEntry entry)
        => entry.PropertyChanged -= Entry_PropertyChanged;

    /// <summary>
    /// Fires when a shortcut's Name (or Path) changes — most importantly
    /// when the user renames a shortcut. We rebuild the menus so the new
    /// Name appears in the File menu header right away.
    /// </summary>
    private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShortcutEntry.Name) ||
            e.PropertyName == nameof(ShortcutEntry.Path))
        {
            RefreshShortcutsMenus();
        }
    }

    /// <summary>
    /// Rebuilds the dynamic shortcut entries in both the File menu
    /// (clickable "open folder" items) and the Shortcuts menu (each with
    /// an "Open", "Rename" and "Remove" submenu). Called whenever the
    /// <see cref="MainViewModel.Shortcuts"/> collection changes and once
    /// on initial load.
    /// </summary>
    private void RefreshShortcutsMenus()
    {
        if (_vm == null) return;

        RefreshFileMenuShortcuts();
        RefreshShortcutsMenu();
    }

    /// <summary>
    /// In the File menu, between <see cref="ShortcutsStartSeparator"/> and
    /// <see cref="ShortcutsEndSeparator"/>, inserts one MenuItem per shortcut
    /// whose header is the shortcut's friendly name and whose click opens
    /// the folder in Explorer. Hides both separators when there are no
    /// shortcuts.
    /// </summary>
    private void RefreshFileMenuShortcuts()
    {
        var fileMenu = FileMenu;

        var startSep = FindNamed(fileMenu.Items, "ShortcutsStartSeparator") as Separator;
        var endSep = FindNamed(fileMenu.Items, "ShortcutsEndSeparator") as Separator;
        if (startSep == null || endSep == null) return;

        int startIdx = fileMenu.Items.IndexOf(startSep);
        int endIdx = fileMenu.Items.IndexOf(endSep);

        // Remove anything currently between the two separators.
        for (int i = endIdx - 1; i > startIdx; i--)
            fileMenu.Items.RemoveAt(i);

        bool hasAny = _vm!.QuickSaveShortcuts.Count > 0;
        // startSep only shows when there are shortcuts to introduce;
        // endSep stays visible at all times because it doubles as the
        // divider before "Add quick save shortcut…" — keeping it visible avoids
        // a double-separator (endSep + a static Separator) when shortcuts
        // exist, and still gives a clean divider when there are none.
        // Both stay hidden. They are insertion markers and nothing else now
        // that the section carries a caption — "Quick save" says what the list
        // under it is, which is what the dividers were standing in for.
        _ = hasAny;
        startSep.Visibility = Visibility.Collapsed;
        endSep.Visibility = Visibility.Collapsed;

        int insertAt = startIdx + 1;
        foreach (var entry in _vm.QuickSaveShortcuts)
            fileMenu.Items.Insert(insertAt++, BuildFileMenuShortcutItem(entry));
    }

    /// <summary>
    /// In the Shortcuts menu, appends one MenuItem per shortcut after
    /// <see cref="ShortcutsMenuStartSeparator"/>. Each entry's header is the
    /// shortcut's friendly name and expands into "Open", "Rename…", and
    /// "Remove" sub-items. The dynamic entries are the last items in the
    /// menu — there is intentionally no trailing separator.
    /// </summary>
    private void RefreshShortcutsMenu()
    {
        var shortcutsMenu = ShortcutsMenu;

        var startSep = FindNamed(shortcutsMenu.Items, "ShortcutsMenuStartSeparator") as Separator;
        if (startSep == null) return;

        int startIdx = shortcutsMenu.Items.IndexOf(startSep);

        // Everything ABOVE the separator is the dynamic range. The list sits
        // at the top of this menu and Add/Manage below it, so the separator is
        // the rule between the two rather than the start of anything.
        for (int i = startIdx - 1; i >= 0; i--)
            shortcutsMenu.Items.RemoveAt(i);

        bool hasAny = _vm!.Shortcuts.Count > 0;

        // The one divider kept on purpose, and only when it divides something.
        startSep.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;

        int insertAt = 0;
        foreach (var entry in _vm.Shortcuts)
            shortcutsMenu.Items.Insert(insertAt++, BuildShortcutsMenuItem(entry));
    }

    /// <summary>
    /// The picture beside a menu entry built in code.
    /// </summary>
    /// <remarks>
    /// The same MenuGlyph style the entries written in the XAML use, so the
    /// generated ones are not the only items in a menu with an empty icon
    /// column — which is what made the dynamic lists look like a different
    /// program. A fresh TextBlock per call: a visual belongs to one parent, and
    /// one instance shared between menu items would appear in whichever was
    /// built last.
    /// </remarks>
    private TextBlock MenuGlyph(string glyph) =>
        new() { Text = glyph, Style = (Style)FindResource("MenuGlyph") };

    /// <summary>
    /// Builds a quick save shortcut entry for the File menu. Header is the
    /// leaf folder name with the full path in the tooltip; the submenu offers
    /// "Set" (point quick save at this folder) and "Remove". These never open
    /// anything — that is what the Shortcuts menu is for.
    /// </summary>
    private MenuItem BuildFileMenuShortcutItem(ShortcutEntry entry)
    {
        var header = string.IsNullOrWhiteSpace(entry.Name) ? entry.Path : entry.Name;
        var parent = new MenuItem
        {
            // The folder glyph used to be part of the header text, two spaces
            // and all. It is an icon, so it is in the icon slot now, where it
            // lines up with every other entry in the menu.
            Header = header,
            Icon = MenuGlyph("📁"),
            ToolTip = entry.Path,
            Tag = entry
        };

        var set = new MenuItem { Header = "Set", Icon = MenuGlyph("⚡"), ToolTip = $"Quick save to {entry.Path}" };
        set.Click += (_, _) => _vm?.SetQuickSaveShortcutCommand.Execute(entry);

        var remove = new MenuItem { Header = "Remove", Icon = MenuGlyph("🗑") };
        remove.Click += (_, _) => _vm?.RemoveQuickSaveShortcutCommand.Execute(entry);

        parent.Items.Add(set);
        parent.Items.Add(remove);
        return parent;
    }

    /// <summary>
    /// Builds a MenuItem for the Shortcuts menu: header is the shortcut's
    /// friendly name (full path in the tooltip), with three sub-items —
    /// "Open" (opens the folder in Explorer), "Rename…" (prompts for a new
    /// display name), and "Remove" (removes the shortcut from the list).
    /// </summary>
    private MenuItem BuildShortcutsMenuItem(ShortcutEntry entry)
    {
        var header = string.IsNullOrWhiteSpace(entry.Name) ? entry.Path : entry.Name;
        var parent = new MenuItem
        {
            Header = header,
            Icon = MenuGlyph("📁"),
            ToolTip = entry.Path,
            Tag = entry
        };

        var open = new MenuItem { Header = "Open", Icon = MenuGlyph("📂"), Tag = entry };
        open.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.OpenShortcutCommand.Execute(entry);
        };

        var rename = new MenuItem { Header = "Rename…", Icon = MenuGlyph("✎"), Tag = entry };
        rename.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.RenameShortcutCommand.Execute(entry);
        };

        var remove = new MenuItem { Header = "Remove", Icon = MenuGlyph("🗑"), Tag = entry };
        remove.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.RemoveShortcutCommand.Execute(entry);
        };

        parent.Items.Add(open);
        parent.Items.Add(rename);
        parent.Items.Add(remove);
        return parent;
    }

    // FindMenuItem(parent, header) used to live here, matching a top-level menu
    // by its header text. Every caller now holds an x:Name instead, and it is
    // deliberately not replaced: the lookup could only fail by returning null,
    // every caller responded by returning quietly, and the result was a menu
    // that simply stopped filling itself in with nothing anywhere saying so.
    // It broke this way twice — once on a rename, once when the headers took
    // icons — because the failure looks exactly like "there is nothing to show".

    private static object? FindNamed(System.Collections.IList items, string name)
    {
        foreach (var item in items)
            if (item is FrameworkElement fe && fe.Name == name)
                return fe;
        return null;
    }

    /// <summary>
    /// Rebuilds the File → Recent submenu whenever the
    /// <see cref="MainViewModel.RecentVideos"/> collection changes — most
    /// commonly when a new video is loaded (promoted to the top of the
    /// list), but also when an entry is removed (file not found, manual
    /// remove) or the list is cleared.
    /// </summary>
    private void RecentVideos_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshRecentMenu();

    /// <summary>
    /// Rebuilds the dynamic entries in the File → Recent submenu. Each
    /// recent video gets one parent MenuItem whose header is the file's
    /// display name (with the full path as a tooltip) and which expands
    /// into "Open" (loads the video) and "Remove from list" (deletes the
    /// entry without opening it). When the list is empty, the
    /// "(no recent videos)" placeholder is shown instead.
    /// </summary>
    private void RefreshRecentMenu()
    {
        if (_vm == null) return;

        // RecentMenuItem is generated directly from x:Name="RecentMenuItem"
        // in MainWindow.xaml — reference it straight from the field rather
        // than re-deriving it by walking Header strings. That walk (via
        // FindMenuItem) previously assumed _Recent lived one level deeper
        // than it actually needed to be searched for, and was a needless
        // extra failure point; the generated field can't miss it.
        var recentMenu = RecentMenuItem;
        if (recentMenu == null) return;

        var startSep = FindNamed(recentMenu.Items, "RecentStartSeparator") as Separator;
        var endSep = FindNamed(recentMenu.Items, "RecentEndSeparator") as Separator;
        var emptyPlaceholder = FindNamed(recentMenu.Items, "RecentEmptyPlaceholder") as MenuItem;
        var clearItem = FindNamed(recentMenu.Items, "RecentClearItem") as MenuItem;
        if (startSep == null || endSep == null) return;

        int startIdx = recentMenu.Items.IndexOf(startSep);
        int endIdx = recentMenu.Items.IndexOf(endSep);

        // Remove anything currently between the two separators.
        for (int i = endIdx - 1; i > startIdx; i--)
            recentMenu.Items.RemoveAt(i);

        bool hasAny = _vm.RecentVideos.Count > 0;

        // Both stay hidden. They are insertion markers, and they used to show
        // themselves whenever there was a list — which put a rule above it and
        // another below. The "Viewed" caption introduces the list now, and
        // nothing else in these menus is ruled off any more.
        startSep.Visibility = Visibility.Collapsed;
        endSep.Visibility = Visibility.Collapsed;
        if (emptyPlaceholder != null) emptyPlaceholder.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;
        if (clearItem != null) clearItem.IsEnabled = hasAny;

        if (!hasAny) return;

        int insertAt = startIdx + 1;
        int position = 1; // 1-based position shown in the header for quick visual scanning
        foreach (var path in _vm.RecentVideos)
            recentMenu.Items.Insert(insertAt++, BuildRecentMenuItem(path, position++));
    }

    /// <summary>
    /// Builds a single recent-video entry for the File → Recent submenu.
    /// Header shows the file name prefixed with its 1-based position; the
    /// full path is shown in the tooltip. Expands into "Open" (loads the
    /// video) and "Remove from list" (deletes the entry without opening).
    /// </summary>
    private MenuItem BuildRecentMenuItem(string path, int position)
    {
        var fileName = System.IO.Path.GetFileName(path);
        var parent = new MenuItem
        {
            Header = $"{position}.  {fileName}",
            Icon = MenuGlyph("🎬"),
            ToolTip = path,
            Tag = path
        };

        // Single-click on the parent also opens — convenient for the
        // common case. Sub-items are for the less-frequent "remove" action.
        parent.Click += (_, _) => _vm?.OpenRecentVideoCommand.Execute(path);

        var open = new MenuItem { Header = "Open", Icon = MenuGlyph("📂"), Tag = path };
        open.Click += (_, _) => _vm?.OpenRecentVideoCommand.Execute(path);

        var remove = new MenuItem { Header = "Remove from list", Icon = MenuGlyph("🗑"), Tag = path };
        remove.Click += (_, _) => _vm?.RemoveRecentVideoCommand.Execute(path);

        parent.Items.Add(open);
        parent.Items.Add(remove);
        return parent;
    }

    /// <summary>
    /// Handler for the "Clear recent list" entry at the bottom of the
    /// File → Recent submenu. Confirms with the user, then asks the
    /// ViewModel to clear both the in-memory collection and settings.json.
    /// </summary>
    private void ClearRecentList_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        if (_vm.RecentVideos.Count == 0) return;

        var result = MessageBox.Show(
            $"Clear all {_vm.RecentVideos.Count} recent video(s)?",
            "Clear recent list",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _vm.ClearRecentVideosCommand.Execute(null);
    }

    // ------------------------------------------------------------------
    // Suffix menu
    // ------------------------------------------------------------------

    private void Suffixes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Wire up new entries so a later rename refreshes the menu;
        // unwire removed entries to avoid leaks.
        if (e.NewItems != null)
            foreach (SuffixEntry entry in e.NewItems)
                HookSuffixEntry(entry);
        if (e.OldItems != null)
            foreach (SuffixEntry entry in e.OldItems)
                UnhookSuffixEntry(entry);

        RefreshSuffixMenu();
    }

    private void HookSuffixEntry(SuffixEntry entry)
        => entry.PropertyChanged += SuffixEntry_PropertyChanged;

    private void UnhookSuffixEntry(SuffixEntry entry)
        => entry.PropertyChanged -= SuffixEntry_PropertyChanged;

    /// <summary>
    /// Fires when a suffix's Text changes (rename). Rebuild the menu so
    /// the new display text appears.
    /// </summary>
    private void SuffixEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SuffixEntry.Text))
            RefreshSuffixMenu();
    }

    /// <summary>
    /// Fires when a ViewModel property changes. We care about
    /// <see cref="MainViewModel.ActiveSuffixDisplay"/> — when it changes,
    /// the active suffix changed, so we rebuild the menu to update the
    /// ✓ checkmark on the newly-active entry.
    /// </summary>
    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveSuffixDisplay))
            RefreshSuffixMenu();

        if (e.PropertyName == nameof(MainViewModel.StatusLevel))
            FlashStatusStrip();


        // The tick marking the loaded playlist is drawn when the menu is
        // built, and loading one does not change the set of playlists — so
        // without this the mark stayed on whichever was loaded when the menu
        // was last rebuilt.
        if (e.PropertyName == nameof(MainViewModel.LoadedPlaylistPath))
            Dispatcher.BeginInvoke(RefreshPlaylistsMenu);
    }


    /// <summary>
    /// Colours the status strip for the level the ViewModel has just set.
    /// </summary>
    /// <remarks>
    /// From here rather than from a trigger in the XAML, for the same reason
    /// <see cref="PulseJobPanel"/> is: a Storyboard names its target, and
    /// <c>Begin(this)</c> is what hands it the window as the scope to resolve
    /// that name in.
    ///
    /// This was a DataTrigger with a BeginStoryboard in a Style, which looks
    /// equivalent and is not. A Style has no namescope of its own, so the
    /// TargetName resolved against the styled Grid, found nothing, and threw —
    /// inside the PropertyChanged that set the level, which was raised from
    /// JobProgress.Finished, which is called from a fire-and-forget
    /// CompleteAsync. The exception went nowhere and took the rest of
    /// completion with it: every finished job in the program was left with its
    /// panel up, its clock running and its headline frozen mid-operation.
    /// </remarks>
    private void FlashStatusStrip()
    {
        var key = _vm?.StatusLevel switch
        {
            StatusLevel.Ok => "StatusFlashOk",
            StatusLevel.Failed => "StatusFlashFailed",
            _ => null                      // back to Normal: nothing to begin
        };

        if (key is not null && FindResource(key) is Storyboard flash)
            flash.Begin(this);
    }

    /// <summary>
    /// Rebuilds the dynamic suffix entries in the Suffix menu, between
    /// <see cref="SuffixListStartSeparator"/> and
    /// <see cref="SuffixListEndSeparator"/>. Each entry's header shows
    /// the suffix in brackets (e.g. <c>[done]</c>); the active one gets
    /// a ✓ prefix. Clicking an entry sets it as active.
    /// </summary>
    private void RefreshSuffixMenu()
    {
        if (_vm == null) return;

        // Found by name. This menu was once renamed from "_Suffix" to
        // "_Options", the header lookup then matched nothing, and this whole
        // rebuild silently did nothing — newly added naming tags never appeared.
        // It happened a second time when the headers gained icons. A name is
        // checked by the compiler and cannot drift with the wording.
        var suffixMenu = TagsMenu;

        var startSep = FindNamed(suffixMenu.Items, "SuffixListStartSeparator") as Separator;
        var endSep = FindNamed(suffixMenu.Items, "SuffixListEndSeparator") as Separator;
        if (startSep == null || endSep == null) return;

        int startIdx = suffixMenu.Items.IndexOf(startSep);
        int endIdx = suffixMenu.Items.IndexOf(endSep);

        for (int i = endIdx - 1; i > startIdx; i--)
            suffixMenu.Items.RemoveAt(i);

        int insertAt = startIdx + 1;

        // "None" first, and built here rather than sitting in the XAML so it
        // carries the same tick as the tags and is chosen the same way. A tag
        // used to be mandatory — the list always held one and one was always
        // active — so every file the program wrote gained a bracket whether or
        // not that was wanted.
        // The tick is in the icon column now, rather than being two characters
        // of the header with four spaces standing in for it on every other
        // line. That column exists for exactly this, and the padding only ever
        // lined up while every entry was in the same font — which the active
        // one is not, since it is also the bold one.
        // "[no tag]" rather than "None", bracketed like every other entry: the
        // brackets are what a tag looks like in a filename, so a list where
        // one line has them and the rest do not reads as one odd line rather
        // than as a set of alternatives. It is bracketed in the toolbar for
        // the same reason.
        var noneActive = _vm.ActiveSuffixText.Length == 0;
        var noneItem = new MenuItem
        {
            Header = "[no tag]",
            Icon = MenuGlyph(noneActive ? "✓" : "🏷"),
            ToolTip = "Write output under the source's own name, with no tag added. " +
                      "Saved beside the source that means the same filename, so you will be " +
                      "asked whether to replace it.",
            FontWeight = noneActive ? FontWeights.SemiBold : FontWeights.Normal
        };

        // Red while it is the active one, as on the toolbar: every other tag
        // being on is a file gaining a bracket, and this one being on is the
        // state where output keeps the source's own name — the one worth
        // noticing before a merge rather than after. Only while active; red on
        // an entry that is merely available would be warning about a choice
        // nobody has made.
        if (noneActive && TryFindResource("StatusError") is Brush danger)
            noneItem.Foreground = danger;
        noneItem.Click += (_, _) => _vm?.ClearActiveSuffixCommand.Execute(null);
        suffixMenu.Items.Insert(insertAt++, noneItem);

        foreach (var entry in _vm.Suffixes)
        {
            // Compare against the active text directly. This used to scrape
            // it out of ActiveSuffixDisplay, which broke the moment that
            // label's wording changed.
            var isActive = string.Equals(entry.Text, _vm.ActiveSuffixText,
                                         StringComparison.OrdinalIgnoreCase);
            // Bracketed, which is how the tag reads in a filename and how it
            // is written everywhere else it is named — the tooltip below, the
            // toolbar, the example line above. Bare text here was the one
            // place it was spelled differently.
            //
            // Underscores doubled: a MenuItem header renders through
            // AccessText, where a single one is an access-key marker, so a tag
            // named my_tag arrived as "mytag" with the t underlined.
            var item = new MenuItem
            {
                Header = $"[{entry.Text.Replace("_", "__")}]",
                Icon = MenuGlyph(isActive ? "✓" : "🏷"),
                ToolTip = $"Click to set [{entry.Text}] as the active tag",
                Tag = entry,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal
            };
            item.Click += (_, _) => _vm?.SetActiveSuffixCommand.Execute(entry);
            suffixMenu.Items.Insert(insertAt++, item);
        }
    }

    // ------------------------------------------------------------------
    // Playlist menu (dynamic — built from disk)
    // ------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the dynamic entries in the Playlist menu, between
    /// <see cref="PlaylistStartSeparator"/> and
    /// <see cref="PlaylistEndSeparator"/>. The menu's static layout is:
    /// Playlist folder / Refresh list / [startSep] / (placeholder or
    /// playlist items) / [endSep] / New playlist… / Add current video to
    /// playlist…. Each .pls file in the active playlist folder becomes a
    /// parent MenuItem that expands to show:
    /// <list type="bullet">
    ///   <item>Its video entries, numbered 1, 2, 3, … (click to open in MPC-HC;
    ///         each has a "Remove entry" sub-item).</item>
    ///   <item>A separator.</item>
    ///   <item>"Open playlist file" — shell-executes the .pls in the default handler.</item>
    ///   <item>"Add current video" — adds the currently-loaded video to this playlist.</item>
    ///   <item>"Delete playlist" — removes the .pls file from disk (with confirmation).</item>
    /// </list>
    /// When the playlist folder is unset or empty, the
    /// <see cref="PlaylistEmptyPlaceholder"/> is shown instead (and both
    /// separators collapse to avoid a stray divider line).
    /// </summary>
    private void RefreshPlaylistsMenu()
    {
        if (_vm == null) return;

        var playlistMenu = PlaylistMenu;

        var startSep = FindNamed(playlistMenu.Items, "PlaylistStartSeparator") as Separator;
        var endSep = FindNamed(playlistMenu.Items, "PlaylistEndSeparator") as Separator;
        var emptyPlaceholder = FindNamed(playlistMenu.Items, "PlaylistEmptyPlaceholder") as MenuItem;
        if (startSep == null || endSep == null) return;

        int startIdx = playlistMenu.Items.IndexOf(startSep);
        int endIdx = playlistMenu.Items.IndexOf(endSep);

        // Remove anything currently between the two separators, EXCEPT the
        // placeholder (which lives inside the separator pair so it shows
        // exactly where the playlist list would be when empty).
        for (int i = endIdx - 1; i > startIdx; i--)
        {
            if (playlistMenu.Items[i] == emptyPlaceholder) continue;
            playlistMenu.Items.RemoveAt(i);
        }

        // The path itself, not the label that describes it. This used to cut
        // the path back out of the header text, which meant the header and the
        // list below it could disagree about whether a folder was even set —
        // and did, the moment the header gained anything to say beyond a path.
        var plsFolder = _vm.PlaylistFolderPath;

        bool isSet = plsFolder.Length > 0;
        bool hasFolder = _vm.PlaylistFolderExists;
        bool hasAny = false;

        if (hasFolder)
        {
            // Every format PlaylistService understands, not just .pls — an
            // m3u8 in the folder is a playlist and belongs in this menu.
            var playlists = System.IO.Directory.EnumerateFiles(plsFolder)
                .Where(PlaylistService.IsPlaylist)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            hasAny = playlists.Count > 0;

            int insertAt = startIdx + 1;
            foreach (var plsPath in playlists)
                playlistMenu.Items.Insert(insertAt++, BuildPlaylistMenuItem(plsPath));
        }

        // The pair stays hidden: markers, not rules. They used to appear
        // whenever there were playlists, which ruled the list off top and
        // bottom — and the captions below it say where the list ends without
        // a line. The one rule in this menu is the static one above Refresh.
        startSep.Visibility = Visibility.Collapsed;
        endSep.Visibility = Visibility.Collapsed;
        if (emptyPlaceholder != null)
        {
            emptyPlaceholder.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;

            // Three states, not two. A folder that is set but missing — the
            // drive letter moved, the disk is out — is neither "none set" nor
            // "none in it", and saying either of those while the header names
            // the folder is the contradiction this had.
            emptyPlaceholder.Header = hasFolder ? "(no playlists in folder)"
                : isSet ? "(the playlist folder is not there — set it again)"
                : "(no playlist folder set)";
        }
    }

    /// <summary>
    /// Builds a single playlist's parent MenuItem for the Playlist menu.
    /// Header is the playlist's filename followed by how many entries it
    /// lists (e.g. <c>"my_videos.pls (50)"</c>); expands into the action
    /// items first (View playlist content / Load this playlist / Add current
    /// video / Remove missing entries / Delete playlist), then a separator,
    /// then the numbered video entries (1, 2, 3, …).
    /// </summary>
    private MenuItem BuildPlaylistMenuItem(string plsPath)
    {
        var plsName = System.IO.Path.GetFileName(plsPath);
        // Entries listed, not entries present. Counting what still exists
        // would mean a File.Exists per video on every rebuild, which is the
        // per-video stat cost the lazy submenu below exists to avoid.
        var entryCount = _vm?.CountPlaylistEntriesForMenu(plsPath) ?? 0;
        // Exactly one playlist is the loaded one, and nothing in this menu
        // said which — so a list of eight clipboards gave no clue where "Add
        // current video" was about to send anything. The tick is the one glyph
        // that means "this one" without having to be explained.
        var isLoaded = !string.IsNullOrEmpty(_vm?.LoadedPlaylistPath) &&
                       string.Equals(System.IO.Path.GetFullPath(_vm!.LoadedPlaylistPath!),
                                     System.IO.Path.GetFullPath(plsPath),
                                     StringComparison.OrdinalIgnoreCase);

        var parent = new MenuItem
        {
            // A MenuItem's string header renders through AccessText, which
            // swallows a single underscore as an access-key marker — without
            // doubling them, "my_videos.pls" shows up as "myvideos.pls".
            Header = $"{plsName.Replace("_", "__")} ({entryCount})",
            Icon = MenuGlyph(isLoaded ? "✔" : "📋"),
            ToolTip = isLoaded ? plsPath + "  — loaded" : plsPath,
            Tag = plsPath
        };

        // Action items first — at the top of the submenu so they're always
        // reachable without scrolling past a long list of video entries.
        // View playlist content (shell-execute the .pls in default handler)
        var openFile = new MenuItem { Header = "View playlist content", Icon = MenuGlyph("👁"), Tag = plsPath };
        openFile.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.OpenPlaylistFileCommand.Execute(plsPath);
        };
        parent.Items.Add(openFile);

        // Load this playlist — make it the active target for "Add current
        // video to playlist" without prompting. Distinct from "Open
        // playlist file" which just shell-executes the .pls in the user's
        // default player without remembering it.
        var loadThis = new MenuItem { Header = "Load this playlist", Icon = MenuGlyph("📂"), Tag = plsPath };
        loadThis.Click += (_, e) =>
        {
            e.Handled = true;
            // Loading also starts playback at the first entry that still exists.
            _vm?.LoadPlaylistAndPlayCommand.Execute(plsPath);
        };
        parent.Items.Add(loadThis);

        // Add current video to this playlist.
        //
        // Enabled only when there is a video to add. The command already refused
        // without one, but refusing is not the same as saying so beforehand: the
        // entry looked available with nothing loaded, and clicking it produced a
        // dialog explaining why it could not have worked. The Playlist menu's own
        // "Add current video to playlist…" has been gated on this from the start;
        // this one is built in code, so it never picked up the command's
        // CanExecute the way a XAML Command binding would.
        //
        // Bound rather than assigned, because these items are built when the
        // playlist list changes and a video can be loaded long afterwards —
        // a value set once here would be stale by the time it mattered.
        var addCurrent = new MenuItem { Header = "Add current video", Icon = MenuGlyph("➕"), Tag = plsPath };
        if (_vm is not null)
        {
            addCurrent.SetBinding(UIElement.IsEnabledProperty,
                new Binding(nameof(MainViewModel.HasActiveVideo)) { Source = _vm });
        }
        addCurrent.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.AddCurrentToPlaylistNamedCommand.Execute(plsPath);
        };
        parent.Items.Add(addCurrent);

        // Drops entries whose files are gone, then rebuilds so the list and the
        // header count reflect it. No ellipsis in the header and no
        // confirmation behind it: this runs on the click and reports to the
        // status bar, matching the per-entry Remove below.
        //
        // Deliberately never grayed out, either: knowing in advance whether
        // anything is missing means a File.Exists per entry, and doing that on
        // every menu rebuild is exactly the stat storm the lazy submenu avoids.
        // Clicking it when nothing is gone simply says so afterwards.
        // Offered above removal deliberately: a file that is not where the
        // playlist says has usually been moved, not deleted, and finding it
        // again is almost always the better answer than dropping the entry.
        var relocate = new MenuItem
        {
            Header = "Find moved files…",
            Icon = MenuGlyph("🔍"),
            Tag = plsPath,
            ToolTip = "Searches a folder you choose for the files of entries that are confirmed " +
                      "gone, and repoints the playlist at them instead of removing them."
        };
        relocate.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.RelocatePlaylistEntriesCommand.Execute(plsPath);
        };
        parent.Items.Add(relocate);

        var removeMissing = new MenuItem
        {
            Header = "Remove missing entries",
            Icon = MenuGlyph("🧹"),
            Tag = plsPath,
            ToolTip = "Removes entries whose file is gone and whose drive is connected — " +
                      "immediately, without asking. Entries on a disconnected drive cannot " +
                      "be checked and are always left alone."
        };
        removeMissing.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.RemoveMissingPlaylistEntriesCommand.Execute(plsPath);
        };
        parent.Items.Add(removeMissing);

        // Delete playlist
        var delete = new MenuItem { Header = "Delete playlist…", Icon = MenuGlyph("🗑"), Tag = plsPath };
        delete.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.DeletePlaylistCommand.Execute(plsPath);
        };
        parent.Items.Add(delete);

        parent.Items.Add(new Separator());

        // The video entries are populated lazily, on first submenu open.
        //
        // Building them eagerly meant that every rebuild of this menu — at
        // startup, and again on every playlist change — read each .pls from
        // disk, created a MenuItem per video, and called File.Exists once per
        // video, all synchronously on the UI thread. Across a realistic
        // playlist folder that is hundreds of stat calls against whatever
        // drive the videos live on; if it has spun down, the first ones block
        // for seconds. None of that work is visible until the user actually
        // opens this specific playlist's submenu, so it waits until they do.
        var placeholder = new MenuItem { Header = "(loading…)", IsEnabled = false };
        parent.Items.Add(placeholder);

        bool populated = false;
        parent.SubmenuOpened += (_, _) =>
        {
            if (populated) return;
            populated = true;

            List<PlaylistEntry> entries;
            try
            {
                entries = _vm != null
                    ? _vm.ClassifyPlaylistEntriesForMenu(plsPath)
                    : new List<PlaylistEntry>();
            }
            catch
            {
                entries = new List<PlaylistEntry>();
            }

            parent.Items.Remove(placeholder);

            if (entries.Count == 0)
            {
                parent.Items.Add(new MenuItem { Header = "(empty)", IsEnabled = false });
                return;
            }

            int position = 1;
            foreach (var entry in entries)
                parent.Items.Add(BuildPlaylistEntryItem(plsPath, position++, entry));
        };

        return parent;
    }

    /// <summary>
    /// Builds a single video-entry MenuItem for a playlist's submenu. The
    /// header shows the entry's 1-based position, the video's filename, and —
    /// when the file is not simply there — which of the two possible reasons
    /// applies. Clicking a present entry opens it in MPC-HC; a "Remove"
    /// sub-item takes it out of the .pls file.
    /// </summary>
    private MenuItem BuildPlaylistEntryItem(string plsPath, int index, PlaylistEntry entry)
    {
        var videoPath = entry.Path;
        // Underscores doubled for the same AccessText reason as the playlist
        // header above; video filenames carry them far more often than
        // playlist names do.
        var fileName = System.IO.Path.GetFileName(videoPath).Replace("_", "__");
        var fileExists = entry.Status == PlaylistEntryStatus.Present;

        // Three states, three labels. "(missing)" is now reserved for a file
        // whose drive is connected and which genuinely is not there. A file
        // behind a detached drive is labeled for what is actually known —
        // that the drive is away — rather than given a verdict the app is in
        // no position to reach.
        var (suffix, color) = entry.Status switch
        {
            PlaylistEntryStatus.Present => (string.Empty, ThemeService.Brush(nameof(ThemePalette.MenuBarForeground))),
            PlaylistEntryStatus.Missing => ("  (missing)", ThemeService.Brush(nameof(ThemePalette.StatusError))),
            _                           => ("  (drive offline)", ThemeService.Brush(nameof(ThemePalette.StatusWarn)))
        };

        var parent = new MenuItem
        {
            Header = $"{index}.  {fileName}{suffix}",
            Icon = MenuGlyph("🎬"),
            ToolTip = entry.Status == PlaylistEntryStatus.Unknown
                ? $"{videoPath}\n\n{DriveAvailability.RootOf(videoPath)} is not connected — " +
                  "whether this file still exists is unknown."
                : videoPath,
            Tag = videoPath,
            // NOTE: do NOT use `null` here — assigning null to a dependency
            // property becomes an explicit local value that overrides the
            // implicit MenuItem style's Foreground=Black, making the header
            // text invisible. Use an explicit brush so present entries render
            // the same as every other menu item, while the other two states
            // keep their warning color.
            Foreground = color
        };
        // Only a file that is actually there can be opened; the other two
        // states would produce nothing but an error dialog.
        if (fileExists)
            parent.Click += (_, _) => _vm?.OpenPlaylistEntryCommand.Execute(videoPath);

        // NOTE: MenuItem.Click is a bubbling routed event — a click on a
        // child MenuItem bubbles up to the parent MenuItem and fires its
        // Click handler too. Without e.Handled = true, clicking "Open"
        // would open the video twice (once for the child, once for the
        // parent), and clicking "Remove entry" would also open the video
        // (parent's Click handler fires after the remove runs). Setting
        // Handled = true on every child click stops the bubbling.
        if (fileExists)
        {
            var open = new MenuItem { Header = "Open", Icon = MenuGlyph("📂"), Tag = videoPath };
            open.Click += (_, e) =>
            {
                e.Handled = true;
                _vm?.OpenPlaylistEntryCommand.Execute(videoPath);
            };
            parent.Items.Add(open);
        }

        // Removal by hand stays available whatever the status, including for an
        // entry that cannot currently be verified — that is the user's call to
        // make. What changed is that the bulk action no longer makes it for
        // them on the strength of a disconnected drive.
        var remove = new MenuItem { Header = "Remove", Icon = MenuGlyph("🗑"), Tag = (plsPath, index) };
        remove.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.RemovePlaylistEntryCommand.Execute((plsPath, index));
        };
        parent.Items.Add(remove);

        return parent;
    }

    /// <summary>
    /// Handler for Playlist → "Refresh list". Re-reads the playlist
    /// folder from disk and rebuilds the menu. Useful if the user adds
    /// or removes .pls files outside this app while it's running.
    /// </summary>
    private void RefreshPlaylists_Click(object sender, RoutedEventArgs e)
        => RefreshPlaylistsMenu();

    /// <summary>
    /// Handler for Playlist → "New playlist…". Delegates to the
    /// ViewModel's NewPlaylist command, which prompts for a name and
    /// creates the file. The PlaylistsChanged event then triggers a
    /// menu rebuild.
    /// </summary>
    private void NewPlaylist_Click(object sender, RoutedEventArgs e)
        => _vm?.NewPlaylistCommand.Execute(null);

    /// <summary>
    /// Handler for Playlist → "Load playlist…". Delegates to the
    /// ViewModel's LoadPlaylist command with a null argument, which
    /// opens a file picker so the user can browse for any .pls file
    /// (not just ones in the configured playlist folder).
    /// </summary>
    private void LoadPlaylist_Click(object sender, RoutedEventArgs e)
        => _vm?.LoadPlaylistAndPlayCommand.Execute(null);

    /// <summary>
    /// Handler for Playlist → "Clear loaded playlist". Delegates to
    /// the ViewModel's ClearLoadedPlaylist command, which forgets the
    /// currently-loaded playlist path. The menu label and status bar
    /// both update reactively via the bound properties.
    /// </summary>
    private void ClearLoadedPlaylist_Click(object sender, RoutedEventArgs e)
        => _vm?.ClearLoadedPlaylistCommand.Execute(null);

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Handles clicks on a bookmark's start/end timestamp Hyperlink in the
    /// list. We use a Click event handler here instead of binding Command
    /// directly on the Hyperlink: Hyperlink is a FrameworkContentElement
    /// (it lives in the TextBlock's inline content, not the visual tree),
    /// so a Command="{Binding ..., RelativeSource={RelativeSource
    /// AncestorType=ListView}}" binding can silently fail to resolve —
    /// the Command stays null and clicking it does nothing. The routed
    /// Click event, by contrast, reliably bubbles up regardless. The
    /// specific time to seek to is carried on Tag (bound to StartSeconds
    /// or EndSeconds per-instance in the XAML).
    /// </summary>
    private void TimestampHyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { Tag: double seconds })
            _vm?.SeekToTimeCommand.Execute(seconds);
    }

    /// <summary>
    /// Nudges one end of the row the arrow belongs to.
    /// </summary>
    /// <remarks>
    /// A handler rather than a command because two things have to travel: which
    /// row, and which of its four arrows. A command takes one parameter, so the
    /// direction went in it and the row came from the selection — but these
    /// arrows appear on whichever row the pointer is over, which need not be
    /// the selected one. The row comes off the button's own DataContext, which
    /// is the bookmark it is drawn on; the direction rides on Tag.
    /// </remarks>
    private void NudgeArrow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Bookmark b, Tag: string request })
            _vm?.NudgeSecond(b, request);
    }

    /// <summary>
    /// Flashes the job panel green once, on <see cref="JobProgress.Finished"/>.
    /// </summary>
    /// <remarks>
    /// <c>Begin(this)</c> rather than a parameterless <c>Begin()</c>: the
    /// storyboard names <c>JobCompleteGlow</c> rather than carrying a
    /// reference to it, and the window is what owns that name in its scope.
    /// </remarks>
    private void PulseJobPanel()
    {
        if (FindResource("JobCompletePulse") is Storyboard pulse)
            pulse.Begin(this);
    }

    /// <summary>
    /// Seeks the player to wherever the timeline was clicked.
    /// </summary>
    /// <remarks>
    /// The same command the timestamps in the list use, given a time worked out
    /// from where the click landed instead of one read off a bookmark. The bar
    /// already showed the position; this makes it somewhere you can put it.
    ///
    /// Measured against the inner area rather than the Border so the border
    /// line, which stands for no time at all, does not shift every seek by a
    /// pixel's worth of video.
    /// </remarks>
    private void Timeline_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;

        var duration = _vm.Session.VideoDurationSeconds;
        var width = TimelineArea.ActualWidth;

        // No duration means no scale to map the click onto, and the bar is
        // drawing nothing either.
        if (duration <= 0 || width <= 0) return;

        var x = e.GetPosition(TimelineArea).X;

        // A press that lands on a bracket resizes the cut instead of seeking
        // — the same gesture a person already reaches for to move a mark,
        // just closer to the mark itself than a click-and-retype.
        var grabbed = FindBracketNear(x, width, duration);
        if (grabbed is { } g)
        {
            _draggingBookmark = g.Bookmark;
            _draggingIsStart = g.IsStart;
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
            return;
        }

        var fraction = Math.Clamp(x / width, 0, 1);
        _vm.SeekToTimeCommand.Execute(fraction * duration);
    }

    // ------------------------------------------------------------------
    // Dragging a bracket to resize a cut
    // ------------------------------------------------------------------

    /// <summary>How close, in pixels, a press has to land to a bracket to grab it rather than seek.</summary>
    private const double BracketGrabPixels = 6;

    private Bookmark? _draggingBookmark;
    private bool _draggingIsStart;

    /// <summary>
    /// The nearest bracket to <paramref name="pointerX"/> within
    /// <see cref="BracketGrabPixels"/>, or null when nothing is that close.
    /// </summary>
    /// <remarks>
    /// Every bookmark's own two ends are candidates, not just the selected
    /// row's — a cut can be resized from the bar without selecting it first,
    /// the same as it can already be sought to without selecting it.
    /// </remarks>
    private (Bookmark Bookmark, bool IsStart)? FindBracketNear(double pointerX, double width, double duration)
    {
        if (_vm is null) return null;

        Bookmark? bestBookmark = null;
        bool bestIsStart = false;
        var bestDistance = double.PositiveInfinity;

        void Consider(Bookmark b, bool isStart, double seconds)
        {
            var x = Math.Clamp(seconds / duration, 0, 1) * width;
            var distance = Math.Abs(x - pointerX);
            if (distance > BracketGrabPixels || distance >= bestDistance) return;

            bestBookmark = b;
            bestIsStart = isStart;
            bestDistance = distance;
        }

        foreach (var b in _vm.Session.Bookmarks)
        {
            Consider(b, true, b.StartSeconds);
            if (b.IsValid) Consider(b, false, b.EndSeconds);
        }

        return bestBookmark is null ? null : (bestBookmark, bestIsStart);
    }

    /// <summary>How close, in pixels, a dragged bracket has to come to an edit point to land on it.</summary>
    private const double SnapPixels = 7;

    /// <summary>
    /// Pulls a dragged bracket onto the nearest edit point when it comes
    /// within <see cref="SnapPixels"/>, or returns the time unchanged.
    /// </summary>
    /// <remarks>
    /// Measured in pixels rather than seconds on purpose: the pointer is
    /// being aimed on screen, so what "close" means is a distance on screen.
    /// The same seven pixels is a fraction of a second on a short video and
    /// several seconds on a long one, which is exactly right — on the long
    /// one there is no way to aim finer than that anyway.
    ///
    /// Holding Alt passes straight through. A snap that is nearly always
    /// wanted still has to be refusable, and refusing it per-drag beats a
    /// setting the one time in fifty it gets in the way.
    /// </remarks>
    private double SnapDragSeconds(double seconds, double width, double duration)
    {
        if (_vm is null || !_vm.SnapToEditPoints) return seconds;
        if (_vm.SnapPoints.Count == 0 || width <= 0 || duration <= 0) return seconds;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return seconds;

        var secondsPerPixel = duration / width;
        var radius = SnapPixels * secondsPerPixel;

        var best = seconds;
        var bestDistance = radius;

        foreach (var point in _vm.SnapPoints)
        {
            var distance = Math.Abs(point - seconds);
            if (distance > bestDistance) continue;

            best = point;
            bestDistance = distance;
        }

        return best;
    }

    private void Timeline_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingBookmark is null) return;
        EndBracketDrag();
        ((UIElement)sender).ReleaseMouseCapture();
    }

    /// <summary>
    /// Safety net for a drag that ends some other way — the window losing
    /// focus mid-drag, say — rather than an ordinary mouse-up. Harmless to
    /// call twice: <see cref="Timeline_MouseLeftButtonUp"/> already clears
    /// <see cref="_draggingBookmark"/> before releasing capture, so the
    /// <see cref="EndBracketDrag"/> this triggers in turn is a no-op.
    /// </summary>
    private void Timeline_LostMouseCapture(object sender, MouseEventArgs e) => EndBracketDrag();

    private void EndBracketDrag()
    {
        if (_draggingBookmark is null) return;

        _vm?.CommitBracketDrag(_draggingBookmark, _draggingIsStart, RangesControl.DragSeconds);
        _draggingBookmark = null;
        RangesControl.DragBookmark = null;
    }

    // ------------------------------------------------------------------
    // Setting a clip's speed where there is no room for a slider
    // ------------------------------------------------------------------

    /// <summary>
    /// The eight stops the row list's slider snaps to, in order.
    /// </summary>
    /// <remarks>
    /// Listed rather than stepped by arithmetic so the wheel lands on exactly
    /// the same values the slider and the menu offer. A quarter added to 0.75
    /// is not reliably 1.0 in binary, and a clip left at 0.9999 would read as
    /// normal while still being re-encoded as though it were not.
    /// </remarks>
    private static readonly double[] ClipSpeeds = { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0 };

    /// <summary>
    /// Sets the clicked clip's speed from the storyboard's context menu.
    /// </summary>
    private void ClipSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        if (item.DataContext is not Bookmark bookmark) return;
        if (!double.TryParse(item.Tag as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed)) return;

        bookmark.Speed = speed;
    }

    /// <summary>
    /// Ctrl and the wheel steps the speed of whichever card the pointer is
    /// over, one stop at a time.
    /// </summary>
    /// <remarks>
    /// Ctrl rather than the wheel alone: the wheel's own job here is to scroll
    /// the list, and taking that away to adjust whatever happened to be under
    /// the pointer would be a trap. Unhandled without Ctrl, so scrolling is
    /// untouched.
    ///
    /// The card under the pointer, not the selected one — the pointer is
    /// already doing the pointing, and having to select a card first to adjust
    /// it would be a step for nothing.
    /// </remarks>
    private void Storyboard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.OriginalSource is not DependencyObject source) return;

        var container = ItemsControl.ContainerFromElement((ItemsControl)sender, source) as FrameworkElement;
        if (container?.DataContext is not Bookmark bookmark) return;

        var index = Array.FindIndex(ClipSpeeds, s => Math.Abs(s - bookmark.Speed) < 0.01);
        if (index < 0) index = Array.IndexOf(ClipSpeeds, 1.0);

        index = Math.Clamp(index + (e.Delta > 0 ? 1 : -1), 0, ClipSpeeds.Length - 1);
        bookmark.Speed = ClipSpeeds[index];

        // Otherwise the list scrolls as well as the speed changing.
        e.Handled = true;
    }

    // ------------------------------------------------------------------
    // Committing a chapter name
    // ------------------------------------------------------------------

    /// <summary>
    /// Enter accepts what has been typed into a chapter name; Escape puts
    /// back what was there before. Either way the box gives up focus.
    /// </summary>
    private void ChapterName_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;

        if (e.Key == Key.Enter)
        {
            CommitTextBox(box);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Re-reads the bookmark, discarding the edit. The binding only
            // writes on focus leaving, so nothing has been changed yet.
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Makes a click anywhere outside a text box commit what is in it.
    /// </summary>
    /// <remarks>
    /// The chapter name writes back on losing focus, and most of this window
    /// is panels and borders — none of which are focusable, so clicking them
    /// left focus sitting in the box. A name could be typed, clicked away
    /// from, and look accepted while never having been written anywhere.
    ///
    /// Preview rather than the bubbling event, so this runs before whatever
    /// was clicked gets to act on it, and nothing is marked handled — the
    /// click still does whatever it was going to do.
    /// </remarks>
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.FocusedElement is not TextBox box) return;

        // A click inside the box itself is just a click inside the box.
        if (e.OriginalSource is DependencyObject source && box.IsAncestorOf(source)) return;

        CommitTextBox(box);
    }

    /// <summary>
    /// Writes a text box's value back to what it is bound to, and lets go of
    /// the keyboard.
    /// </summary>
    /// <remarks>
    /// The binding is updated by hand rather than left to the focus change to
    /// do. Clearing focus does normally raise it, but what is being clicked
    /// can take focus itself — or take none at all — and relying on the one
    /// to cause the other is how the edit went missing in the first place.
    /// Updating twice is harmless; not updating at all is not.
    /// </remarks>
    private static void CommitTextBox(TextBox box)
    {
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        Keyboard.ClearFocus();
    }

    // ------------------------------------------------------------------
    // Hover-scrub preview
    // ------------------------------------------------------------------

    /// <summary>
    /// Debounces the actual frame fetch. Reset on every mouse move rather
    /// than left running, so dragging across the bar asks ffmpeg for one
    /// frame — wherever the pointer stops — rather than one per pixel
    /// crossed on the way there.
    /// </summary>
    private DispatcherTimer? _hoverPreviewTimer;

    /// <summary>Where the pointer was when the timer was last restarted.</summary>
    private double _hoverPreviewSeconds;

    /// <summary>What the popup is currently showing a frame for, so landing within the same half-second again is a no-op.</summary>
    private double _hoverPreviewLoadedSeconds = double.NaN;

    /// <summary>Cancels a fetch the pointer has already moved on from.</summary>
    private CancellationTokenSource? _hoverPreviewCts;

    /// <summary>
    /// Opens the preview and moves it to follow the pointer; the frame itself
    /// arrives later, once <see cref="_hoverPreviewTimer"/> fires.
    /// </summary>
    /// <remarks>
    /// The popup's width is fixed, so centring it on the pointer is simple
    /// arithmetic rather than a measure pass — <c>85</c> is half of it plus
    /// the border and padding around the image.
    /// </remarks>
    private void Timeline_MouseMove(object sender, MouseEventArgs e)
    {
        if (_vm is null || string.IsNullOrWhiteSpace(_vm.Session.VideoPath))
        {
            HoverPreviewPopup.IsOpen = false;
            return;
        }

        var duration = _vm.Session.VideoDurationSeconds;
        var width = TimelineArea.ActualWidth;
        if (duration <= 0 || width <= 0)
        {
            HoverPreviewPopup.IsOpen = false;
            return;
        }

        var x = e.GetPosition(TimelineArea).X;
        var fraction = Math.Clamp(x / width, 0, 1);
        _hoverPreviewSeconds = fraction * duration;

        if (_draggingBookmark is { } dragging)
        {
            // Clamped here, in the View, so the bracket never visually
            // crosses a limit it will be refused at anyway — see
            // GetBracketDragRange's remarks on why this does not touch the
            // bookmark itself.
            var (floor, ceiling) = _vm.GetBracketDragRange(dragging, _draggingIsStart);

            // Snapped before clamping, so a snap that lands outside what the
            // drag allows is pulled back to the limit rather than taken.
            var wanted = SnapDragSeconds(_hoverPreviewSeconds, width, duration);

            var clamped = floor > ceiling
                ? _draggingIsStart ? dragging.StartSeconds : dragging.EndSeconds
                : Math.Clamp(wanted, Math.Max(0, floor), ceiling);

            RangesControl.DragBookmark = dragging;
            RangesControl.DragIsStart = _draggingIsStart;
            RangesControl.DragSeconds = clamped;

            // The popup's clock follows the bracket, not the raw pointer —
            // otherwise it would claim a time the drag just refused.
            _hoverPreviewSeconds = clamped;
        }
        else
        {
            var grabbed = FindBracketNear(x, width, duration);
            ((Border)sender).Cursor = grabbed is null ? Cursors.Hand : Cursors.SizeWE;
        }

        HoverPreviewTime.Text = Bookmark.FormatTime(_hoverPreviewSeconds);
        ShowHoverChapterName(_hoverPreviewSeconds);
        HoverPreviewPopup.PlacementTarget = TimelineArea;
        HoverPreviewPopup.HorizontalOffset = x - 85;

        // A Popup is a real OS window, and IsHitTestVisible="False" only
        // keeps WPF's own hit-testing from routing clicks to it — it does
        // not stop Windows from reporting the cursor as having left this
        // window the moment the popup's actual on-screen rectangle sits
        // under it. That reads as a MouseLeave here, closes the popup, puts
        // the cursor back over this window, reopens it, and repeats —
        // indefinitely, for as long as the overlap holds, which is exactly
        // the flicker this was. The popup is roughly 114px tall; -105 left
        // its bottom edge 9px back inside the 22px bar the cursor sits on.
        // -140 clears it with margin to spare.
        HoverPreviewPopup.VerticalOffset = -140;
        HoverPreviewPopup.IsOpen = true;

        _hoverPreviewTimer ??= MakeHoverPreviewTimer();
        _hoverPreviewTimer.Stop();
        _hoverPreviewTimer.Start();
    }

    /// <summary>
    /// Names the cut the pointer is inside, on the line under the preview's
    /// clock — or hides that line when the pointer is between cuts.
    /// </summary>
    /// <remarks>
    /// Only while chapter names are in use. With the setting off a cut has no
    /// name worth reading, and the generated "Chapter 3" that the list would
    /// show in its place says nothing the number between the brackets has not
    /// already said.
    ///
    /// The whole span between a pair of brackets answers, not just the
    /// brackets themselves: the name belongs to the range, and the range is
    /// what the pointer is in.
    /// </remarks>
    private void ShowHoverChapterName(double seconds)
    {
        if (_vm is null || !_vm.UseChapterNames)
        {
            HoverPreviewChapter.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var b in _vm.Session.Bookmarks)
        {
            if (!b.IsValid || !b.HasLabel) continue;
            if (seconds < b.StartSeconds || seconds > b.EndSeconds) continue;

            HoverPreviewChapter.Text = b.Label;
            HoverPreviewChapter.Visibility = Visibility.Visible;
            return;
        }

        HoverPreviewChapter.Visibility = Visibility.Collapsed;
    }

    private DispatcherTimer MakeHoverPreviewTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += HoverPreviewTimer_Tick;
        return timer;
    }

    private void Timeline_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverPreviewTimer?.Stop();
        _hoverPreviewCts?.Cancel();
        HoverPreviewPopup.IsOpen = false;
    }

    /// <summary>
    /// Fetches the frame the pointer has settled on, once it has stopped
    /// moving for a whole interval.
    /// </summary>
    private async void HoverPreviewTimer_Tick(object? sender, EventArgs e)
    {
        _hoverPreviewTimer!.Stop();
        if (_vm is null) return;

        var seconds = _hoverPreviewSeconds;

        // Close enough to what is already on screen that a fresh decode
        // would not show anything different — the common case while the
        // pointer sits nearly still between two debounced ticks.
        if (Math.Abs(seconds - _hoverPreviewLoadedSeconds) < 0.4) return;

        _hoverPreviewCts?.Cancel();
        _hoverPreviewCts?.Dispose();
        var cts = new CancellationTokenSource();
        _hoverPreviewCts = cts;

        try
        {
            var png = await _vm.ExtractHoverFrameAsync(seconds, cts.Token);

            // Not just cancelled — stale, in case a newer request's result
            // ever arrived first and this slower one landed after it. Two
            // fetches a seek apart can finish in either order; only the one
            // still current when it returns gets to draw.
            if (!ReferenceEquals(cts, _hoverPreviewCts) || cts.IsCancellationRequested || png is null) return;

            var image = new BitmapImage();
            using (var stream = new MemoryStream(png))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }
            image.Freeze();

            HoverPreviewImage.Source = image;
            _hoverPreviewLoadedSeconds = seconds;
        }
        catch (OperationCanceledException) { }
        catch
        {
            // Decoration. A frame that failed to decode just leaves the
            // previous one showing rather than interrupting anything.
        }
    }
}
