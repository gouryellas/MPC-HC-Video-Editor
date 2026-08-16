using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MpcHcVideoEditor.Dialogs;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly FFmpegService _ffmpeg;
    private readonly MpcHcService _mpc;
    private readonly BookmarkService _bookmarks;
    private readonly SettingsService _settings;
    private readonly PlaylistService _playlists;
    private readonly ImageConversionService _images;
    private readonly HotkeyService _hotkeys;
    private readonly ToastService _toast;
    private readonly StallMonitor _stalls;
    private readonly System.Windows.Threading.DispatcherTimer _pollTimer;
    private string? _lastLoadedPath;
    private (double Current, double Duration) _cachedPlaybackPosition;
    private DateTime _lastPositionUpdate = DateTime.MinValue;
    private bool _autoLoadingVideo;

    /// <summary>
    /// Tracks window focus so the poll can do less work while the window is in
    /// the background. Deliberately a plain flag rather than a change to
    /// <c>_pollTimer.Interval</c>: assigning Interval makes DispatcherTimer
    /// tear down and re-post its internal timer operation, which the stall log
    /// caught as ~200ms of DispatcherTimer.Restart on every focus change.
    /// </summary>
    private bool _windowFocused = true;

    /// <summary>
    /// Cancellation token source for the active "Play all" / "Play selected"
    /// playback loop. Non-null while a playback loop is running; cancelled
    /// by <see cref="StopPlayback"/> or by starting a new playback. Null
    /// again once the loop exits.
    /// </summary>
    private CancellationTokenSource? _playbackCts;

    /// <summary>
    /// True while a "Play all" / "Play selected" loop is actively sequencing
    /// through bookmarks. Used by <see cref="PollMpc"/> to skip its normal
    /// "auto-load a new file" path so playback isn't interrupted if MPC-HC
    /// briefly reports a window-title glitch mid-seek. Bound to the
    /// "Stop playback" menu item's IsEnabled so the user can only click it
    /// while a playback is actually running.
    /// </summary>
    [ObservableProperty] private bool _isPlayingCuts;

    [ObservableProperty] private EditSession _session = new();
    [ObservableProperty] private string _statusText = "Ready – open a video in MPC-HC, then set timestamps with your hotkey";
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private Bookmark? _selectedBookmark;
    [ObservableProperty] private bool _isMpcRunning;
    [ObservableProperty] private string _currentTimeDisplay = "00:00";
    [ObservableProperty] private string _durationDisplay = "00:00";
    [ObservableProperty] private double _timelineProgress;
    [ObservableProperty] private string _hotkeyStatus = "MButton: ON";
    /// <summary>
    /// Compact label shown on the Hotkey menu's top-level header so the
    /// user can see the current binding at a glance without expanding the
    /// menu (e.g. "Hotkey: MButton"). Kept in sync by
    /// <see cref="UpdateHotkeyStatus"/>.
    /// </summary>
    [ObservableProperty] private string _hotkeyMenuLabel = "Hotkey: MButton";
    [ObservableProperty] private string _playlistFolderDisplay = "Playlist folder: (not set)";
    [ObservableProperty] private string _quickSaveFolderDisplay = "Quick save: (not set)";

    /// <summary>
    /// Absolute path of the .pls playlist the user has explicitly "loaded"
    /// via Playlist → Load playlist… (or by opening a .pls file from
    /// File → Open…). When non-null, "Add current video to playlist"
    /// routes here directly with no picker dialog. Cleared by
    /// Playlist → Clear loaded playlist, by deleting the loaded playlist,
    /// and by Reset everything. Not persisted across app restarts.
    /// </summary>
    [ObservableProperty] private string? _loadedPlaylistPath;

    /// <summary>
    /// Friendly name of <see cref="LoadedPlaylistPath"/> for display in
    /// the status bar and the Playlist menu's "Clear loaded playlist"
    /// item. Updated whenever <see cref="LoadedPlaylistPath"/> changes
    /// via <see cref="OnLoadedPlaylistPathChanged"/>. Empty string when
    /// nothing is loaded.
    /// </summary>
    [ObservableProperty] private string _loadedPlaylistName = string.Empty;

    /// <summary>
    /// Display string for the status bar's "Playlist:" slot. Shows the
    /// loaded playlist's filename, or "(not loaded)" when nothing is
    /// loaded. Kept in sync with <see cref="LoadedPlaylistPath"/> via
    /// <see cref="OnLoadedPlaylistPathChanged"/>.
    /// </summary>
    [ObservableProperty] private string _playlistFileDisplay = "Playlist: (not loaded)";

    /// <summary>
    /// Display string for the status bar's "Bookmarks:" slot. Shows the
    /// active CSV path's filename, or "(not loaded)" when no CSV is bound
    /// to the session. Kept in sync with <see cref="EditSession.CsvPath"/>
    /// via the partial <see cref="OnSessionChanged"/> handler below.
    /// </summary>
    [ObservableProperty] private string _bookmarksFileDisplay = "Bookmarks: (not loaded)";

    /// <summary>
    /// Filename-only display for the right panel (no "Bookmarks: " prefix).
    /// Shows just the CSV filename, or "(not loaded)".
    /// </summary>
    public string BookmarksFileName => string.IsNullOrEmpty(Session.CsvPath) || !File.Exists(Session.CsvPath)
        ? "<none>"
        : Path.GetFileName(Session.CsvPath);

    /// <summary>
    /// True when the session has at least one valid (complete) bookmark,
    /// so the Edit Length section should be visible in the right panel.
    /// </summary>
    public bool HasValidBookmarks => Session.Bookmarks.Any(b => b.IsValid);

    // ------------------------------------------------------------------
    // Menu enablement state
    //
    // These four predicates decide which menu items and toolbar buttons are
    // clickable. Every gated command's CanExecute is written in terms of
    // them, so the rules stay readable and there is one place to look when
    // an item is unexpectedly greyed out. RefreshCommandStates() re-runs
    // them whenever anything they depend on moves.
    // ------------------------------------------------------------------

    /// <summary>
    /// True when MPC-HC is running AND actually has a video open. Both
    /// halves matter: the player can be running with no file loaded, in
    /// which case there is no position to timestamp and nothing to cut.
    /// </summary>
    public bool HasActiveVideo => IsMpcRunning && Session.HasVideo;

    /// <summary>
    /// True when a bookmark CSV is bound to this session and exists on
    /// disk. Explicit state rather than a live <c>File.Exists</c> probe so
    /// "loaded" is something the app sets and clears deliberately: creating
    /// the first timestamp turns it on, deleting the file turns it off.
    /// </summary>
    [ObservableProperty] private bool _isBookmarkFileLoaded;

    /// <summary>
    /// Number of complete bookmarks — ones with both a start and an end.
    /// A lone opening timestamp (still awaiting its close) does not count.
    /// </summary>
    public int CompletePairCount => Session.Bookmarks.Count(b => b.IsValid);

    /// <summary>
    /// Complete bookmarks the user has checked. There is no "any selection"
    /// counterpart: <see cref="Bookmark.IsSelected"/> refuses to be set on an
    /// incomplete bookmark, so a checked bookmark is always a complete one.
    /// </summary>
    public int SelectedPairCount => Session.Bookmarks.Count(b => b.IsSelected && b.IsValid);

    /// <summary>Progress state for the panel above the status bar.</summary>
    public JobProgress Job { get; } = new();

    /// <summary>Drives the minimal overlay's "(no bookmarks yet)" line.</summary>
    public bool HasNoBookmarks => Session.Bookmarks.Count == 0;

    /// <summary>
    /// When set, the view follows focus: the overlay while MPC-HC is the
    /// active window, the full window while this one is. See
    /// <see cref="ApplyAutoViewSwitch"/>. Persisted.
    /// </summary>
    [ObservableProperty] private bool _autoSwitchViews;

    partial void OnAutoSwitchViewsChanged(bool value)
    {
        _settings.Current.AutoSwitchViews = value;
        _settings.Save();

        // Apply straight away rather than waiting for the next poll tick.
        // Clearing the edge marker makes the next evaluation act rather than
        // treat the current focus as "already handled".
        _lastMpcFocused = null;
        if (value) ApplyAutoViewSwitch();
    }

    /// <summary>
    /// Whether the window minimises to the tray and survives being closed.
    /// Read by the View, which owns the tray icon.
    /// </summary>
    public RunMode RunMode => _settings.Current.RunMode;

    /// <summary>Raised when <see cref="RunMode"/> changes, so the View can add or remove the tray icon.</summary>
    public event Action? RunModeChanged;

    /// <summary>Corner the overlay parks in. Read by the View when it shows it.</summary>
    public OverlayCorner OverlayCorner => _settings.Current.OverlayCorner;

    /// <summary>Overlay background opacity. Read by the View when it shows it.</summary>
    public double OverlayOpacity => _settings.Current.OverlayOpacity;

    /// <summary>Whether the compact overlay is the view currently showing.</summary>
    private bool _minimalViewActive;

    /// <summary>
    /// Set when the user picks View ▸ Minimal by hand. While set, the overlay
    /// stays up through every focus change; only the X restore key and
    /// View ▸ Full clear it.
    /// </summary>
    /// <remarks>
    /// A hand-picked overlay used to follow focus like an automatic one, which
    /// meant clicking away from this window took it down again — the user had
    /// asked for the overlay and then watched it vanish on the next click. It
    /// is now a mode that lasts until it is explicitly ended, which is also
    /// what the X key already implied by existing.
    ///
    /// This is checked ahead of <see cref="AutoSwitchViews"/> rather than
    /// replacing it, so the two are layers, not alternatives: picking Minimal
    /// by hand while the setting is on holds the overlay until X, and the
    /// setting then carries on as before. Nothing here writes the setting —
    /// see <see cref="ShowMinimalView"/>.
    ///
    /// Being pinned is not the same as being on screen: see
    /// <see cref="_overlayShown"/>.
    /// </remarks>
    private bool _overlayPinned;

    /// <summary>
    /// Whether the overlay window is currently on screen, as opposed to merely
    /// being the current view. Only ever false while <see cref="_overlayPinned"/>
    /// holds it open behind the scenes.
    /// </summary>
    /// <remarks>
    /// A pinned overlay lasts until X, but "lasts" and "is visible" are
    /// different questions. Left purely to the pin, it sat on top of whatever
    /// else the user switched to — a bookmark list floating over a browser.
    /// So while pinned it is shown only when this app or MPC-HC has focus, and
    /// hidden otherwise: the window is not closed and the pin is not dropped,
    /// so going back to either one brings it straight back with no state lost.
    ///
    /// Tracked here rather than read back off the window because the View owns
    /// the windows — the ViewModel only says what should be on screen — and
    /// comparing against it keeps the poll from re-issuing Show or Hide several
    /// times a second.
    /// </remarks>
    private bool _overlayShown;

    /// <summary>
    /// Whether MPC-HC had focus at the last evaluation, or null when there is
    /// no decision on record. Makes <see cref="ApplyAutoViewSwitch"/>
    /// edge-triggered — see there.
    /// </summary>
    /// <remarks>
    /// Null means "evaluate the current focus as if it were new", and is used
    /// both before the first evaluation and by one that deliberately declined
    /// to act — an empty bookmark list, say. Without it, declining would look
    /// exactly like having already handled the focus, and the overlay would
    /// wait for the player to lose and regain focus before reconsidering.
    /// </remarks>
    private bool? _lastMpcFocused;

    /// <summary>
    /// Follows focus: the overlay while MPC-HC is the active window, the full
    /// window while this one is.
    /// </summary>
    /// <remarks>
    /// Focus, not window size. This used to key off the player being
    /// fullscreen or maximised, which missed the ordinary case of a windowed
    /// player being worked in and fired on a maximised player sitting behind
    /// something else. What actually decides whether the full window is worth
    /// showing is whether the user is looking at it.
    ///
    /// Driven from the poll rather than from an event, because focus changes
    /// in another process do not notify us.
    ///
    /// There are two ways the overlay can be up, and only one of them follows
    /// focus at all:
    ///
    /// Pinned by hand — View ▸ Minimal — ignores focus entirely and holds until
    /// X or View ▸ Full. See <see cref="_overlayPinned"/>.
    ///
    /// Automatic — the setting — is the focus-following one, and its two
    /// directions are not symmetric. Leaving the player always restores the
    /// full window, whatever took focus: the overlay exists to sit over the
    /// video, and anywhere else it is a box in the way. Returning to the player
    /// drops back to the overlay only on the edge, so pressing X does not
    /// bounce straight back to the overlay a tick later while the player still
    /// holds focus.
    ///
    /// Either way the overlay only stands while it has something to show. With
    /// an empty list it is a panel listing nothing, so the full window keeps
    /// the screen instead — see the <see cref="HasNoBookmarks"/> check below.
    /// </remarks>
    private void ApplyAutoViewSwitch()
    {
        // Nothing to show, so nothing goes up — checked ahead of the pin,
        // because an overlay listing nothing is not what was pinned, and it is
        // why View ▸ Minimal is disabled in this state (CanShowMinimalView).
        //
        // Level-triggered rather than edge-triggered, because the list can
        // empty while the overlay is already up — picking the next video in
        // the player clears the bookmarks with it — so an overlay already up
        // has to come down, not merely be prevented from going up.
        //
        // The focus is left unrecorded on purpose. The hotkey that creates the
        // first bookmark is pressed in the player, so there is no focus change
        // on the way to trigger on; the next tick has to be free to act on the
        // focus the player already has. See _lastMpcFocused.
        if (HasNoBookmarks)
        {
            _lastMpcFocused = null;
            if (_minimalViewActive) RestoreFullView(activate: false);
            return;
        }

        // Pinned by hand, so focus does not end it and there is no edge to
        // track — X and View ▸ Full both re-record the focus as they unpin.
        // What focus still decides is whether it is on screen, and re-entering
        // covers an empty list having taken it down before bookmarks returned.
        if (_overlayPinned)
        {
            if (!_minimalViewActive) EnterMinimalView();
            ApplyPinnedOverlayVisibility();
            return;
        }

        var mpcFocused = _mpc.IsForeground();

        if (!mpcFocused)
        {
            _lastMpcFocused = false;

            // Only an automatic overlay can be up here, and that only ever
            // happens while the player has focus — so losing it is always a
            // real departure and the restore needs no further guard.
            //
            // Without activating: the user just moved to something else, and
            // taking focus back off whatever they chose would be worse than
            // the overlay was. If they came back to this window, it already
            // has focus and there is nothing to take.
            if (_minimalViewActive) RestoreFullView(activate: false);

            return;
        }

        if (!AutoSwitchViews)
        {
            // Record the focus so a later transition still reads as an edge,
            // but do not act on it.
            _lastMpcFocused = true;
            return;
        }

        if (_lastMpcFocused == true) return;
        _lastMpcFocused = true;

        // EnterMinimalView rather than ShowMinimalView: this is the setting
        // doing its job, not the user pinning anything. See there.
        if (IsBookmarkFileLoaded && !_minimalViewActive) EnterMinimalView();
    }

    /// <summary>
    /// Raised when the view should change. The View owns the windows; the
    /// ViewModel only signals the intent.
    /// </summary>
    /// <remarks>
    /// The second argument says whether the full window should also be
    /// activated. An explicit request — the View menu, the X key — should
    /// bring the window to the front, because the user just asked for it. A
    /// restore that happens because focus moved to some third application
    /// should not, or the app would snatch focus back from whatever they
    /// switched to.
    /// </remarks>
    public event Action<bool, bool>? MinimalViewRequested;

    /// <summary>
    /// Raised to show or hide the overlay window on its own, without leaving
    /// minimal view. The full window is not touched either way.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="MinimalViewRequested"/> because it is a
    /// different question: that one swaps which view the app is in, this one
    /// only says whether the overlay belonging to the current view is on
    /// screen. Folding the two together would mean hiding the overlay had to
    /// bring the full window back, which is exactly what a pinned overlay must
    /// not do. See <see cref="_overlayShown"/>.
    /// </remarks>
    public event Action<bool>? OverlayVisibilityRequested;

    /// <summary>
    /// Shows the pinned overlay while this app or the player has focus, and
    /// hides it while anything else does.
    /// </summary>
    /// <remarks>
    /// Only meaningful for a pinned overlay. An automatic one is already gone
    /// by the time focus lands anywhere else — leaving the player restores the
    /// full window outright — so there is nothing left to hide.
    ///
    /// <see cref="_windowFocused"/> covers this app: in minimal view the full
    /// window is hidden, so it is normally false, but the tray icon can put
    /// that window back up with the overlay still pinned, and then both are on
    /// screen and this is what keeps the overlay there.
    /// </remarks>
    private void ApplyPinnedOverlayVisibility()
    {
        var shouldShow = _windowFocused || _mpc.IsForeground();
        if (shouldShow == _overlayShown) return;

        SetOverlayShown(shouldShow);
        OverlayVisibilityRequested?.Invoke(shouldShow);
    }

    /// <summary>
    /// Records whether the overlay is on screen, and arms the X restore key to
    /// match.
    /// </summary>
    /// <remarks>
    /// The two are the same fact, so they are set in one place. X is a bare
    /// letter on a global hook: armed whenever the overlay merely existed, it
    /// fired on the "x" in anything the user typed in another application, and
    /// with a pinned overlay hidden there was nothing on screen to explain why
    /// the editor had just jumped to the front. Armed only while the overlay is
    /// visible, the key belongs to something the user can see.
    ///
    /// That does not strand a hidden pinned overlay: it is hidden because
    /// another application has focus, and clicking back to the player or this
    /// window brings it — and the key — straight back.
    /// </remarks>
    private void SetOverlayShown(bool shown)
    {
        _overlayShown = shown;
        _hotkeys.RestoreArmed = shown;
    }

    /// <summary>
    /// Pins the overlay up until the user takes it down with X or View ▸ Full.
    /// </summary>
    /// <remarks>
    /// Asking for the overlay is asking for a mode, not for one window swap, so
    /// this pins rather than switching once — clicking away from this window
    /// used to take it straight back down, which read as the menu item failing.
    /// See <see cref="_overlayPinned"/>.
    ///
    /// Disabled with an empty bookmark list, via
    /// <see cref="CanShowMinimalView"/>: the overlay is a bookmark list, and
    /// pinning one with nothing in it would put up a panel that
    /// <see cref="ApplyAutoViewSwitch"/> immediately takes down again. This
    /// used to be deliberately ungated, on the grounds that a greyed-out menu
    /// item explains itself less well than an empty list does — but an empty
    /// list that cannot stay on screen explains nothing at all.
    ///
    /// Deliberately does not touch <see cref="AutoSwitchViews"/>. It used to
    /// turn it off and persist that, on the grounds that manual control and
    /// automatic switching are alternatives; pinning makes them layers instead,
    /// so the setting no longer has to be spent to hold the overlay still, and
    /// it now changes only when the user changes it.
    ///
    /// Hands focus to the player on the way, which is what makes the result the
    /// same every time: a pinned overlay is only on screen while this app or the
    /// player has focus, and hiding this window hands focus to whatever happened
    /// to be behind it. Left to chance, picking Minimal over a browser put the
    /// overlay up and hid it again in the same breath.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanShowMinimalView))]
    private void ShowMinimalView()
    {
        _overlayPinned = true;

        // Before the view swap, not after: SetForegroundWindow is only reliably
        // granted to the process that already owns the foreground, and this
        // window is it right up until EnterMinimalView hides it. Afterwards the
        // call would be at the mercy of Windows' foreground-stealing rules —
        // exactly when getting it right matters most. Hiding a window that is
        // no longer the foreground one does not move focus again, so the player
        // keeps it.
        _mpc.BringToFront();

        EnterMinimalView();
    }

    /// <summary>
    /// Whether View ▸ Minimal is available: only with something to show.
    /// </summary>
    private bool CanShowMinimalView() => !HasNoBookmarks;

    /// <summary>
    /// Puts the overlay up without saying anything about why it went up.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ShowMinimalView"/> for the same reason
    /// <see cref="RestoreFullView"/> is separate from
    /// <see cref="ShowFullView"/>: the window swap and the statement about how
    /// the view is being driven are different things. Choosing View ▸ Minimal
    /// pins the overlay; <see cref="ApplyAutoViewSwitch"/> is the setting doing
    /// the very thing it was turned on for, and pins nothing.
    ///
    /// The automatic path used to call the command, so the first switch it ever
    /// made turned the setting off and persisted that — the Settings checkbox
    /// came back unchecked and the next launch started with automatic switching
    /// off. It only looked right for the rest of the session because the
    /// command armed manual focus-following on its way past.
    ///
    /// The current focus is recorded as the baseline so this very call does
    /// not read as an edge on the next poll tick and immediately undo itself.
    /// </remarks>
    private void EnterMinimalView()
    {
        _lastMpcFocused = _mpc.IsForeground();
        _minimalViewActive = true;

        // The View shows the overlay as part of the swap, so it is on screen as
        // of this call — and the X key is armed with it. A pinned overlay that
        // should not be showing gets hidden again by
        // ApplyPinnedOverlayVisibility in this same tick, before anything is
        // rendered, which disarms the key along with it.
        SetOverlayShown(true);
        MinimalViewRequested?.Invoke(true, false);
    }

    /// <summary>
    /// Puts the full window back without ending whatever was driving the view.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ShowFullView"/> because the two mean different
    /// things. Focus leaving the player, or the list going empty, is a reason to
    /// show the window — not a statement that the user is done with the overlay.
    /// So the pin and the setting both survive this, and the overlay comes back
    /// when the reason does. Only an explicit View ▸ Full or the X key ends it.
    /// </remarks>
    private void RestoreFullView(bool activate)
    {
        _minimalViewActive = false;

        // The View hides the overlay as part of the swap, so this stays in step
        // with what is actually on screen — otherwise a later pinned re-entry
        // would think it was still showing and skip putting it back up — and it
        // disarms the X key, which has nothing to restore any more.
        SetOverlayShown(false);
        MinimalViewRequested?.Invoke(false, activate);
    }

    /// <summary>
    /// Returns to the full window and unpins the overlay. Bound to View ▸ Full
    /// and to the X restore key.
    /// </summary>
    /// <remarks>
    /// Unpinning is the whole point: this is the user saying they are done with
    /// the overlay, which is the one thing <see cref="RestoreFullView"/>
    /// deliberately does not assume.
    ///
    /// <see cref="AutoSwitchViews"/> is left alone — X means "full window now",
    /// not "stop switching views from now on", and the setting is the user's to
    /// change. Recording the current focus is what stops the overlay coming
    /// straight back on the next poll tick while the player still holds focus;
    /// going back to the player afresh brings it back, which is what the
    /// setting is for.
    /// </remarks>
    [RelayCommand]
    private void ShowFullView()
    {
        _overlayPinned = false;
        _lastMpcFocused = _mpc.IsForeground();

        // Asked for explicitly, so bring the window to the front.
        RestoreFullView(activate: true);
    }

    /// <summary>
    /// True when the right panel should show EDIT LENGTH: a video is loaded,
    /// its bookmark file is loaded, and there is at least one complete pair
    /// to measure.
    /// </summary>
    public bool HasEditLength => Session.HasVideo && IsBookmarkFileLoaded && CompletePairCount >= 1;

    /// <summary>
    /// Destination for every action except the one-click row button — merge,
    /// split, convert, strip audio, bulk merge.
    /// </summary>
    /// <remarks>
    /// Deliberately not persisted. It follows the loaded video's own folder
    /// until the user picks a folder explicitly, and from then on stays put
    /// for the rest of the session even as other videos are loaded. A fresh
    /// run starts following the video again.
    /// </remarks>
    private string _pinnedSaveToFolder = string.Empty;

    [ObservableProperty] private string _saveToFolderDisplay = "Save to: (not set)";

    /// <summary>Header for Bookmarks ▸ Set timestamp, carrying the live hotkey.</summary>
    [ObservableProperty] private string _setTimestampMenuLabel = "Set timestamp";

    /// <summary>Greyed-out example of what the active naming tag produces.</summary>
    [ObservableProperty] private string _suffixExampleDisplay = "Example: video_name[done].mp4";

    /// <summary>
    /// Folder shortcuts shown in the File menu. Clicking one's "Set" points
    /// the quick save folder at it. Separate from <see cref="Shortcuts"/>,
    /// which only opens folders in Explorer.
    /// </summary>
    public ObservableCollection<ShortcutEntry> QuickSaveShortcuts { get; } = new();

    /// <summary>
    /// True when the playlist folder is set and holds at least one .pls.
    /// Cached rather than computed on demand because it touches the disk,
    /// and CanExecute is re-evaluated every time a menu opens.
    /// </summary>
    [ObservableProperty] private bool _hasPlaylistFiles;

    /// <summary>
    /// True when a playlist is loaded and has at least one video in it.
    /// Also cached — reading it means parsing the .pls.
    /// </summary>
    [ObservableProperty] private bool _loadedPlaylistHasEntries;

    /// <summary>
    /// Filename-only display for the right panel (no "Playlist: " prefix).
    /// Shows just the PLS filename, or "(not loaded)".
    /// </summary>
    public string PlaylistFileName => string.IsNullOrEmpty(LoadedPlaylistPath) || !File.Exists(LoadedPlaylistPath)
        ? "(not loaded)"
        : Path.GetFileName(LoadedPlaylistPath);

    /// <summary>
    /// True when a playlist is deliberately loaded (non-null
    /// <see cref="LoadedPlaylistPath"/> pointing at an existing file).
    /// Bound to the "Clear loaded playlist" menu item's IsEnabled so it
    /// is only clickable when there's actually something to clear.
    /// </summary>
    public bool HasLoadedPlaylist
        => !string.IsNullOrEmpty(LoadedPlaylistPath) && File.Exists(LoadedPlaylistPath);

    /// <summary>
    /// Label for the "Add current video to playlist…" menu item. When a
    /// playlist is loaded, becomes
    /// <c>"Add current video to 'name.pls'"</c>; otherwise stays as the
    /// neutral <c>"Add current video to a playlist…"</c> that prompts
    /// the user to pick.
    /// </summary>
    public string AddToPlaylistMenuLabel
        => !string.IsNullOrEmpty(LoadedPlaylistPath) && File.Exists(LoadedPlaylistPath)
            ? $"Add current video to '{Path.GetFileName(LoadedPlaylistPath)}'"
            : "Add current video to a playlist…";

    /// <summary>
    /// Tooltip for the "Add current video to playlist…" menu item.
    /// Explains what will happen on click — either "add to the loaded
    /// playlist directly" or "pick a playlist".
    /// </summary>
    public string AddToPlaylistToolTip
        => !string.IsNullOrEmpty(LoadedPlaylistPath) && File.Exists(LoadedPlaylistPath)
            ? $"Add the current video to the loaded playlist ({Path.GetFileName(LoadedPlaylistPath)}) directly"
            : "Pick an existing playlist (or type a new name) and add the current video to it";

    public ObservableCollection<string> RecentVideos { get; } = new();

    /// <summary>
    /// Folder shortcuts that appear as clickable items at the bottom of
    /// the File menu. Each entry is a <see cref="ShortcutEntry"/> with a
    /// friendly name and an absolute folder path. Bound to the File menu
    /// via the code-behind in MainWindow.xaml.cs. Mutations (add / remove
    /// / rename / reorder) flow through the commands below so that
    /// settings.json stays in sync.
    /// </summary>
    public ObservableCollection<ShortcutEntry> Shortcuts { get; } = new();

    /// <summary>
    /// User-defined filename suffixes appended to all video operation
    /// outputs. Bound to the Suffix menu via code-behind in
    /// MainWindow.xaml.cs. The active suffix (tracked by
    /// <see cref="ActiveSuffixDisplay"/>) is the one currently applied
    /// to new outputs; the user can switch by clicking a different entry.
    /// </summary>
    public ObservableCollection<SuffixEntry> Suffixes { get; } = new();

    /// <summary>
    /// Display string for the Options menu's active-tag line, e.g.
    /// <c>"Current rename tag: done"</c>. Updated whenever the active tag
    /// changes (via <see cref="UpdateActiveSuffixDisplay"/>).
    /// </summary>
    [ObservableProperty] private string _activeSuffixDisplay = "Current rename tag: done";

    public MainViewModel()
    {
        // Settings first: the ffmpeg folder override is a constructor argument,
        // and the port, quality and poll interval are pushed into their
        // services immediately below.
        _settings = new SettingsService();

        _ffmpeg = new FFmpegService(_settings.Current.FfmpegFolder);
        _mpc = new MpcHcService();
        _bookmarks = new BookmarkService();
        _playlists = new PlaylistService();
        _images = new ImageConversionService();
        _hotkeys = new HotkeyService();

        // Toasts appear over MPC-HC's monitor — the hotkey is usually pressed
        // while the player has focus, where the status bar can't be seen.
        _toast = new ToastService(() => _mpc.FindMpcWindow());

        // Diagnostic for the input-lag investigation: logs UI-thread stalls to
        // stalls.log next to the exe. Cheap enough to leave running.
        _stalls = new StallMonitor();
        if (HotkeyService.HooksDisabledByEnvironment)
            _stalls.Note("MPCHC_EDITOR_NO_HOOKS=1 — global input hooks NOT installed");

        foreach (var r in _settings.Current.RecentVideos)
            RecentVideos.Add(r);

        foreach (var s in _settings.Current.Shortcuts)
            Shortcuts.Add(s);

        foreach (var s in _settings.Current.QuickSaveShortcuts)
            QuickSaveShortcuts.Add(s);

        foreach (var suf in _settings.Current.Suffixes)
            Suffixes.Add(suf);
        UpdateActiveSuffixDisplay();

        AutoSwitchViews = _settings.Current.AutoSwitchViews;

        // Restore the pinned output folder from the last session, if the user
        // asked for it to be remembered and it still exists. A folder that has
        // since been deleted falls back to following the video, which is the
        // unpinned behaviour and needs no explanation.
        if (_settings.Current.RememberSaveToFolder &&
            !string.IsNullOrWhiteSpace(_settings.Current.SaveToFolder) &&
            Directory.Exists(_settings.Current.SaveToFolder))
        {
            _pinnedSaveToFolder = _settings.Current.SaveToFolder;
        }

        // Created before the settings are applied so ApplyServiceSettings can
        // set its interval unconditionally. Started at the end of the
        // constructor, once there is something worth polling for.
        _pollTimer = new System.Windows.Threading.DispatcherTimer();
        _pollTimer.Tick += (_, _) => PollMpc();

        // Everything the services need from settings, in one place so the
        // Settings dialog can re-run exactly this on save.
        ApplyServiceSettings();

        // Single configurable hotkey for the "set bookmark timestamp"
        // action. Migrated from the legacy MiddleMouseHotkeyEnabled /
        // KeyboardHotkey fields on first load by SettingsService.
        var binding = _settings.GetTimestampHotkey();
        _hotkeys.Binding = binding;
        _hotkeys.Triggered += OnTimestampHotkey;

        // X brings the full window back, whatever the timestamp hotkey happens
        // to be bound to. Goes through ShowFullView so the tracked view state
        // stays honest — otherwise auto-switching would think the overlay was
        // still up and refuse to raise it again.
        _hotkeys.RestoreRequested += ShowFullView;
        if (binding.Kind != HotkeyBinding.HotkeyKind.None)
            _hotkeys.Start();

        UpdateHotkeyStatus();
        RefreshFolderDisplays();

        // Menu enablement has to react to the bookmark list changing, to any
        // individual bookmark being checked or closed, and to the session's
        // video/CSV paths moving. OnSessionChanged does not fire for the field
        // initializer, so the initial session is hooked here.
        HookSession(Session);

        // Every playlist mutation already raises PlaylistsChanged for the
        // code-behind's menu rebuild; piggy-back on it so the cached playlist
        // predicates never go stale.
        PlaylistsChanged += RefreshPlaylistState;
        RefreshPlaylistState();
        RefreshCommandStates();

        _pollTimer.Start();
    }

    /// <summary>
    /// Pushes the settings that live inside services into those services.
    /// Called at startup and again whenever Settings is saved.
    /// </summary>
    /// <remarks>
    /// The ffmpeg folder is the one exception — it is resolved once in
    /// <see cref="FFmpegService"/>'s constructor, and re-resolving means
    /// shelling out to <c>where.exe</c>. Changing it therefore asks for a
    /// restart rather than silently doing nothing.
    /// </remarks>
    private void ApplyServiceSettings()
    {
        _mpc.WebInterfacePort = _settings.Current.MpcWebInterfacePort;
        _ffmpeg.QualityArgs = _settings.GetQualityArgs();

        _toast.Enabled = _settings.Current.ToastsEnabled;
        _toast.HoldDuration = TimeSpan.FromSeconds(_settings.Current.ToastSeconds);

        // Static rather than injected — see RecycleBin.SendToBin. Pushed here
        // so it is set before anything can delete, and re-pushed when Settings
        // is saved.
        RecycleBin.SendToBin = _settings.Current.DeleteToRecycleBin;

        _pollTimer.Interval = _settings.GetPollInterval();
    }

    // ------------------------------------------------------------------
    // Menu enablement plumbing
    // ------------------------------------------------------------------

    /// <summary>
    /// Snapshot of everything the CanExecute predicates read. Comparing it
    /// lets <see cref="RefreshCommandStates"/> be called from the 300ms poll
    /// tick without re-notifying a dozen commands three times a second.
    /// </summary>
    private string _commandStateKey = string.Empty;

    /// <summary>
    /// Subscribes the enablement plumbing to a session. Safe to call twice on
    /// the same instance — every handler is detached before being attached.
    /// </summary>
    private void HookSession(EditSession session)
    {
        session.PropertyChanged -= Session_PropertyChanged;
        session.PropertyChanged += Session_PropertyChanged;

        session.Bookmarks.CollectionChanged -= Bookmarks_CollectionChanged;
        session.Bookmarks.CollectionChanged += Bookmarks_CollectionChanged;

        foreach (var b in session.Bookmarks)
        {
            b.PropertyChanged -= Bookmark_PropertyChanged;
            b.PropertyChanged += Bookmark_PropertyChanged;
        }
    }

    private void Bookmarks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (Bookmark b in e.OldItems)
                b.PropertyChanged -= Bookmark_PropertyChanged;
        if (e.NewItems != null)
            foreach (Bookmark b in e.NewItems)
                b.PropertyChanged += Bookmark_PropertyChanged;

        // Reset() (i.e. Clear()) reports no OldItems, so a stale subscription
        // could survive. Re-subscribing is idempotent enough here because the
        // cleared instances are dropped entirely.
        RefreshCommandStates();
    }

    private void Bookmark_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only the properties the gates actually read.
        if (e.PropertyName is nameof(Bookmark.IsSelected)
                           or nameof(Bookmark.IsIncomplete)
                           or nameof(Bookmark.StartSeconds)
                           or nameof(Bookmark.EndSeconds))
            RefreshCommandStates();
    }

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditSession.VideoPath) or nameof(EditSession.CsvPath))
            RefreshCommandStates();
    }

    partial void OnIsMpcRunningChanged(bool value) => RefreshCommandStates();

    partial void OnIsBookmarkFileLoadedChanged(bool value)
    {
        RefreshCommandStates();

        // The bookmark file is only half the condition — the player also has
        // to have focus. Clearing the edge marker lets the check act on the
        // current focus rather than waiting for it to move: loading a bookmark
        // file while already in the player should drop straight to the overlay.
        _lastMpcFocused = null;
        ApplyAutoViewSwitch();
    }

    /// <summary>
    /// Re-evaluates every gated command's CanExecute and the properties the
    /// menu binds to. Cheap to call often — it bails immediately unless
    /// something it depends on actually changed.
    /// </summary>
    private void RefreshCommandStates()
    {
        // HasNoBookmarks is in here in its own right, not covered by
        // CompletePairCount: an opening timestamp with no close yet is a
        // bookmark the overlay lists but that pair count does not see, so
        // without it View ▸ Minimal's enablement would miss the list going
        // empty (or stopping being empty) whenever no pair completed with it.
        var key = string.Join('|', HasActiveVideo, IsBookmarkFileLoaded, CompletePairCount,
                                   SelectedPairCount, HasNoBookmarks,
                                   HasPlaylistFiles, LoadedPlaylistHasEntries);
        if (key == _commandStateKey) return;
        _commandStateKey = key;

        OnPropertyChanged(nameof(HasActiveVideo));
        OnPropertyChanged(nameof(CompletePairCount));
        OnPropertyChanged(nameof(SelectedPairCount));
        OnPropertyChanged(nameof(HasValidBookmarks));

        OnPropertyChanged(nameof(HasEditLength));
        OnPropertyChanged(nameof(HasNoBookmarks));

        SetTimestampCommand.NotifyCanExecuteChanged();
        ShowMinimalViewCommand.NotifyCanExecuteChanged();
        UndoLastBookmarkCommand.NotifyCanExecuteChanged();
        EditBookmarksCommand.NotifyCanExecuteChanged();
        DeleteBookmarksCommand.NotifyCanExecuteChanged();
        EnterTimeManualCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ToggleFlipCommand.NotifyCanExecuteChanged();
        PlayAllCommand.NotifyCanExecuteChanged();
        PlaySelectedCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        SelectNoneCommand.NotifyCanExecuteChanged();
        MergeSelectedCommand.NotifyCanExecuteChanged();
        SplitSelectedCommand.NotifyCanExecuteChanged();
        AddCurrentToPlaylistCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Refreshes the two disk-backed playlist predicates. Called on startup,
    /// whenever <see cref="PlaylistsChanged"/> fires, and when the loaded
    /// playlist changes.
    /// </summary>
    private void RefreshPlaylistState()
    {
        var folder = _settings.Current.PlaylistFolder;
        HasPlaylistFiles = !string.IsNullOrWhiteSpace(folder)
                           && Directory.Exists(folder)
                           && _playlists.ListPlaylists(folder).Any();

        LoadedPlaylistHasEntries = !string.IsNullOrEmpty(LoadedPlaylistPath)
                                   && File.Exists(LoadedPlaylistPath)
                                   && _playlists.ReadEntries(LoadedPlaylistPath).Count > 0;

        RefreshCommandStates();
    }

    // ------------------------------------------------------------------
    // CanExecute predicates — see the "Menu enablement state" block above
    // for what HasActiveVideo / IsBookmarkFileLoaded / CompletePairCount /
    // SelectedPairCount mean.
    // ------------------------------------------------------------------

    private bool CanSetTimestamp() => HasActiveVideo;
    private bool CanEnterTimeManual() => HasActiveVideo;
    private bool CanUndoLastBookmark() => IsBookmarkFileLoaded;
    private bool CanDeleteSelected() => IsBookmarkFileLoaded && SelectedPairCount >= 1;
    private bool CanSelectAll() => HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount >= 1;
    private bool CanSelectNone() => HasActiveVideo && IsBookmarkFileLoaded && SelectedPairCount >= 1;

    // Play needs something to sequence, so it wants two or more pairs. Split
    // works on a single pair, and so does Merge — one cut is a trim.
    private bool CanPlayAll() => HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount > 1;
    private bool CanPlaySelected() => CanPlayAll() && SelectedPairCount > 1;
    private bool CanMergeSelected() => HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount >= 1;
    private bool CanSplitSelected() => HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount >= 1;

    private bool CanAddCurrentToPlaylist() => HasActiveVideo;

    private bool CanToggleFlip() =>
        HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount >= 1 && SelectedPairCount >= 1;

    // "Edit bookmarks" opens the CSV, so it needs something in it to edit.
    private bool CanEditBookmarks() => IsBookmarkFileLoaded && CompletePairCount >= 1;

    // "Delete bookmarks" only needs the file to exist.
    private bool CanDeleteBookmarks() => IsBookmarkFileLoaded;

    // Merge is always available: with no video or bookmarks it falls back to
    // asking the user which files to join.
    private bool CanMergeAlways() => true;

    private void UpdateHotkeyStatus()
    {
        var binding = _hotkeys.Binding;
        var label = binding.Kind == HotkeyBinding.HotkeyKind.None
            ? "OFF"
            : binding.Display;
        HotkeyStatus = $"Hotkey: {label}";
        HotkeyMenuLabel = $"Hotkey: {label}";
        SetTimestampMenuLabel = $"Set timestamp: {label}";
    }

    /// <summary>
    /// Slows polling while the window is unfocused. It must NOT stop: the
    /// normal workflow is bookmarking from MPC-HC with this window in the
    /// background, and polling is what notices a newly opened video. A longer
    /// interval just trims work nobody is looking at.
    /// </summary>
    public void PausePollTimer()
    {
        _windowFocused = false;
        _stalls.NoteFocus(false);
    }

    /// <summary>Restores the responsive poll interval when the window regains focus.</summary>
    public void ResumePollTimer()
    {
        _windowFocused = true;
        _stalls.NoteFocus(true);
    }

    /// <summary>
    /// Refreshes <see cref="ActiveSuffixDisplay"/> from the settings.
    /// Called on startup, after any suffix mutation (add/remove/rename/
    /// set-active), and from the code-behind when the Suffixes collection
    /// changes. The menu header binds to this property so the "Current:"
    /// label updates live.
    /// </summary>
    /// <summary>
    /// Text of the active naming tag, without brackets. Exposed so the menu
    /// can mark the active entry by comparing values instead of scraping the
    /// display label.
    /// </summary>
    public string ActiveSuffixText => _settings.GetActiveSuffixText();

    private void UpdateActiveSuffixDisplay()
    {
        var text = _settings.GetActiveSuffixText();
        OnPropertyChanged(nameof(ActiveSuffixText));

        // Brackets are an output detail, not part of the tag's name — the
        // menu shows "done", the example below it shows where the brackets
        // actually land.
        ActiveSuffixDisplay = $"Current rename tag: {text}";
        SuffixExampleDisplay = BuildSuffixExample(text);
    }

    /// <summary>
    /// The "filename.mp4 → filename[done].mp4" line under the naming tags.
    /// </summary>
    /// <remarks>
    /// Uses the configured output container, so changing the format in
    /// Settings updates the example to match what will actually be written
    /// rather than leaving a stale <c>.mp4</c> on screen.
    /// </remarks>
    private string BuildSuffixExample(string suffixText)
    {
        var ext = OutputFormat.Extension;
        return $"Example: filename{ext}  →  filename[{suffixText}]{ext}";
    }

    /// <summary>
    /// Generates a unique output path by appending the active suffix to
    /// <paramref name="basePath"/> (without its extension) and adding
    /// <paramref name="extension"/>. If the result already exists, a
    /// counter starting at 2 is appended inside the brackets until a free
    /// filename is found:
    /// <c>&lt;name&gt;[done].mp4</c>, <c>&lt;name&gt;[done2].mp4</c>,
    /// <c>&lt;name&gt;[done3].mp4</c>, …
    /// </summary>
    /// <param name="basePath">The source file path (used for directory +
    /// name without extension).</param>
    /// <param name="extension">The output extension, including the dot
    /// (e.g. <c>".mp4"</c>, <c>".mp3"</c>).</param>
    /// <param name="startIndex">1 for the first clip (no number), 2+ to
    /// begin counting from a specific index (used by split).</param>
    /// <param name="outputDirectory">Optional directory override. When null
    /// or empty the file lands next to <paramref name="basePath"/>; the
    /// one-click split passes the quick save folder here.</param>
    private string GetUniqueOutputPath(string basePath, string extension, int startIndex = 1,
                                       string? outputDirectory = null)
    {
        var dir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(basePath) ?? ""
            : outputDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var suffix = _settings.GetActiveSuffixText();

        int counter = Math.Max(1, startIndex);
        while (true)
        {
            var bracket = counter == 1 ? $"[{suffix}]" : $"[{suffix}{counter}]";
            var candidate = Path.Combine(dir, $"{nameWithoutExt}{bracket}{extension}");
            if (!File.Exists(candidate)) return candidate;
            counter++;
        }
    }

    /// <summary>
    /// Where one-click clips go. The quick save folder wins when it is set
    /// (File ▸ "Quick save: …"), and is created if it does not exist yet.
    /// When it is unset — or unusable, e.g. a folder on a drive that is no
    /// longer attached — this falls back to the directory of the video the
    /// clip came from, so a click always produces a file somewhere sane.
    /// </summary>
    /// <returns>An existing directory path, or null when even the video's
    /// own directory cannot be determined.</returns>
    private string? ResolveQuickSaveDirectory()
    {
        var quickSave = _settings.Current.QuickSaveFolder;
        if (!string.IsNullOrWhiteSpace(quickSave))
        {
            try
            {
                Directory.CreateDirectory(quickSave);
                return quickSave;
            }
            catch (Exception ex)
            {
                // Drive unplugged, permissions, bad path saved by hand —
                // don't fail the split, just use the video's folder.
                StatusText = $"Quick save folder unavailable ({ex.Message}) — saving next to the video.";
            }
        }

        return Path.GetDirectoryName(Session.VideoPath);
    }

    private void RefreshFolderDisplays()
    {
        var pl = _settings.Current.PlaylistFolder;
        PlaylistFolderDisplay = string.IsNullOrWhiteSpace(pl)
            ? "Playlist folder: (not set)"
            : "Playlist folder: " + pl;

        var qs = _settings.Current.QuickSaveFolder;
        QuickSaveFolderDisplay = string.IsNullOrWhiteSpace(qs)
            ? "Quick save: (not set)"
            : "Quick save: " + qs;

        var saveTo = ResolveSaveToDirectory();
        SaveToFolderDisplay = string.IsNullOrWhiteSpace(saveTo)
            ? "Save to: (not set)"
            : "Save to: " + saveTo;
    }

    /// <summary>
    /// Where merge / split / convert / strip audio / bulk merge write. Once
    /// the user has picked a folder it wins for the rest of the session;
    /// until then it follows the loaded video's own folder.
    /// </summary>
    /// <remarks>
    /// With "remember Save to folder" set, a folder picked in a previous
    /// session is restored into the pin at startup, so it wins here just as a
    /// freshly-picked one would.
    /// </remarks>
    private string ResolveSaveToDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_pinnedSaveToFolder))
            return _pinnedSaveToFolder;

        return string.IsNullOrEmpty(Session.VideoPath)
            ? string.Empty
            : Path.GetDirectoryName(Session.VideoPath) ?? string.Empty;
    }

    /// <summary>
    /// Recomputes <see cref="LoadedPlaylistName"/>,
    /// <see cref="PlaylistFileDisplay"/>, and the computed menu-bound
    /// properties (<see cref="HasLoadedPlaylist"/>,
    /// <see cref="AddToPlaylistMenuLabel"/>,
    /// <see cref="AddToPlaylistToolTip"/>) from
    /// <see cref="LoadedPlaylistPath"/>. Called automatically by the
    /// source-generated <c>OnLoadedPlaylistPathChanged</c> partial.
    /// </summary>
    partial void OnLoadedPlaylistPathChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !File.Exists(value))
        {
            LoadedPlaylistName = string.Empty;
            PlaylistFileDisplay = "Playlist: (not loaded)";
        }
        else
        {
            LoadedPlaylistName = Path.GetFileName(value);
            PlaylistFileDisplay = "Playlist: " + LoadedPlaylistName;
        }
        // The computed menu-bound properties depend on LoadedPlaylistPath
        // — fire PropertyChanged so WPF re-reads them.
        OnPropertyChanged(nameof(HasLoadedPlaylist));
        OnPropertyChanged(nameof(AddToPlaylistMenuLabel));
        OnPropertyChanged(nameof(AddToPlaylistToolTip));
        OnPropertyChanged(nameof(PlaylistFileName));

        // "Clear loaded playlist" is gated on the loaded playlist having
        // entries, which means re-reading the .pls.
        RefreshPlaylistState();
    }

    /// <summary>
    /// Hooks <see cref="Session"/>'s <c>PropertyChanged</c> so we can
    /// refresh <see cref="BookmarksFileDisplay"/> whenever
    /// <see cref="EditSession.CsvPath"/> changes (via Open video, Open
    /// bookmark CSV, Open playlist entry, etc.).
    /// </summary>
    partial void OnSessionChanged(EditSession value)
    {
        value.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditSession.CsvPath))
                RefreshBookmarksFileDisplay();
        };
        value.Bookmarks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasValidBookmarks));
            Session.NotifyDurationChanged();
        };
        HookSession(value);
        RefreshBookmarksFileDisplay();
    }

    /// <summary>
    /// Updates <see cref="BookmarksFileDisplay"/> from
    /// <see cref="Session"/>'s current CsvPath. Shows the filename when
    /// a CSV is bound, "(not loaded)" otherwise.
    /// </summary>
    private void RefreshBookmarksFileDisplay()
    {
        BookmarksFileDisplay = string.IsNullOrEmpty(Session.CsvPath)
            ? "Bookmarks: (not loaded)"
            : "Bookmarks: " + Path.GetFileName(Session.CsvPath);
        OnPropertyChanged(nameof(BookmarksFileName));
    }

    /// <summary>
    /// Handler for the configurable timestamp hotkey. Delegates to
    /// <see cref="SetTimestamp"/> — the same command wired to the menu item
    /// and toolbar button — but only when MPC-HC is actually what the press
    /// should act on.
    /// </summary>
    /// <remarks>
    /// The hook behind this is global: every press of the configured mouse
    /// button or key combination reaches this method no matter which window
    /// has focus, since that is the only way a hotkey can work while MPC-HC
    /// itself is focused. Left unguarded, that means it also reaches this
    /// method for a press meant for something else entirely — the default
    /// binding is the middle mouse button, so middle-clicking a link to open
    /// it in a new tab would silently set a bookmark against whatever MPC-HC
    /// last had loaded.
    ///
    /// So it fires only where it means something: the player is the active
    /// window, and it has a video open. The second half is not covered by the
    /// first — MPC-HC sits there with no file loaded quite happily, and there
    /// is no position to timestamp then. It is the same condition the menu item
    /// and toolbar button are gated on (<see cref="CanSetTimestamp"/>), which a
    /// direct call to <see cref="SetTimestamp"/> would otherwise walk straight
    /// past, since only the generated command consults it.
    ///
    /// A press anywhere else is dropped in silence, with no status message:
    /// there is no reason to think the window that would show one is something
    /// the user is looking at.
    ///
    /// This deliberately does not fire for the cursor merely hovering an
    /// inactive player. That was allowed for a while, so a mouse binding could
    /// mark a moment without pulling focus off whatever else was being worked
    /// in — but it is the same class of accident as the unguarded version, one
    /// stray middle-click away from a bookmark nobody asked for.
    /// </remarks>
    private void OnTimestampHotkey()
    {
        if (!_mpc.IsForeground() || !HasActiveVideo) return;
        SetTimestamp();
    }

    /// <summary>
    /// Until when <see cref="PollMpc"/> must not tear the session down.
    /// </summary>
    /// <remarks>
    /// Loading a video sets the session up immediately, but MPC-HC takes a
    /// moment to actually open the file and report it in its window title.
    /// In that gap the poll saw "no file" and wiped everything — which is why
    /// the video name, duration and edit length would flash and vanish, why
    /// the bookmark CSV silently stopped being written, and why clicking a
    /// timestamp reported "No video loaded" with a video plainly loaded.
    /// </remarks>
    private DateTime _loadGraceUntilUtc = DateTime.MinValue;

    /// <summary>
    /// Drops the loaded video and its bookmarks. Does nothing while a load is
    /// still settling, so a deliberate load is never undone by the poll.
    /// </summary>
    private void ClearLoadedSession()
    {
        if (string.IsNullOrEmpty(_lastLoadedPath)) return;
        if (DateTime.UtcNow < _loadGraceUntilUtc) return;

        _lastLoadedPath = string.Empty;
        Session.VideoPath = string.Empty;
        Session.CsvPath = string.Empty;
        IsBookmarkFileLoaded = false;
        Session.Bookmarks.Clear();
        Session.CurrentTimeSeconds = 0;
        Session.VideoDurationSeconds = 0;
        RefreshBookmarksFileDisplay();
        OnPropertyChanged(nameof(HasValidBookmarks));
        DurationDisplay = "00:00";
        CurrentTimeDisplay = "00:00";
        TimelineProgress = 0;
        ProgressPercent = 0;
        OnPropertyChanged(nameof(Session));

        StatusText = "No video loaded";
    }

    private void PollMpc()
    {
        if (IsBusy) return;
        IsMpcRunning = _mpc.IsRunning;

        // Focus moves between processes without notifying us, so the view
        // condition is re-evaluated on every tick. It returns immediately when
        // nothing needs to change.
        ApplyAutoViewSwitch();
        if (!IsMpcRunning)
        {
            ClearLoadedSession();
            return;
        }

        // Throttle expensive window enumeration: only update position every 600ms
        // instead of every 300ms to avoid hammering the UI thread with repeated
        // EnumChildWindows calls. Use the cached value in between.
        // Refresh the position less often while the window is in the background:
        // nobody is reading the timeline, but polling must not stop entirely
        // because it is also what notices a newly opened video.
        var now = DateTime.UtcNow;
        var positionIntervalMs = _windowFocused ? 600 : 1500;
        if ((now - _lastPositionUpdate).TotalMilliseconds >= positionIntervalMs)
        {
            _stalls.Time("GetPlaybackPosition",
                () => _cachedPlaybackPosition = _mpc.GetPlaybackPosition());
            _lastPositionUpdate = now;
        }

        var (current, durationFromPlayer) = _cachedPlaybackPosition;
        if (current > 0 || Session.CurrentTimeSeconds == 0)
            Session.CurrentTimeSeconds = current;
        if (Session.VideoDurationSeconds <= 0 && durationFromPlayer > 0)
            Session.VideoDurationSeconds = durationFromPlayer;

        CurrentTimeDisplay = Bookmark.FormatTime(Session.CurrentTimeSeconds);
        DurationDisplay = Bookmark.FormatTime(Session.VideoDurationSeconds);
        if (Session.VideoDurationSeconds > 0)
            TimelineProgress = Math.Clamp(Session.CurrentTimeSeconds / Session.VideoDurationSeconds, 0, 1);

        // While a "Play all" / "Play selected" loop is sequencing through
        // bookmarks, do NOT auto-load a new file even if MPC-HC's window
        // title momentarily reports a different path (it can glitch during
        // rapid seeks). The playback loop is in control of seeks/plays.
        if (IsPlayingCuts) return;

        // Determine what file MPC-HC currently has open.
        // When no video is loaded the title is just "Media Player Classic"
        // (or contains no real filename), so GetCurrentFilePath() returns null
        // or a string without a file extension (e.g. "Home Cinema").
        string? path = null;
        _stalls.Time("GetCurrentFilePath", () => path = _mpc.GetCurrentFilePath());
        if (string.IsNullOrEmpty(path) || !Path.HasExtension(path))
        {
            ClearLoadedSession();
            return;
        }

        // MPC-HC's window title usually shows only the bare filename, not
        // the full path (e.g. "clip.mp4 - Media Player Classic"). If that's
        // what GetCurrentFilePath() handed back, and it matches the file we
        // already know is loaded, this is NOT a new video — it's the same
        // one we opened via File → Open/Recent, just observed through the
        // title bar. Treat it as unchanged rather than reloading with a
        // directory-less path, which previously corrupted Session.VideoPath
        // and inserted a bogus, unresolvable entry into the recent list on
        // every single open.
        bool sameFile = string.Equals(path, _lastLoadedPath, StringComparison.OrdinalIgnoreCase)
            || (!Path.IsPathRooted(path)
                && string.Equals(Path.GetFileName(path), Path.GetFileName(_lastLoadedPath), StringComparison.OrdinalIgnoreCase));
        if (sameFile) return;

        // Only auto-load a "different" file if it's a real, resolvable
        // path (rooted, or it exists relative to the working directory).
        // A bare filename we can't resolve to an actual file on disk isn't
        // something we can safely add to the recent list or use for CSV/
        // duration lookups, so ignore it rather than polluting state.
        if (!Path.IsPathRooted(path) && !File.Exists(path)) return;

        // Only auto-load actual videos. Handing the player a .pls makes it
        // report the playlist as its current file for a moment, and picking
        // that up bound the session — and the history — to the playlist itself.
        if (!IsVideoFile(path)) return;

        // Fire-and-forget: load the video asynchronously without blocking the poll.
        // Use a flag to prevent concurrent loads if multiple polls fire in quick succession.
        if (!_autoLoadingVideo)
        {
            _autoLoadingVideo = true;
            _ = LoadVideoAsync(path!).ContinueWith(_ => _autoLoadingVideo = false);
        }
    }

    private async Task LoadVideoAsync(string path)
    {
        _lastLoadedPath = path;

        // Hold the poll off while MPC-HC gets around to opening the file and
        // reporting it; see _loadGraceUntilUtc.
        _loadGraceUntilUtc = DateTime.UtcNow.AddSeconds(8);

        Session.VideoPath = path;
        // Actions write to the "Save to" folder, which follows the video until
        // the user pins one. Quick save is a separate setting and applies only
        // to the one-click button on each bookmark row.
        Session.OutputDirectory = ResolveSaveToDirectory();
        if (string.IsNullOrWhiteSpace(Session.OutputDirectory))
            Session.OutputDirectory = Path.GetDirectoryName(path)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // Always bind the conventional CSV path (video.csv next to video.mp4),
        // whether or not the file exists yet. Leaving it empty for a video with
        // no CSV meant SaveBookmarks silently did nothing, so the first
        // session's bookmarks were never written to disk at all. "Loaded" is
        // tracked separately, by whether the file is actually there.
        var csvPath = _bookmarks.GetCsvPathForVideo(path);
        Session.CsvPath = csvPath;
        IsBookmarkFileLoaded = File.Exists(csvPath);
        RefreshBookmarksFileDisplay();
        Session.Bookmarks.Clear();
        if (File.Exists(csvPath))
            foreach (var b in _bookmarks.LoadFromCsv(csvPath))
                Session.Bookmarks.Add(b);


        try
        {
            if (File.Exists(path))
            {
                var dur = await _ffmpeg.GetDurationAsync(path);
                if (dur > 0) Session.VideoDurationSeconds = dur;
            }
        }
        catch { }

        DurationDisplay = Bookmark.FormatTime(Session.VideoDurationSeconds);

        // Only actual videos belong in the history. Handing the player a .pls
        // makes it report the playlist as its current file, and the auto-load
        // path then tried to add the playlist itself as a recent "video".
        // Only actual videos belong in the history. Handing the player a .pls
        // makes it report the playlist as its current file, and the auto-load
        // path then tried to add the playlist itself as a recent "video".
        //
        // Both lists are guarded together: the persisted one and the in-memory
        // one that drives the History menu. Guarding only the former let a
        // non-video sit in the menu for the session and put the two out of sync.
        if (IsVideoFile(path))
        {
            _settings.AddRecent(path);

            // Mirror the persisted list exactly: remove the path if it was
            // already in the collection (so re-playing a video promotes it
            // back to the top), insert at position 0, then trim to MaxHistory.
            for (int i = 0; i < RecentVideos.Count; i++)
            {
                if (string.Equals(RecentVideos[i], path, StringComparison.OrdinalIgnoreCase))
                {
                    RecentVideos.RemoveAt(i);
                    break;
                }
            }
            RecentVideos.Insert(0, path);
            while (RecentVideos.Count > _settings.Current.MaxHistory)
                RecentVideos.RemoveAt(RecentVideos.Count - 1);
        }

        // Never report "<none>" as loaded — that placeholder belongs only to
        // the right panel's CURRENT VIDEO line.
        StatusText = Session.HasVideo
            ? $"Loaded: {Session.VideoFileName}  ({Session.Bookmarks.Count} bookmarks)"
            : "No video loaded";
    }

    /// <summary>
    /// Sets a timestamp at the current MPC-HC playback position. The
    /// system determines automatically whether this opens or closes a
    /// bookmark: if there's no incomplete bookmark, a new one is opened
    /// with this timestamp; if there is an incomplete one, this timestamp
    /// closes it. This is the single user-facing action — wired to the
    /// Bookmarks → "Set timestamp" menu item, the 📍 toolbar button, and
    /// the configurable hotkey. The old separate "Add bookmark (Start)"
    /// and "Complete last (End)" commands have been folded into this.
    /// </summary>
    /// <summary>
    /// The bookmark waiting for its closing timestamp, or null when the next
    /// press should open a new one.
    /// </summary>
    /// <remarks>
    /// This is the anchor that decides what a press does, and it is a question
    /// asked of the bookmark list rather than a flag kept beside it.
    ///
    /// It was a flag — <c>_awaitingEnd</c> — assigned from twelve places, four
    /// of which recomputed it from this very expression. That is two sources
    /// of truth, and they drifted: a press that should have closed bookmark 1
    /// opened bookmark 2 instead, because the flag said "not waiting" while
    /// the list plainly held an open bookmark.
    ///
    /// Last rather than first, so a file hand-edited to contain several open
    /// bookmarks closes the most recent one — the one the user was working on.
    /// </remarks>
    private Bookmark? OpenBookmark => Session.Bookmarks.LastOrDefault(b => b.IsIncomplete);

    [RelayCommand(CanExecute = nameof(CanSetTimestamp))]
    private void SetTimestamp()
    {
        if (IsBusy)
        {
            StatusText = "Another operation is running — wait for it to finish.";
            return;
        }
        if (!_mpc.IsRunning)
        {
            StatusText = "Open a video in MPC-HC first.";
            return;
        }

        // Read once: the list must not be consulted again between deciding and
        // acting, or the two could disagree.
        var open = OpenBookmark;

        if (open == null)
            BeginNewBookmark();
        else
            FinalizeBookmark(open);
    }

    /// <summary>
    /// Opens a new incomplete bookmark at the current playback position.
    /// Called by <see cref="SetTimestamp"/> when no incomplete bookmark
    /// is awaiting its closing timestamp.
    /// </summary>
    private void BeginNewBookmark()
    {
        var (current, _) = _mpc.GetPlaybackPosition();
        if (current > 0) Session.CurrentTimeSeconds = current;

        // A cut starting at exactly 0 is almost always the player reporting
        // position before playback has really begun, and ffmpeg's seek at 0 is
        // unreliable anyway — start at the first second instead.
        var timestamp = Session.CurrentTimeSeconds <= 0 ? 1 : Session.CurrentTimeSeconds;

        // No end time is what makes it open — there is no separate flag to set.
        var nextIndex = Session.Bookmarks.Count + 1;
        var bookmark = new Bookmark { Index = nextIndex, StartSeconds = timestamp, EndSeconds = 0 };
        Session.Bookmarks.Add(bookmark);

        // Persist immediately rather than waiting for the closing timestamp.
        // This is what creates the CSV on the very first press, so the opening
        // timestamp survives a crash or a switch away from the video — and it
        // is the state "Undo last bookmark" deletes the file from.
        SaveBookmarks();

        StatusText = $"Timestamp {nextIndex} set at {Bookmark.FormatTime(timestamp)}  (press again to close)";
        _toast.Show($"Timestamp {nextIndex} set",
                    $"{Bookmark.FormatTime(timestamp)} — press again to close");
    }

    /// <summary>
    /// Closes <paramref name="incomplete"/> at the current playback position.
    /// </summary>
    /// <remarks>
    /// The bookmark is passed in rather than looked up again, so the one this
    /// closes is provably the one <see cref="SetTimestamp"/> decided on.
    /// </remarks>
    private void FinalizeBookmark(Bookmark incomplete)
    {
        var (current, _) = _mpc.GetPlaybackPosition();
        if (current > 0) Session.CurrentTimeSeconds = current;
        var closing = Session.CurrentTimeSeconds;

        // A close at or before the open is not a cut. Rather than fudging it
        // forward by a second — which quietly produced a bogus one-second pair
        // — throw the whole attempt away, so no invalid or half-written entry
        // survives for the next action to trip over.
        if (closing <= incomplete.StartSeconds)
        {
            DiscardOpenBookmark(incomplete,
                $"Closing time {Bookmark.FormatTime(closing)} is not after the opening time " +
                $"{incomplete.StartDisplay} — bookmark {incomplete.Index} discarded");
            return;
        }

        // Setting the end time is what closes it; there is nothing else to say.
        incomplete.EndSeconds = closing;
        SaveBookmarks();
        Session.NotifyDurationChanged();
        StatusText = $"Bookmark {incomplete.Index} closed ({incomplete.DurationDisplay})";
        _toast.Show($"Bookmark {incomplete.Index} closed",
                    $"{incomplete.StartDisplay} → {incomplete.EndDisplay}  ({incomplete.DurationDisplay})",
                    "✅");
    }

    /// <summary>
    /// Removes an open bookmark that cannot be completed validly, leaving no
    /// trace of it in the list or on disk.
    /// </summary>
    /// <remarks>
    /// The point is that a bad entry never outlives the action that created
    /// it. If dropping it empties the list the CSV goes too, rather than
    /// lingering as a zero-byte file that still counts as loaded.
    /// </remarks>
    private void DiscardOpenBookmark(Bookmark bookmark, string reason)
    {
        Session.Bookmarks.Remove(bookmark);

        if (Session.Bookmarks.Count == 0)
        {
            TryDeleteBookmarkFile(out _);
        }
        else
        {
            Renumber();
            SaveBookmarks();
        }

        Session.NotifyDurationChanged();
        StatusText = reason;
        _toast.Show("Bookmark discarded", reason, "⚠");
    }

    /// <summary>
    /// Deletes every bookmark the user has checked (IsSelected = true),
    /// matching how "selected" works everywhere else in the app (Play
    /// selected, merge, split). Falls back to deleting just the
    /// currently-highlighted row (<see cref="SelectedBookmark"/>) if no
    /// checkboxes are checked, so a plain single click + delete still works.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private void DeleteSelected()
    {
        var toDelete = Session.Bookmarks.Where(b => b.IsSelected).ToList();

        if (toDelete.Count == 0 && SelectedBookmark != null)
            toDelete.Add(SelectedBookmark);

        if (toDelete.Count == 0)
        {
            StatusText = "Nothing to delete — check the boxes next to the bookmarks you want, or click a row first.";
            return;
        }

        foreach (var b in toDelete)
            Session.Bookmarks.Remove(b);

        Renumber();
        SaveBookmarks();
        Session.NotifyDurationChanged();
        StatusText = toDelete.Count == 1
            ? $"Deleted bookmark {toDelete[0].Index}"
            : $"Deleted {toDelete.Count} bookmarks";
    }

    /// <summary>
    /// Marks every checked cut for vertical inversion, or clears the mark if
    /// they are all already marked. The [F] prefix on each row is the
    /// indication; the flip is applied by ffmpeg at merge/split time.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleFlip))]
    private void ToggleFlip()
    {
        var selected = Session.Bookmarks.Where(b => b.IsSelected && b.IsValid).ToList();
        if (selected.Count == 0) return;

        // Clear only when every selected cut is already flipped, so a mixed
        // selection turns them all on rather than toggling them out of sync.
        var turningOn = !selected.All(b => b.IsFlipped);
        foreach (var b in selected)
            b.IsFlipped = turningOn;

        Session.NotifyDurationChanged();
        StatusText = turningOn
            ? $"{selected.Count} timestamp pair(s) marked for inversion"
            : $"{selected.Count} timestamp pair(s) restored to normal";
    }

    /// <summary>
    /// Deletes the session's CSV and marks the bookmark file as no longer
    /// loaded. <see cref="EditSession.CsvPath"/> is deliberately kept: it is
    /// the conventional "video.csv next to video.mp4" path, so a later
    /// timestamp recreates the file in the right place.
    /// </summary>
    /// <remarks>
    /// Goes to the Recycle Bin rather than being unlinked. A bookmark file is
    /// a hand-built list of timestamps that can represent a lot of watching,
    /// and it is small — there is no case for making it unrecoverable.
    /// </remarks>
    private bool TryDeleteBookmarkFile(out string error)
    {
        error = string.Empty;
        var csvPath = Session.CsvPath;

        if (!string.IsNullOrEmpty(csvPath) && File.Exists(csvPath))
        {
            if (!RecycleBin.TryDelete(csvPath, out var failure))
            {
                error = failure ?? "unknown error";
                return false;
            }
        }

        IsBookmarkFileLoaded = false;
        RefreshBookmarksFileDisplay();
        return true;
    }

    /// <summary>
    /// Checks every complete bookmark. Incomplete ones are skipped — they
    /// cannot be selected (see <see cref="Bookmark.IsSelected"/>), so this is
    /// explicit about it rather than relying on the setter to ignore them.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void SelectAll()
    {
        foreach (var b in Session.Bookmarks.Where(b => b.IsValid))
            b.IsSelected = true;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNone))]
    private void SelectNone() { foreach (var b in Session.Bookmarks) b.IsSelected = false; }

    /// <summary>
    /// Removes the single most recent <em>timestamp</em>, not the whole
    /// bookmark: a completed pair loses only its closing time and reopens,
    /// and a lone opening time is dropped entirely.
    /// </summary>
    /// <remarks>
    /// Undoing a pair outright meant one stray press cost both timestamps,
    /// when what the last press actually did was close the pair. Stepping
    /// back one entry at a time mirrors how they were added.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanUndoLastBookmark))]
    private void UndoLastBookmark()
    {
        if (Session.Bookmarks.Count == 0)
        {
            StatusText = "Nothing to undo.";
            return;
        }

        var last = Session.Bookmarks[^1];

        // Completed pair: take back the closing timestamp and leave it open,
        // exactly as it was before the last press.
        if (last.IsValid)
        {
            var removedEnd = last.EndDisplay;

            // Clearing the end time is what reopens it, and what clears its
            // selection — the bookmark has no range to be selected for.
            last.EndSeconds = 0;

            SaveBookmarks();
            Session.NotifyDurationChanged();
            StatusText = $"Removed closing timestamp {removedEnd} — bookmark {last.Index} is open again";
            return;
        }

        // Lone opening timestamp: drop the entry.
        var removedStart = last.StartDisplay;
        var removedIndex = last.Index;
        Session.Bookmarks.RemoveAt(Session.Bookmarks.Count - 1);

        // Nothing left to store, so the file goes with it rather than
        // lingering empty and still counting as "loaded".
        if (Session.Bookmarks.Count == 0)
        {
            if (!TryDeleteBookmarkFile(out var error))
            {
                Session.NotifyDurationChanged();
                StatusText = "Undid the timestamp but could not delete the file: " + error;
                return;
            }

            Session.NotifyDurationChanged();
            StatusText = $"Removed timestamp {removedStart} — bookmark file deleted";
            return;
        }

        Renumber();
        SaveBookmarks();
        Session.NotifyDurationChanged();
        StatusText = $"Removed opening timestamp {removedStart} (bookmark {removedIndex})";
    }

    // ------------------------------------------------------------------
    // Cut playback (Play all / Play selected / Stop)
    // ------------------------------------------------------------------

    /// <summary>
    /// Plays every valid cut in the active video, sequentially: seeks to
    /// each bookmark's start, calls Play(), waits for the cut's effective
    /// duration (DurationSeconds / Speed), then seeks to the next
    /// bookmark's start, and so on. Incomplete bookmarks are skipped.
    /// </summary>
    /// <remarks>
    /// The loop is cancellable via <see cref="StopPlayback"/>; starting a
    /// new Play all / Play selected also cancels any in-progress one.
    /// While the loop is running, <see cref="IsPlayingCuts"/> is true so
    /// <see cref="PollMpc"/> knows not to fight the loop with auto-loads.
    /// <see cref="IsBusy"/> is NOT set — the polling timer keeps running
    /// so the user can still see the playback position update in the
    /// status bar.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanPlayAll))]
    private async Task PlayAllAsync()
    {
        var cuts = Session.Bookmarks.Where(b => b.IsValid).OrderBy(b => b.StartSeconds).ToList();
        if (cuts.Count == 0)
        {
            StatusText = "No valid bookmarks to play — complete at least one cut first.";
            return;
        }
        await PlayCutsAsync(cuts, "all");
    }

    /// <summary>
    /// Same as <see cref="PlayAllAsync"/> but restricted to bookmarks the
    /// user has checked (IsSelected = true) and that are valid (have a
    /// proper end time). Falls back to "nothing to play" if none are
    /// selected — does NOT auto-expand to all bookmarks like the merge
    /// command does, because for playback the user's checkmarks are the
    /// whole point.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPlaySelected))]
    private async Task PlaySelectedAsync()
    {
        var cuts = Session.Bookmarks.Where(b => b.IsSelected && b.IsValid)
                                    .OrderBy(b => b.StartSeconds)
                                    .ToList();
        if (cuts.Count == 0)
        {
            StatusText = "No valid selected bookmarks — check at least one cut first.";
            return;
        }
        await PlayCutsAsync(cuts, "selected");
    }

    /// <summary>
    /// Shared implementation for <see cref="PlayAllAsync"/> and
    /// <see cref="PlaySelectedAsync"/>. Sequences through the given list
    /// of bookmarks, seeking + playing each one for its effective
    /// duration. Cancellable via <see cref="_playbackCts"/>.
    /// </summary>
    /// <param name="cuts">The ordered list of bookmarks to play.</param>
    /// <param name="label">"all" or "selected" — used in the status text
    /// so the user can tell which mode is running.</param>
    private async Task PlayCutsAsync(List<Bookmark> cuts, string label)
    {
        if (IsBusy)
        {
            StatusText = "Another operation is running — wait for it to finish.";
            return;
        }
        if (!_mpc.IsRunning || string.IsNullOrEmpty(Session.VideoPath))
        {
            StatusText = "Open a video in MPC-HC first.";
            return;
        }

        // Cancel any previous playback loop before starting a new one.
        _playbackCts?.Cancel();
        _playbackCts = new CancellationTokenSource();
        var token = _playbackCts.Token;

        IsPlayingCuts = true;
        _mpc.BringToFront();

        try
        {
            for (int i = 0; i < cuts.Count; i++)
            {
                if (token.IsCancellationRequested) break;

                var b = cuts[i];
                // Effective play time = real duration divided by speed,
                // so a 2x bookmark plays for half as long, etc.
                var playSeconds = b.DurationSeconds / Math.Max(0.25, b.Speed);

                StatusText = $"Playing {label} cut {i + 1}/{cuts.Count}: " +
                             $"{b.StartDisplay} → {b.EndDisplay}  " +
                             $"({Bookmark.FormatDuration(playSeconds)} at {b.SpeedDisplay})";

                if (!await _mpc.SeekToAsync(b.StartSeconds))
                {
                    StatusText = "Seek failed — playback stopped.";
                    break;
                }

                try
                {
                    // Let the seek land before asking it to play. Seeking can
                    // leave MPC-HC paused (the trackbar fallback in particular),
                    // and a Play sent into a still-settling seek was being
                    // swallowed — which is why every cut after the first sat
                    // paused on its first frame.
                    await Task.Delay(150, token);
                    _mpc.Play();

                    // CMD_PLAY is an explicit play, not a toggle, so re-asserting
                    // it is harmless and covers a seek that paused late.
                    await Task.Delay(200, token);
                    _mpc.Play();

                    // Time the cut against the wall clock. The old loop added a
                    // nominal 100ms per iteration while each actually took ~115,
                    // so it under-counted and every cut ran ~15% long — the
                    // "played past the end" symptom.
                    var clock = Stopwatch.StartNew();
                    var target = TimeSpan.FromSeconds(playSeconds);
                    while (clock.Elapsed < target)
                    {
                        if (token.IsCancellationRequested) break;
                        var remaining = target - clock.Elapsed;
                        await Task.Delay(remaining > TimeSpan.FromMilliseconds(100)
                            ? TimeSpan.FromMilliseconds(100)
                            : remaining, token);
                    }
                }
                catch (TaskCanceledException) { break; }
            }

            if (!token.IsCancellationRequested)
            {
                try { _mpc.Pause(); } catch { }
                StatusText = $"Finished playing {cuts.Count} cut(s).";
            }
            else
            {
                StatusText = "Playback stopped.";
            }
        }
        finally
        {
            IsPlayingCuts = false;
            try { _playbackCts.Dispose(); } catch { }
            _playbackCts = null;
        }
    }

    // ------------------------------------------------------------------
    // Reset everything (process restart)
    // ------------------------------------------------------------------

    /// <summary>
    /// Restarts the application — equivalent to closing and reopening it
    /// manually. Saves the current bookmark CSV first, then launches a
    /// fresh process instance and shuts this one down. Useful when the
    /// user wants to clear transient state (mid-bookmark, error states,
    /// a stuck polling loop) without manually closing/reopening.
    /// </summary>
    /// <remarks>
    /// Settings, recent videos, suffixes, and shortcuts all persist
    /// across the restart (they live in settings.json). Only the
    /// in-memory session state is reset, which is exactly the intent.
    /// </remarks>
    [RelayCommand]
    private void ResetEverything()
    {
        var result = MessageBox.Show(
            "Reset everything?\n\nThis will restart the application. Your " +
            "settings, recent videos, suffixes, and shortcuts are all " +
            "preserved (they live in settings.json). Only the current " +
            "session state (loaded video, bookmarks, mid-bookmark) is reset.\n\n" +
            "Any unsaved bookmark changes will be saved to the CSV before " +
            "the restart.",
            "Reset everything",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            // Persist the current bookmark state so we don't lose anything
            // the user just added via the hotkey (incomplete bookmarks
            // included — SaveBookmarks writes them as "start," lines).
            SaveBookmarks();
        }
        catch { /* best-effort; don't block the restart on a save error */ }

        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath == null || !File.Exists(exePath))
            {
                MessageBox.Show(
                    "Could not determine the application executable path " +
                    "for restart. Please close and reopen the app manually.",
                    "Reset failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // UseShellExecute=true so the new process is independent of
            // this one (doesn't die when we shut down).
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not restart the application:\n\n" + ex.Message +
                "\n\nPlease close and reopen the app manually.",
                "Reset failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the bookmark CSV in a text editor. Edits are picked up by
    /// <see cref="ReloadBookmarksFromDisk"/> when this window regains focus.
    /// </summary>
    /// <remarks>
    /// Notepad unless the machine associates .csv with a different *text*
    /// editor, in which case that one is used. Anything else — Excel being
    /// the usual culprit — would silently requote and reformat the file on
    /// save, so we fall back to the "Open with" picker and let the user
    /// choose.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanEditBookmarks))]
    private void EditBookmarks()
    {
        var csv = Session.CsvPath;
        if (string.IsNullOrEmpty(csv) || !File.Exists(csv))
        {
            StatusText = "No bookmark file to edit.";
            return;
        }

        OpenInTextEditor(csv, "bookmark file",
                         " — changes load when this window regains focus");
    }

    /// <summary>
    /// Opens a plain-text file in a text editor.
    /// </summary>
    /// <remarks>
    /// Never the shell's default handler. Both file types this is used for are
    /// registered to something that acts on them rather than shows them — .csv
    /// to a spreadsheet that would reformat it on save, .pls to a media player
    /// that simply starts playing it. Notepad unless the extension is already
    /// associated with a known text editor; otherwise the "Open with" picker.
    /// </remarks>
    /// <param name="path">File to open.</param>
    /// <param name="label">What it is, for the status message.</param>
    /// <param name="suffix">Optional extra text appended to the status message.</param>
    private void OpenInTextEditor(string path, string label, string suffix = "")
    {
        var editor = ResolveTextEditor(Path.GetExtension(path));
        try
        {
            if (editor != null)
            {
                Process.Start(new ProcessStartInfo(editor, $"\"{path}\"") { UseShellExecute = true });
                StatusText = $"Opened {Path.GetFileName(path)}{suffix}";
            }
            else
            {
                Process.Start(new ProcessStartInfo("rundll32.exe",
                    $"shell32.dll,OpenAs_RunDLL \"{path}\"") { UseShellExecute = true });
                StatusText = $"Pick a text editor to view the {label}{suffix}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open the {label}: {ex.Message}";
        }
    }

    /// <summary>
    /// A text editor safe to open <paramref name="extension"/> with, or null
    /// when the association points at something that would act on the file
    /// rather than display it.
    /// </summary>
    private static string? ResolveTextEditor(string extension)
    {
        // A small allow-list beats trying to classify arbitrary handlers:
        // these are the editors that round-trip a plain text file untouched.
        string[] safe = { "notepad.exe", "notepad++.exe", "code.exe", "sublime_text.exe", "gvim.exe" };

        var assoc = AssociatedExecutableFor(extension);
        if (assoc != null)
        {
            var leaf = Path.GetFileName(assoc);
            if (safe.Any(s => string.Equals(s, leaf, StringComparison.OrdinalIgnoreCase)))
                return assoc;

            // Associated with something else (Excel, LibreOffice, …) — the
            // caller falls back to the "Open with" picker.
            return null;
        }

        return "notepad.exe";
    }

    /// <summary>Best-effort lookup of the executable registered for an extension.</summary>
    private static string? AssociatedExecutableFor(string extension)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c assoc {extension}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var assoc = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit();
            if (string.IsNullOrEmpty(assoc) || !assoc.Contains('=')) return null;

            var fileType = assoc[(assoc.IndexOf('=') + 1)..].Trim();

            psi = new ProcessStartInfo("cmd.exe", $"/c ftype {fileType}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var q = Process.Start(psi);
            var ftype = q?.StandardOutput.ReadToEnd().Trim();
            q?.WaitForExit();
            if (string.IsNullOrEmpty(ftype) || !ftype.Contains('=')) return null;

            var command = ftype[(ftype.IndexOf('=') + 1)..].Trim();
            if (command.StartsWith('"'))
            {
                var end = command.IndexOf('"', 1);
                return end > 1 ? command[1..end] : null;
            }
            var space = command.IndexOf(' ');
            return space > 0 ? command[..space] : command;
        }
        catch { return null; }
    }

    /// <summary>
    /// Re-reads the bookmark CSV after an external edit. An emptied file is
    /// treated as "delete these bookmarks": the file is removed and the
    /// bookmark status goes back to not loaded.
    /// </summary>
    public void ReloadBookmarksFromDisk()
    {
        var csv = Session.CsvPath;
        if (string.IsNullOrEmpty(csv) || !IsBookmarkFileLoaded) return;

        if (!File.Exists(csv))
        {
            Session.Bookmarks.Clear();
            IsBookmarkFileLoaded = false;
            RefreshBookmarksFileDisplay();
            Session.NotifyDurationChanged();
            return;
        }

        var stamp = File.GetLastWriteTimeUtc(csv);
        if (stamp == _lastBookmarkWriteUtc) return;
        _lastBookmarkWriteUtc = stamp;

        var loaded = _bookmarks.LoadFromCsv(csv);

        if (loaded.Count == 0)
        {
            // Saved with everything deleted — take that at face value.
            Session.Bookmarks.Clear();
            TryDeleteBookmarkFile(out _);
            Session.NotifyDurationChanged();
            StatusText = "Bookmark file was emptied — deleted it";
            return;
        }

        Session.Bookmarks.Clear();
        int i = 1;
        foreach (var b in loaded)
        {
            b.Index = i++;
            Session.Bookmarks.Add(b);
        }
        Session.NotifyDurationChanged();
        StatusText = $"Reloaded {loaded.Count} bookmark(s) from disk";
    }

    private DateTime _lastBookmarkWriteUtc;

    // ------------------------------------------------------------------
    // Output filename collisions
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a path that does not exist yet, asking the user what to do
    /// each time the candidate is taken. Returns null if they cancel.
    /// </summary>
    /// <remarks>
    /// Loops rather than asking once: a rename or an increment can collide
    /// too, and the user has to be re-asked until the name is actually free.
    /// </remarks>
    private Task<string?> ResolveOutputPathAsync(string candidate)
    {
        // Policy first: a name we are about to write must satisfy
        // FileNameRules, whatever the source file happens to be called.
        candidate = EnforceFileNamePolicy(candidate, out var cancelled);
        if (cancelled) return Task.FromResult<string?>(null);

        while (File.Exists(candidate))
        {
            // Already told how to handle the rest of the batch.
            if (_conflictAllChoice == ConflictResult.Overwrite)
                return Task.FromResult<string?>(candidate);

            if (_conflictAllChoice == ConflictResult.Increment)
            {
                candidate = IncrementSuffix(candidate);
                continue;
            }

            var dlg = new ConflictDialog(Path.GetFileName(candidate),
                                         Path.GetFileName(IncrementSuffix(candidate)),
                                         offerApplyToAll: _batchRemaining > 1)
            { Owner = null };

            if (dlg.ShowDialog() != true) return Task.FromResult<string?>(null);

            // Remember it only for the choices that can repeat. A name typed
            // into Rename would collide with itself on the next file, so that
            // one always asks again.
            if (dlg.ApplyToAll &&
                dlg.Result is ConflictResult.Overwrite or ConflictResult.Increment)
            {
                _conflictAllChoice = dlg.Result;
            }

            switch (dlg.Result)
            {
                case ConflictResult.Overwrite:
                    // ffmpeg is invoked with -y, so it replaces it.
                    return Task.FromResult<string?>(candidate);

                case ConflictResult.Increment:
                    candidate = IncrementSuffix(candidate);
                    break;

                case ConflictResult.Rename when !string.IsNullOrWhiteSpace(dlg.NewName):
                    // Only the base name changes; the bracket suffix stays.
                    var dir = Path.GetDirectoryName(candidate) ?? "";
                    var ext = Path.GetExtension(candidate);
                    var (_, bracket) = FileNameRules.SplitSuffix(
                        Path.GetFileNameWithoutExtension(candidate));

                    candidate = Path.Combine(dir, dlg.NewName + bracket + ext);

                    // A hand-typed replacement has to satisfy the policy too.
                    candidate = EnforceFileNamePolicy(candidate, out var renameCancelled);
                    if (renameCancelled) return Task.FromResult<string?>(null);
                    break;

                default:
                    return Task.FromResult<string?>(null);
            }
        }

        return Task.FromResult<string?>(candidate);
    }

    /// <summary>
    /// Returns a path whose filename satisfies <see cref="FileNameRules"/>,
    /// asking the user to rename it when it does not. The source file is left
    /// alone — only what we are about to write has to comply.
    /// </summary>
    /// <param name="cancelled">True if the user backed out.</param>
    /// <summary>
    /// How many files remain in the current batch, so the rename prompt knows
    /// whether "do this for all remaining files" is worth offering.
    /// </summary>
    private int _batchRemaining;

    /// <summary>
    /// Set once the user ticks "do this for all remaining files": later names
    /// that break the rules are corrected silently instead of prompting.
    /// </summary>
    private bool _autoCorrectNames;

    /// <summary>
    /// Set once the user ticks "do this for all remaining files" on the
    /// file-exists prompt. Only ever Overwrite or Increment.
    /// </summary>
    private ConflictResult? _conflictAllChoice;

    /// <summary>
    /// Starts a batch of <paramref name="fileCount"/> files, resetting the
    /// "apply to all" choice to whatever Settings says. A decision made during
    /// one batch must not silently carry into the next — least of all
    /// Overwrite, which would destroy files without asking.
    /// </summary>
    /// <remarks>
    /// The collision preference is seeded here rather than checked at the
    /// prompt, because "apply to all" and "always do this" want identical
    /// behaviour and this is already the one place that decides it. A setting
    /// of Ask leaves it null, which is exactly the old behaviour.
    /// </remarks>
    private void BeginNameBatch(int fileCount)
    {
        _batchRemaining = fileCount;
        _autoCorrectNames = false;

        _conflictAllChoice = _settings.Current.OnNameCollision switch
        {
            CollisionPolicy.Increment => ConflictResult.Increment,
            CollisionPolicy.Overwrite => ConflictResult.Overwrite,
            _ => null
        };
    }

    private string EnforceFileNamePolicy(string candidate, out bool cancelled)
    {
        cancelled = false;

        var dir = Path.GetDirectoryName(candidate) ?? "";
        var ext = Path.GetExtension(candidate);
        var (stem, suffix) = FileNameRules.SplitSuffix(Path.GetFileNameWithoutExtension(candidate));

        // Spaces are corrected without asking. They are in most media
        // filenames, dashes are the only sensible substitution, and there is
        // nothing here for the user to weigh up — being prompted on virtually
        // every operation was the whole problem. Anything else still asks.
        stem = FileNameRules.NormalizeSpaces(stem);

        // The bracket suffix comes from the naming tag, which is already
        // constrained, so only the stem is worth checking.
        while (!FileNameRules.IsValid(stem))
        {
            // Already told to handle the rest — take the suggestion and move on.
            if (_autoCorrectNames)
            {
                stem = FileNameRules.Sanitize(stem);
                continue;
            }

            var dlg = new RenameFileDialog(
                Path.GetFileName(candidate), stem, suffix, ext,
                "This output filename contains characters that are not allowed. " +
                "Enter a name to save it as.",
                offerApplyToAll: _batchRemaining > 1)
            { Owner = null };

            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.NewStem))
            {
                cancelled = true;
                return candidate;
            }

            // Applies from the NEXT file onward — the name typed here is still
            // used for this one.
            if (dlg.ApplyToAll) _autoCorrectNames = true;

            stem = dlg.NewStem;
        }

        return Path.Combine(dir, stem + suffix + ext);
    }

    /// <summary>
    /// The container every video operation writes, from settings. Read fresh
    /// each time rather than cached, so a change in the Settings dialog
    /// applies to the very next operation.
    /// </summary>
    private VideoFormats.Format OutputFormat => _settings.GetDefaultVideoFormat();

    /// <summary>
    /// The suffixed output path without any collision handling —
    /// <c>&lt;name&gt;[done].mp4</c>. Unlike
    /// <see cref="GetUniqueOutputPath"/> this does not skip past existing
    /// files; <see cref="ResolveOutputPathAsync"/> asks the user instead.
    /// </summary>
    private string GetSuffixedOutputPath(string basePath, string extension, string? outputDirectory = null)
    {
        var dir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(basePath) ?? ""
            : outputDirectory;

        return Path.Combine(dir,
            $"{Path.GetFileNameWithoutExtension(basePath)}[{_settings.GetActiveSuffixText()}]{extension}");
    }

    /// <summary>
    /// Bumps the number inside the trailing bracket suffix:
    /// <c>[done]</c> → <c>[done2]</c>, <c>[done2]</c> → <c>[done3]</c>,
    /// <c>[cs3]</c> → <c>[cs4]</c>. A suffix with no number starts at 2.
    /// Names with no bracket at all get one appended from the active tag.
    /// </summary>
    private string IncrementSuffix(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var ext = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(path);

        var open = stem.LastIndexOf('[');
        if (open < 0 || !stem.EndsWith(']'))
            return Path.Combine(dir, $"{stem}[{_settings.GetActiveSuffixText()}2]{ext}");

        var head = stem[..open];
        var inner = stem[(open + 1)..^1];

        // Split the trailing digits off the suffix text.
        int digits = inner.Length;
        while (digits > 0 && char.IsDigit(inner[digits - 1])) digits--;

        var text = inner[..digits];
        var number = inner[digits..];
        var next = string.IsNullOrEmpty(number) ? 2 : int.Parse(number) + 1;

        return Path.Combine(dir, $"{head}[{text}{next}]{ext}");
    }

    /// <summary>
    /// Handles a file dropped on the window. Dispatches by extension the same
    /// way File ▸ Open… does: a video plays and loads its sibling CSV, a CSV
    /// loads as the bookmark set, a .pls loads and starts playing.
    /// </summary>
    [RelayCommand]
    private async Task OpenDroppedFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "Dropped file not found.";
            return;
        }

        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".csv":
                LoadBookmarksFromCsv(path);
                // LoadBookmarksFromCsv already picks up a sibling video of the
                // same base name and loads it.
                break;

            case ".pls":
                LoadPlaylist(path);
                await PlayFirstAvailableAsync(path);
                break;

            default:
                await OpenRecentVideo(path);
                break;
        }
    }

    /// <summary>
    /// Loads a playlist and starts playing it. Wired to Playlist ▸ "Load
    /// playlist…" and to each playlist's "Load this playlist" sub-item.
    /// </summary>
    [RelayCommand]
    private async Task LoadPlaylistAndPlay(string? plsPath)
    {
        var before = LoadedPlaylistPath;
        LoadPlaylist(plsPath);

        // LoadPlaylist prompts when given null, and refuses an empty playlist,
        // so re-read what it settled on rather than assuming it took.
        var loaded = LoadedPlaylistPath;
        if (string.IsNullOrEmpty(loaded) || !File.Exists(loaded)) return;
        if (loaded == before && plsPath == null) return;

        await PlayFirstAvailableAsync(loaded);
    }

    /// <summary>
    /// Plays the first entry of a playlist that still exists on disk, skipping
    /// missing ones, and loads it as the current video.
    /// </summary>
    private async Task PlayFirstAvailableAsync(string plsPath)
    {
        var entries = _playlists.ReadEntries(plsPath);
        var first = entries.FirstOrDefault(File.Exists);

        if (first == null)
        {
            StatusText = entries.Count == 0
                ? $"{Path.GetFileName(plsPath)} is empty"
                : $"No playable entries in {Path.GetFileName(plsPath)}";
            return;
        }

        // Hand the player the playlist itself, once, so the whole list ends up
        // in its playlist and it starts on the first entry. Launching the first
        // video instead would leave the player holding a single file.
        _mpc.LaunchVideo(plsPath);
        await Task.Delay(500);
        _mpc.BringToFront();

        // Then bind our own session to that first entry. LoadVideoAsync only
        // reads metadata — it launches nothing, so there is no second launch
        // to race the one above.
        await LoadVideoAsync(first);
    }

    /// <summary>
    /// The "any" arm of Merge: pick two or more files and join them. Same
    /// work Bulk merge does, reached automatically when there is no loaded
    /// video with cuts to merge instead.
    /// </summary>
    private async Task MergeArbitraryFilesAsync()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm|All Files|*.*",
            Multiselect = true,
            Title = "Select 2+ videos to merge"
        };
        if (ofd.ShowDialog() != true) return;

        if (ofd.FileNames.Length < 2)
        {
            Notify("Select at least 2 files.", "Merge",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var basePath = GetUniqueOutputPath(ofd.FileNames[0], ".mp4",
            outputDirectory: Path.GetDirectoryName(ofd.FileNames[0]));
        // Single output: reset so a previous batch cannot carry its
        // "apply to all" decision into this prompt.
        BeginNameBatch(1);
        var outPath = await ResolveOutputPathAsync(basePath);
        if (outPath == null) return;

        IsBusy = true; ProgressPercent = 0; StatusText = "Merging…";
        Job.Begin("Merge files", ofd.FileNames.Length);
        Job.SetFile(1, Path.GetFileName(outPath));
        try
        {
            var progress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                ProgressPercent = p.Percent;
                StatusText = p.Message;
                Job.Report(p.Message, p.Percent);
            });
            await _ffmpeg.ConcatFilesAsync(ofd.FileNames, outPath, progress);
            StatusText = $"Merged {ofd.FileNames.Length} files → {Path.GetFileName(outPath)}";
        }
        catch (Exception ex) { StatusText = "Merge failed"; MessageBox.Show(ex.Message); }
        finally { IsBusy = false; ProgressPercent = 0; Job.End(); }
    }

    /// <summary>
    /// Deletes the bookmark file and clears the list, without the
    /// confirmation prompt that Clear all shows.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteBookmarks))]
    private void DeleteBookmarks()
    {
        var csvPath = Session.CsvPath;
        if (MessageBox.Show($"Delete the bookmark file?\n\n{csvPath}", "Delete bookmarks",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        Session.Bookmarks.Clear();

        if (!TryDeleteBookmarkFile(out var error))
        {
            StatusText = "Could not delete the bookmark file: " + error;
            return;
        }

        Session.NotifyDurationChanged();
        StatusText = $"Deleted {Path.GetFileName(csvPath)}";
    }

    [RelayCommand(CanExecute = nameof(CanEnterTimeManual))]
    private void EnterTimeManual()
    {
        const string basePrompt =
            "Single time = set one timestamp (incomplete)\n" +
            "Range (1:00 - 2:30) = full bookmark";

        var prompt = basePrompt;
        var value = CurrentTimeDisplay;

        // Loop rather than closing on a bad value: the entry is re-shown with
        // what went wrong and what is accepted, so the user can correct it or
        // cancel — deciding for themselves rather than starting over.
        while (true)
        {
            var dlg = new InputDialog("Enter time / range", prompt, value);
            if (dlg.ShowDialog() != true) return;

            value = dlg.Value.Trim();

            // A dash separates a range, but only when it is not a leading
            // minus on a single value.
            var dash = value.IndexOfAny(new[] { '-', '–' }, 1);
            if (dash > 0)
            {
                var left = value[..dash].Trim();
                var right = value[(dash + 1)..].Trim();

                if (!TryParseFlexibleTime(left, out var start, out var startError))
                {
                    prompt = $"{startError}\n\n{TimeFormatHelp}";
                    continue;
                }
                if (!TryParseFlexibleTime(right, out var end, out var endError))
                {
                    prompt = $"{endError}\n\n{TimeFormatHelp}";
                    continue;
                }
                if (end <= start)
                {
                    prompt = $"The end time ({Bookmark.FormatTime(end)}) must be after the " +
                             $"start time ({Bookmark.FormatTime(start)}).\n\n{TimeFormatHelp}";
                    continue;
                }

                Session.Bookmarks.Add(new Bookmark
                {
                    Index = Session.Bookmarks.Count + 1,
                    StartSeconds = start,
                    EndSeconds = end
                });
                StatusText = $"Added bookmark {Bookmark.FormatTime(start)} → {Bookmark.FormatTime(end)}";
            }
            else
            {
                if (!TryParseFlexibleTime(value, out var start, out var error))
                {
                    prompt = $"{error}\n\n{TimeFormatHelp}";
                    continue;
                }

                Session.Bookmarks.Add(new Bookmark
                {
                    Index = Session.Bookmarks.Count + 1,
                    StartSeconds = start,
                    EndSeconds = 0
                });
                StatusText = $"Opened bookmark at {Bookmark.FormatTime(start)}";
            }

            SaveBookmarks();
            Session.NotifyDurationChanged();
            return;
        }
    }

    /// <summary>What the time field accepts, shown when a value is rejected.</summary>
    private const string TimeFormatHelp =
        "Accepted formats:\n" +
        "  90            seconds\n" +
        "  1:30          minutes:seconds\n" +
        "  1:02:03       hours:minutes:seconds\n" +
        "  22s  5m  1h   with a unit\n" +
        "  1m30s         units combined\n" +
        "A range is two of those separated by a dash, e.g. 1:00 - 2:30.";

    /// <summary>
    /// Parses a time, reporting why rather than throwing. Accepts colon form
    /// (<c>1:02:03</c>), plain seconds, and unit-suffixed values
    /// (<c>22s</c>, <c>5m</c>, <c>1h</c>, <c>1m30s</c>).
    /// </summary>
    private static bool TryParseFlexibleTime(string? input, out double seconds, out string error)
    {
        seconds = 0;
        error = string.Empty;

        var s = (input ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            error = "No time was entered.";
            return false;
        }

        // Unit form: any of h / m / s, e.g. "22s", "1m30s", "1h2m3s".
        var units = Regex.Match(s,
            @"^\s*(?:(?<h>\d+(?:\.\d+)?)\s*h)?\s*(?:(?<m>\d+(?:\.\d+)?)\s*m)?\s*(?:(?<s>\d+(?:\.\d+)?)\s*s)?\s*$",
            RegexOptions.IgnoreCase);
        if (units.Success && (units.Groups["h"].Success || units.Groups["m"].Success || units.Groups["s"].Success))
        {
            seconds = Part(units, "h") * 3600 + Part(units, "m") * 60 + Part(units, "s");
            return true;
        }

        // Colon form, or bare seconds.
        var parts = s.Split(':');
        if (parts.Length > 3)
        {
            error = $"\"{s}\" has too many colons.";
            return false;
        }

        double total = 0;
        foreach (var part in parts)
        {
            if (!double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                error = $"\"{part.Trim()}\" is not a number.";
                return false;
            }
            total = total * 60 + value;
        }

        if (total < 0)
        {
            error = "A time cannot be negative.";
            return false;
        }

        seconds = total;
        return true;

        static double Part(Match m, string name) =>
            m.Groups[name].Success
                ? double.Parse(m.Groups[name].Value, CultureInfo.InvariantCulture)
                : 0;
    }

    /// <summary>
    /// Joins the checked cuts, or every cut when none are checked. With no
    /// video and bookmarks to work from it falls back to asking which files
    /// to join, so the command is always available.
    /// </summary>
    /// <remarks>
    /// One cut is a legitimate job, not a failed merge: the result is that
    /// single span written out on its own — a trim. It runs down exactly the
    /// same path as a many-cut merge (the concat of one segment is that
    /// segment), so nothing downstream needs to special-case it.
    ///
    /// Because a lone cut is now workable, an explicit single check is
    /// honoured as such. Previously one checked cut was indistinguishable
    /// from none and quietly merged everything instead.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanMergeAlways))]
    private async Task MergeSelectedAsync()
    {
        var haveSource = !string.IsNullOrEmpty(Session.VideoPath) && File.Exists(Session.VideoPath);

        var toMerge = Session.Bookmarks.Where(b => b.IsSelected && b.IsValid).ToList();
        if (toMerge.Count == 0) toMerge = Session.Bookmarks.Where(b => b.IsValid).ToList();

        // Nothing to work from in the current session — merge arbitrary files.
        if (!haveSource || toMerge.Count == 0)
        {
            await MergeArbitraryFilesAsync();
            return;
        }

        // Default filename uses the active suffix and the configured
        // container: <name>[done].mp4
        var format = OutputFormat;
        var defaultName = GetSuffixedOutputPath(Session.VideoPath, format.Extension, ResolveSaveToDirectory());
        var dlg = new SaveFileDialog
        {
            Filter = VideoFormats.SaveFilter(format),
            FileName = Path.GetFileName(defaultName),
            InitialDirectory = ResolveSaveToDirectory(),
            // Our own conflict flow handles this, and it offers Increment as
            // well as Overwrite/Rename. Leaving this on would ask twice.
            OverwritePrompt = false
        };
        if (dlg.ShowDialog() != true) return;

        // Single output: reset so a previous batch cannot carry its
        // "apply to all" decision into this prompt.
        BeginNameBatch(1);
        var outPath = await ResolveOutputPathAsync(dlg.FileName);
        if (outPath == null) return;

        // A single cut is a trim; say so rather than reporting a merge of one.
        var trimming = toMerge.Count == 1;

        // Captured before the operation: cleanup must act on the file this run
        // consumed, not on whatever happens to be loaded when it finishes.
        var source = Session.VideoPath;
        var succeeded = false;

        IsBusy = true; ProgressPercent = 0; StatusText = trimming ? "Trimming…" : "Merging…";
        Job.Begin(trimming ? "Trim cut" : "Merge cuts");
        Job.SetFile(1, Path.GetFileName(outPath));
        try
        {
            var progress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                ProgressPercent = p.Percent;
                StatusText = p.Message;
                Job.Report(p.Message, p.Percent);
            });
            await _ffmpeg.MergeBookmarksAsync(Session.VideoPath, outPath, toMerge, progress, default, format);

            Job.Report("Complete", 100);
            await Task.Delay(600);
            StatusText = $"Created {Path.GetFileName(outPath)}";
            succeeded = true;
        }
        catch (Exception ex) { StatusText = trimming ? "Trim failed" : "Merge failed"; MessageBox.Show(ex.Message); }
        finally { IsBusy = false; ProgressPercent = 0; Job.End(); }

        // Only after the panel is down and the output is on disk.
        if (succeeded) await RunPostOperationCleanup(source, includeBookmarks: true, justWrote: outPath);
    }

    [RelayCommand(CanExecute = nameof(CanSplitSelected))]
    private async Task SplitSelectedAsync()
    {
        var toSplit = Session.Bookmarks.Where(b => b.IsSelected && b.IsValid).ToList();
        if (toSplit.Count == 0) toSplit = Session.Bookmarks.Where(b => b.IsValid).ToList();
        if (toSplit.Count == 0) { Notify("No valid bookmarks."); return; }

        var outDir = Path.Combine(Session.OutputDirectory, Path.GetFileNameWithoutExtension(Session.VideoFileName) + "_clips");
        Directory.CreateDirectory(outDir);

        var format = OutputFormat;
        var source = Session.VideoPath;
        var succeeded = false;

        IsBusy = true; ProgressPercent = 0;
        try
        {
            // Linear naming: clip 1 = <name>[done].mp4, clip 2 = <name>[done2].mp4, etc.
            // Collisions are put to the user rather than silently skipped past,
            // so a second split into the same folder is a deliberate choice.
            Job.Begin("Split clips", toSplit.Count);
            BeginNameBatch(toSplit.Count);
            int i = 0, written = 0;
            foreach (var b in toSplit)
            {
                i++; ProgressPercent = (double)i / toSplit.Count * 100; StatusText = $"Splitting {i}/{toSplit.Count}";
                _batchRemaining = toSplit.Count - i + 1;
                Job.SetFile(i, Path.GetFileName(Session.VideoPath));
                Job.Report($"Cutting {b.StartDisplay} → {b.EndDisplay}", (double)(i - 1) / toSplit.Count * 100);

                var outPath = await ResolveOutputPathAsync(
                    BuildSplitPath(outDir, Session.VideoFileName, i, format.Extension));
                if (outPath == null) continue;   // cancelled this clip

                await _ffmpeg.MergeBookmarksAsync(Session.VideoPath, outPath, new[] { b }, null, default, format);
                written++;
            }
            // Let the bar land on 100% and hold it briefly. Previously a "Done"
            // dialog appeared the instant the last clip finished, freezing the
            // bar short of full and demanding a click before anything moved on.
            Job.Report("Complete", 100);
            await Task.Delay(600);

            StatusText = $"Created {written} clip(s) in {outDir}";

            // A run in which every clip was cancelled wrote nothing, so there
            // is nothing the originals are redundant to.
            succeeded = written > 0;
        }
        catch (Exception ex) { StatusText = "Split failed"; MessageBox.Show(ex.Message); }
        finally { IsBusy = false; ProgressPercent = 0; Job.End(); }

        if (succeeded) await RunPostOperationCleanup(source, includeBookmarks: true);
    }

    /// <summary>
    /// Builds a split-clip output path in <paramref name="outDir"/> using
    /// the active suffix and the given 1-based index. Index 1 produces
    /// <c>&lt;name&gt;[done].mp4</c>; index 2+ produces
    /// <c>&lt;name&gt;[done2].mp4</c>, <c>&lt;name&gt;[done3].mp4</c>, etc.
    /// </summary>
    private string BuildSplitPath(string outDir, string videoFileName, int index, string? extension = null)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(videoFileName);
        var suffix = _settings.GetActiveSuffixText();
        var bracket = index == 1 ? $"[{suffix}]" : $"[{suffix}{index}]";
        return Path.Combine(outDir, $"{nameWithoutExt}{bracket}{extension ?? OutputFormat.Extension}");
    }

    /// <summary>
    /// Splits a single bookmark into its own clip — the "radio button"
    /// one-click split from the bookmark list. Output goes to the
    /// quick save folder, falling back to the source video's own directory
    /// when no quick save folder is set (see
    /// <see cref="ResolveQuickSaveDirectory"/>), using the active suffix and
    /// auto-incrementing on collision via
    /// <see cref="GetUniqueOutputPath"/>. No save dialog — the click is
    /// the entire action.
    /// </summary>
    /// <param name="bookmark">The bookmark to extract. Must be valid
    /// (complete + EndSeconds &gt; StartSeconds) or the command no-ops
    /// with a status message.</param>
    [RelayCommand]
    private async Task SplitSingle(Bookmark? bookmark)
    {
        if (bookmark == null)
        {
            StatusText = "No bookmark to split.";
            return;
        }
        if (!bookmark.IsValid)
        {
            StatusText = "Bookmark is incomplete — close it before splitting.";
            return;
        }
        if (string.IsNullOrEmpty(Session.VideoPath) || !File.Exists(Session.VideoPath))
        {
            Notify("Source video not found.", "Split clip",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // The one-click button is the only thing that uses the quick save
        // folder; everything else writes to "Save to".
        var outDir = ResolveQuickSaveDirectory();
        if (string.IsNullOrWhiteSpace(outDir))
        {
            Notify("Could not determine where to save the clip.", "Split clip",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BeginNameBatch(1);
        var format = OutputFormat;

        // Correct the name first, then walk for a free one. The other order
        // looks equivalent and is not: correcting a name after checking it is
        // free can land right back on an existing file.
        var named = EnforceFileNamePolicy(
            GetSuffixedOutputPath(Session.VideoPath, format.Extension, outDir),
            out var nameCancelled);
        if (nameCancelled) return;

        // Every row builds its name from the video rather than the bookmark,
        // so all of them propose the same file. GetUniqueOutputPath walks
        // [done], [done2], [done3]… until one is free, which keeps the click a
        // single action as intended.
        //
        // This used to call ResolveOutputPathAsync, which prompts instead: the
        // first click wrote video[done].mp4 and every click after it reported
        // that file as already existing — which, by then, it was.
        var correctedStem = FileNameRules.SplitSuffix(
            Path.GetFileNameWithoutExtension(named)).Stem;

        var outPath = GetUniqueOutputPath(
            Path.Combine(outDir, correctedStem + format.Extension),
            format.Extension,
            outputDirectory: outDir);

        IsBusy = true;
        ProgressPercent = 0;
        try
        {
            StatusText = $"Splitting clip {bookmark.Index} → {Path.GetFileName(outPath)}";
            ProgressPercent = 50;

            // Reuse the same FFmpeg path that SplitSelected uses — it
            // honours Speed and IsFlipped on the bookmark.
            await _ffmpeg.MergeBookmarksAsync(Session.VideoPath, outPath, new[] { bookmark }, null, default, format);

            ProgressPercent = 100;
            StatusText = $"Created clip → {outPath}";
            _toast.Show($"Clip {bookmark.Index} saved",
                        Path.GetFileName(outPath),
                        "🎬");
        }
        catch (Exception ex)
        {
            StatusText = "Split failed: " + ex.Message;
            MessageBox.Show(ex.Message, "Split clip failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
            ProgressPercent = 0;
        }
    }

    /// <summary>
    /// Converts picked images to <paramref name="formatKey"/>, following the
    /// same naming tag, filename policy and collision handling as every
    /// other action.
    /// </summary>
    /// <param name="formatKey">
    /// One of <see cref="ImageConversionService.Formats"/>, supplied as the
    /// CommandParameter of the menu item that was clicked.
    /// </param>
    [RelayCommand]
    private async Task ConvertImages(string? formatKey)
    {
        var format = ImageConversionService.FindFormat(formatKey);
        if (format == null)
        {
            StatusText = $"Unknown image format '{formatKey}'.";
            return;
        }

        var pattern = string.Join(";", ImageConversionService.ReadableExtensions.Select(e => "*" + e));
        var ofd = new OpenFileDialog
        {
            Filter = $"Image Files|{pattern}|All Files|*.*",
            Multiselect = true,
            Title = $"Select images to convert to {format.Display}"
        };
        if (ofd.ShowDialog() != true) return;

        IsBusy = true;
        int done = 0;
        var errors = new List<string>();

        // Sources that converted cleanly, and so are safe to offer for
        // deletion afterwards. Skipped, cancelled and failed files never make
        // it in, so a failure can never cost the original.
        var convertedSources = new List<string>();

        Job.Begin($"Convert images to {format.Display}", ofd.FileNames.Length);
        BeginNameBatch(ofd.FileNames.Length);

        foreach (var file in ofd.FileNames)
        {
            _batchRemaining = ofd.FileNames.Length - done;
            Job.SetFile(done + 1, Path.GetFileName(file));
            Job.Report($"Writing {format.Display}", (double)done / ofd.FileNames.Length * 100);
            StatusText = $"Converting {Path.GetFileName(file)}…";

            var outPath = await ResolveOutputPathAsync(
                GetSuffixedOutputPath(file, format.Extension, ResolveSaveToDirectory()));
            if (outPath == null) { done++; continue; }

            try
            {
                // Quick, but pushed off the UI thread so a large batch cannot
                // freeze the window mid-run.
                await Task.Run(() => _images.Convert(file, outPath, format));

                // Overwriting in place means the "original" is the file we just
                // wrote — deleting it would throw away the conversion.
                if (!string.Equals(file, outPath, StringComparison.OrdinalIgnoreCase))
                    convertedSources.Add(file);
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }

            done++;
            ProgressPercent = (double)done / ofd.FileNames.Length * 100;
            Job.Report($"Writing {format.Display}", ProgressPercent);
        }

        Job.Report("Complete", 100);
        await Task.Delay(600);

        IsBusy = false; ProgressPercent = 0;
        Job.End();

        StatusText = errors.Count == 0
            ? $"Converted {done} image(s) to {format.Display}"
            : $"Finished with {errors.Count} error(s)";

        if (errors.Count > 0)
            MessageBox.Show(string.Join("\n", errors), "Image conversion",
                MessageBoxButton.OK, MessageBoxImage.Warning);

        OfferToDeleteSources(convertedSources, format);
    }

    // ------------------------------------------------------------------
    // Post-operation cleanup
    // ------------------------------------------------------------------

    /// <summary>
    /// Single-source overload — see the list version.
    /// </summary>
    private Task RunPostOperationCleanup(string? sourceVideo, bool includeBookmarks, string? justWrote = null)
    {
        var sources = string.IsNullOrWhiteSpace(sourceVideo)
            ? new List<string>()
            : new List<string> { sourceVideo };

        return RunPostOperationCleanup(sources, includeBookmarks, justWrote);
    }

    /// <summary>
    /// Applies the File ▸ Settings cleanup preferences once an operation has
    /// succeeded: the source video, and for the operations that consume it,
    /// the bookmark file.
    /// </summary>
    /// <param name="sourceVideos">
    /// Only files the operation actually consumed successfully. A failed,
    /// skipped or cancelled file must never reach here — a cleanup that can
    /// fire after a failure is a cleanup that destroys work.
    /// </param>
    /// <param name="includeBookmarks">
    /// False for Convert, which operates on files it picked itself and has no
    /// claim on whatever bookmark file the session happens to hold.
    /// </param>
    /// <param name="justWrote">
    /// The output path, when the operation had one. The save dialog will
    /// happily aim the output at the source file, and deleting "the original"
    /// would then delete the result — so it is excluded by name.
    /// </param>
    /// <remarks>
    /// Everything goes to the Recycle Bin rather than being unlinked, which is
    /// what makes "delete without asking" a defensible option at all: the
    /// worst case is a trip to the bin, not lost footage.
    ///
    /// Deliberately not awaited anywhere — it runs after the progress panel is
    /// down, so its prompts read as a follow-up question rather than as part
    /// of the operation.
    /// </remarks>
    private async Task RunPostOperationCleanup(List<string> sourceVideos, bool includeBookmarks, string? justWrote = null)
    {
        // A source that no longer exists (already cleaned up by an earlier
        // run, or moved) is silently dropped rather than reported — the user
        // wanted it gone and it is.
        var videos = sourceVideos
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .Where(p => !string.Equals(p, justWrote, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var csv = includeBookmarks ? Session.CsvPath : null;
        var haveCsv = !string.IsNullOrWhiteSpace(csv) && File.Exists(csv);

        var videoMode = _settings.Current.DeleteOriginalVideo;
        var csvMode = _settings.Current.DeleteBookmarksFile;

        var deleted = new List<string>();
        var failed = new List<string>();

        if (videos.Count > 0 && videoMode != CleanupMode.Never)
        {
            var ask = videoMode == CleanupMode.Ask;
            var prompt = videos.Count == 1
                ? $"Delete the original video?\n\n{Path.GetFileName(videos[0])}\n\nIt will go to the Recycle Bin."
                : $"Delete the {videos.Count} original video files this operation used?\n\nThey will go to the Recycle Bin.";

            if (!ask || Confirm(prompt, "Delete original video"))
            {
                // MPC-HC holds an open handle on the file it is playing, and
                // will not let go on its own — no amount of retrying gets past
                // that. Closing the media releases it; the player stays up.
                var playing = videos.FirstOrDefault(p =>
                    string.Equals(p, Session.VideoPath, StringComparison.OrdinalIgnoreCase));

                if (playing != null) _mpc.CloseFile();

                foreach (var path in videos)
                    await RecycleAsync(path, deleted, failed);

                // The session still points at a file that is now in the bin,
                // and the player has nothing loaded either. Leaving the old
                // path on screen invites the next action to fail on it.
                if (playing != null && deleted.Contains(playing))
                    ClearLoadedSession();
            }
        }

        if (haveCsv && csvMode != CleanupMode.Never)
        {
            var ask = csvMode == CleanupMode.Ask;
            var prompt = $"Delete the bookmarks file?\n\n{Path.GetFileName(csv!)}\n\nIt will go to the Recycle Bin.";

            if (!ask || Confirm(prompt, "Delete bookmarks file"))
            {
                // The list on screen would otherwise describe a file that is
                // gone, so clear it first and let the shared helper do the
                // delete — it is the same path Bookmarks ▸ Delete bookmarks
                // takes, including marking the file unloaded.
                Session.Bookmarks.Clear();

                if (TryDeleteBookmarkFile(out var csvError))
                {
                    deleted.Add(csv!);
                    Session.NotifyDurationChanged();
                }
                else
                {
                    failed.Add($"{Path.GetFileName(csv!)}: {csvError}");
                }
            }
        }

        if (deleted.Count > 0)
            StatusText = deleted.Count == 1
                ? $"Moved {Path.GetFileName(deleted[0])} to the Recycle Bin"
                : $"Moved {deleted.Count} file(s) to the Recycle Bin";

        if (failed.Count > 0)
            MessageBox.Show(string.Join("\n", failed), "Delete after operation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>Yes/No prompt that defaults to No, so Enter can never delete.</summary>
    private static bool Confirm(string prompt, string caption) =>
        MessageBox.Show(prompt, caption, MessageBoxButton.YesNo,
                        MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    /// <summary>
    /// Recycles one file, waiting for it to be released first.
    /// </summary>
    /// <remarks>
    /// Async so the wait does not freeze the window. Cleanup runs the moment
    /// an operation reports success, and ffmpeg's handle on the source
    /// outlives its own exit by a fraction of a second — long enough for a
    /// delete issued immediately to fail with a sharing violation.
    /// </remarks>
    private static async Task<bool> RecycleAsync(string path, List<string> deleted, List<string> failed)
    {
        var (ok, error) = await RecycleBin.TryDeleteAsync(path);

        if (ok)
        {
            deleted.Add(path);
            return true;
        }

        failed.Add($"{Path.GetFileName(path)}: {error}");
        return false;
    }

    private static bool Recycle(string path, List<string> deleted, List<string> failed)
    {
        if (RecycleBin.TryDelete(path, out var error))
        {
            deleted.Add(path);
            return true;
        }

        failed.Add($"{Path.GetFileName(path)}: {error}");
        return false;
    }

    /// <summary>
    /// Asks whether the originals should be deleted once conversion is done.
    /// </summary>
    /// <remarks>
    /// Only the sources that actually converted are offered, so a file that
    /// failed, was skipped or was cancelled can never be lost. This keeps its
    /// dialog — deleting the user's images is precisely the sort of thing a
    /// status-bar line should not decide silently, and it defaults to No.
    /// </remarks>
    private void OfferToDeleteSources(List<string> sources, ImageConversionService.Format format)
    {
        if (sources.Count == 0) return;

        var prompt =
            $"Delete the {sources.Count} original image(s) that were converted to {format.Display}?\n\n" +
            "They will go to the Recycle Bin.";

        if (!Confirm(prompt, "Delete originals")) return;

        var deletedPaths = new List<string>();
        var failed = new List<string>();

        foreach (var file in sources)
            Recycle(file, deletedPaths, failed);

        int deleted = deletedPaths.Count;

        StatusText = failed.Count == 0
            ? $"Moved {deleted} original image(s) to the Recycle Bin"
            : $"Deleted {deleted}, could not delete {failed.Count}";

        if (failed.Count > 0)
            MessageBox.Show(string.Join("\n", failed), "Delete originals",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [RelayCommand]
    private async Task ConvertFilesAsync()
    {
        var ofd = new OpenFileDialog { Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.ts;*.m4v|All Files|*.*", Multiselect = true };
        if (ofd.ShowDialog() != true) return;

        var format = OutputFormat;
        var label = format.Key.ToUpperInvariant();

        // Sources that converted cleanly, and so are safe to offer for
        // deletion afterwards. Skipped, cancelled and failed files never make
        // it in, so a failure can never cost the original.
        var convertedSources = new List<string>();

        IsBusy = true; int done = 0; var errors = new List<string>();
        Job.Begin("Convert video", ofd.FileNames.Length);
        BeginNameBatch(ofd.FileNames.Length);
        foreach (var file in ofd.FileNames)
        {
            _batchRemaining = ofd.FileNames.Length - done;
            Job.SetFile(done + 1, Path.GetFileName(file));
            Job.Report($"Encoding to {label}", (double)done / ofd.FileNames.Length * 100);
            // Suffix-based output: <name>[done].mp4 in the "Save to" folder.
            // A name that is already taken is put to the user rather than
            // silently skipped past.
            var outPath = await ResolveOutputPathAsync(
                GetSuffixedOutputPath(file, format.Extension, ResolveSaveToDirectory()));
            if (outPath == null) { done++; continue; }

            StatusText = $"Converting {Path.GetFileName(file)}…";
            ProgressPercent = (double)done / ofd.FileNames.Length * 100;

            // Scale this file's own progress into its slice of the batch, so
            // the bar advances smoothly across all of them rather than jumping.
            var slice = 100.0 / ofd.FileNames.Length;
            var basePct = done * slice;
            var fileProgress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                Job.Report(p.Message, basePct + p.Percent / 100.0 * slice);
                ProgressPercent = basePct + p.Percent / 100.0 * slice;
            });

            try
            {
                await _ffmpeg.ConvertVideoAsync(file, outPath, format, fileProgress);

                // Converting in place means the "original" is the file we just
                // wrote — deleting it would throw away the conversion.
                if (!string.Equals(file, outPath, StringComparison.OrdinalIgnoreCase))
                    convertedSources.Add(file);
            }
            catch (Exception ex) { errors.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
            done++;
        }
        // Land on 100% before the panel disappears.
        Job.Report("Complete", 100);
        await Task.Delay(600);

        IsBusy = false; ProgressPercent = 0;
        Job.End();
        StatusText = errors.Count == 0 ? $"Converted {done} file(s) to {label}" : $"Finished with {errors.Count} error(s)";
        if (errors.Count > 0) MessageBox.Show(string.Join("\n", errors));

        // Convert has no bookmark file of its own — the loaded session's CSV
        // belongs to a different video and must not be swept up here.
        await RunPostOperationCleanup(convertedSources, includeBookmarks: false);
    }

    [RelayCommand]
    private async Task StripAudioAsync()
    {
        var ofd = new OpenFileDialog { Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm|All Files|*.*", Multiselect = true };
        if (ofd.ShowDialog() != true) return;
        IsBusy = true; int done = 0;
        Job.Begin("Strip audio", ofd.FileNames.Length);
        BeginNameBatch(ofd.FileNames.Length);
        foreach (var file in ofd.FileNames)
        {
            _batchRemaining = ofd.FileNames.Length - done;
            StatusText = $"Extracting: {Path.GetFileName(file)}";
            ProgressPercent = (double)done / ofd.FileNames.Length * 100;
            Job.SetFile(done + 1, Path.GetFileName(file));
            Job.Report("Extracting audio to MP3", (double)done / ofd.FileNames.Length * 100);

            // Suffix applies to audio extraction too: <name>[done].mp3
            var outPath = await ResolveOutputPathAsync(
                GetSuffixedOutputPath(file, ".mp3", ResolveSaveToDirectory()));
            if (outPath == null) { done++; continue; }

            var slice = 100.0 / ofd.FileNames.Length;
            var basePct = done * slice;
            var fileProgress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                Job.Report(p.Message, basePct + p.Percent / 100.0 * slice);
                ProgressPercent = basePct + p.Percent / 100.0 * slice;
            });

            try { await _ffmpeg.StripAudioAsync(file, outPath, fileProgress); } catch (Exception ex) { MessageBox.Show(ex.Message); }
            done++;
        }
        Job.Report("Complete", 100);
        await Task.Delay(600);

        IsBusy = false; ProgressPercent = 0; Job.End(); StatusText = "Audio extraction finished";
    }

    [RelayCommand]
    private void SetPlaylistFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Select playlist folder" };
        if (dlg.ShowDialog() != true) return;
        _settings.Current.PlaylistFolder = dlg.FolderName.TrimEnd('\\') + "\\";
        _settings.Save();
        RefreshFolderDisplays();
        StatusText = PlaylistFolderDisplay;
        PlaylistsChanged?.Invoke();
    }

    /// <summary>
    /// Adds the currently-loaded video to a playlist. If a playlist has
    /// been deliberately loaded (via Playlist → Load playlist… or by
    /// opening a .pls file from File → Open…), the video is added to
    /// that loaded playlist directly with no prompt. Otherwise, the user
    /// is asked to pick a playlist by name (with the option to type a new
    /// one). Raises <see cref="PlaylistsChanged"/> on success.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddCurrentToPlaylist))]
    private void AddCurrentToPlaylist()
    {
        if (!Session.HasVideo) { Notify("No video loaded."); return; }

        // Fast path: a playlist is deliberately loaded — add to it directly.
        if (!string.IsNullOrEmpty(LoadedPlaylistPath) && File.Exists(LoadedPlaylistPath))
        {
            _playlists.AddFiles(LoadedPlaylistPath, new[] { Session.VideoPath });
            StatusText = $"Added {Path.GetFileName(Session.VideoPath)} → {Path.GetFileName(LoadedPlaylistPath)}";
            PlaylistsChanged?.Invoke();
            return;
        }

        // No loaded playlist — prompt the user to pick one.
        var folder = _settings.Current.PlaylistFolder;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { Notify("Set a playlist folder first, or use Playlist → Load playlist… to load one."); return; }
        var list = _playlists.ListPlaylists(folder).ToList();
        var names = list.Select(Path.GetFileName).ToArray();
        var pick = new InputDialog("Add to playlist", "Enter playlist filename:\n" + string.Join("\n", names.Take(12)), names.FirstOrDefault() ?? "playlist.pls");
        if (pick.ShowDialog() != true) return;
        var target = list.FirstOrDefault(p => string.Equals(Path.GetFileName(p), pick.Value, StringComparison.OrdinalIgnoreCase))
            ?? Path.Combine(folder, pick.Value.EndsWith(".pls") ? pick.Value : pick.Value + ".pls");
        _playlists.AddFiles(target, new[] { Session.VideoPath });
        StatusText = $"Added to {Path.GetFileName(target)}";
        PlaylistsChanged?.Invoke();
    }

    // ------------------------------------------------------------------
    // Per-playlist management (open entry / remove entry / delete
    // playlist / open playlist file / new playlist / add current to
    // named playlist). The Playlist menu in MainWindow.xaml.cs is built
    // dynamically from disk and calls these methods directly — they're
    // exposed as commands too so they could be bound in XAML if needed.
    // After any mutation, <see cref="PlaylistsChanged"/> is raised so
    // the code-behind can rebuild the menu.
    // ------------------------------------------------------------------

    /// <summary>
    /// Raised whenever a playlist is created, deleted, or has an entry
    /// added/removed, and also when the playlist folder itself changes.
    /// MainWindow.xaml.cs subscribes to this and rebuilds the dynamic
    /// Playlist menu in response.
    /// </summary>
    public event Action? PlaylistsChanged;

    /// <summary>
    /// Reads the video file paths from a .pls playlist, in order.
    /// Exposed so MainWindow.xaml.cs can build the Playlist menu's
    /// per-entry sub-items without taking a direct dependency on
    /// <see cref="PlaylistService"/>.
    /// </summary>
    public List<string> ReadPlaylistEntriesForMenu(string plsPath)
    {
        var result = new List<string>();
        _stalls.Time($"ReadPlaylistEntries({Path.GetFileName(plsPath)})",
            () => result = _playlists.ReadEntries(plsPath));
        return result;
    }

    /// <summary>
    /// Lets the view report how long a menu rebuild took, so the stall log
    /// attributes UI-thread time spent in code-behind menu construction.
    /// </summary>
    public void TimeUiWork(string operation, Action work) => _stalls.Time(operation, work);

    /// <summary>
    /// Opens a single video file from a playlist entry in MPC-HC, just
    /// like clicking a recent video. If the file no longer exists, the
    /// user is offered the choice to remove the dead entry from its
    /// parent playlist.
    /// </summary>
    [RelayCommand]
    private async Task OpenPlaylistEntry(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "No playlist entry selected.";
            return;
        }

        if (!File.Exists(path))
        {
            var result = MessageBox.Show(
                $"This video file no longer exists:\n\n{path}\n\n" +
                "Remove the dead entry from its playlist?",
                "File not found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            // The caller (MainWindow.xaml.cs) passes the plsPath + index
            // via the Tag, but since this command only takes the path,
            // we can't remove it from here. The code-behind's click
            // handler is responsible for offering removal — this branch
            // is a fallback that just informs the user.
            StatusText = "File not found — use 'Remove entry' on the playlist menu item.";
            return;
        }

        bool launched = _mpc.LaunchVideo(path);
        if (!launched)
        {
            StatusText = "Could not launch MPC-HC.";
            return;
        }

        await LoadVideoAsync(path);
        await Task.Delay(300);
        _mpc.BringToFront();
    }

    /// <summary>
    /// Removes a single entry (by 1-based index) from a .pls playlist
    /// file. The remaining entries are renumbered so they stay contiguous.
    /// Raises <see cref="PlaylistsChanged"/> so the menu rebuilds.
    /// </summary>
    /// <param name="args">A tuple of <c>(PlsPath, Index)</c> — the
    /// absolute path to the .pls file and the 1-based index of the entry
    /// to remove. Passed as a single tuple because <c>[RelayCommand]</c>
    /// only supports a single parameter; the code-behind builds the tuple
    /// when wiring up the click handler.</param>
    [RelayCommand]
    private void RemovePlaylistEntry((string PlsPath, int Index) args)
    {
        var (plsPath, index) = args;
        if (string.IsNullOrEmpty(plsPath) || index < 1)
        {
            StatusText = "Invalid playlist entry.";
            return;
        }

        if (_playlists.RemoveEntry(plsPath, index))
        {
            StatusText = $"Removed entry {index} from {Path.GetFileName(plsPath)}";
            PlaylistsChanged?.Invoke();
        }
        else
        {
            StatusText = "Could not remove entry — it may already be gone.";
        }
    }

    /// <summary>
    /// Deletes an entire .pls playlist file from disk after confirming
    /// with the user. Raises <see cref="PlaylistsChanged"/> on success.
    /// </summary>
    [RelayCommand]
    private void DeletePlaylist(string? plsPath)
    {
        if (string.IsNullOrWhiteSpace(plsPath))
        {
            StatusText = "No playlist selected.";
            return;
        }
        if (!File.Exists(plsPath))
        {
            StatusText = "Playlist file not found.";
            PlaylistsChanged?.Invoke();
            return;
        }

        var entryCount = _playlists.ReadEntries(plsPath).Count;
        var msg = entryCount == 0
            ? $"Delete this empty playlist?\n\n{Path.GetFileName(plsPath)}"
            : $"Delete \"{Path.GetFileName(plsPath)}\" and its {entryCount} entr(y/ies)?\n\nThis cannot be undone.";

        if (MessageBox.Show(msg, "Delete playlist",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        if (_playlists.DeletePlaylist(plsPath))
        {
            // If we just deleted the loaded playlist, clear the loaded
            // state too so subsequent "Add to playlist" actions don't
            // silently route to a now-nonexistent file.
            if (string.Equals(LoadedPlaylistPath, plsPath, StringComparison.OrdinalIgnoreCase))
                LoadedPlaylistPath = null;
            StatusText = $"Deleted playlist: {Path.GetFileName(plsPath)}";
            PlaylistsChanged?.Invoke();
        }
        else
        {
            StatusText = "Could not delete playlist — it may be in use by another program.";
            MessageBox.Show("Could not delete the playlist file — it may be locked by another program.",
                "Delete failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Opens the .pls file itself in the user's default handler (usually
    /// MPC-HC or VLC), which loads all the playlist's videos into that
    /// player's internal playlist. Uses shell-execute so the registered
    /// handler is invoked regardless of which player the user has set as
    /// default for .pls files.
    /// </summary>
    [RelayCommand]
    private void OpenPlaylistFile(string? plsPath)
    {
        if (string.IsNullOrWhiteSpace(plsPath))
        {
            StatusText = "No playlist selected.";
            return;
        }
        if (!File.Exists(plsPath))
        {
            StatusText = "Playlist file not found.";
            PlaylistsChanged?.Invoke();
            return;
        }

        // Deliberately a text editor, not the shell's default handler: .pls is
        // registered to a media player, so "open" launched the playlist and
        // started playing it instead of showing its contents.
        OpenInTextEditor(plsPath, "playlist");
    }

    /// <summary>
    /// Creates a new empty .pls playlist in the playlist folder. Prompts
    /// for a name (auto-appends <c>.pls</c> if omitted). Raises
    /// <see cref="PlaylistsChanged"/> on success.
    /// </summary>
    [RelayCommand]
    private void NewPlaylist()
    {
        var folder = _settings.Current.PlaylistFolder;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            Notify("Set a playlist folder first (Playlist → Playlist folder…).",
                "New playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new InputDialog("New playlist",
            "Playlist filename (letters, numbers, spaces, -, _):",
            "new_playlist.pls");
        if (dlg.ShowDialog() != true) return;

        var name = dlg.Value.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Notify("Name cannot be empty.", "New playlist",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!name.EndsWith(".pls", StringComparison.OrdinalIgnoreCase))
            name += ".pls";

        // Validate filename characters.
        char[] invalid = Path.GetInvalidFileNameChars();
        if (name.IndexOfAny(invalid) >= 0)
        {
            Notify("Name contains invalid filename characters.",
                "New playlist", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var fullPath = Path.Combine(folder, name);
        if (File.Exists(fullPath))
        {
            Notify("A playlist with that name already exists.",
                "New playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_playlists.CreatePlaylist(fullPath))
        {
            StatusText = $"Created playlist: {name}";
            PlaylistsChanged?.Invoke();
        }
        else
        {
            StatusText = "Could not create playlist.";
        }
    }

    /// <summary>
    /// Adds the currently-loaded video to a specific .pls playlist (no
    /// InputDialog picker — the playlist is passed in directly by the
    /// code-behind when the user clicks "Add current video" on a
    /// playlist's submenu). Raises <see cref="PlaylistsChanged"/>.
    /// </summary>
    [RelayCommand]
    private void AddCurrentToPlaylistNamed(string? plsPath)
    {
        if (string.IsNullOrWhiteSpace(plsPath))
        {
            StatusText = "No playlist selected.";
            return;
        }
        if (!Session.HasVideo)
        {
            Notify("No video loaded.", "Add to playlist",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!File.Exists(plsPath))
        {
            StatusText = "Playlist file not found.";
            PlaylistsChanged?.Invoke();
            return;
        }

        _playlists.AddFiles(plsPath, new[] { Session.VideoPath });
        StatusText = $"Added {Path.GetFileName(Session.VideoPath)} → {Path.GetFileName(plsPath)}";
        PlaylistsChanged?.Invoke();
    }

    [RelayCommand]
    private void SetQuickSaveFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Select quick save folder" };
        if (dlg.ShowDialog() != true) return;
        _settings.Current.QuickSaveFolder = dlg.FolderName.TrimEnd('\\') + "\\";
        _settings.Save();
        RefreshFolderDisplays();
        StatusText = QuickSaveFolderDisplay;
    }

    /// <summary>
    /// Pins the destination for merge / split / convert / strip audio / bulk
    /// merge. Session-only: a new run goes back to following the video.
    /// </summary>
    [RelayCommand]
    private void SetSaveToFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Select where actions should save" };
        if (dlg.ShowDialog() != true) return;

        _pinnedSaveToFolder = dlg.FolderName.TrimEnd('\\') + "\\";
        Session.OutputDirectory = _pinnedSaveToFolder;

        // Only written when the user asked for it to persist. Otherwise the
        // pin stays a session-scoped decision, as it always has been.
        if (_settings.Current.RememberSaveToFolder)
        {
            _settings.Current.SaveToFolder = _pinnedSaveToFolder;
            _settings.Save();
        }

        RefreshFolderDisplays();
        StatusText = SaveToFolderDisplay;
    }

    // ------------------------------------------------------------------
    // Quick save shortcuts (File menu). Distinct from the Shortcuts menu:
    // these set the quick save folder rather than opening anything.
    // ------------------------------------------------------------------

    /// <summary>Adds a folder to the quick save shortcut list, named after its last segment.</summary>
    [RelayCommand]
    private void AddQuickSaveShortcut()
    {
        var dlg = new OpenFolderDialog { Title = "Select a folder to add as a quick save shortcut" };
        if (dlg.ShowDialog() != true) return;

        var path = dlg.FolderName.TrimEnd('\\');
        if (QuickSaveShortcuts.Any(s => string.Equals(s.Path, path + "\\", StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = "That folder is already a quick save shortcut.";
            return;
        }

        // Display just the leaf folder ("i:\test\folder\cars" -> "cars");
        // the menu item's tooltip carries the full path.
        var entry = new ShortcutEntry
        {
            Path = path + "\\",
            Name = Path.GetFileName(path) is { Length: > 0 } leaf ? leaf : path
        };

        QuickSaveShortcuts.Add(entry);
        _settings.Current.QuickSaveShortcuts.Add(entry);
        _settings.Save();
        StatusText = $"Added quick save shortcut: {entry.Name}";
    }

    /// <summary>Points the quick save folder at a shortcut.</summary>
    [RelayCommand]
    private void SetQuickSaveShortcut(ShortcutEntry? entry)
    {
        if (entry == null) return;

        _settings.Current.QuickSaveFolder = entry.Path;
        _settings.Save();
        RefreshFolderDisplays();
        StatusText = $"Quick save set to {entry.Path}";
    }

    /// <summary>
    /// Removes a quick save shortcut from the list.
    /// </summary>
    /// <remarks>
    /// Deliberately leaves <c>QuickSaveFolder</c> alone. The shortcut list and
    /// the "Quick save:" destination are independent: a shortcut is a way to
    /// set that destination, not a thing the destination depends on. Clearing
    /// it here meant removing a shortcut silently unset a destination the user
    /// had chosen — even when they only happened to point at the same folder.
    /// </remarks>
    [RelayCommand]
    private void RemoveQuickSaveShortcut(ShortcutEntry? entry)
    {
        if (entry == null) return;

        QuickSaveShortcuts.Remove(entry);
        _settings.Current.QuickSaveShortcuts.RemoveAll(
            s => string.Equals(s.Path, entry.Path, StringComparison.OrdinalIgnoreCase));

        _settings.Save();
        StatusText = $"Removed quick save shortcut: {entry.Name}";
    }

    /// <summary>
    /// Opens the "Set Timestamp Hotkey" dialog where the user can pick a
    /// mouse button (Middle / Side 1 / Side 2) or capture a keyboard
    /// combo (e.g. Ctrl+Shift+T). The chosen binding is persisted to
    /// settings.json and applied to the live hook immediately.
    /// </summary>
    [RelayCommand]
    private void SetTimestampHotkey()
    {
        var current = _hotkeys.Binding;
        var dlg = new CaptureHotkeyDialog(current) { Owner = null };
        if (dlg.ShowDialog() != true) return;
        if (dlg.Result == null) return;

        var newBinding = dlg.Result;
        _settings.SetTimestampHotkey(newBinding);
        _hotkeys.Binding = newBinding;

        // If the new binding is "None", stop the hooks entirely (no point
        // running them if nothing will fire). Otherwise make sure the
        // hooks are running so the new binding takes effect.
        if (newBinding.Kind == HotkeyBinding.HotkeyKind.None)
            _hotkeys.Stop();
        else
            _hotkeys.Start();

        UpdateHotkeyStatus();
        StatusText = HotkeyStatus;
    }

    /// <summary>
    /// Quick convenience command for the "Disable hotkey" menu entry —
    /// equivalent to opening the dialog and clicking Disable, but
    /// without the extra clicks.
    /// </summary>
    [RelayCommand]
    private void DisableTimestampHotkey()
    {
        if (_hotkeys.Binding.Kind == HotkeyBinding.HotkeyKind.None) return;
        _settings.SetTimestampHotkey(HotkeyBinding.None);
        _hotkeys.Binding = HotkeyBinding.None;
        _hotkeys.Stop();
        UpdateHotkeyStatus();
        StatusText = "Hotkey disabled.";
    }

    /// <summary>
    /// Writes the current bookmarks to the session's CSV, creating the file
    /// if it does not exist yet, and marks the bookmark file as loaded.
    /// </summary>
    [RelayCommand]
    private void SaveBookmarks()
    {
        if (string.IsNullOrEmpty(Session.CsvPath)) return;

        // Never bring an empty CSV into existence. ResetEverything saves on the
        // way out, and with no bookmarks that wrote a zero-byte file which the
        // next run loaded, counted as "loaded", then deleted on first focus —
        // reporting "Bookmark file was emptied" for a file the user never made.
        if (Session.Bookmarks.Count == 0) return;

        try
        {
            _bookmarks.SaveToCsv(Session.CsvPath, Session.Bookmarks);

            // Writing the first timestamp is what brings the bookmark file
            // into existence, so this is where "loaded" becomes true.
            IsBookmarkFileLoaded = File.Exists(Session.CsvPath);
            RefreshBookmarksFileDisplay();
        }
        catch (Exception ex) { StatusText = "Save failed: " + ex.Message; }
    }

    /// <summary>
    /// Unified "Open…" command for File → Open… Dispatches by file
    /// extension: a video file is loaded into the editor and launched in
    /// MPC-HC; a .csv file is loaded as the active bookmark set (and a
    /// sibling video with the same base name is loaded too, if present);
    /// a .pls file is loaded as the active playlist (so subsequent "Add
    /// current video to playlist" actions route to it without a picker).
    /// Unknown extensions pop a brief status message and do nothing.
    /// </summary>
    [RelayCommand]
    private async Task OpenFile()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "All supported|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.ts;*.m4v;*.csv;*.pls|" +
                     "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.ts;*.m4v|" +
                     "Bookmark CSV|*.csv|" +
                     "Playlist|*.pls|" +
                     "All Files|*.*",
            Title = "Open video, bookmark CSV, or playlist"
        };
        if (ofd.ShowDialog() != true) return;

        var path = ofd.FileName;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "File not found.";
            return;
        }

        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

        switch (ext)
        {
            case "csv":
                LoadBookmarksFromCsv(path);
                break;
            case "pls":
                LoadPlaylist(path);
                break;
            default:
                // Treat anything else as a video — launch it in
                // MPC-HC so it actually starts playing, then load
                // metadata + bookmarks into the editor.
                _mpc.LaunchVideo(path);
                await LoadVideoAsync(path);
                break;
        }
    }

    /// <summary>
    /// Loads bookmarks from a .csv file into the session, replacing any
    /// existing bookmarks. If a video file with the same base name exists
    /// next to the CSV, that video is loaded too — this matches the
    /// convention (video.csv lives next to video.mp4).
    /// </summary>
    private void LoadBookmarksFromCsv(string csvPath)
    {
        var loaded = _bookmarks.LoadFromCsv(csvPath);

        Session.Bookmarks.Clear();
        int i = 1;
        foreach (var b in loaded)
        {
            b.Index = i++;
            Session.Bookmarks.Add(b);
        }
        Session.CsvPath = csvPath;
        IsBookmarkFileLoaded = File.Exists(csvPath);
        RefreshBookmarksFileDisplay();
        Session.NotifyDurationChanged();

        // If a video path with the same base name as the CSV happens to
        // exist alongside it, treat that as the source video too — this
        // matches the original convention (video.csv lives next to video.mp4).
        var siblingVideo = Path.ChangeExtension(csvPath, null);
        var candidates = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".ts", ".mpg", ".mpeg" };
        foreach (var ext in candidates)
        {
            var p = siblingVideo + ext;
            if (File.Exists(p))
            {
                _ = LoadVideoAsync(p);
                break;
            }
        }

        StatusText = $"Loaded {Session.Bookmarks.Count} bookmark(s) from {Path.GetFileName(csvPath)}";
    }

    /// <summary>
    /// Loads a .pls playlist file as the "active" playlist — subsequent
    /// "Add current video to playlist" actions route to it directly
    /// without prompting the user to pick. Also shell-executes the .pls
    /// so the user's default player opens the playlist and starts
    /// playback of its first entry. The loaded playlist is NOT persisted
    /// across app restarts; it survives until cleared, deleted, or the
    /// app exits.
    /// </summary>
    [RelayCommand]
    private void LoadPlaylist(string? plsPath)
    {
        if (string.IsNullOrWhiteSpace(plsPath))
        {
            // No-arg form: prompt with a file picker so the user can
            // browse for a .pls file outside the configured playlist
            // folder as well.
            var ofd = new OpenFileDialog
            {
                Filter = "Playlist|*.pls|All Files|*.*",
                Title = "Load playlist"
            };
            if (ofd.ShowDialog() != true) return;
            plsPath = ofd.FileName;
        }

        if (string.IsNullOrWhiteSpace(plsPath) || !File.Exists(plsPath))
        {
            StatusText = "Playlist file not found.";
            return;
        }

        // A playlist with nothing playable in it is not worth loading — every
        // action that follows would have nothing to act on.
        var entries = _playlists.ReadEntries(plsPath);
        if (entries.Count == 0)
        {
            StatusText = $"{Path.GetFileName(plsPath)} is empty — nothing to load";
            return;
        }
        if (!entries.Any(File.Exists))
        {
            StatusText = $"{Path.GetFileName(plsPath)} has no videos that still exist — nothing to load";
            return;
        }

        LoadedPlaylistPath = plsPath;
        StatusText = $"Loaded playlist: {Path.GetFileName(plsPath)}";

        // Deliberately does NOT launch anything. This used to shell-execute the
        // .pls, and the caller then launched the first entry as well — two
        // launches racing, which is why the player showed one video, stalled,
        // then swapped to the whole list. LoadPlaylistAndPlay owns launching.
    }

    /// <summary>
    /// Clears the loaded-playlist state — subsequent "Add current video
    /// to playlist" actions will prompt the user to pick a playlist
    /// again. Does NOT delete or modify the playlist file on disk.
    /// </summary>
    [RelayCommand]
    private void ClearLoadedPlaylist()
    {
        if (string.IsNullOrEmpty(LoadedPlaylistPath))
        {
            StatusText = "No playlist is loaded.";
            return;
        }
        var name = LoadedPlaylistName;
        LoadedPlaylistPath = null;
        StatusText = $"Cleared loaded playlist: {name}";
    }

    /// <summary>
    /// Opens a video from the File → Recent submenu. If the file no longer
    /// exists, the entry is pruned from both the in-memory collection and
    /// settings.json, and the user is informed. Otherwise the video is
    /// launched in MPC-HC (so it actually starts playing) and its
    /// metadata + bookmarks are loaded into the editor immediately — the
    /// polling timer will sync playback position once MPC-HC reports the
    /// new file as loaded.
    /// </summary>
    [RelayCommand]
    private async Task OpenRecentVideo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "No recent video selected.";
            return;
        }

        if (!File.Exists(path))
        {
            // Auto-prune dead entries so the menu doesn't fill up with
            // files the user has moved or deleted.
            _settings.RemoveRecent(path);
            for (int i = 0; i < RecentVideos.Count; i++)
            {
                if (string.Equals(RecentVideos[i], path, StringComparison.OrdinalIgnoreCase))
                {
                    RecentVideos.RemoveAt(i);
                    break;
                }
            }
            StatusText = $"File not found — removed from recent list:\n{path}";
            Notify(
                $"This file no longer exists and has been removed from the recent list:\n\n{path}",
                "Recent video",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Launch the video in MPC-HC so it actually starts playing. If
        // MPC-HC is already running, this replaces the current file in
        // the existing instance (its default single-instance behavior).
        // The polling timer will detect the new file within ~300ms.
        bool launched = _mpc.LaunchVideo(path);
        if (!launched)
        {
            StatusText = "Could not launch MPC-HC — loading metadata only.";
        }

        // Load metadata + bookmarks into the editor right away so the
        // user sees the bookmarks/duration without waiting for MPC-HC
        // to finish loading the file. The polling timer will sync the
        // playback position once MPC-HC reports the new file.
        await LoadVideoAsync(path);

        if (launched)
        {
            // Give MPC-HC a moment to register the new file, then bring
            // it to the front so the user can see playback starting.
            await Task.Delay(300);
            _mpc.BringToFront();
        }
    }

    /// <summary>
    /// Removes a single entry from the recent list without opening it.
    /// Wired up to the "Remove from list" sub-item of each recent entry
    /// in the File → Recent submenu.
    /// </summary>
    [RelayCommand]
    private void RemoveRecentVideo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (_settings.RemoveRecent(path))
        {
            for (int i = 0; i < RecentVideos.Count; i++)
            {
                if (string.Equals(RecentVideos[i], path, StringComparison.OrdinalIgnoreCase))
                {
                    RecentVideos.RemoveAt(i);
                    break;
                }
            }
            StatusText = $"Removed from recent list: {Path.GetFileName(path)}";
        }
    }

    /// <summary>
    /// Clears the entire recent videos list. Confirms with the user first
    /// via the code-behind handler in MainWindow.xaml.cs (which then calls
    /// this command).
    /// </summary>
    [RelayCommand]
    private void ClearRecentVideos()
    {
        if (RecentVideos.Count == 0) return;
        _settings.ClearRecents();
        RecentVideos.Clear();
        StatusText = "Recent video list cleared.";
    }

    [RelayCommand]
    private void AddShortcut()
    {
        var dlg = new OpenFolderDialog { Title = "Select folder to add as a shortcut" };
        if (dlg.ShowDialog() != true) return;

        var folder = dlg.FolderName;
        if (_settings.AddShortcut(folder))
        {
            // Re-read the persisted entry so we get the auto-derived Name.
            var entry = _settings.Current.Shortcuts.Last();
            Shortcuts.Add(new ShortcutEntry(entry.Path, entry.Name));
            StatusText = $"Shortcut added: {entry.Name}";
        }
        else
        {
            StatusText = "That folder is already in your shortcuts.";
        }
    }

    [RelayCommand]
    private void RemoveShortcut(ShortcutEntry? entry)
    {
        if (entry == null)
        {
            // Legacy fallback: if there's only one shortcut, remove it.
            if (Shortcuts.Count == 1)
                entry = Shortcuts[0];
            else
            {
                Notify(
                    "Use the 'Remove' entry next to a shortcut in the Shortcuts menu, or open Manage Shortcuts.",
                    "Remove shortcut",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        if (_settings.RemoveShortcut(entry.Path))
        {
            var match = Shortcuts.FirstOrDefault(s => string.Equals(
                (s.Path ?? "").TrimEnd('\\', '/'), entry.Path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
            if (match != null) Shortcuts.Remove(match);
            StatusText = $"Removed shortcut: {entry.Name}";
        }
    }

    [RelayCommand]
    private void RenameShortcut(ShortcutEntry? entry)
    {
        if (entry == null)
        {
            Notify("Select a shortcut to rename, or use Manage Shortcuts.",
                "Rename shortcut", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new InputDialog("Rename shortcut",
            $"New display name for:\n{entry.Path}",
            entry.Name);
        if (dlg.ShowDialog() != true) return;

        var newName = dlg.Value.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            StatusText = "Name cannot be empty.";
            return;
        }
        if (newName == entry.Name) return;

        if (_settings.RenameShortcut(entry.Path, newName))
        {
            var match = Shortcuts.FirstOrDefault(s => string.Equals(
                (s.Path ?? "").TrimEnd('\\', '/'), entry.Path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
            if (match != null) match.Name = newName;
            StatusText = $"Renamed shortcut to: {newName}";
        }
    }

    [RelayCommand]
    private void OpenShortcut(ShortcutEntry? entry)
    {
        if (entry == null) return;
        var path = entry.Path;
        if (!Directory.Exists(path))
        {
            Notify($"Folder not found:\n{path}", "Open shortcut",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _settings.RemoveShortcut(path);
            var match = Shortcuts.FirstOrDefault(s => string.Equals(
                (s.Path ?? "").TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
            if (match != null) Shortcuts.Remove(match);
            return;
        }
        Process.Start("explorer.exe", $"\"{path}\"");
        StatusText = $"Opened: {entry.Name}";
    }

    [RelayCommand]
    private void ManageShortcuts()
    {
        var dlg = new ManageShortcutsDialog(Shortcuts) { Owner = null };
        dlg.ShowDialog();

        // The dialog mutates the ObservableCollection in place (add / remove
        // / rename / reorder). Persist the final order so it survives restarts.
        _settings.ReorderShortcuts(Shortcuts);
        StatusText = Shortcuts.Count > 0
            ? $"{Shortcuts.Count} shortcut(s) saved"
            : "No shortcuts";
    }

    // ------------------------------------------------------------------
    // Settings and Help
    // ------------------------------------------------------------------

    /// <summary>
    /// Opens File ▸ Settings and applies the result.
    /// </summary>
    /// <remarks>
    /// The dialog edits a copy and returns it; persistence happens here. That
    /// keeps Cancel meaningful for <see cref="AutoSwitchViews"/>, which has a
    /// visible effect the instant it is set, and keeps the ViewModel's own
    /// reaction to a changed setting next to the save rather than split
    /// across two files.
    /// </remarks>
    [RelayCommand]
    private void OpenSettings()
    {
        var dlg = new SettingsDialog(_settings.Current, AutoSwitchViews)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dlg.ShowDialog() != true) return;

        var s = _settings.Current;

        s.DefaultVideoFormat = VideoFormats.FromKey(dlg.VideoFormatKey).Key;
        s.DeleteOriginalVideo = dlg.DeleteOriginalVideo;
        s.DeleteBookmarksFile = dlg.DeleteBookmarksFile;
        s.DeleteToRecycleBin = dlg.DeleteToRecycleBin;
        s.Quality = dlg.Quality;
        s.OnNameCollision = dlg.OnNameCollision;
        s.PollSpeed = dlg.PollSpeed;
        s.MpcWebInterfacePort = dlg.MpcWebInterfacePort;
        s.FfmpegFolder = dlg.FfmpegFolder;
        s.ToastsEnabled = dlg.ToastsEnabled;
        s.ToastSeconds = dlg.ToastSeconds;
        s.RememberSaveToFolder = dlg.RememberSaveToFolder;
        s.OverlayCorner = dlg.OverlayCorner;

        var runModeChanged = s.RunMode != dlg.RunMode;
        s.RunMode = dlg.RunMode;
        s.AllowMultipleInstances = dlg.AllowMultipleInstances;
        s.OverlayOpacity = dlg.OverlayOpacity;
        s.MaxHistory = dlg.MaxHistory;

        // Turning "remember" on adopts whatever folder is pinned right now,
        // rather than waiting for the user to pick one again. Turning it off
        // forgets the stored folder so it cannot come back on a later restart.
        s.SaveToFolder = dlg.RememberSaveToFolder ? _pinnedSaveToFolder : "";

        _settings.Save();

        // Push the settings that live inside services into them, so the next
        // operation and the next poll tick use the new values.
        ApplyServiceSettings();

        // Assigning the observable property runs OnAutoSwitchViewsChanged,
        // which persists it and re-evaluates the view — so it is set after the
        // save rather than written into settings above.
        AutoSwitchViews = dlg.AutoSwitchViews;

        // The suffix example spells out an extension, and the overlay reads
        // its appearance when it next appears.
        SuffixExampleDisplay = BuildSuffixExample(_settings.GetActiveSuffixText());
        OnPropertyChanged(nameof(OverlayCorner));
        OnPropertyChanged(nameof(OverlayOpacity));

        // The View owns the tray icon, so it is told rather than asked.
        if (runModeChanged)
        {
            OnPropertyChanged(nameof(RunMode));
            RunModeChanged?.Invoke();
        }

        TrimRecentVideosToLimit();

        StatusText = dlg.FfmpegFolderChanged
            ? "Settings saved — restart to pick up the new ffmpeg folder"
            : $"Settings saved — output format: {OutputFormat.Key.ToUpperInvariant()}";
    }

    /// <summary>
    /// Drops recent entries beyond the configured limit, in the list and on
    /// disk, so lowering the setting takes effect immediately rather than at
    /// the next launch.
    /// </summary>
    private void TrimRecentVideosToLimit()
    {
        var limit = _settings.Current.MaxHistory;
        if (RecentVideos.Count <= limit) return;

        while (RecentVideos.Count > limit)
            RecentVideos.RemoveAt(RecentVideos.Count - 1);

        _settings.Current.RecentVideos = RecentVideos.ToList();
        _settings.Save();
    }

    [RelayCommand]
    private void ShowAbout()
    {
        var dlg = new AboutDialog { Owner = Application.Current?.MainWindow };
        dlg.ShowDialog();
    }

    [RelayCommand]
    private void OpenRepository() => AboutDialog.OpenUrl(AboutDialog.RepositoryUrl);

    [RelayCommand]
    private void OpenIssues() => AboutDialog.OpenUrl(AboutDialog.RepositoryUrl + "/issues");

    // ------------------------------------------------------------------
    // Suffix commands
    // ------------------------------------------------------------------

    /// <summary>
    /// Prompts the user for a new suffix text, validates it (alphanumeric,
    /// 1–50 chars, unique), and adds it to the list.
    /// </summary>
    [RelayCommand]
    private void AddSuffix()
    {
        var text = PromptForSuffixText("Add suffix", "Enter suffix text (letters and numbers only, max 50 chars).\n\nIt will be wrapped in brackets in filenames, e.g. [done]", "");
        if (text == null) return;

        if (_settings.AddSuffix(text))
        {
            Suffixes.Add(new SuffixEntry(text));
            UpdateActiveSuffixDisplay();
            StatusText = $"Suffix added: [{text}]";
        }
        else
        {
            Notify("That suffix already exists.", "Add suffix",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Prompts for a new name and renames the given suffix entry. The
    /// active selection follows if the renamed entry was active.
    /// </summary>
    [RelayCommand]
    private void RenameSuffix(SuffixEntry? entry)
    {
        if (entry == null)
        {
            Notify("Select a suffix to rename, or use Manage Suffixes.",
                "Rename suffix", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var text = PromptForSuffixText("Rename suffix",
            $"New text for:\n{entry.Display}\n\nLetters and numbers only, max 50 chars.",
            entry.Text);
        if (text == null || text == entry.Text) return;

        if (_settings.RenameSuffix(entry.Text, text))
        {
            entry.Text = text;
            UpdateActiveSuffixDisplay();
            StatusText = $"Suffix renamed to: [{text}]";
        }
        else
        {
            Notify("Could not rename — a suffix with that text already exists.",
                "Rename suffix", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Removes a suffix from the list. If it was the active one, the
    /// active selection falls back to the first remaining entry.
    /// </summary>
    [RelayCommand]
    private void RemoveSuffix(SuffixEntry? entry)
    {
        if (entry == null)
        {
            Notify("Select a suffix to remove, or use Manage Suffixes.",
                "Remove suffix", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_settings.RemoveSuffix(entry.Text))
        {
            var match = Suffixes.FirstOrDefault(s =>
                string.Equals(s.Text, entry.Text, StringComparison.OrdinalIgnoreCase));
            if (match != null) Suffixes.Remove(match);
            UpdateActiveSuffixDisplay();
            StatusText = $"Removed suffix: {entry.Display}";
        }
    }

    /// <summary>
    /// Sets the given suffix as the active one (the one applied to new
    /// video outputs). Wired to the click handler on each dynamic menu
    /// item in the Suffix menu.
    /// </summary>
    [RelayCommand]
    private void SetActiveSuffix(SuffixEntry? entry)
    {
        if (entry == null) return;
        if (_settings.SetActiveSuffix(entry.Text))
        {
            UpdateActiveSuffixDisplay();
            StatusText = $"Active suffix: {entry.Display}";
        }
    }

    /// <summary>
    /// Opens the full Manage Suffixes dialog (add / rename / remove /
    /// drag-reorder). Persists the final order on close.
    /// </summary>
    [RelayCommand]
    private void ManageSuffixes()
    {
        var dlg = new ManageSuffixesDialog(Suffixes) { Owner = null };
        dlg.ShowDialog();

        _settings.ReorderSuffixes(Suffixes);
        UpdateActiveSuffixDisplay();
        StatusText = $"{Suffixes.Count} suffix(es) saved";
    }

    /// <summary>
    /// Validation + input loop for suffix text. Returns the validated
    /// text, or null if the user cancelled. Enforces:
    /// <list type="bullet">
    ///   <item>Non-empty after trimming.</item>
    ///   <item>Alphanumeric only (a–z, A–Z, 0–9).</item>
    ///   <item>Length 1–50 characters.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// A rejected value re-shows the dialog with the reason folded into its
    /// prompt, rather than stacking a second window on top of the first. The
    /// status bar is no use here — it is behind the dialog.
    /// </remarks>
    private static string? PromptForSuffixText(string title, string prompt, string defaultValue)
    {
        var currentPrompt = prompt;
        var value = defaultValue;

        while (true)
        {
            var dlg = new InputDialog(title, currentPrompt, value);
            if (dlg.ShowDialog() != true) return null;

            value = dlg.Value.Trim();

            if (string.IsNullOrEmpty(value))
            {
                currentPrompt = $"The name cannot be empty.\n\n{prompt}";
                continue;
            }
            if (value.Length > 50)
            {
                currentPrompt = $"That name is {value.Length} characters — the limit is 50.\n\n{prompt}";
                continue;
            }
            if (!value.All(char.IsLetterOrDigit))
            {
                currentPrompt =
                    "Only letters and numbers (a–z, A–Z, 0–9) are allowed — " +
                    $"no spaces, brackets, or special characters.\n\n{prompt}";
                continue;
            }

            return value;
        }
    }

    [RelayCommand] private void BringMpcToFront() => _mpc.BringToFront();

    /// <summary>
    /// Seeks MPC-HC to the given absolute time (in seconds).
    /// Bound to the bookmark start / end timestamps in the list so that
    /// clicking a timestamp jumps the player to that moment.
    /// </summary>
    [RelayCommand]
    private async Task SeekToTime(double seconds)
    {
        if (!_mpc.IsRunning)
        {
            StatusText = "MPC-HC is not running — open a video first.";
            return;
        }
        if (string.IsNullOrEmpty(Session.VideoPath))
        {
            StatusText = "No video loaded.";
            return;
        }

        if (seconds < 0) seconds = 0;

        if (await _mpc.SeekToAsync(seconds))
            StatusText = $"Seeked to {Bookmark.FormatTime(seconds)}";
        else
            StatusText = "Could not seek — " + (_mpc.LastSeekFailureReason ?? "make sure MPC-HC is playing a video.");
    }

    private void Renumber() { int i = 1; foreach (var b in Session.Bookmarks) b.Index = i++; }

    /// <summary>
    /// Extensions we treat as playable video. Used to keep playlists and
    /// bookmark files out of places that only make sense for a video.
    /// </summary>
    private static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpeg", ".mpg", ".ts", ".m4v", ".flv", ".m2ts" };

    private static bool IsVideoFile(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    // ------------------------------------------------------------------
    // Notifications
    //
    // Everything routine goes to the status bar rather than a dialog: a
    // popup steals focus, has to be dismissed, and — as with the "Done" box
    // after a split — freezes the progress bar short of 100%. Dialogs are
    // now reserved for the two cases that earn one: confirming a destructive
    // action, and reporting a failure the user has to know about.
    //
    // These overloads mirror MessageBox.Show's shapes so a call site can be
    // switched over without rewriting its arguments. The caption and the
    // button/icon arguments are accepted and ignored.
    // ------------------------------------------------------------------

    private void Notify(string message) => StatusText = FlattenForStatus(message);

    private void Notify(string message, string caption) => Notify(message);

    private void Notify(string message, string caption, MessageBoxButton button) => Notify(message);

    private void Notify(string message, string caption, MessageBoxButton button, MessageBoxImage icon)
        => Notify(message);

    /// <summary>
    /// Collapses a multi-line dialog message onto the single status-bar line,
    /// keeping it readable rather than truncating at the first newline.
    /// </summary>
    private static string FlattenForStatus(string message)
    {
        var text = message.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        while (text.Contains("  ")) text = text.Replace("  ", " ");
        return text.Trim();
    }

    /// <summary>
    /// Stops everything that would otherwise keep running after the window is
    /// gone. Called from <c>MainWindow.OnClosed</c>.
    /// </summary>
    /// <remarks>
    /// Nothing called this before, so the low-level input hooks stayed
    /// installed, the poll timer kept ticking, the stall monitor kept its
    /// threads, and any running ffmpeg carried on writing.
    ///
    /// Order matters at the front: the timer is stopped first so a tick
    /// already queued on the dispatcher cannot run against services that are
    /// mid-teardown. Every step is guarded independently — one failure during
    /// shutdown must not skip the rest, particularly not the ffmpeg kill.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Try(() => _pollTimer.Stop());

        // Cancel an in-flight "play all cuts" walk, which otherwise keeps
        // seeking a player the app no longer has any business driving.
        Try(() => _playbackCts?.Cancel());

        // Before the hooks: killing ffmpeg is the one step with a visible
        // consequence if it is skipped.
        Try(() => _ffmpeg.KillAll());

        Try(() => _hotkeys.Dispose());
        Try(() => _toast.Dispose());
        Try(() => _stalls.Dispose());

        static void Try(Action action)
        {
            try { action(); } catch { /* teardown: nobody left to tell */ }
        }
    }

    private bool _disposed;
}
