using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    private readonly ThumbnailService _thumbnails;
    private readonly MpcHcService _mpc;
    private readonly BookmarkService _bookmarks;
    private readonly SettingsService _settings;
    private readonly PlaylistService _playlists;
    private readonly ImageConversionService _images;

    /// <summary>
    /// Reads image metadata. Built here rather than taken in the constructor:
    /// it holds nothing, reads from disk on demand, and nothing else needs to
    /// share the instance.
    /// </summary>
    private readonly ImageMetadataService _imageInfo = new();
    private readonly HotkeyService _hotkeys;
    private readonly ToastService _toast;
    private readonly StallMonitor _stalls;
    private readonly System.Windows.Threading.DispatcherTimer _pollTimer;
    private string? _lastLoadedPath;
    private (double Current, double Duration) _cachedPlaybackPosition;
    private DateTime _lastPositionUpdate = DateTime.MinValue;
    private bool _autoLoadingVideo;

    /// <summary>
    /// The last playback position the fallback saw move, and when. Used only
    /// when the Web Interface cannot say whether the player is running — see
    /// <see cref="InferPlayingFromPosition"/>.
    /// </summary>
    private double _lastMovedPosition = -1;
    private DateTime _positionLastMovedUtc = DateTime.MinValue;

    /// <summary>
    /// When the play/pause button last acted on its own authority. Readings the
    /// player made before this are ignored: they describe the state it was in
    /// before it was told to change, and treating them as current is what made
    /// the button flash through the wrong picture on its way to the right one.
    /// </summary>
    private DateTime _playStateAssumedUtc = DateTime.MinValue;

    /// <summary>
    /// How long the position may stand still before the fallback calls it
    /// paused.
    /// </summary>
    /// <remarks>
    /// Generous because the status bar reports whole seconds: at ordinary speed
    /// one ticks over every second, but at half speed every two, and a
    /// threshold tight enough to feel quick would have the button flapping
    /// between the two pictures on a video that is playing perfectly well.
    /// Slower than half speed still reads as paused — the honest answer there
    /// is to turn the Web Interface back on.
    /// </remarks>
    private const int PositionIdleMs = 3000;

    /// <summary>
    /// How long a cut playback will sit paused before giving up on being
    /// resumed and ending the sequence.
    /// </summary>
    /// <remarks>
    /// Long enough that no ordinary pause reaches it, short enough that a
    /// sequence paused and forgotten does not hold
    /// <see cref="IsPlayingCuts"/> — and with it the poll's auto-load — for the
    /// rest of the session.
    /// </remarks>
    private const int PausedSequenceGiveUpMs = 120_000;

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
    /// playback loop. Non-null while a playback loop is running; canceled
    /// by starting a new playback or at shutdown — there is no "stop" of its
    /// own. Null again once the loop exits.
    /// </summary>
    private CancellationTokenSource? _playbackCts;

    /// <summary>
    /// True while a "Play all" / "Play selected" loop is actively sequencing
    /// through bookmarks. Used by <see cref="PollMpc"/> to skip its normal
    /// "auto-load a new file" path so playback isn't interrupted if MPC-HC
    /// briefly reports a window-title glitch mid-seek.
    /// </summary>
    [ObservableProperty] private bool _isPlayingCuts;

    [ObservableProperty] private EditSession _session = new();
    [ObservableProperty] private string _statusText = "Ready – open a video in MPC-HC, then set bookmarks with your hotkey";
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private Bookmark? _selectedBookmark;

    /// <summary>First frame of the selected clip, or null while it is being made.</summary>
    [ObservableProperty] private BitmapSource? _clipInThumbnail;

    /// <summary>Last frame of the selected clip, or null while it is being made.</summary>
    [ObservableProperty] private BitmapSource? _clipOutThumbnail;

    /// <summary>
    /// Whether the preview pane has anything to show. Kept separate from the
    /// two images so the pane can appear the moment a clip is chosen rather
    /// than popping in once the frames finish rendering.
    /// </summary>
    [ObservableProperty] private bool _hasClipPreview;

    /// <summary>Heading over the preview, naming which clip is shown.</summary>
    [ObservableProperty] private string _clipPreviewHeading = string.Empty;

    /// <summary>Line under the preview: what the clip will run to, and how to change it.</summary>
    [ObservableProperty] private bool _isMpcRunning;
    [ObservableProperty] private string _currentTimeDisplay = "00:00";
    [ObservableProperty] private string _durationDisplay = "00:00";
    [ObservableProperty] private double _timelineProgress;
    [ObservableProperty] private string _hotkeyStatus = "MButton: ON";
    /// <summary>
    /// The binding on its own — "MButton", "Ctrl+Shift+T", "OFF" — for the
    /// CURRENT HOTKEY block in the right panel and the Current line of the
    /// Hotkey menu. Unprefixed, because both already carry a heading saying
    /// what it is.
    /// </summary>
    /// <remarks>
    /// The menu had its own "Hotkey: MButton" label until it gained the
    /// Current heading, which left the word on screen three times — the menu,
    /// the heading and the line — before the binding itself.
    /// </remarks>
    [ObservableProperty] private string _hotkeyDisplay = "MButton";
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
    /// Whether <see cref="BookmarksFileName"/> is naming a real file rather
    /// than its placeholder. Written to the exact same test, so the panel
    /// cannot colour a filename as missing or a placeholder as present.
    /// </summary>
    public bool HasBookmarksFile => !string.IsNullOrEmpty(Session.CsvPath) && File.Exists(Session.CsvPath);

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
    // an item is unexpectedly grayed out. RefreshCommandStates() re-runs
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
    /// Complete bookmarks the user has checked — what the actions that need a
    /// real range (cut, merge, play, flip) are gated on.
    /// </summary>
    public int SelectedPairCount => Session.Bookmarks.Count(b => b.IsSelected && b.IsValid);

    /// <summary>
    /// Checked bookmarks of any kind, complete or still open.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="SelectedPairCount"/> because the two answer
    /// different questions. "Can this be cut" wants complete pairs; "is
    /// anything marked" — delete, and clearing the selection — wants whatever
    /// the user actually checked. Gating delete on the pair count left a lone
    /// opening timestamp checked but undeletable.
    /// </remarks>
    public int SelectedCount => Session.Bookmarks.Count(b => b.IsSelected);

    /// <summary>Progress state for the panel above the status bar.</summary>
    public JobProgress Job { get; } = new();

    /// <summary>
    /// The action toolbar, in the order it is drawn. Reordered by dragging a
    /// button onto another; see <see cref="SaveToolbarOrder"/>.
    /// </summary>
    public ObservableCollection<ToolbarItem> ToolbarItems { get; } = new();

    /// <summary>
    /// Every action that can be put on the toolbar, and the three fillers.
    /// </summary>
    /// <remarks>
    /// A chosen set rather than every command in the menus. Forty-four of those
    /// are bound, but most either need an argument — a playlist, a naming tag, a
    /// recent file — or are things nobody reaches for often enough to spend a
    /// button on. What is here is what someone might want under the pointer
    /// while cutting.
    ///
    /// Built fresh on each call because the items carry per-instance state: the
    /// Customize dialog hands out copies, and a spacer dropped twice has to be
    /// two objects.
    /// </remarks>
    public List<ToolbarItem> ToolbarCatalogue() => new List<ToolbarItem>
    {
        // The thirteen the toolbar ships with.
        new() { Key = "set-timestamp", Group = "Bookmarks", Label = "📍 Set bookmark", Icon = "📍", IconFile = "icon-set_timestamp", Command = SetTimestampCommand,
                ToolTip = "Set a bookmark at the current MPC-HC position" },
        new() { Key = "save-frame", Group = "Actions", Label = "📷 Save frame", Icon = "📷", IconFile = "icon-safe_frame", Command = SaveCurrentFrameCommand,
                ToolTip = "Save image frame" },
        new() { Key = "delete", Group = "Bookmarks", Label = "🗑 Remove", Icon = "🗑", IconFile = "icon-remove", Command = DeleteSelectedCommand,
                ToolTip = "Remove" },
        new() { Key = "flip", Group = "Actions", Label = "↕ Flip", Icon = "↕", IconFile = "icon-flip", Command = ToggleFlipCommand,
                ToolTip = "Flip" },
        new() { Key = "rotate", Group = "Actions", Label = "⟳ Rotate", Icon = "⟳", IconFile = "icon-rotate", Command = RotateSelectedCommand,
                ToolTip = "Rotate" },
        new() { Key = "mute", Group = "Actions", Label = "🔇 Mute", Icon = "🔇", IconFile = "icon-mute", Command = ToggleMuteCommand,
                ToolTip = "Mute" },
        // Three entries, one per answer. "fade" keeps its key and its meaning
        // of both ends, so a toolbar already carrying it is unchanged.
        new() { Key = "fade", Group = "Actions", Label = "◐ Fade", Icon = "◐", IconFile = "icon-fade", Command = ToggleFadeCommand,
                ToolTip = "Fade in and out" },
        new() { Key = "fade-in", Group = "Actions", Label = "◐ Fade in", Icon = "◐", IconFile = "icon-fade_in", Command = FadeInCommand,
                ToolTip = "Fade the start only — the end stays hard" },
        new() { Key = "fade-out", Group = "Actions", Label = "◐ Fade out", Icon = "◐", IconFile = "icon-fade_out", Command = FadeOutCommand,
                ToolTip = "Fade the end only — the start stays hard" },
        new() { Key = "crop", Group = "Actions", Label = "⬚ Crop", Icon = "⬚", IconFile = "icon-crop", Command = CropSelectedCommand,
                ToolTip = "Crop video" },
        new() { Key = "select-all", Group = "Bookmarks", Label = "Select All", Icon = "☑", IconFile = "icon-select_all", Command = ToggleSelectAllCommand,
                MinWidth = 104, ToolTip = "Check every bookmark, or clear them all." },
        new() { Key = "merge", Group = "Actions", Label = "🎬 Merge", Icon = "🎬", IconFile = "icon-merge", Command = MergeSelectedCommand,
                ToolTip = "Merge clips" },
        new() { Key = "split", Group = "Actions", Label = "✂ Split", Icon = "✂", IconFile = "icon-split", Command = SplitSelectedCommand,
                ToolTip = "Split clips" },
        new() { Key = "convert", Group = "Actions", Label = "🔄 Convert video", Icon = "🔄", IconFile = "icon-convert_video", Command = ConvertFilesCommand,
                ToolTip = "Convert video files to another format" },
        new() { Key = "strip-audio", Group = "Actions", Label = "🔊 Strip audio", Icon = "🔊", IconFile = "icon-strip_audio", Command = StripAudioCommand,
                ToolTip = "Pulls the sound out of files you pick: an MP3 of the audio, a silent copy of the video, or both." },

        // Everything else worth a button.
        new() { Key = "open-file", Group = "File", Label = "📂 Open video", Icon = "📂", IconFile = "icon-open_video", Command = OpenFileCommand,
                ToolTip = "Open a video in MPC-HC" },
        new() { Key = "settings", Group = "File", Label = "⚙ Settings", Icon = "⚙", IconFile = "icon-settings", Command = OpenSettingsCommand,
                ToolTip = "Open Settings" },
        new() { Key = "undo-edit", Group = "Edit", Label = "↶ Undo", Icon = "↶", IconFile = "icon-undo", Command = UndoEditCommand,
                ToolTip = "Undo" },
        new() { Key = "redo-edit", Group = "Edit", Label = "↷ Redo", Icon = "↷", IconFile = "icon-redo", Command = RedoEditCommand,
                ToolTip = "Redo" },
        new() { Key = "undo-bookmark", Group = "Bookmarks", Label = "⎌ Undo bookmark", Icon = "⎌", IconFile = "icon-undo_timestamp", Command = UndoLastBookmarkCommand,
                ToolTip = "Remove the last single bookmark" },
        new() { Key = "enter-time", Group = "Bookmarks", Label = "⌨ Enter time", Icon = "⌨", IconFile = "icon-enter_time", Command = EnterTimeManualCommand,
                ToolTip = "Type a time or a range instead of using the hotkey" },
        new() { Key = "split-at-playhead", Group = "Bookmarks", Label = "✂ Split time", Icon = "✂", IconFile = "icon-split_time", Command = SplitAtPlayheadCommand,
                ToolTip = "Put a boundary wherever the player is now" },
        new() { Key = "detect", Group = "Bookmarks", Label = "🔍 Detect bookmarks", Icon = "🔍", IconFile = "icon-detect_cuts", Command = DetectBookmarksCommand,
                ToolTip = "Propose bookmarks from silence, black frames or scene changes" },
        new() { Key = "edit-bookmarks", Group = "Bookmarks", Label = "✎ Edit bookmarks", Icon = "✎", IconFile = "icon-edit_bookmarks", Command = EditBookmarksCommand,
                ToolTip = "Open the bookmark CSV in a text editor" },
        // Walking the cuts, as two buttons rather than the one this used to
        // have. Telling them apart was the problem the first time: play/pause
        // and "play the cuts" were a blue ▶ apiece doing unrelated things.
        // Their own pictures settle that, so they are back.
        new() { Key = "play-all", Group = "Bookmarks", Label = "▶ Play all", Icon = "▶", IconFile = "icon-play_all", Command = PlayAllCommand,
                ToolTip = "Seek the player to each bookmark in turn and play it" },
        new() { Key = "play-selected", Group = "Bookmarks", Label = "▶ Play selected", Icon = "▶", IconFile = "icon-play_selected", Command = PlaySelectedCommand,
                MinWidth = 104, ToolTip = "Seek the player to each checked bookmark in turn and play it" },

        // Still keyed "pause" although it is now both halves: a key is what a
        // saved layout is a list of, and this one has shipped. Renaming it
        // would quietly drop the button from any toolbar already carrying it.
        // The label and picture are what anybody sees, and those do change.
        new() { Key = "pause", Group = "Bookmarks", Label = "▶ Play", Icon = "▶", IconFile = "icon-play", Command = TogglePauseCommand,
                ToolTip = "Play or pause the video" },
        new() { Key = "export-animation", Group = "Actions", Label = "🎞 GIF / WebP", Icon = "🎞", IconFile = "icon-gif_webp", Command = ExportAnimationCommand,
                ToolTip = "Export the checked bookmarks as an animation" },
        new() { Key = "convert-images", Group = "Actions", Label = "🖼 Convert images", Icon = "🖼", IconFile = "icon-convert_images", Command = ConvertImagesCommand,
                ToolTip = "Convert images" },
        new() { Key = "resize-images", Group = "Actions", Label = "📐 Resize images", Icon = "📐", IconFile = "icon-resize_images", Command = ResizeImagesCommand,
                ToolTip = "Resize images" },
        new() { Key = "image-info", Group = "Actions", Label = "ℹ Image info", Icon = "ℹ", IconFile = "icon-image_info", Command = ShowImageInfoCommand,
                ToolTip = "Read image info" },
        new() { Key = "thumbnails", Group = "Actions", Label = "▦ Save thumbnails", Icon = "▦", IconFile = "icon-save_thumbnails", Command = SaveThumbnailsCommand,
                ToolTip = "Save thumbnails" },
        new() { Key = "export-chapters", Group = "Actions", Label = "🔖 Chapters", Icon = "🔖", IconFile = "icon-chapters", Command = ExportChaptersCommand,
                ToolTip = "Write the bookmarks out as a chapter file" },
        new() { Key = "add-to-playlist", Group = "Playlist", Label = "➕ To playlist", Icon = "➕", IconFile = "icon-to_playlist", Command = AddCurrentToPlaylistCommand,
                ToolTip = "Add the current video to a playlist" },

        // The fillers. Each is a real entry in the layout; only their drawing is
        // particular to the Customize dialog.
        new() { Key = "spacer", Group = "Layout", Kind = ToolbarItemKind.Spacer, Label = "Spacer",
                ToolTip = "A fixed gap between two groups of buttons" },
        new() { Key = "expander", Group = "Layout", Kind = ToolbarItemKind.Expander, Label = "Expander",
                ToolTip = "Takes up whatever width is left, pushing what follows it to the right" },
        new() { Key = "row-break", Group = "Layout", Kind = ToolbarItemKind.RowBreak, Label = "New row",
                ToolTip = "Everything after this is drawn on the next line of the toolbar" },
    }.Concat(SuffixButtons()).Select(InFamily).ToList();

    /// <summary>
    /// Gives a catalogue entry the style of the family it belongs to.
    /// </summary>
    /// <remarks>
    /// Assigned here rather than written on each entry, because the catch-all
    /// family is every button that is not one of the named operations — which
    /// is most of them, and a list that would have to be kept in step by hand
    /// every time a button was added.
    ///
    /// The fillers are left alone. A spacer draws nothing at runtime, so a
    /// style would be a colour nobody can see attached to a gap.
    /// </remarks>
    private static ToolbarItem InFamily(ToolbarItem item)
    {
        if (item.Kind != ToolbarItemKind.Button) return item;

        item.StyleKey = ButtonColors.StyleKeyOf(ButtonColors.FamilyOf(item.Key));
        return item;
    }

    /// <summary>
    /// A button for each naming tag, which makes that tag the active one.
    /// </summary>
    /// <remarks>
    /// Read from the settings rather than written down, because the tags are
    /// the user's — added, renamed and deleted under Options — so the catalogue
    /// has to be whatever the list says today.
    ///
    /// Keyed <c>tag:done</c>, on the tag's own text. A layout naming a tag that
    /// has since been deleted quietly loses that button, which is the same rule
    /// every other key follows, and a tag added later appears in the Customize
    /// dialog rather than on the toolbar.
    ///
    /// The command is made here per tag: there is no one action to bind, since
    /// each button means a different value.
    /// </remarks>
    private IEnumerable<ToolbarItem> SuffixButtons()
    {
        foreach (var suffix in _settings.Current.Suffixes)
        {
            var text = suffix.Text;
            if (string.IsNullOrWhiteSpace(text)) continue;

            yield return new ToolbarItem
            {
                Key = "tag:" + text,
                Group = "Naming tags",
                Label = "[" + text + "]",
                ToolTip = $"Name output files with [{text}]",
                Command = new RelayCommand(() => ApplyNamingTag(text))
            };
        }

        yield return new ToolbarItem
        {
            Key = "tag:none",
            Group = "Naming tags",
            Label = "[no tag]",
            ToolTip = "Write output filenames with no tag at all",
            Command = new RelayCommand(ClearNamingTag)
        };
    }

    /// <summary>Makes a tag the active one, as the Options menu does.</summary>
    private void ApplyNamingTag(string text)
    {
        _settings.SetActiveSuffix(text);
        _settings.Save();
        UpdateActiveSuffixDisplay();
        StatusText = $"Naming tag: [{text}]";
    }

    private void ClearNamingTag()
    {
        _settings.ClearActiveSuffix();
        _settings.Save();
        UpdateActiveSuffixDisplay();
        StatusText = "Naming tag: none";
    }

    /// <summary>The layout the program ships with.</summary>
    private static readonly string[] _defaultToolbar =
    {
        "set-timestamp", "save-frame", "delete", "flip", "rotate", "mute", "fade",
        "spacer", "select-all", "spacer",
        "merge", "split", "convert", "strip-audio"
    };

    /// <summary>
    /// Builds the toolbar from the saved layout, or from the default when there
    /// is none.
    /// </summary>
    /// <remarks>
    /// The catalogue owns what a button <em>is</em> — label, command, colour —
    /// and the saved list only says which ones and in what order. So a button
    /// whose tooltip or style changes in a later release picks that up even for
    /// someone who rearranged the row.
    ///
    /// A key that no longer matches anything is dropped. A button the saved
    /// layout does not mention is <em>not</em> added: once the toolbar has been
    /// customized it belongs to the user, and a later release's button waits in
    /// the Customize dialog rather than turning up at the end of their row.
    /// <see cref="AppSettings.ToolbarCustomized"/> is what tells "never touched"
    /// from "emptied on purpose".
    /// </remarks>
    public void BuildToolbar()
    {
        var catalogue = ToolbarCatalogue();
        var layout = _settings.Current.ToolbarCustomized
            ? _settings.Current.ToolbarOrder
            : _defaultToolbar.ToList();

        ToolbarItems.Clear();

        var compact = _settings.Current.SmallButtonIcons;
        var flat = _settings.Current.FlatButtonIcons;

        foreach (var key in layout)
            if (catalogue.FirstOrDefault(c => c.Key == key) is { } known)
            {
                var item = known.Clone();
                item.IsCompact = compact;
                item.IsFlat = flat;
                ToolbarItems.Add(item);
            }

        // The row is new, so whichever tag is active has to be lit again, and
        // the play/pause button has to be pointed at the right one of its two.
        RefreshNamingTagButtons();
        RefreshPlayPauseButton();
        RefreshToolbarRows();
    }

    /// <summary>
    /// The toolbar split into rows on its break markers.
    /// </summary>
    /// <remarks>
    /// A row is a collection of its own because each is laid out separately —
    /// an expander shares out the width of its own line, not of the whole
    /// toolbar. The breaks are the boundaries and are not carried into the
    /// rows; drawing one would put a gap in a row that asked for none.
    /// </remarks>
    public ObservableCollection<ObservableCollection<ToolbarItem>> ToolbarRows { get; } = new();

    private void RefreshToolbarRows()
    {
        ToolbarRows.Clear();

        var row = new ObservableCollection<ToolbarItem>();

        foreach (var item in ToolbarItems)
        {
            if (item.Kind == ToolbarItemKind.RowBreak)
            {
                ToolbarRows.Add(row);
                row = new ObservableCollection<ToolbarItem>();
                continue;
            }

            row.Add(item);
        }

        ToolbarRows.Add(row);
    }

    /// <summary>Writes the current toolbar layout to settings.</summary>
    public void SaveToolbarOrder()
    {
        _settings.Current.ToolbarOrder = ToolbarItems.Select(i => i.Key).ToList();
        _settings.Current.ToolbarCustomized = true;
        _settings.Save();
        RefreshToolbarRows();
    }

    /// <summary>
    /// Rebuilds the saved layout from the rows, putting a break back between
    /// each one.
    /// </summary>
    /// <remarks>
    /// Dragging works on the rows, because that is what is on screen and what
    /// has to rearrange under the pointer. The flat list is the thing that gets
    /// written down, so it is derived from them afterwards rather than kept in
    /// step during the drag — one direction, at one moment, instead of two
    /// collections trying to mirror each other mid-gesture.
    /// </remarks>
    public void RebuildOrderFromRows()
    {
        var flat = new List<ToolbarItem>();

        foreach (var row in ToolbarRows)
        {
            if (flat.Count > 0)
                flat.Add(new ToolbarItem { Key = "row-break", Kind = ToolbarItemKind.RowBreak, Label = "New row" });

            flat.AddRange(row);
        }

        ToolbarItems.Clear();
        foreach (var item in flat) ToolbarItems.Add(item);

        _settings.Current.ToolbarOrder = ToolbarItems.Select(i => i.Key).ToList();
        _settings.Current.ToolbarCustomized = true;
        _settings.Save();
    }

    /// <summary>The row a toolbar item is currently in, or null.</summary>
    public ObservableCollection<ToolbarItem>? RowContaining(ToolbarItem item) =>
        ToolbarRows.FirstOrDefault(r => r.Contains(item));

    /// <summary>Replaces the toolbar with a layout built in the Customize dialog.</summary>
    public void ApplyToolbarLayout(IEnumerable<ToolbarItem> layout)
    {
        ToolbarItems.Clear();

        foreach (var item in layout)
        {
            // The dialog draws its chips full-size whatever the setting says —
            // a row of unlabelled icons is no way to choose between them — so
            // the items come back needing to be told.
            item.IsCompact = _settings.Current.SmallButtonIcons;
            item.IsFlat = _settings.Current.FlatButtonIcons;
            ToolbarItems.Add(item);
        }

        RefreshNamingTagButtons();
        RefreshPlayPauseButton();
        SaveToolbarOrder();
    }

    /// <summary>
    /// Opens the colour editor for the operation families.
    /// </summary>
    /// <remarks>
    /// Applied before it is saved, and reapplied either way: the resources are
    /// pushed from the dialog's answer, so the toolbar and the menus repaint
    /// without anything being rebuilt.
    /// </remarks>
    [RelayCommand]
    private void ChangeButtonColors()
    {
        var dlg = new ButtonColorsDialog(_settings.Current.ButtonColors)
        {
            Owner = DialogOwner
        };

        if (dlg.ShowDialog() != true) return;

        _settings.Current.ButtonColors = dlg.Result;
        _settings.Save();

        ButtonColors.Apply(_settings.Current.ButtonColors, ThemeService.Current);
        StatusText = "Button colors updated";
    }

    /// <summary>Opens the toolbar editor.</summary>
    [RelayCommand]
    private void CustomizeToolbar()
    {
        // How the bar stood when the dialog opened. The settings are not
        // written while it is open, so these are the only record of what
        // Cancel has to put back.
        var wasHidden = HideToolbar;
        var wasSmall = _settings.Current.SmallButtonIcons;
        var wasFlat = _settings.Current.FlatButtonIcons;

        var dlg = new CustomizeToolbarDialog(ToolbarCatalogue(), ToolbarItems, ToolbarMinimumWidth,
                                             _settings.Current.SmallButtonIcons,
                                             _settings.Current.FlatButtonIcons,
                                             wasHidden)
        {
            Owner = DialogOwner,
            PreviewHidden = PreviewToolbarHidden,
            PreviewChrome = ApplyToolbarIconStyle
        };

        if (dlg.ShowDialog() != true || dlg.Layout is null)
        {
            // Cancelled, so both previews come back off — as the icon preview
            // does when Settings is cancelled.
            PreviewToolbarHidden(wasHidden);
            ApplyToolbarIconStyle(wasSmall, wasFlat);
            return;
        }

        // Written before the layout is applied, not after: ApplyToolbarLayout
        // dresses the returning items from these two, so saving them second
        // would rebuild the toolbar from the settings the dialog was opened
        // with and quietly discard whichever box was just ticked.
        _settings.Current.SmallButtonIcons = dlg.SmallButtonIcons;
        _settings.Current.FlatButtonIcons = dlg.FlatButtonIcons;

        // Already in force — the box put it there as it was ticked. Set again
        // through the same path rather than trusted, so the saved file says
        // what the window shows, and the two menu entries that bind to
        // HideToolbar hear about it even if the preview never ran.
        PreviewToolbarHidden(dlg.HideToolbar);
        _settings.Save();

        ApplyToolbarLayout(dlg.Layout);
        StatusText = dlg.HideToolbar ? "Toolbar updated and hidden" : "Toolbar updated";
    }

    /// <summary>
    /// Shows or hides the real toolbar without writing anything, for the
    /// Hide toolbar box in the Customize dialog.
    /// </summary>
    /// <remarks>
    /// Writes the in-memory setting rather than going through
    /// <see cref="HideToolbar"/>, which saves: the dialog can still be
    /// cancelled, and a preview that had already written itself to disk would
    /// make Cancel a lie. Whoever opens the dialog restores this on the way
    /// out; the save path writes the same value through properly.
    /// </remarks>
    private void PreviewToolbarHidden(bool hidden)
    {
        if (_settings.Current.ShowToolbar == !hidden) return;

        _settings.Current.ShowToolbar = !hidden;
        OnPropertyChanged(nameof(ShowToolbar));
        OnPropertyChanged(nameof(HideToolbar));
    }

    /// <summary>
    /// Opens the colour editor for the seekbar's position bar and border.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="ChangeButtonColors"/>: applied before it is saved,
    /// and reapplied either way, so the bar repaints from the resources the
    /// dialog's answer pushes rather than from anything rebuilt.
    /// </remarks>
    [RelayCommand]
    private void ChangeSeekbarColors()
    {
        var dlg = new SeekbarColorsDialog(_settings.Current)
        {
            Owner = DialogOwner
        };

        if (dlg.ShowDialog() != true) return;

        var s = _settings.Current;
        s.SeekbarColor = dlg.SeekbarColor;
        s.SeekbarBorderColor = dlg.SeekbarBorderColor;
        s.SeekbarTrackColor = dlg.SeekbarTrackColor;
        s.SeekbarMarkColor = dlg.SeekbarMarkColor;
        s.SeekbarPlayheadColor = dlg.SeekbarPlayheadColor;

        // Caught before the assignment: unlike the four colours above, this
        // one is not a resource an Image repaints from on its own — it is
        // baked into the picture, so a change here means asking ffmpeg to
        // decode the file again rather than just pushing a brush.
        var waveformColorChanged = s.SeekbarWaveformColor != dlg.SeekbarWaveformColor;
        s.SeekbarWaveformColor = dlg.SeekbarWaveformColor;
        _settings.Save();

        SeekbarAppearance.Apply(s, ThemeService.Current);
        if (waveformColorChanged) _ = RefreshWaveformAsync(Session.VideoPath, force: true);
        StatusText = "Seekbar colors updated";
    }

    /// <summary>
    /// Opens the editor for how a timestamp pair is marked on the seekbar,
    /// its transparency, and whether the filled bar draws at all.
    /// </summary>
    [RelayCommand]
    private void CustomizeSeekbar()
    {
        var dlg = new CustomizeSeekbarDialog(_settings.Current)
        {
            Owner = DialogOwner
        };

        if (dlg.ShowDialog() != true) return;

        var s = _settings.Current;
        s.SeekbarBracketStyle = dlg.BracketStyle;
        s.SeekbarOpacity = dlg.SeekbarOpacity;
        s.SeekbarBarEnabled = dlg.SeekbarBarEnabled;
        _settings.Save();

        OnPropertyChanged(nameof(SeekbarBracketStyle));
        OnPropertyChanged(nameof(SeekbarOpacity));
        OnPropertyChanged(nameof(SeekbarBarEnabled));
        StatusText = "Seekbar updated";
    }

    /// <summary>
    /// Puts the seekbar back the way the program ships it: colour, border,
    /// transparency, the bar on/off, and how a timestamp pair is marked.
    /// </summary>
    /// <remarks>
    /// One command for the lot, unlike <see cref="ResetToolbarOrder"/>, which
    /// only resets the order and leaves button colours alone. Those are two
    /// separate dialogs with two separate "reset" buttons of their own; the
    /// seekbar's colours and its bracket style are small enough, and asked
    /// for together often enough, that a second scoped command here would be
    /// a second place to look rather than a second thing worth choosing
    /// between.
    /// </remarks>
    [RelayCommand]
    private void ResetSeekbar()
    {
        var s = _settings.Current;
        s.SeekbarColor = SeekbarAppearance.FollowTheme;
        s.SeekbarBorderColor = SeekbarAppearance.FollowTheme;
        s.SeekbarTrackColor = SeekbarAppearance.FollowTheme;
        s.SeekbarMarkColor = SeekbarAppearance.FollowTheme;
        s.SeekbarPlayheadColor = SeekbarAppearance.FollowTheme;

        var waveformColorChanged = s.SeekbarWaveformColor != SeekbarAppearance.FollowTheme;
        s.SeekbarWaveformColor = SeekbarAppearance.FollowTheme;

        s.SeekbarOpacity = 0.5;
        s.SeekbarBarEnabled = true;
        s.SeekbarBracketStyle = SeekbarBracketStyle.Classic;
        _settings.Save();

        SeekbarAppearance.Apply(s, ThemeService.Current);
        if (waveformColorChanged) _ = RefreshWaveformAsync(Session.VideoPath, force: true);
        OnPropertyChanged(nameof(SeekbarBarEnabled));
        OnPropertyChanged(nameof(SeekbarOpacity));
        OnPropertyChanged(nameof(SeekbarBracketStyle));
        StatusText = "Seekbar back to its defaults";
    }

    /// <summary>
    /// How wide the toolbar can count on being: the width of the narrowest the
    /// window is allowed to get.
    /// </summary>
    /// <remarks>
    /// Set by the view once it has measured the menu bar, which is what holds
    /// the window's minimum width. The Customize dialog draws its preview at
    /// this width so that a row built there fits at any size the window can
    /// take — anything wider is slack, and slack is what an expander is for.
    /// </remarks>
    public double ToolbarMinimumWidth { get; set; } = 900;

    /// <summary>Puts the toolbar back the way the program ships it.</summary>
    [RelayCommand]
    private void ResetToolbarOrder()
    {
        _settings.Current.ToolbarOrder = new List<string>();
        _settings.Current.ToolbarCustomized = false;
        _settings.Save();
        BuildToolbar();
        StatusText = "Toolbar back in its original order";
    }

    /// <summary>Drives the minimal overlay's "(no bookmarks yet)" line.</summary>
    public bool HasNoBookmarks => Session.Bookmarks.Count == 0;

    /// <summary>Overlay footer while the player is covering the screen.</summary>
    private const string FullscreenHint = "Hitting  X  restores the program";

    /// <summary>Overlay footer while the player is in a window.</summary>
    private const string WindowedHint = "Unfocusing MPC-HC restores the program";

    /// <summary>
    /// The line at the foot of the overlay saying how to get the full window
    /// back. Which way is easiest depends on how the player is presented, so
    /// the line names whichever one applies right now.
    /// </summary>
    /// <remarks>
    /// Fullscreen, there is nothing else on screen to click and X is the only
    /// way out, so that is what it says. Windowed, clicking anywhere else
    /// already brings the full window back — and having just been told to press
    /// X, a user who clicks away instead sees the window appear for a reason
    /// the overlay never mentioned.
    ///
    /// X still works either way. This picks which of the two to name, not which
    /// one is available.
    /// </remarks>
    [ObservableProperty] private string _minimalHint = FullscreenHint;

    /// <summary>
    /// Updates <see cref="MinimalHint"/> for how the player is presented now.
    /// Called from the poll, because the user can go fullscreen and back while
    /// the overlay is up and nothing tells us when they do.
    /// </summary>
    private void RefreshMinimalHint()
        => MinimalHint = _mpc.GetWindowState() == MpcHcService.PlayerWindowState.Fullscreen
            ? FullscreenHint
            : WindowedHint;

    /// <summary>
    /// When set, the view follows focus: the overlay while MPC-HC is the
    /// active window, the full window while this one is. See
    /// <see cref="ApplyAutoViewSwitch"/>. Persisted, and on by default.
    /// </summary>
    /// <remarks>
    /// The only view control there is. Choosing the view by hand — View ▸
    /// Minimal and View ▸ Full — used to sit alongside this, and is gone: it
    /// was a mode with nothing on screen naming it, and following focus is what
    /// it was mostly being used to approximate anyway.
    ///
    /// Turning it off leaves the full window up permanently. That costs the
    /// user the overlay as a confirmation that the hotkey did anything, which
    /// is why the bookmark toasts stop being optional in that state — see
    /// <see cref="NeedsHotkeyToast"/>.
    /// </remarks>
    [ObservableProperty] private bool _autoSwitchViews;

    partial void OnAutoSwitchViewsChanged(bool value)
    {
        _settings.Current.AutoSwitchViews = value;
        _settings.Save();

        // Apply straight away rather than waiting for the next poll tick.
        // Clearing the edge marker makes the next evaluation act rather than
        // treat the current focus as "already handled".
        _lastMpcFocused = null;
        if (value)
        {
            ApplyAutoViewSwitch();
            return;
        }

        // Turned off with the overlay up — the player has focus and the user
        // reached this from the tray or a second launch. The setting is the
        // only thing holding the overlay there, so it comes down now rather
        // than waiting for the player to lose focus.
        if (_minimalViewActive) RestoreFullView(activate: true);
    }

    /// <summary>
    /// Whether the window minimizes to the tray and survives being closed.
    /// Read by the View, which owns the tray icon.
    /// </summary>
    public RunMode RunMode => _settings.Current.RunMode;

    /// <summary>Raised when <see cref="RunMode"/> changes, so the View can add or remove the tray icon.</summary>
    public event Action? RunModeChanged;

    /// <summary>Corner the overlay parks in. Read by the View when it shows it.</summary>
    public OverlayCorner OverlayCorner => _settings.Current.OverlayCorner;

    /// <summary>Overlay background opacity. Read by the View when it shows it.</summary>
    public double OverlayOpacity => _settings.Current.OverlayOpacity;

    /// <summary>
    /// Whether the overlay's timestamps seek when clicked. Read by the View
    /// when it shows it.
    /// </summary>
    public bool OverlayClickable => _settings.Current.OverlayClickable;

    /// <summary>
    /// Where a scan found the video actually changing. The timeline draws a
    /// tick at each, and a dragged bracket lands on one when it comes close.
    /// </summary>
    /// <remarks>
    /// Observable so both of those follow a scan without being told. Empty
    /// until <see cref="ScanEditPointsCommand"/> or a run of
    /// <see cref="DetectBookmarksCommand"/> fills it, and cleared when the
    /// video changes — the points describe one file and mean nothing against
    /// another.
    /// </remarks>
    public ObservableCollection<double> SnapPoints { get; } = new();

    /// <summary>Whether a scan has left anything to snap to.</summary>
    /// <remarks>
    /// What the seekbar menu shows its snap switch on. A switch for a thing
    /// that cannot happen reads as a setting that is simply off, which is a
    /// different claim — and the one that made people look for a global
    /// behaviour rather than one belonging to the last scan.
    /// </remarks>
    public bool HasSnapPoints => SnapPoints.Count > 0;

    /// <summary>
    /// Whether the seekbar is put away.
    /// </summary>
    /// <remarks>
    /// The inverse of the setting rather than the setting itself, because the
    /// menu entry reads "Hide seekbar" and a checkable item that meant the
    /// opposite of its own label would be a trap. Settable where
    /// <see cref="ShowSeekbar"/> is not: that one is read by the layout and
    /// changed through the Customize dialog, which is several clicks away from
    /// somebody who only wants the bar gone for a minute.
    /// </remarks>
    public bool HideSeekbar
    {
        get => !_settings.Current.ShowSeekbar;
        set
        {
            if (_settings.Current.ShowSeekbar == !value) return;
            _settings.Current.ShowSeekbar = !value;
            _settings.Save();
            OnPropertyChanged();

            // The bar's own visibility reads the positive form.
            OnPropertyChanged(nameof(ShowSeekbar));
        }
    }

    /// <summary>
    /// Whether the action toolbar is put away.
    /// </summary>
    /// <remarks>
    /// The inverse of the setting, for the same reason as
    /// <see cref="HideSeekbar"/>: the menu entry reads "Hide toolbar" and a
    /// checkable item meaning the opposite of its own label would be a trap.
    ///
    /// Nothing about the arrangement is touched. The buttons, their order and
    /// their colours are all still there to come back to — this is the bar
    /// being out of the way, not being emptied.
    /// </remarks>
    public bool HideToolbar
    {
        get => !_settings.Current.ShowToolbar;
        set
        {
            if (_settings.Current.ShowToolbar == !value) return;
            _settings.Current.ShowToolbar = !value;
            _settings.Save();
            OnPropertyChanged();

            // The bar's own visibility reads the positive form.
            OnPropertyChanged(nameof(ShowToolbar));
        }
    }

    /// <summary>Whether the action toolbar is shown at all.</summary>
    public bool ShowToolbar => _settings.Current.ShowToolbar;

    /// <summary>
    /// Whether the hotkey stops here rather than also reaching MPC-HC.
    /// </summary>
    /// <remarks>
    /// Only while the player is the window in front — see
    /// <see cref="HotkeyService.BlockInPlayer"/>. The hook is global, and a
    /// key taken out of every program on the machine would be a cure worse
    /// than the collision.
    /// </remarks>
    public bool BlockHotkeyInPlayer
    {
        get => _settings.Current.BlockHotkeyInPlayer;
        set
        {
            if (_settings.Current.BlockHotkeyInPlayer == value) return;
            _settings.Current.BlockHotkeyInPlayer = value;
            _settings.Save();
            _hotkeys.BlockInPlayer = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Whether a dragged bracket snaps to a nearby edit point.</summary>
    public bool SnapToEditPoints
    {
        get => _settings.Current.SnapToEditPoints;
        set
        {
            if (_settings.Current.SnapToEditPoints == value) return;
            _settings.Current.SnapToEditPoints = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>Whether the cuts are shown as thumbnail cards rather than rows.</summary>
    public bool StoryboardView
    {
        get => _settings.Current.StoryboardView;
        set
        {
            if (_settings.Current.StoryboardView != value)
            {
                _settings.Current.StoryboardView = value;
                _settings.Save();
                OnPropertyChanged(nameof(ShowSelectAllHeader));

                // Nothing drew them while the list was rows, so the cards
                // would otherwise come up empty and stay that way.
                if (value) _ = RefreshStoryboardThumbnailsAsync();
            }

            // Raised even when nothing moved. The two menu entries are a pair
            // of alternatives rather than two switches, and clicking the one
            // already ticked has to leave it ticked — which means telling the
            // menu to re-read a value that did not change.
            OnPropertyChanged();
            OnPropertyChanged(nameof(CutsAsList));
            OnPropertyChanged(nameof(CutsAsStoryboard));

            // Each of the four layout flags carries "and the storyboard is
            // what is being shown", so leaving for the list unlights all four
            // rather than leaving one lit beside it.
            foreach (var name in LayoutFlagNames) OnPropertyChanged(name);
        }
    }

    /// <summary>
    /// The View menu's two ways of showing the cuts, as a pair of
    /// alternatives: exactly one of them is ticked at any time.
    /// </summary>
    /// <remarks>
    /// WPF has no radio group for menu items, so each is an ordinary
    /// checkable entry whose setter only ever acts on being <em>turned on</em>.
    /// Turning one off is what clicking the current view does, and the answer
    /// to that is to put the tick straight back rather than to leave the menu
    /// claiming neither view is in use.
    /// </remarks>
    public bool CutsAsList
    {
        get => !StoryboardView;
        set
        {
            if (value) StoryboardView = false;
            else OnPropertyChanged();
        }
    }

    /// <inheritdoc cref="CutsAsList"/>
    public bool CutsAsStoryboard
    {
        get => StoryboardView;
        set
        {
            if (value) StoryboardView = true;
            else OnPropertyChanged();
        }
    }

    /// <summary>Which arrangement the storyboard is drawn in.</summary>
    public StoryboardLayout StoryboardLayout
    {
        get => _settings.Current.StoryboardLayout;
        set
        {
            if (_settings.Current.StoryboardLayout != value)
            {
                _settings.Current.StoryboardLayout = value;
                _settings.Save();
                OnPropertyChanged();
            }

            // Only the filmstrip wants a closing frame, so arriving at it is
            // the moment to go and get them.
            if (value == StoryboardLayout.Filmstrip && StoryboardView)
                _ = RefreshStoryboardThumbnailsAsync();

            foreach (var name in LayoutFlagNames) OnPropertyChanged(name);
        }
    }

    private static readonly string[] LayoutFlagNames =
        { nameof(LayoutIsCards), nameof(LayoutIsContactSheet), nameof(LayoutIsDetail), nameof(LayoutIsFilmstrip) };

    /// <summary>
    /// The storyboard's four layouts as a set of alternatives for the menu,
    /// on the same terms as <see cref="CutsAsList"/>: picking one selects it
    /// and switches to the storyboard, and clicking the one already ticked
    /// leaves it ticked.
    /// </summary>
    public bool LayoutIsCards
    {
        get => StoryboardView && StoryboardLayout == StoryboardLayout.Cards;
        set => ChooseLayout(StoryboardLayout.Cards, value, nameof(LayoutIsCards));
    }

    /// <inheritdoc cref="LayoutIsCards"/>
    public bool LayoutIsContactSheet
    {
        get => StoryboardView && StoryboardLayout == StoryboardLayout.ContactSheet;
        set => ChooseLayout(StoryboardLayout.ContactSheet, value, nameof(LayoutIsContactSheet));
    }

    /// <inheritdoc cref="LayoutIsCards"/>
    public bool LayoutIsDetail
    {
        get => StoryboardView && StoryboardLayout == StoryboardLayout.Detail;
        set => ChooseLayout(StoryboardLayout.Detail, value, nameof(LayoutIsDetail));
    }

    /// <inheritdoc cref="LayoutIsCards"/>
    public bool LayoutIsFilmstrip
    {
        get => StoryboardView && StoryboardLayout == StoryboardLayout.Filmstrip;
        set => ChooseLayout(StoryboardLayout.Filmstrip, value, nameof(LayoutIsFilmstrip));
    }

    /// <summary>
    /// Picking a layout from the menu also leaves the list view, since
    /// choosing how the storyboard looks while looking at something else
    /// would be a setting rather than a view.
    /// </summary>
    private void ChooseLayout(StoryboardLayout layout, bool chosen, string flagName)
    {
        if (!chosen)
        {
            OnPropertyChanged(flagName);
            return;
        }

        StoryboardLayout = layout;
        StoryboardView = true;
    }

    /// <summary>Whether the seekbar — timeline, position and length — is shown at all.</summary>
    public bool ShowSeekbar => _settings.Current.ShowSeekbar;

    /// <summary>Whether the waveform is drawn behind the seekbar.</summary>
    public bool ShowSeekbarWaveform => _settings.Current.ShowSeekbarWaveform;

    /// <summary>
    /// Whether the seekbar's filled position bar draws at all. Off leaves
    /// only the caret.
    /// </summary>
    public bool SeekbarBarEnabled => _settings.Current.SeekbarBarEnabled;

    /// <summary>Opacity of the seekbar's filled position wash.</summary>
    public double SeekbarOpacity => _settings.Current.SeekbarOpacity;

    /// <summary>How a timestamp pair is marked on the seekbar.</summary>
    public SeekbarBracketStyle SeekbarBracketStyle => _settings.Current.SeekbarBracketStyle;

    /// <summary>Whether the compact overlay is the view currently showing.</summary>
    private bool _minimalViewActive;

    /// <summary>
    /// Whether the main window is minimized. Set by the View, which owns the
    /// window state; read when deciding whether a restore should raise it.
    /// </summary>
    /// <remarks>
    /// Reported rather than asked for, because the question is "what did the
    /// user last do with this window", and the overlay hides the window while
    /// it is up — so by the time the answer is wanted there is nothing visible
    /// to read it off.
    /// </remarks>
    public bool WindowMinimized { get; set; }

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
    /// fullscreen or maximized, which missed the ordinary case of a windowed
    /// player being worked in and fired on a maximized player sitting behind
    /// something else. What actually decides whether the full window is worth
    /// showing is whether the user is looking at it.
    ///
    /// Driven from the poll rather than from an event, because focus changes
    /// in another process do not notify us.
    ///
    /// The setting is the only thing that puts the overlay up. Choosing the
    /// view by hand used to sit alongside this as a pin that ignored focus;
    /// both it and the pin are gone, so there is one rule here rather than two
    /// layered ones, and no invisible mode for the user to be in.
    ///
    /// The two directions are not symmetric. Leaving the player always restores
    /// the full window, whatever took focus: the overlay exists to sit over the
    /// video, and anywhere else it is a box in the way. Returning to the player
    /// drops back to the overlay only on the edge, so pressing X does not
    /// bounce straight back to the overlay a tick later while the player still
    /// holds focus.
    ///
    /// The overlay only stands while it has something to show. With an empty
    /// list it is a panel listing nothing, so the full window keeps the screen
    /// instead — see the <see cref="HasNoBookmarks"/> check below.
    /// </remarks>
    private void ApplyAutoViewSwitch()
    {
        // The app is blocked on a prompt — "this file exists", a rename, a save
        // dialog, a message box. The view must not move underneath it.
        //
        // A modal loop still pumps messages, so the poll goes on ticking while
        // the prompt waits, and clicking the player was enough to switch to the
        // overlay and hide the main window with the prompt behind it. Since the
        // prompt is modal, the app then accepted no input anywhere and there was
        // nothing left on screen to answer: only Alt+Tab got it back, and these
        // dialogs are ShowInTaskbar="False", so even that is awkward.
        //
        // Checked before everything else: whatever the view is when a prompt
        // opens is the view it keeps until the prompt is answered. IsThreadModal
        // is set by WPF's own ShowDialog and MessageBox, so this covers every
        // prompt without each one having to remember to announce itself.
        if (ComponentDispatcher.IsThreadModal) return;

        // Nothing to show, so nothing goes up — an overlay listing nothing is
        // just a box over the video.
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

        var mpcFocused = _mpc.IsForeground();

        if (!mpcFocused)
        {
            _lastMpcFocused = false;

            // The overlay only ever goes up while the player has focus, so
            // losing it is always a real departure, and the window always comes
            // back: the overlay is topmost and cannot be left floating over
            // another application. What varies is whether it is raised.
            //
            // Raised only when this program is where the focus went — leaving
            // the player for this window is the gesture that asks for it back,
            // and the overlay says so in as many words while the player is
            // windowed. It is a one-off raise, not topmost: this window has no
            // more claim on the foreground afterwards than any other, and
            // clicking back to the player hands it straight over.
            //
            // Going to some third program is not that gesture. The window still
            // returns, behind what was clicked, because a window that shoves
            // itself in front of the thing someone just chose is interrupting
            // them — and it has nothing to say that was worth the interruption.
            //
            // The same for a window the user had minimized, whoever took the
            // focus. Minimizing says this window is not wanted on screen, and
            // that outlives a trip to the player and back.
            //
            // Everything else is raised, including focus that went nowhere at
            // all — the player quit, or was dismissed onto the desktop. Nobody
            // is being interrupted there, and the alternative is the editor
            // sitting behind an empty screen waiting to be found.
            if (_minimalViewActive)
                RestoreFullView(activate: !WindowMinimized && !ForegroundWindow.OwnedByAnotherApp());

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

        if (IsBookmarkFileLoaded && !_minimalViewActive) EnterMinimalView();
    }

    /// <summary>
    /// Raised when the view should change. The View owns the windows; the
    /// ViewModel only signals the intent.
    /// </summary>
    /// <remarks>
    /// The second argument says whether the full window should also be
    /// activated. An explicit request — the X key, a second launch — should
    /// bring the window to the front, because the user just asked for it. A
    /// restore that happens because focus moved to some third application
    /// should not, or the app would snatch focus back from whatever they
    /// switched to.
    /// </remarks>
    public event Action<bool, bool>? MinimalViewRequested;

    /// <summary>
    /// Arms the X restore key to match whether the overlay is on screen.
    /// </summary>
    /// <remarks>
    /// X is a bare letter on a global hook: armed whenever the overlay merely
    /// existed, it fired on the "x" in anything the user typed in another
    /// application, and with the overlay hidden there was nothing on screen to
    /// explain why the editor had just jumped to the front. Armed only while
    /// the overlay is visible, the key belongs to something the user can see.
    ///
    /// That strands nothing: the overlay is only ever hidden with the full
    /// window up in its place.
    /// </remarks>
    private void SetOverlayShown(bool shown) => _hotkeys.RestoreArmed = shown;

    /// <summary>
    /// Puts the overlay up.
    /// </summary>
    /// <remarks>
    /// The current focus is recorded as the baseline so this very call does
    /// not read as an edge on the next poll tick and immediately undo itself.
    /// </remarks>
    private void EnterMinimalView()
    {
        _lastMpcFocused = _mpc.IsForeground();
        _minimalViewActive = true;

        // Before the overlay is shown, so the first frame carries the right
        // line rather than last session's and a correction a tick later.
        RefreshMinimalHint();

        // The View shows the overlay as part of the swap, so it is on screen as
        // of this call — and the X key is armed with it.
        SetOverlayShown(true);
        MinimalViewRequested?.Invoke(true, false);
    }

    /// <summary>
    /// Puts the full window back without saying the user is done with the
    /// overlay.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ShowFullView"/> because the two mean different
    /// things. Focus leaving the player, or the list going empty, is a reason to
    /// show the window — not a statement that the user wants it to stay. So the
    /// setting survives this, and the overlay comes back when the reason does.
    /// </remarks>
    private void RestoreFullView(bool activate)
    {
        _minimalViewActive = false;

        // The View hides the overlay as part of the swap, so this stays in step
        // with what is actually on screen — otherwise a later re-entry would
        // think it was still showing and skip putting it back up — and it
        // disarms the X key, which has nothing to restore any more.
        SetOverlayShown(false);
        MinimalViewRequested?.Invoke(false, activate);
    }

    /// <summary>
    /// Returns to the full window. Bound to the X restore key, and to a second
    /// launch of the app asking to be seen.
    /// </summary>
    /// <remarks>
    /// A fullscreen player is taken out of fullscreen first. Nothing can be put
    /// in front of one: it covers the monitor, it holds the foreground, and
    /// MPC-HC keeps it above the ordinary z-order — so restoring the window
    /// behind it looked from the outside exactly like X doing nothing at all.
    /// Leaving fullscreen is therefore part of what X means here, not a side
    /// effect: the user asked to be shown this window, and that is what showing
    /// it costs. A windowed player is left exactly as it is.
    ///
    /// None of which applies to a minimized window. Then X dismisses the
    /// overlay and stops there — no window goes up, so the player keeps its
    /// fullscreen and the one put away stays put away.
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
        // Minimized, so X means only what it says on the overlay: take this
        // away. The window was deliberately put out of sight and stays there,
        // and the player keeps its fullscreen — leaving fullscreen is a cost
        // paid to give this window somewhere to be seen, and there is no
        // window going up to need it.
        if (WindowMinimized)
        {
            _lastMpcFocused = _mpc.IsForeground();
            RestoreFullView(activate: false);
            return;
        }

        _mpc.ExitFullscreen();
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
    [ObservableProperty] private string _setTimestampMenuLabel = "Set bookmark";

    /// <summary>Grayed-out example of what the active naming tag produces.</summary>
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
    /// Shows just the PLS filename, or the same placeholder the video and
    /// bookmark fields use — three slots reporting nothing should say it the
    /// same way.
    /// </summary>
    public string PlaylistFileName => string.IsNullOrEmpty(LoadedPlaylistPath) || !File.Exists(LoadedPlaylistPath)
        ? "<none>"
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
            // Doubled, because a MenuItem's string header renders through
            // AccessText and a single underscore is an access-key marker —
            // "puta_man.pls" arrived as "putaman.pls" with the m underlined.
            // The same doubling is done where the playlist submenus are built.
            ? $"Add current video to '{Path.GetFileName(LoadedPlaylistPath).Replace("_", "__")}'"
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
        _thumbnails = new ThumbnailService(_ffmpeg);
        _mpc = new MpcHcService();
        _bookmarks = new BookmarkService();
        _playlists = new PlaylistService();
        _images = new ImageConversionService();
        _hotkeys = new HotkeyService();

        // Toasts appear over MPC-HC's monitor — the hotkey is usually pressed
        // while the player has focus, where the status bar can't be seen.
        // Over whatever is in front, not always over the player. The anchor
        // only picks the monitor — and with the player on one screen and this
        // window on another, every toast raised by a button in here was
        // appearing on the screen the user was not looking at. Which is
        // indistinguishable from toasts being broken.
        //
        // The hotkey case is unchanged: pressed while watching, the player is
        // the foreground window and the toast lands over it as before.
        _toast = new ToastService(() =>
        {
            var front = ForegroundWindow.Handle();
            return front != IntPtr.Zero ? front : _mpc.FindMpcWindow();
        });

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
        // unpinned behavior and needs no explanation.
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

        // After the commands exist — every button in it holds one.
        BuildToolbar();

        // Subscribed once, here, rather than assigned in ApplySettings. It was
        // assigned there — "so saving Settings twice cannot end up playing the
        // sound twice" — and an assignment takes the whole invocation list, so
        // the first time Settings was saved it silently dropped the window's
        // own handler and the job panel stopped flashing green for the rest of
        // the session. Subscribing once is what that comment was reaching for;
        // the setting it worried about is read inside the handler anyway, so
        // nothing needed re-pushing.
        Job.Finished += PlayCompletionSound;

        // And the status strip says so too, from the one place every
        // successful operation in the program already reports through.
        Job.Finished += () => FlashStatus(StatusLevel.Ok);

        // And a toast, from the same place and for the same reason. Several
        // operations said nothing but a line of status text, which is the one
        // part of this window somebody running an action is least likely to be
        // looking at — they are watching the player, or the folder the file is
        // about to appear in.
        Job.Finished += ToastJobFinished;

        // The seekbar menu shows its snap switch only while there is
        // something to snap to, so the collection has to say when that
        // changes — a computed property raises nothing on its own.
        SnapPoints.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSnapPoints));

        // Last, so the opening state is the baseline rather than something
        // half-built: everything above this line is setup, not an edit.
        StartWatchingForHistory();

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
    /// <summary>
    /// The player for <see cref="PlayCompletionSound"/>, kept between jobs so
    /// the wav is not re-read for every operation, and rebuilt if the sound
    /// changes underneath it.
    /// </summary>
    private System.Media.SoundPlayer? _completionChime;
    private string? _completionChimePath;

    /// <summary>
    /// The ding at the end of an operation, when it is switched on.
    /// </summary>
    /// <remarks>
    /// Plays <c>tada.wav</c>, the stock Windows fanfare, shipped with every
    /// install since the beginning and unambiguously a "finished" sound. This
    /// started as <c>SystemSounds.Asterisk</c>, which is what dialogs use — so
    /// a finished export announced itself in the voice Windows keeps for
    /// telling you something has gone wrong.
    ///
    /// Playing is asynchronous — <see cref="System.Media.SoundPlayer.Play"/>
    /// hands off to its own thread — so it never holds up the panel.
    ///
    /// Wrapped because audio is hardware: a machine with no output device
    /// throws here, and an operation that wrote its files must not be reported
    /// as failed because nothing could be played afterwards.
    /// </remarks>
    private void PlayCompletionSound()
    {
        if (!_settings.Current.CompletionSound) return;

        try
        {
            var path = CompletionSoundPath();
            if (path == null) return;

            if (_completionChime == null ||
                !string.Equals(_completionChimePath, path, StringComparison.OrdinalIgnoreCase))
            {
                _completionChime = new System.Media.SoundPlayer(path);
                _completionChimePath = path;
            }

            _completionChime.Play();
        }
        catch { /* no audio device — the files are still written */ }
    }

    /// <summary>
    /// Stock Windows sounds to finish a job with, best first.
    /// </summary>
    /// <remarks>
    /// <c>tada.wav</c> is the one meant — a fanfare, unmistakably "done".
    /// The rest are there because the first may not be: the list runs from
    /// most wanted to most likely to exist. <c>chimes</c>, <c>ding</c> and
    /// <c>notify</c> have shipped since Windows 95 and are pleasant enough to
    /// stand in; the Notify System Generic wav is the modern notification
    /// sound, present on 10 and 11 but not before.
    ///
    /// None of them is an error sound. A machine missing the whole list would
    /// be better off silent than announcing a finished export with the noise
    /// Windows makes when something breaks.
    /// </remarks>
    private static readonly string[] _completionSounds =
    {
        "tada.wav",
        "chimes.wav",
        "ding.wav",
        "notify.wav",
        "Windows Notify System Generic.wav"
    };

    /// <summary>
    /// The wav played when a job finishes, or null if there is none to play.
    /// </summary>
    /// <remarks>
    /// Windows' own media folder, located through the environment rather than
    /// assumed to be on C: — a machine that boots from another drive has it
    /// somewhere else, and hardcoding the path would fail there for no reason.
    ///
    /// The sound scheme is the last resort rather than the first. It is the
    /// most certain to resolve to a file that exists on this particular
    /// machine, but it is also whatever the user has pointed their
    /// notifications at, which may be a sound with a meaning of its own — so
    /// it is worth asking only once the known-good list has come up empty.
    /// That takes a Media folder with none of five stock wavs in it, which
    /// means a stripped install, and by then any sound is a good sound.
    /// </remarks>
    private static string? CompletionSoundPath()
    {
        var media = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");

        foreach (var name in _completionSounds)
        {
            var candidate = Path.Combine(media, name);
            if (File.Exists(candidate)) return candidate;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"AppEvents\Schemes\Apps\.Default\Notification.Default\.Current");

            if (key?.GetValue(null) is string configured && File.Exists(configured))
                return configured;
        }
        catch { /* nothing to play, which is survivable */ }

        return null;
    }

    private void ApplyServiceSettings()
    {
        _mpc.WebInterfacePort = ResolveWebInterfacePort();
        // Encoder and quality flags travel together — the flags are only valid
        // for the encoder they were built for.
        _ffmpeg.Encoder = _settings.Current.VideoEncoder;
        _ffmpeg.QualityArgs = _settings.GetQualityArgs();
        _ffmpeg.PreciseCuts = _settings.Current.PreciseCuts;
        _ffmpeg.NormalizeAudio = _settings.Current.NormalizeAudio;

        _hotkeys.BlockInPlayer = _settings.Current.BlockHotkeyInPlayer;

        _toast.Enabled = _settings.Current.ToastsEnabled;
        _toast.HoldDuration = TimeSpan.FromSeconds(_settings.Current.ToastSeconds);

        // Static rather than injected — see RecycleBin.SendToBin. Pushed here
        // so it is set before anything can delete, and re-pushed when Settings
        // is saved.
        RecycleBin.SendToBin = _settings.Current.DeleteToRecycleBin;

        _pollTimer.Interval = _settings.GetPollInterval();
    }

    /// <summary>
    /// The port to talk to MPC-HC on: taken from the player's own settings when
    /// automatic detection is on, and from the manual setting otherwise.
    /// </summary>
    /// <remarks>
    /// Detection failing is not an error. An unusual install simply falls back
    /// to the manual value, which is exactly where this feature started, so
    /// nobody ends up worse off than before it existed.
    /// </remarks>
    private int ResolveWebInterfacePort()
    {
        var manual = _settings.Current.MpcWebInterfacePort;
        if (!_settings.Current.AutoDetectMpcWebInterface)
        {
            _mpc.LastDetectedWebConfig = null;
            return manual;
        }

        var detected = MpcHcService.DetectWebInterface();
        _mpc.LastDetectedWebConfig = detected;
        return detected?.Port ?? manual;
    }

    // ------------------------------------------------------------------
    // Selected-clip preview
    // ------------------------------------------------------------------

    /// <summary>Cancels the in-flight preview render when the target moves on.</summary>
    private CancellationTokenSource? _previewCts;

    /// <summary>
    /// The clip the pane shows: the selected one, or the first complete clip
    /// when nothing is selected.
    /// </summary>
    /// <remarks>
    /// The fallback exists because the pane originally required a row to be
    /// clicked and said so nowhere. With one bookmark in the list and no
    /// selection, the whole feature was invisible — and the most inviting thing
    /// to click on a row is its timestamp, which seeks the player instead of
    /// selecting anything. Showing the first clip means the pane is there to be
    /// noticed, and clicking a row still changes it.
    /// </remarks>
    private Bookmark? PreviewTarget =>
        SelectedBookmark is { IsValid: true } selected
            ? selected
            : Session.Bookmarks.FirstOrDefault(b => b.IsValid);

    /// <summary>Re-renders when a different clip is selected.</summary>
    /// <summary>
    /// Re-renders when a different clip is highlighted, and re-asks the
    /// commands that can act on it.
    /// </summary>
    /// <remarks>
    /// The command refresh is what lets Flip, Rotate, Mute and Fade come alive
    /// on a row that has merely been clicked. Their predicate reads
    /// <see cref="SelectedBookmark"/>, and a predicate nobody re-evaluates
    /// leaves the buttons grey until something else happens to refresh them.
    /// </remarks>
    partial void OnSelectedBookmarkChanged(Bookmark? value)
    {
        RefreshClipPreview();
        RefreshCommandStates();
    }

    /// <summary>
    /// Renders the selected clip's first and last frame into the side panel.
    /// </summary>
    /// <remarks>
    /// Every call cancels the one before it. Arrowing down a list fires this
    /// once per row, and without cancellation the panel would end up showing
    /// whichever ffmpeg happened to finish last rather than the clip that is
    /// actually selected.
    /// </remarks>
    private void RefreshClipPreview()
    {
        // Canceled but not disposed: the render it belongs to may still be
        // holding the token, and disposing under it turns an ordinary
        // cancellation into an ObjectDisposedException. Nothing here registers
        // callbacks or timers on it, so letting the collector have it costs
        // nothing.
        _previewCts?.Cancel();
        _previewCts = null;

        var bookmark = PreviewTarget;
        var video = Session.VideoPath;

        if (bookmark is null || string.IsNullOrWhiteSpace(video))
        {
            HasClipPreview = false;
            ClipPreviewHeading = string.Empty;
            ClipInThumbnail = null;
            ClipOutThumbnail = null;
            PreviewedBookmark = null;
            return;
        }

        // Numbered rather than called "selected": with nothing selected this is
        // the first clip, and a heading claiming otherwise would be a small lie
        // repeated every time the app opens.
        ClipPreviewHeading = $"CLIP [{bookmark.Index}]";
        PreviewedBookmark = bookmark;

        // The pane appears immediately and fills in, rather than popping into
        // existence a few hundred milliseconds later.
        HasClipPreview = true;
        ClipInThumbnail = null;
        ClipOutThumbnail = null;

        var cts = new CancellationTokenSource();
        _previewCts = cts;
        _ = RenderClipPreviewAsync(video, bookmark.StartSeconds, bookmark.EndSeconds, cts.Token);
    }

    /// <summary>
    /// The clip currently drawn, so a change to it can be noticed — and so the
    /// panel can bind to its settings.
    /// </summary>
    /// <remarks>
    /// Observable rather than a plain field because the preview shows the clip
    /// as it will be written: turned, mirrored, silenced, faded. Those come off
    /// the bookmark itself, which raises its own notifications, so binding
    /// through this property means toggling Rotate or Mute on the selected row
    /// changes the frames on screen without re-running ffmpeg for them.
    /// </remarks>
    [ObservableProperty] private Bookmark? _previewedBookmark;

    private async Task RenderClipPreviewAsync(string video, double start, double end, CancellationToken ct)
    {
        try
        {
            var first = await _thumbnails.GetAsync(video, start, ct);
            if (ct.IsCancellationRequested) return;
            ClipInThumbnail = first;

            // A shade before the end rather than exactly on it: asking for the
            // end timestamp can land one frame past the last one and come back
            // with nothing at all.
            var lastAt = Math.Max(start, end - 0.1);
            var last = await _thumbnails.GetAsync(video, lastAt, ct);
            if (ct.IsCancellationRequested) return;
            ClipOutThumbnail = last;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection; nothing to report.
        }
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

        // Adding or removing a row changes which row is above which, and a
        // start's floor is the row above it.
        RefreshNudgeLimits();

        // The first bookmark appearing is what makes the preview pane possible
        // at all, and a removed one may have been the clip it was drawing.
        RefreshClipPreview();

        // Adding or removing a cut changes both of these. They used to be
        // raised from a handler attached in OnSessionChanged, which never runs:
        // Session is only ever its field initializer, and the generated partial
        // fires on assignment, not on construction. Here they are wired by
        // HookSession, which the constructor does call.
        OnPropertyChanged(nameof(HasValidBookmarks));
        Session.NotifyDurationChanged();

        // A cut added while the storyboard is up needs a card drawn for it.
        if (StoryboardView && e.NewItems != null) _ = RefreshStoryboardThumbnailsAsync();
    }

    private void Bookmark_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only the properties the gates actually read.
        if (e.PropertyName is nameof(Bookmark.IsSelected)
                           or nameof(Bookmark.IsIncomplete)
                           or nameof(Bookmark.StartSeconds)
                           or nameof(Bookmark.EndSeconds))
            RefreshCommandStates();

        // Edit length is the length of what the next action would actually
        // produce: the checked cuts, or every cut when none are checked. So
        // ticking a checkbox changes it, and so does moving a timestamp or a
        // speed slider. It was computed correctly and simply never re-read —
        // the figure on screen was whatever it had been when something else
        // happened to refresh it.
        if (e.PropertyName is nameof(Bookmark.IsSelected)
                           or nameof(Bookmark.StartSeconds)
                           or nameof(Bookmark.EndSeconds)
                           or nameof(Bookmark.Speed))
            Session.NotifyDurationChanged();

        // Moving the ends of the clip on screen moves the frames on screen.
        // Only for the one actually drawn — editing clip 9 while clip 1 is
        // shown should not re-run ffmpeg.
        if (e.PropertyName is nameof(Bookmark.StartSeconds) or nameof(Bookmark.EndSeconds)
            && ReferenceEquals(sender, PreviewedBookmark))
            RefreshClipPreview();

        // A bookmark becoming complete can make it the first valid one, which
        // is what the pane falls back to when nothing is selected. A drawn one
        // becoming incomplete has to be dropped for the same reason — it is no
        // longer a clip with two ends to show.
        if (e.PropertyName is nameof(Bookmark.IsIncomplete)
            && (PreviewedBookmark is null || ReferenceEquals(sender, PreviewedBookmark)))
            RefreshClipPreview();

        // A moved timestamp changes how much room the arrows either side of it
        // have left — and, since a start is held off the row above, how much
        // room the row below has too. So the whole list is recomputed, not just
        // the row that moved.
        if (e.PropertyName is nameof(Bookmark.StartSeconds)
                           or nameof(Bookmark.EndSeconds)
                           or nameof(Bookmark.IsIncomplete))
            RefreshNudgeLimits();

        // The card draws the cut's first frame, so moving the start makes the
        // one on screen a picture of somewhere the cut no longer begins.
        // Dropped rather than re-fetched here: the refresh below only redraws
        // what is missing, and this is how it is told what is.
        if (e.PropertyName is nameof(Bookmark.StartSeconds) or nameof(Bookmark.EndSeconds)
            && sender is Bookmark moved)
        {
            if (e.PropertyName == nameof(Bookmark.StartSeconds)) moved.Thumbnail = null;
            else moved.ThumbnailEnd = null;

            if (StoryboardView) _ = RefreshStoryboardThumbnailsAsync();
        }
    }

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditSession.VideoPath) or nameof(EditSession.CsvPath))
            RefreshCommandStates();

        // The length arrives a moment after the video, and it is the ceiling
        // every forward arrow is measured against.
        if (e.PropertyName is nameof(EditSession.VideoDurationSeconds))
            RefreshNudgeLimits();

        // A different video makes every cached frame useless, and the pane has
        // to be redrawn from the new one.
        if (e.PropertyName is nameof(EditSession.VideoPath))
        {
            _thumbnails.Clear();
            RefreshClipPreview();

            // Both of these describe the file that just went away. Edit points
            // found in one video are meaningless in another, and a card drawn
            // from one would be a frame of the wrong film.
            // The switch goes with them. It belongs to the scan that filled
            // these in, and a video that has not been scanned has nothing for
            // it to mean — leaving it on would be the setting outliving its
            // subject, which is what made it read as global.
            SnapPoints.Clear();
            SnapToEditPoints = false;

            foreach (var b in Session.Bookmarks) { b.Thumbnail = null; b.ThumbnailEnd = null; }

            if (StoryboardView) _ = RefreshStoryboardThumbnailsAsync();
        }
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
        // without it the overlay's "(no bookmarks yet)" line would miss the
        // list going empty whenever no pair completed with it.
        //
        // Session.HasVideo is here in its own right too, and not folded into
        // HasActiveVideo: that one also asks whether MPC-HC is running, so
        // loading a video with the player closed moves neither it nor the key,
        // and RevealVideo — which does not care about the player — would never
        // be told its answer had changed.
        // SelectedCount is in here alongside SelectedPairCount, not folded into
        // it: checking a bookmark that has no closing timestamp yet moves only
        // the former, and without it delete and "select none" would not notice
        // the one kind of selection they are the only commands to accept.
        // Whether a row is highlighted, because the commands that fall back to
        // it when nothing is checked have to be re-asked when it moves. Without
        // this, the method returned early on a plain click — the key was
        // unchanged, so those commands were never re-asked and stayed grey
        // until a check happened to move one of the counts.
        //
        // Two flags, because the fallbacks do not agree on what counts. Flip,
        // Rotate, Mute and Fade need a cut with a range; delete also takes a
        // lone opening timestamp, which is exactly the row someone wants rid of
        // after a mis-timed press.
        //
        // Which row it is stays out of the key. Nothing gated here tells one
        // valid row from another, and an identity in here would defeat the
        // bail-out on every arrow-key press down a long list.
        var hasHighlightedCut = SelectedBookmark is { IsValid: true };
        var hasHighlightedRow = SelectedBookmark is not null;

        var key = string.Join('|', HasActiveVideo, Session.HasVideo,
                                   IsBookmarkFileLoaded, CompletePairCount,
                                   SelectedPairCount, SelectedCount, HasNoBookmarks,
                                   HasPlaylistFiles, LoadedPlaylistHasEntries,
                                   hasHighlightedCut, hasHighlightedRow);
        if (key == _commandStateKey) return;
        _commandStateKey = key;

        OnPropertyChanged(nameof(HasActiveVideo));
        OnPropertyChanged(nameof(CanRevealVideo));
        OnPropertyChanged(nameof(CompletePairCount));
        OnPropertyChanged(nameof(SelectedPairCount));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasValidBookmarks));

        OnPropertyChanged(nameof(HasEditLength));
        OnPropertyChanged(nameof(HasNoBookmarks));
        OnPropertyChanged(nameof(AllBookmarksSelected));
        OnPropertyChanged(nameof(ShowSelectAllHeader));

        SetTimestampCommand.NotifyCanExecuteChanged();
        UndoLastBookmarkCommand.NotifyCanExecuteChanged();
        EditBookmarksCommand.NotifyCanExecuteChanged();
        DeleteBookmarksCommand.NotifyCanExecuteChanged();
        EnterTimeManualCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        // All four share CanToggleFlip. Notifying only the first left Rotate
        // and Mute stuck in the state they were evaluated in at startup —
        // disabled, since nothing was selected then — so neither button ever
        // became usable.
        ToggleFlipCommand.NotifyCanExecuteChanged();
        RotateSelectedCommand.NotifyCanExecuteChanged();
        ToggleMuteCommand.NotifyCanExecuteChanged();
        ToggleFadeCommand.NotifyCanExecuteChanged();
        FadeInCommand.NotifyCanExecuteChanged();
        FadeOutCommand.NotifyCanExecuteChanged();
        SaveCurrentFrameCommand.NotifyCanExecuteChanged();
        ExportAnimationCommand.NotifyCanExecuteChanged();
        PlayAllCommand.NotifyCanExecuteChanged();
        PlaySelectedCommand.NotifyCanExecuteChanged();
        TogglePauseCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        SelectNoneCommand.NotifyCanExecuteChanged();
        ToggleSelectAllCommand.NotifyCanExecuteChanged();

        // The selection just moved, which is the only thing that can turn the
        // toolbar's select button around.
        RefreshSelectionButton();
        MergeSelectedCommand.NotifyCanExecuteChanged();
        SplitSelectedCommand.NotifyCanExecuteChanged();
        AddCurrentToPlaylistCommand.NotifyCanExecuteChanged();
        RevealVideoCommand.NotifyCanExecuteChanged();

        // Both gate on facts this key already tracks, and neither was being
        // told. It went unnoticed while they were menu entries, which are
        // opened rarely enough that the stale answer was usually the right one
        // by the time anybody looked. On the toolbar they are in view the whole
        // time, so each sat in the state it was evaluated in at startup — with
        // no video open, which is to say disabled for the rest of the session.
        DetectBookmarksCommand.NotifyCanExecuteChanged();
        SplitAtPlayheadCommand.NotifyCanExecuteChanged();
        ExportChaptersCommand.NotifyCanExecuteChanged();
        ExportCutListCommand.NotifyCanExecuteChanged();
        ImportCutListCommand.NotifyCanExecuteChanged();
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
    // These three work on the checks themselves rather than on what can be cut,
    // so they count every checked row, not only the complete ones. A list
    // holding nothing but a lone opening timestamp can still be selected and
    // deleted.
    // Or the highlighted row, which is what the command body has always fallen
    // back to — it just could not be reached, because this said no unless
    // something was checked.
    //
    // Any highlighted row, not only a complete one: delete is the one action
    // that works on a lone opening timestamp, which is exactly the row someone
    // wants rid of after a mis-timed press.
    private bool CanDeleteSelected() =>
        IsBookmarkFileLoaded && (SelectedCount >= 1 || SelectedBookmark is not null);
    private bool CanSelectAll() => HasActiveVideo && IsBookmarkFileLoaded && !HasNoBookmarks;
    private bool CanSelectNone() => HasActiveVideo && IsBookmarkFileLoaded && SelectedCount >= 1;

    // One cut is enough to play. These wanted two, on the reasoning that
    // sequencing needs something to sequence — but playing a single cut is
    // seeking to its start and stopping at its end, which is the most direct
    // way there is to check that a cut is the piece you meant. Merge and Split
    // already work on one, for the same reason.
    private bool CanPlayAll() => HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount >= 1;
    private bool CanPlaySelected() => CanPlayAll() && SelectedPairCount >= 1;
    private bool CanMergeSelected() => HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount >= 1;
    private bool CanSplitSelected() => HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount >= 1;

    private bool CanAddCurrentToPlaylist() => HasActiveVideo;

    // Only a video, deliberately. Grabbing a still has nothing to do with the
    // cut list, and needing a bookmark file first would put a still behind a
    // decision about where the cuts are going to be saved.
    private bool CanSaveCurrentFrame() => HasActiveVideo;

    /// <summary>
    /// The cuts that Flip, Rotate, Mute and Fade act on: the checked ones, or
    /// the highlighted row when nothing is checked.
    /// </summary>
    /// <remarks>
    /// These four describe how one clip is written, so the row under the
    /// pointer is a perfectly good way to say which clip. Requiring a check as
    /// well made setting a property on a single cut a two-step job — click it,
    /// then tick it, then remember to untick it — where the click had already
    /// said everything.
    ///
    /// Checks still win when there are any. They are the explicit statement of
    /// a set, and a selection made deliberately must not be quietly narrowed to
    /// whichever row happens to be highlighted within it.
    /// </remarks>
    private List<Bookmark> ModifierTargets()
    {
        var ticked = Session.Bookmarks.Where(b => b.IsSelected && b.IsValid).ToList();
        if (ticked.Count > 0) return ticked;

        return SelectedBookmark is { IsValid: true } row
            ? new List<Bookmark> { row }
            : new List<Bookmark>();
    }

    /// <summary>
    /// Flip, Rotate, Mute and Fade: enabled whenever there is a cut to act on.
    /// </summary>
    /// <remarks>
    /// A complete cut existing, rather than one being checked or highlighted.
    /// Requiring the choice as well left four buttons greyed out in a list full
    /// of cuts — the thing you reach for them with is right there, and a button
    /// that goes grey until you click the row beside it reads as broken rather
    /// than as waiting.
    ///
    /// Pressed with nothing chosen they say so, the way Delete already does.
    /// That is the same bargain: the gate asks whether the action is possible,
    /// and the command says what is missing.
    /// </remarks>
    private bool CanToggleFlip() =>
        HasActiveVideo && IsBookmarkFileLoaded && CompletePairCount >= 1;

    /// <summary>
    /// Says what to do when one of the four is pressed with nothing checked and
    /// no row highlighted.
    /// </summary>
    private bool NothingToModify()
    {
        StatusText = "Nothing chosen — check the bookmarks you want, or click a row first.";
        return true;
    }

    // Both of these act on the bookmark file rather than on the cuts in it, so
    // both need only the file.
    //
    // "Edit bookmarks" used to also require a complete pair, which sounds right
    // and is not: writing the very first timestamp is what creates the CSV, so
    // a lone opening timestamp is a file that exists and has a row in it. The
    // pencil beside CURRENT BOOKMARKS follows this predicate and hides itself
    // when it is false, so in that state the file was named on screen with no
    // way to open it — and editing by hand is exactly how a stray opening
    // timestamp gets fixed.
    private bool CanEditBookmarks() => IsBookmarkFileLoaded;
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
        HotkeyDisplay = label;
        SetTimestampMenuLabel = $"Set bookmark: {label}";
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
        ActiveSuffixDisplay = text.Length == 0
            ? "Current rename tag: none"
            : $"Current rename tag: {text}";
        SuffixExampleDisplay = BuildSuffixExample(text);

        RefreshNamingTagButtons();
    }

    /// <summary>
    /// Lights the toolbar's naming-tag button that matches the active tag, and
    /// puts the rest back to ordinary.
    /// </summary>
    /// <remarks>
    /// The tag buttons are a set of which exactly one is true, and nothing on
    /// the toolbar said which: pressing [done] looked the same as not pressing
    /// it, and a tag set from the Options menu was invisible out here. Lit by
    /// swapping the style the item names rather than by a trigger, because the
    /// style is already chosen that way and the binding re-reads it.
    ///
    /// "no tag" is one of the set, not the absence of one — with no tag active
    /// it is the button that is true.
    /// </remarks>
    private void RefreshNamingTagButtons()
    {
        const string prefix = "tag:";
        var active = _settings.GetActiveSuffixText();

        foreach (var item in ToolbarItems)
        {
            if (!item.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;

            var tag = item.Key[prefix.Length..];
            var lit = tag == "none"
                ? active.Length == 0
                : string.Equals(tag, active, StringComparison.Ordinal);

            item.StyleKey = lit ? "ActiveTagButton" : null;
        }
    }

    /// <summary>
    /// The "filename.mp4 → filename[done].mp4" line under Tags ▸ Current.
    /// </summary>
    /// <remarks>
    /// Uses the configured output container, so changing the format in
    /// Settings updates the example to match what will actually be written
    /// rather than leaving a stale <c>.mp4</c> on screen.
    ///
    /// No "Example:" in front of it any more. It sits under a heading that
    /// says Current, and a filename with an arrow through it is already
    /// recognisably an example — the word was labelling what the line plainly
    /// was.
    ///
    /// Underscores are doubled because this is a MenuItem header, which
    /// renders through AccessText: a single one is an access-key marker and
    /// would be eaten, so a tag named my_tag would show as "mytag" with the t
    /// underlined.
    /// </remarks>
    private string BuildSuffixExample(string suffixText)
    {
        var ext = OutputFormat.Extension;

        // With no tag the name does not change, so there is no arrow and no
        // second name to draw — showing "filename.mp4 → filename.mp4" made a
        // rename out of a line whose whole point is that nothing is renamed.
        // What replacing the original means is said where it can be acted on,
        // in the [no tag] entry's own tooltip.
        if (suffixText.Length == 0)
            return $"filename{ext}";

        return $"filename{ext}  →  filename[{suffixText.Replace("_", "__")}]{ext}";
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
    /// <param name="bookmark">
    /// The clip being written, when there is one. Only the naming template uses
    /// it — for the position and length tokens — so every existing caller can
    /// keep passing nothing and get exactly the behavior it had before.
    /// </param>
    private string GetUniqueOutputPath(string basePath, string extension, int startIndex = 1,
                                       string? outputDirectory = null, Bookmark? bookmark = null)
    {
        var dir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(basePath) ?? ""
            : outputDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var suffix = _settings.GetActiveSuffixText();
        var template = _settings.Current.NameTemplate;

        int counter = Math.Max(1, startIndex);
        while (true)
        {
            // The counter lives inside the bracket exactly as before, so a
            // collision still reads [done2] rather than gaining a suffix of its
            // own. With a custom template the bracket is whatever {suffix}
            // expanded to, and the counter follows the same rule.
            var bracket = SuffixBracket(counter);
            var stem = NameTemplate.Build(template, nameWithoutExt, bracket, bookmark);
            var candidate = Path.Combine(dir, $"{stem}{extension}");
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

    // ------------------------------------------------------------------
    // Window placement
    // ------------------------------------------------------------------

    /// <summary>
    /// Where the main window was last time, or null on a first run.
    /// </summary>
    /// <remarks>
    /// Read through the ViewModel so the window never touches the settings file
    /// itself. Whether the stored position is usable is the window's business —
    /// it is the one that knows what monitors are attached.
    /// </remarks>
    public WindowPlacement? SavedWindowPlacement => _settings.Current.MainWindowPlacement;

    /// <summary>
    /// The player's window, or zero when it is not running — for putting the
    /// overlay and the toasts on the monitor the video is on.
    /// </summary>
    /// <remarks>
    /// Looked up on each call rather than cached: the player is closed and
    /// reopened freely while this program runs, and a stale handle would place
    /// the overlay against a monitor chosen by a window that no longer exists.
    /// </remarks>
    public IntPtr PlayerWindow => _mpc.FindMpcWindow();

    /// <summary>Remembers where the main window finished up.</summary>
    /// <remarks>
    /// Saved on close rather than on every move: a settings file rewritten on
    /// each drag of a title bar is a lot of disk for a number nobody reads
    /// until the next launch.
    /// </remarks>
    public void SaveWindowPlacement(WindowPlacement placement)
    {
        _settings.Current.MainWindowPlacement = placement;
        _settings.Save();
    }

    /// <summary>
    /// The playlist folder as stored, whatever its state. Empty when none is
    /// set.
    /// </summary>
    /// <remarks>
    /// Here so the view can read the path rather than cutting it back out of
    /// <see cref="PlaylistFolderDisplay"/>, which is a label and free to say
    /// more than the path. That parsing is how the menu came to contradict
    /// itself: the header printed whatever was stored while the list below it
    /// decided the folder was not set.
    /// </remarks>
    public string PlaylistFolderPath => _settings.Current.PlaylistFolder ?? string.Empty;

    /// <summary>Whether the stored playlist folder is actually there.</summary>
    public bool PlaylistFolderExists =>
        PlaylistFolderPath.Length > 0 && Directory.Exists(PlaylistFolderPath);

    private void RefreshFolderDisplays()
    {
        PlaylistFolderDisplay = Describe("Playlist folder", _settings.Current.PlaylistFolder);
        QuickSaveFolderDisplay = Describe("Quick save", _settings.Current.QuickSaveFolder);
        SaveToFolderDisplay = Describe("Save to", ResolveSaveToDirectory());

        OnPropertyChanged(nameof(PlaylistFolderPath));
        OnPropertyChanged(nameof(PlaylistFolderExists));
    }

    /// <summary>
    /// A stored folder, as a label: the path, or that it is unset, or that it
    /// is set to somewhere that is no longer there.
    /// </summary>
    /// <remarks>
    /// The third case is the one that was missing, and it is not rare: a drive
    /// letter moves when an external disk is replugged, and a folder on it
    /// stops existing without anybody changing a setting. Printing the path
    /// alone then claims a folder that is gone, which is how the Playlist menu
    /// came to name a folder in its header and deny having one underneath.
    ///
    /// The path is still shown, not replaced — it is the only clue to what was
    /// meant, and the fix is usually to recognise the old drive letter.
    /// </remarks>
    private static string Describe(string label, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return $"{label}: (not set)";

        return Directory.Exists(folder)
            ? $"{label}: {folder}"
            : $"{label}: {folder}  — not found";
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

        // The collection subscription that used to be added here has moved into
        // Bookmarks_CollectionChanged, which HookSession wires for the initial
        // Session as well as for any replacement. Adding it here reached only a
        // reassignment — something that never happens.
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
        OnPropertyChanged(nameof(HasBookmarksFile));
    }

    /// <summary>
    /// Handler for the configurable timestamp hotkey. Delegates to
    /// <see cref="SetTimestamp"/> — the same command wired to the menu item
    /// and toolbar button — but only when the press is actually meant for
    /// this.
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
    /// So it fires only where it means something: MPC-HC is the active
    /// window, <em>or this editor is</em> — the seekbar's hover preview
    /// means a cut can now be marked by looking at this window instead of
    /// the player, and the hotkey ought to work from either — and either way
    /// there has to be a video open to timestamp. MPC-HC sits there with no
    /// file loaded quite happily, and there is no position to mark then. It
    /// is the same condition the menu item and toolbar button are gated on
    /// (<see cref="CanSetTimestamp"/>), which a direct call to
    /// <see cref="SetTimestamp"/> would otherwise walk straight past, since
    /// only the generated command consults it.
    ///
    /// Typing into a text box in this editor — a chapter name, a naming tag,
    /// a setting — does not also plant a bookmark. A keyboard binding with no
    /// modifiers at all is allowed, and without this a bound letter would
    /// fire on every matching keystroke typed anywhere in the window.
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
        var ownWindowActive = Application.Current?.MainWindow?.IsActive == true;
        if (!_mpc.IsForeground() && !ownWindowActive) return;
        if (!HasActiveVideo) return;

        if (ownWindowActive && System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
            return;

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

    // ------------------------------------------------------------------
    // The waveform behind the timeline
    // ------------------------------------------------------------------

    [ObservableProperty]
    private BitmapSource? _waveform;

    /// <summary>
    /// Cancels the render in flight when the video changes under it.
    /// </summary>
    /// <remarks>
    /// Drawing a waveform decodes the whole audio track, so on a long file it
    /// outlives the load that asked for it. Without this, opening three videos in
    /// a row leaves three decodes running and the last one to finish wins —
    /// which is not necessarily the one for the video on screen.
    /// </remarks>
    private CancellationTokenSource? _waveformRender;

    /// <summary>The video the current waveform was drawn from.</summary>
    private string _waveformSource = string.Empty;

    /// <summary>
    /// Draws the waveform for the loaded video, unless it is already the one on
    /// screen.
    /// </summary>
    /// <remarks>
    /// Not awaited by the load. A waveform is decoration behind the bar; making
    /// the video take a second longer to open for it would be the wrong trade,
    /// so it arrives when it arrives and the bar is usable throughout.
    ///
    /// Rendered 1600 pixels wide, well past any window, so widening the window
    /// stretches the picture rather than starting the decode again.
    /// </remarks>
    /// <param name="force">
    /// Redraws even if this is "the same file as last time" — for a change
    /// that does not touch the file at all, such as a new waveform colour,
    /// where the cache below would otherwise see no reason to do anything.
    /// </param>
    private async Task RefreshWaveformAsync(string path, bool force = false)
    {
        _waveformRender?.Cancel();
        _waveformRender?.Dispose();
        _waveformRender = null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Waveform = null;
            _waveformSource = string.Empty;
            return;
        }

        // Nothing is showing it, so there is nothing to decode a whole audio
        // track for. _waveformSource is left alone rather than cleared: the
        // moment the setting comes back on, this is still "the same file as
        // last time" and OpenSettings asks for a fresh render itself rather
        // than relying on this path noticing the setting changed.
        if (!ShowSeekbarWaveform) return;

        // Same file as last time — the picture on screen is still right. Worth
        // checking because the poll can re-report the same video.
        if (!force
            && string.Equals(path, _waveformSource, StringComparison.OrdinalIgnoreCase)
            && Waveform is not null)
            return;

        Waveform = null;
        var cts = new CancellationTokenSource();
        _waveformRender = cts;

        try
        {
            var png = await _ffmpeg.RenderWaveformAsync(
                path, SeekbarAppearance.ResolveWaveformColor(_settings.Current), ct: cts.Token);
            if (cts.IsCancellationRequested) return;

            // Null is the ordinary answer for a file with no audio, not a
            // failure worth reporting. The bar simply has nothing behind it.
            if (png is null || png.Length == 0)
            {
                _waveformSource = path;
                return;
            }

            var image = new BitmapImage();
            using (var stream = new MemoryStream(png))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }
            // Frozen so it can be handed to the UI without WPF tracking changes
            // to it, and so the stream above can be disposed under it.
            image.Freeze();

            Waveform = image;
            _waveformSource = path;
        }
        catch (OperationCanceledException) { }
        catch
        {
            // Decoration. Nothing here is worth interrupting an edit for.
            Waveform = null;
        }
    }

    /// <summary>
    /// A frame from the loaded video, for the seekbar's hover-scrub preview.
    /// </summary>
    /// <remarks>
    /// Thin wrapper over <see cref="FFmpegService.ExtractFrameAsync"/> rather
    /// than the View reaching for ffmpeg directly — the View owns the popup
    /// and its positioning, which has nothing to do with this service, but it
    /// still has no business holding a reference to it.
    /// </remarks>
    public Task<byte[]?> ExtractHoverFrameAsync(double seconds, CancellationToken ct) =>
        _ffmpeg.ExtractFrameAsync(Session.VideoPath, seconds, height: 90, ct: ct);

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

        // Fire and forget: with no path it only cancels whatever was drawing and
        // clears the picture, so there is nothing to wait for.
        _ = RefreshWaveformAsync(string.Empty);

        StatusText = "No video loaded";
    }

    /// <summary>Whether the player was running on the previous tick.</summary>
    private bool _playerWasRunning;

    private void PollMpc()
    {
        if (IsBusy) return;
        IsMpcRunning = _mpc.IsRunning;

        // The player just appeared. Clear the Web Interface backoff, which by
        // now is certainly engaged: while MPC-HC was closed every probe failed
        // with a refused connection, and a failed probe waits ten seconds
        // before the next — a gap sized for "the interface is switched off",
        // which is the usual reason, and much too long for "the player has
        // only this moment started".
        //
        // It matters because the title cannot cover the gap. MPC-HC's title
        // carries a bare filename, and auto-load refuses an unrooted path it
        // cannot resolve, so until the interface answers there is nothing to
        // load from — a video opened outside this program sat unrecognised for
        // up to ten seconds.
        if (IsMpcRunning && !_playerWasRunning) _mpc.InvalidatePlayerSnapshot();
        _playerWasRunning = IsMpcRunning;

        // Kept current here so the hook never has to look it up. The hook runs
        // ahead of every keystroke on the machine; the poll is already asking
        // about the player anyway, and being a tick out of date costs nothing
        // — a player that has only just closed cannot be the window in front.
        _hotkeys.PlayerWindow = IsMpcRunning ? _mpc.FindMpcWindow() : IntPtr.Zero;

        // Focus moves between processes without notifying us, so the view
        // condition is re-evaluated on every tick. It returns immediately when
        // nothing needs to change.
        ApplyAutoViewSwitch();

        // Only while the overlay is the thing on screen: the hint is the one
        // piece of it that depends on the player rather than on the bookmarks.
        if (_minimalViewActive) RefreshMinimalHint();
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

        // The player's length wins over ffprobe's, rather than only filling in
        // for it.
        //
        // ffprobe reports what the container claims, and a file can claim
        // wrongly: one seen here ends its last video packet at 3:15 while
        // holding 5228 frames, which at its own declared 25fps is 3:29, and
        // MPC-HC plays it as 3:26. No number there is a typo — the timestamps
        // and the frame count disagree with each other, and every tool picks a
        // different one to believe.
        //
        // Which is "right" does not matter here. Every mark in this program is
        // read off MPC-HC's clock, so a timeline that ends before the player's
        // does makes the last stretch of the video impossible to mark and puts
        // every position on the bar in the wrong place. The bar has to measure
        // the same thing the timestamps do.
        //
        // A second of tolerance, because the two agree to the frame on a normal
        // file and this must not rewrite the value on every tick.
        if (durationFromPlayer > 0 &&
            Math.Abs(durationFromPlayer - Session.VideoDurationSeconds) > 1)
        {
            Session.VideoDurationSeconds = durationFromPlayer;
        }

        CurrentTimeDisplay = Bookmark.FormatTime(Session.CurrentTimeSeconds);
        DurationDisplay = Bookmark.FormatTime(Session.VideoDurationSeconds);
        if (Session.VideoDurationSeconds > 0)
            TimelineProgress = Math.Clamp(Session.CurrentTimeSeconds / Session.VideoDurationSeconds, 0, 1);

        // Keep the play/pause button honest. Up here rather than further down
        // because everything below returns early in the ordinary case — the
        // file has not changed — and the button still has to notice the space
        // bar being pressed in MPC-HC's own window.
        var (playing, observedUtc) = _mpc.GetPlaybackState();

        if (playing == MpcHcService.PlaybackState.Unknown)
        {
            InferPlayingFromPosition(now);
        }
        else if (observedUtc > _playStateAssumedUtc)
        {
            SetVideoPlaying(playing == MpcHcService.PlaybackState.Playing);

            // Drop the fallback's baseline while the real answer is available,
            // so that if the interface goes away later it starts measuring from
            // then rather than from a reading of unknown age.
            _lastMovedPosition = -1;
        }

        // The remaining case is an answer older than the last press: the player
        // is being asked about something it had not been told yet. Left alone
        // deliberately — acting on it is what made the button show the new
        // picture, snap back to the old one, and then change again a moment
        // later.

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

        // Always bind this video's place in the bookmarks folder, whether or
        // not the file exists yet. Leaving it empty for a video with no CSV
        // meant SaveBookmarks silently did nothing, so the first session's
        // bookmarks were never written to disk at all. "Loaded" is tracked
        // separately, by whether the file is actually there.
        var csvPath = _bookmarks.GetCsvPathForVideo(path);
        Session.CsvPath = csvPath;
        IsBookmarkFileLoaded = File.Exists(csvPath);
        RefreshBookmarksFileDisplay();
        Session.Bookmarks.Clear();
        if (File.Exists(csvPath))
            foreach (var b in _bookmarks.LoadFromCsv(csvPath))
                Session.Bookmarks.Add(b);

        // The list and the file agree as of now; say so, or the first time this
        // window is activated it re-reads a file nobody has touched and throws
        // away whatever the user had ticked in the meantime.
        RememberBookmarkFile();

        // A different video is a different list. Stepping back past this point
        // would restore cuts belonging to a file that is no longer open, and
        // then write them over the new video's bookmarks on the next save.
        ResetHistory();


        try
        {
            if (File.Exists(path))
            {
                var dur = await _ffmpeg.GetDurationAsync(path);
                if (dur > 0) Session.VideoDurationSeconds = dur;
            }
        }
        catch { }

        // Started, not awaited: it decodes the whole audio track, and the video
        // should be ready to work on long before the picture behind the bar is.
        _ = RefreshWaveformAsync(path);

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
            ? $"Loaded: {Session.VideoFileName}  ({Session.Bookmarks.Count} {Bookmarks(Session.Bookmarks.Count)})"
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
    /// Whether a hotkey confirmation has nowhere to appear except a toast, and
    /// so has to be shown even with toasts turned off.
    /// </summary>
    /// <remarks>
    /// The hotkey is pressed in the player, and normally there are two places
    /// the result shows up: the status bar of the full window, and the overlay's
    /// bookmark list. Both can be off screen at once. The player has focus, so
    /// the full window is behind it; and with <see cref="AutoSwitchViews"/>
    /// turned off the overlay never comes up to take its place. Pressing the
    /// hotkey then produces no visible response at all — the bookmark is
    /// recorded and the user has no way to know it, or which number and time it
    /// got.
    ///
    /// Phrased as "is there any other channel" rather than as a direct read of
    /// the setting, so it also covers the moments when the setting is on but the
    /// overlay is legitimately down — an empty list, or the tick before the poll
    /// notices focus.
    ///
    /// This overrides the user's own "toasts off" preference, which is worth
    /// being uneasy about. It is scoped as tightly as it can be: nothing is
    /// forced while the overlay is up or while this window has focus, because
    /// in both of those cases something on screen already says what happened.
    /// </remarks>
    private bool NeedsHotkeyToast => !_minimalViewActive && _mpc.IsForeground();

    /// <summary>
    /// Reports a hotkey press that was refused, over whatever is on screen.
    /// </summary>
    /// <remarks>
    /// Always forced, unlike the confirmations, and for a reason the general
    /// rule gets wrong. <see cref="NeedsHotkeyToast"/> holds back when the
    /// overlay is up because the overlay already shows what happened — true of
    /// a timestamp that was set, since a row appears, and false of one that was
    /// refused, because the list is exactly as it was. A refusal has no other
    /// channel: the status bar is behind the player, and the overlay has
    /// nothing to show.
    ///
    /// That overrides "toasts off", which is worth being uneasy about. The
    /// setting asks for less noise from a hotkey that worked; it is not a
    /// request for a key that quietly does nothing.
    ///
    /// Held longer than a confirmation. "Timestamp 3 set" only has to be
    /// noticed; "behind the last mark at 4s" has to be read, and two seconds
    /// over a moving picture is not long enough to read a sentence with two
    /// times in it.
    /// </remarks>
    private void ShowRefusal(string title, string detail) =>
        _toast.Show(title, detail, "⚠", force: true,
                    hold: TimeSpan.FromSeconds(Math.Max(4.5, _toast.HoldDuration.TotalSeconds)));

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

        // Behind work already marked. Nothing is added — an opening timestamp
        // that cannot be closed where it stands is not a start, and leaving it
        // in the list as an incomplete row makes the user delete something they
        // never asked for.
        if (WhyCannotOpenAt(timestamp) is { } refusal)
        {
            StatusText = $"Not added — {refusal}. Marks go forwards.";

            // Says the time it refused and the time it wanted, because this is
            // read over a video and "invalid" would send the user to the main
            // window to work out which.
            ShowRefusal("Bookmark not added", refusal + ". Marks go forwards.");
            return;
        }

        // No end time is what makes it open — there is no separate flag to set.
        var nextIndex = Session.Bookmarks.Count + 1;
        var bookmark = new Bookmark { Index = nextIndex, StartSeconds = timestamp, EndSeconds = 0 };
        Session.Bookmarks.Add(bookmark);

        // Persist immediately rather than waiting for the closing timestamp.
        // This is what creates the CSV on the very first press, so the opening
        // timestamp survives a crash or a switch away from the video — and it
        // is the state "Undo last bookmark" deletes the file from.
        SaveBookmarks();

        StatusText = $"Bookmark {nextIndex} set at {Bookmark.FormatTime(timestamp)}  (press again to close)";
        _toast.Show($"Bookmark {nextIndex} set",
                    $"{Bookmark.FormatTime(timestamp)} — press again to close",
                    force: NeedsHotkeyToast);
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

        // A close at or before the open is not a cut, and it is not fudged
        // forward by a second either — that quietly produced a bogus one-second
        // pair. The bookmark is left open and the press is refused.
        //
        // It used to be thrown away, opening timestamp and all, on the reasoning
        // that the pair was what was wrong. It is not: the opening time is still
        // exactly where it was wanted, and the only thing wrong is where the
        // player happens to be now. Discarding meant a mis-timed second press
        // destroyed a good timestamp and left nothing to correct — the same
        // reasoning the overlap check below already followed.
        if (closing <= incomplete.StartSeconds)
        {
            StatusText = $"Cannot close bookmark {incomplete.Index} at " +
                         $"{Bookmark.FormatTime(closing)} — that is " +
                         (Math.Abs(closing - incomplete.StartSeconds) < 0.001 ? "its opening time" : "before its opening time") +
                         $" of {incomplete.StartDisplay}. Seek past it and press again.";

            ShowRefusal($"Bookmark {incomplete.Index} still open",
                        $"A closing time has to come after {incomplete.StartDisplay}");
            return;
        }

        // Closing here would reach over a cut that is already there. Unlike a
        // close that lands before its own opening, the opening timestamp is
        // still perfectly good — it is only this closing time that will not do
        // — so the bookmark is left open to be closed somewhere else, rather
        // than thrown away along with it.
        if (OverlappingCut(incomplete.StartSeconds, closing, incomplete) is { } clash)
        {
            StatusText = $"Closing at {Bookmark.FormatTime(closing)} would run over bookmark " +
                         $"{clash.Index} ({clash.StartDisplay} – {clash.EndDisplay}) — " +
                         $"bookmark {incomplete.Index} is still open";
            ShowRefusal($"Would overlap bookmark {clash.Index}",
                        $"Bookmark {clash.Index} is {clash.StartDisplay} – {clash.EndDisplay}. " +
                        $"Bookmark {incomplete.Index} is still open — close it before {clash.StartDisplay}");
            return;
        }

        // Setting the end time is what closes it; there is nothing else to say.
        incomplete.EndSeconds = closing;
        SaveBookmarks();
        Session.NotifyDurationChanged();
        StatusText = $"Bookmark {incomplete.Index} closed ({incomplete.DurationDisplay})";
        _toast.Show($"Bookmark {incomplete.Index} closed",
                    $"{incomplete.StartDisplay} → {incomplete.EndDisplay}  ({incomplete.DurationDisplay})",
                    "✅",
                    force: NeedsHotkeyToast);
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

        // Nothing left to store, so the file goes with it rather than
        // lingering empty and still counting as "loaded" — the same rule
        // UndoLastBookmark has always applied to the last timestamp, which
        // this path never picked up. Deleting the only pair left a CSV behind
        // that said a file was loaded and had nothing in it.
        //
        // SaveBookmarks cannot do this on its own: it is also called by
        // ResetEverything on the way out, with the list already cleared, and
        // deleting there would take the file every time the session ended.
        if (Session.Bookmarks.Count == 0)
        {
            Renumber();

            if (!TryDeleteBookmarkFile(out var error))
            {
                Session.NotifyDurationChanged();
                ReportFailure("Deleted the bookmarks but could not delete the file: " + error);
                return;
            }

            Session.NotifyDurationChanged();
            StatusText = toDelete.Count == 1
                ? $"Deleted bookmark {toDelete[0].Index} — bookmark file deleted"
                : $"Deleted {toDelete.Count} bookmarks — bookmark file deleted";
            return;
        }

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
        var selected = ModifierTargets();
        if (selected.Count == 0 && NothingToModify()) return;

        // Clear only when every selected cut is already flipped, so a mixed
        // selection turns them all on rather than toggling them out of sync.
        var turningOn = !selected.All(b => b.IsFlipped);
        foreach (var b in selected)
            b.IsFlipped = turningOn;

        Session.NotifyDurationChanged();
        StatusText = turningOn
            ? $"{selected.Count} bookmark(s) marked for inversion"
            : $"{selected.Count} bookmark(s) restored to normal";
    }

    /// <summary>
    /// Deletes the session's CSV and marks the bookmark file as no longer
    /// loaded. <see cref="EditSession.CsvPath"/> is deliberately kept: it is
    /// this video's place in the bookmarks folder, so a later timestamp
    /// recreates the file where the rest of the collection is.
    /// </summary>
    /// <remarks>
    /// Goes to the Recycle Bin rather than being unlinked. A bookmark file is
    /// a hand-built list of timestamps that can represent a lot of watching,
    /// and it is small — there is no case for making it unrecoverable.
    ///
    /// The %APPDATA% copy goes with it, and only once the real one is gone: a
    /// mirror still holding a file the program folder no longer has would have
    /// the next restore bring it back.
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

            BookmarkService.MirrorDelete(csvPath);
        }

        IsBookmarkFileLoaded = false;
        RefreshBookmarksFileDisplay();
        return true;
    }

    /// <summary>
    /// Checks every bookmark, including any still awaiting a closing
    /// timestamp.
    /// </summary>
    /// <remarks>
    /// It used to skip incomplete ones, because they could not be checked at
    /// all. Now that they can, skipping them would leave "Select all" visibly
    /// not selecting all — one row staying clear with no way to tell why.
    /// The cut and merge actions read past the check to <see
    /// cref="Bookmark.IsValid"/>, so nothing acts on the open row regardless.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void SelectAll()
    {
        foreach (var b in Session.Bookmarks)
            b.IsSelected = true;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNone))]
    private void SelectNone() { foreach (var b in Session.Bookmarks) b.IsSelected = false; }

    /// <summary>
    /// Backing state for <see cref="SelectAllButtonLabel"/>: true while the
    /// toolbar's one select button is offering to clear the selection.
    /// </summary>
    private bool _selectionButtonClears;

    /// <summary>
    /// The caption on the toolbar's select button — "Select All" or
    /// "Select None".
    /// </summary>
    /// <remarks>
    /// It replaced a pair of buttons, one of which was always the wrong one to
    /// want. Which way it points is decided in
    /// <see cref="RefreshSelectionButton"/>, not here.
    /// </remarks>
    public string SelectAllButtonLabel => _selectionButtonClears ? "Select None" : "Select All";

    /// <summary>
    /// The same caption for the Bookmarks menu's one selection entry, in the
    /// sentence case the rest of the menu is written in.
    /// </summary>
    /// <remarks>
    /// The menu carried a permanent pair — "Select all" and "Select none" —
    /// one of which was always the wrong one to want, and which the toolbar
    /// had already stopped doing. Both now point at the same command and
    /// swap by the same rule, so the menu and the button never disagree
    /// about which half of the pair is worth offering.
    /// </remarks>
    public string SelectAllMenuHeader => _selectionButtonClears ? "Select none" : "Select all";

    /// <summary>The glyph beside it, following the caption.</summary>
    public string SelectAllMenuGlyph => _selectionButtonClears ? "☐" : "☑";

    /// <summary>
    /// The state of the box at the head of the cut list: checked when every
    /// cut is, clear when none is, and indeterminate on anything between.
    /// </summary>
    /// <remarks>
    /// A checkbox, unlike the menu entry and the toolbar button beside it,
    /// reports rather than offers — so this one does follow a partial
    /// selection instead of holding the last answer. Filled in one state and
    /// empty in the other is what a checkbox at the head of a list of
    /// checkboxes is read as meaning, and leaving it checked over a half-
    /// ticked list would be a plain untruth.
    ///
    /// Setting it either fills or empties the list; the indeterminate state
    /// is only ever shown, never chosen, which is why the box itself is not
    /// three-state.
    /// </remarks>
    public bool? AllBookmarksSelected
    {
        get
        {
            var total = Session.Bookmarks.Count;
            if (total == 0) return false;

            var selected = SelectedCount;
            if (selected == 0) return false;
            return selected == total ? true : null;
        }
        set
        {
            if (value == true) SelectAll();
            else SelectNone();
        }
    }

    /// <summary>
    /// Whether that box is shown at all.
    /// </summary>
    /// <remarks>
    /// Not over an empty list, where it would offer to select nothing, and
    /// not over the storyboard, where it lines up with nothing — the cards
    /// carry their own boxes wherever the grid happens to put them, and a
    /// heading box aligned to a column that is not there reads as a stray
    /// control. The menu entry still covers both cases.
    /// </remarks>
    public bool ShowSelectAllHeader => !HasNoBookmarks && !StoryboardView;

    /// <summary>
    /// Points the select button at whichever action is worth offering.
    /// </summary>
    /// <remarks>
    /// The caption moves only at the two ends — everything checked, or nothing
    /// checked — and holds its ground in between. Flipping it the instant a
    /// selection stopped being complete would mean unticking one row of twenty
    /// took the "clear them all" action off the toolbar, which is exactly the
    /// moment it is wanted. So: filling the list offers to clear it, emptying
    /// the list offers to fill it, and a partial selection leaves the last
    /// answer standing.
    /// </remarks>
    private void RefreshSelectionButton()
    {
        var total = Session.Bookmarks.Count;
        var selected = SelectedCount;

        bool clears;
        if (total > 0 && selected == total) clears = true;
        else if (selected == 0) clears = false;
        else return;                      // Partial — leave it as it was.

        if (clears == _selectionButtonClears) return;
        _selectionButtonClears = clears;
        OnPropertyChanged(nameof(SelectAllButtonLabel));
        OnPropertyChanged(nameof(SelectAllMenuHeader));
        OnPropertyChanged(nameof(SelectAllMenuGlyph));

        // The toolbar is data, so this caption has to be written into the item
        // rather than picked up from a binding on a button that no longer
        // exists in the markup.
        if (ToolbarItems.FirstOrDefault(i => i.Key == "select-all") is { } button)
        {
            button.Label = SelectAllButtonLabel;
            button.Icon = _selectionButtonClears ? "☐" : "☑";
        }
    }

    /// <summary>
    /// The toolbar's select button: fills the selection, or clears it,
    /// according to what it is currently offering.
    /// </summary>
    /// <remarks>
    /// The caption is not set here. Either branch lands the selection on one of
    /// the two ends, and the resulting IsSelected changes run
    /// <see cref="RefreshSelectionButton"/> through the usual plumbing — so the
    /// button reads the same whether it was pressed or the boxes were ticked by
    /// hand, which is the whole point of deriving it from the count.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void ToggleSelectAll()
    {
        if (_selectionButtonClears) SelectNone();
        else SelectAll();
    }

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

            // Clearing the end time is the whole of reopening it. The row keeps
            // its check on purpose — see Bookmark.AnnounceOpenState; the check
            // is the user's mark on a row, not a claim that the row can be cut,
            // and taking a closing timestamp back to move it should not quietly
            // undo the selection as well.
            last.EndSeconds = 0;

            SaveBookmarks();
            Session.NotifyDurationChanged();
            StatusText = $"Removed the closing mark {removedEnd} — bookmark {last.Index} is open again";
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
                StatusText = "Undid the bookmark but could not delete the file: " + error;
                return;
            }

            Session.NotifyDurationChanged();
            StatusText = $"Removed bookmark {removedStart} — bookmark file deleted";
            return;
        }

        Renumber();
        SaveBookmarks();
        Session.NotifyDurationChanged();
        StatusText = $"Removed the opening mark {removedStart} (bookmark {removedIndex})";
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
    /// Starting a new Play all / Play selected cancels any in-progress one;
    /// nothing else ends the loop short of shutdown.
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
            StatusText = "No valid bookmarks to play — complete at least one bookmark first.";
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

    /// <summary>
    /// Pauses the player, or sets it going again if it is already paused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A transport control: it acts on whatever MPC-HC has open, which is what
    /// the seekbar here is reporting. It never starts a cut playback — that is
    /// the Bookmarks menu's job — but it does hold one that is already running,
    /// because a sequence is only the player plus a clock and pausing one
    /// without the other is what made the picture freeze while the cuts carried
    /// on. See <see cref="PlayCutsAsync(List{Bookmark}, string)"/>.
    /// </para>
    /// <para>
    /// One button for both halves, showing whichever the player is <em>not</em>
    /// doing: a pause sign while it plays, a play sign while it does not. That
    /// is the convention every transport control follows, and it is only
    /// honest if the picture tracks the player rather than the last press —
    /// pausing from MPC-HC's own window has to turn this back into a play
    /// sign. See <see cref="RefreshPlayPauseButton"/>.
    /// </para>
    /// <para>
    /// Explicit play and pause rather than the player's own toggle, because the
    /// button has already decided which it means from the state it is showing.
    /// Sending a toggle would act on whatever the player's state had become in
    /// the meantime, which is how a button that reads "pause" ends up starting
    /// playback.
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanPause))]
    private void TogglePause()
    {
        var wasPlaying = IsVideoPlaying;

        if (wasPlaying) _mpc.Pause();
        else _mpc.Play();

        AssumePlaying(!wasPlaying);
    }

    /// <summary>
    /// Records that the player has just been told to play or to stop, without
    /// waiting to be told back.
    /// </summary>
    /// <remarks>
    /// Three things at once, because they are one thought. The button shows the
    /// new state immediately rather than a tick late. Anything the player said
    /// before this moment is now out of date, so later readings are checked
    /// against the time of this call and ignored if they predate it — without
    /// which the next tick answered with pre-press data and the button showed
    /// the new picture, snapped back to the old one, then changed again. And
    /// the next probe is brought forward, so the confirming answer lands on the
    /// following tick instead of up to a second later.
    ///
    /// The position fallback's baseline goes too: its last sample is older than
    /// this instruction, and movement from before a pause would otherwise read
    /// as the video still running.
    /// </remarks>
    private void AssumePlaying(bool playing)
    {
        SetVideoPlaying(playing);

        _playStateAssumedUtc = DateTime.UtcNow;
        _mpc.InvalidatePlayerSnapshot();
        _lastMovedPosition = -1;
    }

    /// <summary>There is nothing to pause without a player with a file in it.</summary>
    private bool CanPause() => HasActiveVideo;

    /// <summary>
    /// True while MPC-HC is playing. Drives the play/pause button's picture.
    /// </summary>
    [ObservableProperty] private bool _isVideoPlaying;

    /// <summary>
    /// Points the play/pause button at whichever of the two it should offer.
    /// </summary>
    /// <remarks>
    /// Swapping the item's own picture and label, the way the Select all button
    /// turns into Select none — the toolbar binds to those, so there is nothing
    /// else to tell.
    /// </remarks>
    private void RefreshPlayPauseButton()
    {
        foreach (var item in ToolbarItems)
        {
            if (item.Key != "pause") continue;

            item.IconFile = IsVideoPlaying ? "icon-pause" : "icon-play";
            item.Icon = IsVideoPlaying ? "⏸" : "▶";
            item.Label = IsVideoPlaying ? "⏸ Pause" : "▶ Play";
        }
    }

    /// <summary>Records the play state, repainting the button when it moves.</summary>
    private void SetVideoPlaying(bool playing)
    {
        if (playing == IsVideoPlaying) return;

        IsVideoPlaying = playing;
        RefreshPlayPauseButton();
    }

    /// <summary>
    /// Works out whether the player is running from whether its position is
    /// moving. Used only when the Web Interface cannot say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A position that has moved recently means playing; one that has stood
    /// still for <see cref="PositionIdleMs"/> means it is not. The position is
    /// already being read on every tick for the timeline, so this asks the
    /// player nothing it was not being asked anyway — which matters, because
    /// the reason the Web Interface is not answering is usually that it is
    /// switched off, and nothing else here can reach it.
    /// </para>
    /// <para>
    /// Measured from the last time the value <em>changed</em> rather than
    /// across a fixed window. Sampling two readings a fixed distance apart
    /// calls a playing video paused whenever both happen to land inside the
    /// same whole second, which is most pairs of samples at a 600ms poll.
    /// </para>
    /// </remarks>
    private void InferPlayingFromPosition(DateTime now)
    {
        var current = _cachedPlaybackPosition.Current;

        // First reading since the interface went quiet: start the clock, and
        // leave the button showing whatever it already was.
        if (_lastMovedPosition < 0)
        {
            _lastMovedPosition = current;
            _positionLastMovedUtc = now;
            return;
        }

        if (Math.Abs(current - _lastMovedPosition) > 0.001)
        {
            _lastMovedPosition = current;
            _positionLastMovedUtc = now;
            SetVideoPlaying(true);
            return;
        }

        if ((now - _positionLastMovedUtc).TotalMilliseconds >= PositionIdleMs)
            SetVideoPlaying(false);
    }

    [RelayCommand(CanExecute = nameof(CanPlaySelected))]
    private async Task PlaySelectedAsync()
    {
        var cuts = Session.Bookmarks.Where(b => b.IsSelected && b.IsValid)
                                    .OrderBy(b => b.StartSeconds)
                                    .ToList();
        if (cuts.Count == 0)
        {
            StatusText = "No valid selected bookmarks — check at least one bookmark first.";
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
    /// <remarks>
    /// Pausing the player holds the sequence: each cut is timed against a clock
    /// that stops with the picture and picks up where it left off, so the rest
    /// of a cut is still the rest of that cut. Without it the wait ran out
    /// behind a frozen frame and the sequence seeked on through the list
    /// against a video that was not moving.
    ///
    /// Left paused for <see cref="PausedSequenceGiveUpMs"/> it gives up rather
    /// than waiting all session, since a running sequence holds off the poll's
    /// auto-load.
    /// </remarks>
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

        // Set when a cut is left paused long enough that the sequence gives up
        // on being resumed. Checked after the inner wait so the for loop ends
        // too, not just the one cut.
        var abandoned = false;

        // The player is deliberately left where it is. Playing the cuts used to
        // pull MPC-HC to the front, on the reasoning that you are about to want
        // to look at it — but it takes the focus away from the editor you
        // pressed the button in, and from whatever you were typing in it. The
        // player keeps playing behind whatever is in front; bringing it forward
        // is a thing to do by hand, when it is wanted.
        try
        {
            for (int i = 0; i < cuts.Count; i++)
            {
                if (token.IsCancellationRequested) break;

                var b = cuts[i];
                // Effective play time = real duration divided by speed,
                // so a 2x bookmark plays for half as long, etc.
                var playSeconds = b.DurationSeconds / Math.Max(0.25, b.Speed);

                StatusText = $"Playing {label} bookmark {i + 1}/{cuts.Count}: " +
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

                    // Re-asserted once, which covers a seek that paused late.
                    // Safe because CMD_PLAY really is an explicit play now:
                    // sent to a player already playing it changes nothing. It
                    // used to be the play/pause toggle, so this second call was
                    // what left every cut sitting paused on its first frame.
                    await Task.Delay(200, token);
                    _mpc.Play();

                    // Say so, rather than leaving the play/pause button to work
                    // it out: it would show a play sign for the first tick of
                    // every cut, and the wait below would read that as the user
                    // having paused and hold against a video that is running.
                    AssumePlaying(true);

                    // Time the cut against the wall clock. The old loop added a
                    // nominal 100ms per iteration while each actually took ~115,
                    // so it under-counted and every cut ran ~15% long — the
                    // "played past the end" symptom.
                    var clock = Stopwatch.StartNew();
                    var target = TimeSpan.FromSeconds(playSeconds);
                    DateTime? pausedAt = null;

                    while (clock.Elapsed < target)
                    {
                        if (token.IsCancellationRequested) break;

                        // Paused, by this program's button or by the player's
                        // own keyboard. The cut is not advancing, so its clock
                        // must not either — otherwise the wait runs out behind a
                        // frozen picture and the sequence seeks on to the next
                        // cut while the video sits still.
                        if (!IsVideoPlaying)
                        {
                            if (clock.IsRunning)
                            {
                                clock.Stop();          // keeps what it has counted
                                pausedAt = DateTime.UtcNow;
                                StatusText = $"Paused — bookmark {i + 1}/{cuts.Count}";
                            }

                            // Not forever, though. IsPlayingCuts holds off the
                            // poll's auto-load, so a sequence left paused and
                            // walked away from would stop the program noticing
                            // a different video being opened.
                            if (pausedAt is { } since &&
                                (DateTime.UtcNow - since).TotalMilliseconds >= PausedSequenceGiveUpMs)
                            {
                                abandoned = true;
                                break;
                            }

                            await Task.Delay(100, token);
                            continue;
                        }

                        // Playing again: pick the clock up where it stopped, so
                        // the rest of the cut is the rest of the cut.
                        if (!clock.IsRunning)
                        {
                            clock.Start();
                            pausedAt = null;
                            StatusText = $"Playing {label} bookmark {i + 1}/{cuts.Count}";
                        }

                        var remaining = target - clock.Elapsed;
                        await Task.Delay(remaining > TimeSpan.FromMilliseconds(100)
                            ? TimeSpan.FromMilliseconds(100)
                            : remaining, token);
                    }

                    if (abandoned) break;
                }
                catch (TaskCanceledException) { break; }
            }

            if (token.IsCancellationRequested)
            {
                StatusText = "Playback stopped.";
            }
            else if (abandoned)
            {
                // Already paused — the player is where the user left it, and
                // pausing it again would say nothing.
                StatusText = "Playback stopped — left paused too long.";
            }
            else
            {
                try { _mpc.Pause(); } catch { }
                AssumePlaying(false);
                StatusText = $"Finished playing {cuts.Count} bookmark(s).";
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
            "settings, recent videos, tags, and shortcuts are all " +
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
        // Resolved against .txt rather than against the file's own extension.
        // Everything routed through here is plain text whatever it is called,
        // and asking about the real extension found the wrong answer every
        // time: .pls is registered to a media player and .csv to a
        // spreadsheet, neither of which is in the safe list, so both fell
        // through to the "Open with" picker — which on current Windows often
        // does nothing at all, and "View playlist content" looked broken.
        //
        // Asking about .txt still honours a chosen editor, since somebody who
        // has put Notepad++ or VS Code on their text files has said which one
        // they want. Notepad is the floor, and it is always there.
        var editor = ResolveTextEditor(".txt") ?? "notepad.exe";
        try
        {
            Process.Start(new ProcessStartInfo(editor, $"\"{path}\"") { UseShellExecute = true });
            StatusText = $"Opened {Path.GetFileName(path)}{suffix}";
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

    /// <summary>
    /// Records the bookmark file's write time as one this session already knows
    /// about, so the next activation does not mistake it for an outside edit.
    /// </summary>
    /// <remarks>
    /// <see cref="ReloadBookmarksFromDisk"/> spots a hand-edit by comparing the
    /// file's timestamp against this, so anything that leaves the list and the
    /// file already agreeing — a save, or a load — has to say so here.
    ///
    /// Loading did not, which cost more than a stray message: the reload
    /// rebuilds every <see cref="Bookmark"/>, and a check lives on the object
    /// rather than in the file. Tick four cuts, look at the player, come back,
    /// and the reload had quietly handed you four fresh unticked ones.
    /// </remarks>
    private void RememberBookmarkFile()
    {
        try
        {
            _lastBookmarkWriteUtc =
                !string.IsNullOrEmpty(Session.CsvPath) && File.Exists(Session.CsvPath)
                    ? File.GetLastWriteTimeUtc(Session.CsvPath)
                    : default;
        }
        catch
        {
            // An unreadable stamp costs one redundant reload, nothing worse.
        }
    }

    // ------------------------------------------------------------------
    // Output filename collisions
    // ------------------------------------------------------------------

    /// <summary>
    /// The window a modal dialog should belong to.
    /// </summary>
    /// <remarks>
    /// Owning one matters more than it looks. A dialog shown with no owner is
    /// still modal — <c>ShowDialog</c> disables the rest of the application —
    /// but Windows has no relationship to enforce, so nothing keeps it in front
    /// of the window it disabled. Clicking the taskbar button then raises a
    /// window that cannot be typed into, with the dialog that disabled it
    /// hidden behind: an application that looks hung and is not.
    /// </remarks>
    private static Window? DialogOwner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;

    // ------------------------------------------------------------------
    // How the status bar is feeling
    // ------------------------------------------------------------------

    /// <summary>
    /// Whether the last thing reported went well, badly, or is neither.
    /// </summary>
    /// <remarks>
    /// Drives a brief colour over the status strip. Deliberately not derived
    /// from the text: the bar is written to from something like a hundred
    /// places, and deciding what a message means by reading it would be a pile
    /// of string matching that is wrong the first time anybody rewords a
    /// sentence. Set where the outcome is actually known instead.
    /// </remarks>
    [ObservableProperty] private StatusLevel _statusLevel = StatusLevel.Normal;

    /// <summary>
    /// Returns the strip to its ordinary colour, however the flash started.
    /// </summary>
    /// <remarks>
    /// Needed even though the animation fades itself out. The view reacts to
    /// the level <em>changing</em>, so one that stayed on Ok would leave the
    /// next success with nothing to notice — a second flash has to be a second
    /// transition, and that means going back to Normal in between.
    /// </remarks>
    private DispatcherTimer? _statusFlashTimer;

    /// <summary>
    /// Colours the status strip for a moment, then lets it settle back.
    /// </summary>
    private void FlashStatus(StatusLevel level)
    {
        _statusFlashTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusFlashTimer.Stop();

        // Back to Normal first. Two successes in a row are two events, and
        // without the gap the second is a write of the value already there,
        // which raises nothing and flashes nothing.
        StatusLevel = StatusLevel.Normal;
        StatusLevel = level;

        _statusFlashTimer.Tick -= StatusFlashElapsed;
        _statusFlashTimer.Tick += StatusFlashElapsed;
        _statusFlashTimer.Start();
    }

    private void StatusFlashElapsed(object? sender, EventArgs e)
    {
        _statusFlashTimer?.Stop();
        StatusLevel = StatusLevel.Normal;
    }

    /// <summary>
    /// Puts the step marker on one bookmark and takes it off the rest.
    /// </summary>
    /// <remarks>
    /// The whole list every time rather than clearing the last one by
    /// reference. It is a handful of rows, and it is the only way to be sure
    /// exactly one is marked — a run that threw halfway through would
    /// otherwise leave its marker behind on a row nothing is working on.
    ///
    /// Null clears the lot, which is what the end of an operation wants.
    /// </remarks>
    private void MarkStep(Bookmark? current)
    {
        foreach (var b in Session.Bookmarks)
            b.IsCurrentStep = ReferenceEquals(b, current);
    }

    /// <summary>Takes the step marker off every row.</summary>
    private void ClearStep() => MarkStep(null);

    /// <summary>
    /// Announces a finished operation over whatever is in front.
    /// </summary>
    /// <remarks>
    /// Built from the job panel's own words, so it says what the panel says
    /// rather than keeping a second set of wordings in step with it.
    ///
    /// Through ShowUnlessRecent: a few operations have something better to say
    /// about their own ending — a warning about the output, a clip's number
    /// and name — and say it just before completing. Theirs wins.
    /// </remarks>
    private void ToastJobFinished()
    {
        var headline = Job.Action;
        if (string.IsNullOrWhiteSpace(headline)) return;

        // CurrentFile is a path for most operations and a folder for the ones
        // that write several. Either way the last part of it is the useful
        // half, and the full thing would wrap the toast to three lines.
        var detail = string.IsNullOrWhiteSpace(Job.CurrentFile)
            ? null
            : Path.GetFileName(Job.CurrentFile.TrimEnd(Path.DirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(detail)) detail = Job.Summary;

        _toast.ShowUnlessRecent(headline, detail, "✅");
    }

    /// <summary>Says something went well, in the bar and on it.</summary>
    private void ReportSuccess(string text)
    {
        StatusText = text;
        FlashStatus(StatusLevel.Ok);
    }

    /// <summary>Says something did not, on the same terms.</summary>
    private void ReportFailure(string text)
    {
        StatusText = text;
        FlashStatus(StatusLevel.Failed);
    }

    /// <summary>
    /// Reports a failure in a box that looks and sounds like one.
    /// </summary>
    /// <remarks>
    /// The icon is not decoration. MessageBox plays the system sound belonging
    /// to whichever icon it is given, and one raised without an icon is silent
    /// — so every failure in this program was announced by an untitled box with
    /// no sound, which is the shape of a notice rather than of something having
    /// gone wrong. Saying it is an error is what makes Windows say so too.
    ///
    /// Owned, like the other dialogs here: an unowned box can end up behind the
    /// window it disabled. See <see cref="DialogOwner"/>.
    ///
    /// An instance method rather than a static one so it can colour the status
    /// strip on the way past. Every failure in the program that raises a dialog
    /// raises it through here, which makes this the one place a red flash can
    /// be put without eight catch blocks each having to remember it.
    /// </remarks>
    private void ShowError(string message, string title)
    {
        FlashStatus(StatusLevel.Failed);

        var owner = DialogOwner;

        if (owner is not null)
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        else
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// Reports an outcome that is not a failure, on the same terms.
    /// </summary>
    /// <remarks>
    /// For the end of an action the user asked for and then stopped watching.
    /// The status bar is where this program says what just happened, and it is
    /// the right place for anything that follows from what is on screen — but
    /// a line there is easy to miss when the answer is the whole reason the
    /// menu item was clicked, and the window may not even be the one in front
    /// by the time it arrives.
    /// </remarks>
    private static void ShowInfo(string message, string title)
    {
        var owner = DialogOwner;

        if (owner is not null)
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Returns a path that does not exist yet, asking the user what to do
    /// each time the candidate is taken. Returns null if they cancel.
    /// </summary>
    /// <remarks>
    /// Loops rather than asking once: a rename or an increment can collide
    /// too, and the user has to be re-asked until the name is actually free.
    /// </remarks>
    /// <param name="enforcePolicy">
    /// Run the name through <see cref="EnforceFileNamePolicy"/> first. Pass
    /// false when the caller has already done that for this file — an operation
    /// writing two outputs from one source shares a stem, and the rename prompt
    /// must not be shown twice for the same name.
    /// </param>
    private Task<string?> ResolveOutputPathAsync(string candidate, bool enforcePolicy = true)
    {
        // Policy first: a name we are about to write must satisfy
        // FileNameRules, whatever the source file happens to be called.
        if (enforcePolicy)
        {
            candidate = EnforceFileNamePolicy(candidate, out var canceled);
            if (canceled) return Task.FromResult<string?>(null);
        }

        while (File.Exists(candidate))
        {
            // Already told how to handle the rest of the batch.
            if (_conflictAllChoice == ConflictResult.Overwrite)
                return Task.FromResult<string?>(candidate);

            if (_conflictAllChoice == ConflictResult.Increment)
            {
                candidate = NextFreeName(candidate);
                continue;
            }

            var dlg = new ConflictDialog(Path.GetFileName(candidate),
                                         Path.GetFileName(NextFreeName(candidate)),
                                         Path.GetDirectoryName(candidate),
                                         offerApplyToAll: _batchRemaining > 1)
            { Owner = DialogOwner };

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
                    candidate = NextFreeName(candidate);
                    break;

                case ConflictResult.Rename when !string.IsNullOrWhiteSpace(dlg.NewName):
                    // Only the base name changes; the bracket suffix stays.
                    var dir = Path.GetDirectoryName(candidate) ?? "";
                    var ext = Path.GetExtension(candidate);
                    var (_, bracket) = FileNameRules.SplitSuffix(
                        Path.GetFileNameWithoutExtension(candidate));

                    candidate = Path.Combine(dir, dlg.NewName + bracket + ext);

                    // A hand-typed replacement has to satisfy the policy too.
                    candidate = EnforceFileNamePolicy(candidate, out var renameCanceled);
                    if (renameCanceled) return Task.FromResult<string?>(null);
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
    /// <param name="canceled">True if the user backed out.</param>
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
    /// behavior and this is already the one place that decides it. A setting
    /// of Ask leaves it null, which is exactly the old behavior.
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

    private string EnforceFileNamePolicy(string candidate, out bool canceled)
    {
        canceled = false;

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
                Path.GetDirectoryName(candidate),
                offerApplyToAll: _batchRemaining > 1)
            { Owner = DialogOwner };

            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.NewStem))
            {
                canceled = true;
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
            $"{Path.GetFileNameWithoutExtension(basePath)}{SuffixBracket()}{extension}");
    }

    /// <summary>
    /// The bracket an output name carries: <c>[done]</c> for the first of
    /// something, <c>[done2]</c> for the second.
    /// </summary>
    /// <param name="counter">
    /// 1-based. One means the first and takes no number, which is the rule every
    /// caller already followed by hand.
    /// </param>
    /// <remarks>
    /// One place for the format, because four callers were spelling it out and
    /// the no-tag case had to reach all of them. With **None** selected under
    /// Options the first of something carries no bracket at all — the whole point
    /// of choosing None — and the rest carry a bare number, since two files in a
    /// folder still cannot share a name.
    /// </remarks>
    private string SuffixBracket(int counter = 1)
    {
        var suffix = _settings.GetActiveSuffixText();

        if (suffix.Length == 0)
            return counter <= 1 ? string.Empty : $"[{counter}]";

        return counter <= 1 ? $"[{suffix}]" : $"[{suffix}{counter}]";
    }

    /// <summary>
    /// Bumps the number inside the trailing bracket suffix:
    /// <c>[done]</c> → <c>[done2]</c>, <c>[done2]</c> → <c>[done3]</c>,
    /// <c>[cs3]</c> → <c>[cs4]</c>. A suffix with no number starts at 2.
    /// Names with no bracket at all get one appended from the active tag.
    /// </summary>
    /// <summary>
    /// The first name Increment can actually use: <see cref="IncrementSuffix"/>
    /// applied until it lands on a free one.
    /// </summary>
    /// <remarks>
    /// One bump is not an offer worth making. Splitting twice into the same
    /// folder leaves [done], [done2] and [done3] sitting there, so a single
    /// step proposed a name that was also taken — the dialog said "Increment
    /// would save as: …[done2].mp4" about a file plainly on disk, then asked
    /// again, once per name already used. The preview and the button now agree,
    /// and one click gets past the whole run of them.
    /// </remarks>
    private string NextFreeName(string path)
    {
        var candidate = IncrementSuffix(path);

        // Bounded. A folder holding a thousand of these is pathological, and
        // spinning here forever would be a worse answer than handing back a
        // taken name for the caller to ask about.
        for (var guard = 0; File.Exists(candidate) && guard < 1000; guard++)
            candidate = IncrementSuffix(candidate);

        return candidate;
    }

    private string IncrementSuffix(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var ext = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(path);

        var open = stem.LastIndexOf('[');
        if (open < 0 || !stem.EndsWith(']'))
            return Path.Combine(dir, $"{stem}{SuffixBracket(2)}{ext}");

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
    /// Handles what was dropped on the window.
    /// </summary>
    /// <remarks>
    /// Pictures are the one kind that arrives in handfuls and the one kind
    /// there is nothing to open: they are not something the editor loads, so a
    /// drop asks the only question anybody has about a picture — what is in it
    /// — and the whole selection goes into one window rather than the first
    /// file going into one and the rest being dropped on the floor.
    ///
    /// Everything else keeps to one file, because loading a video or a
    /// bookmark set is a thing the program can only be doing once.
    /// </remarks>
    [RelayCommand]
    private async Task OpenDroppedFiles(string[]? paths)
    {
        var real = paths?.Where(File.Exists).ToList() ?? new List<string>();

        switch (Classify(real))
        {
            case DropKind.Nothing:
                StatusText = "Dropped file not found.";
                return;

            case DropKind.Pictures:
                await ShowImageFacts(real);
                return;

            default:
                await OpenDroppedFile(real[0]);
                return;
        }
    }

    /// <summary>What a drop turned out to be.</summary>
    internal enum DropKind
    {
        /// <summary>Nothing that exists.</summary>
        Nothing,

        /// <summary>Pictures, all of them.</summary>
        Pictures,

        /// <summary>Something the editor loads: a video, a cut list, a playlist.</summary>
        Something
    }

    /// <summary>
    /// What to do with what was dropped.
    /// </summary>
    /// <remarks>
    /// All of them, not any of them. A video dropped alongside a picture is a
    /// request to open the video; the picture came along for the ride, and
    /// asking what is inside it instead would be answering a question nobody
    /// asked while ignoring the one they did.
    /// </remarks>
    internal static DropKind Classify(IReadOnlyList<string> existing) =>
        existing.Count == 0 ? DropKind.Nothing
        : existing.All(IsImageFile) ? DropKind.Pictures
        : DropKind.Something;

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
                // A picture never goes to the player. MPC-HC will display one,
                // which is not what anybody dropping a PNG on a video editor
                // wants, and it would replace whatever was loaded to do it.
                // Reachable when a picture arrives among other files, since
                // this is the one path that takes a single one of them.
                if (IsImageFile(path))
                {
                    await ShowImageFacts(new[] { path });
                    break;
                }

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

        var fileCount = ofd.FileNames.Length;
        var outName = Path.GetFileName(outPath);

        IsBusy = true; ProgressPercent = 0; StatusText = "Merging…";
        Job.Begin("Merging files", fileCount);
        Job.SetFile(0, Path.GetFileName(ofd.FileNames[0]));
        var completed = false;
        try
        {
            var progress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                ProgressPercent = p.Percent;
                StatusText = p.Message;
                // Current is the 0-based index of the file being prepared,
                // which is also the number already finished — exactly what the
                // counter wants. On the final join it reaches files.Count, so
                // the counter lands on "5/5" there and not a step earlier.
                //
                // This used to be Current + 1 clamped to fileCount, which put
                // "5/5" on screen while the fifth file was still being prepared
                // — at 67%, since the percentage is computed against the real
                // step total of files.Count + 1 — and then held it there for
                // the whole join. No clamp is needed now: Current never exceeds
                // files.Count.
                Job.SetFile(p.Current, p.File ?? outName);
                Job.Report(p.Message, p.Percent);
            });
            await _ffmpeg.ConcatFilesAsync(ofd.FileNames, outPath, progress);

            StatusText = $"Merged {fileCount} files → {outName}";
            completed = true;
        }
        catch (Exception ex) { StatusText = "Merge failed"; ShowError(ex.Message, "Merge failed"); }
        finally
        {
            IsBusy = false; ProgressPercent = 0;
            if (!completed) Job.End();
        }

        // Holds the finished bar on screen for a few seconds, then hides the
        // panel itself — so no Job.End() on this path. After the finally, so
        // the toolbar is usable again while the result is still up.
        if (completed)
        {
            // This path had no summary at all, which made the one place that
            // reports nothing the one where the inputs are least known — the
            // files were picked from anywhere rather than cut from a loaded
            // video. No sync reading here: a concat of whole files has no
            // marked range to check an output against.
            var madeDuration = await _ffmpeg.GetDurationAsync(outPath);
            var madeMb = new FileInfo(outPath) is { Exists: true } made ? made.Length / 1024.0 / 1024.0 : 0;

            Job.Complete($"Merged {fileCount} files into", outName,
                         summary: $"{fileCount} files · {Bookmark.FormatTime(madeDuration)} · {madeMb:0.#} MB");
        }
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
        // Seeded in the precise style rather than the reading shown elsewhere:
        // this value is about to be edited by hand, and "00:01:05" has a field
        // to put an hour in. "1:05" does not, and "37s" does not even look
        // like a time you may put a colon in.
        var value = Bookmark.FormatPrecise(Session.CurrentTimeSeconds);
        string? refusal = null;

        // Loop rather than closing on a refusal: the entry is re-shown with
        // what was wrong, so the user can correct it or cancel — deciding for
        // themselves rather than starting over. Only the reasons below bring
        // us back here; whether the text is a time at all is settled in the
        // dialog, as it is typed.
        while (true)
        {
            var dlg = new EnterTimeDialog(value, refusal) { Owner = DialogOwner };
            if (dlg.ShowDialog() != true) return;

            value = dlg.Value;
            var start = dlg.Parsed.Start;

            if (dlg.Parsed.End is { } end)
            {
                if (end <= start)
                {
                    refusal = $"The end time ({Bookmark.FormatTime(end)}) must be after the " +
                              $"start time ({Bookmark.FormatTime(start)}).";
                    continue;
                }
                if (OverlappingCut(start, end) is { } clash)
                {
                    refusal = $"That range runs over bookmark {clash.Index} " +
                              $"({clash.StartDisplay} – {clash.EndDisplay}). Bookmarks cannot overlap.";
                    continue;
                }
                if (WhyCannotOpenAt(start) is { } backwards)
                {
                    refusal = $"{backwards}. Bookmarks are marked going forwards, so a new one " +
                              "has to start at or after the last mark.";
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
                // A single time is the next timestamp, not necessarily a new
                // bookmark. With one already open this closes it, exactly as
                // pressing the hotkey would — typing a time and pressing the
                // key are two ways of saying the same thing, and they were
                // disagreeing: this branch always added a row, so a second
                // opening timestamp appeared while the first was still waiting
                // for its end. Two open bookmarks at once is a state nothing
                // else in the program can produce or knows what to do with.
                if (OpenBookmark is { } open)
                {
                    if (start <= open.StartSeconds)
                    {
                        refusal = $"{Bookmark.FormatTime(start)} cannot close bookmark {open.Index} — " +
                                  "a closing time has to come after its opening time of " +
                                  $"{open.StartDisplay}.";
                        continue;
                    }

                    if (OverlappingCut(open.StartSeconds, start, open) is { } inTheWay)
                    {
                        refusal = $"Closing at {Bookmark.FormatTime(start)} would run over bookmark " +
                                  $"{inTheWay.Index} ({inTheWay.StartDisplay} – {inTheWay.EndDisplay}). " +
                                  "Bookmarks cannot overlap.";
                        continue;
                    }

                    open.EndSeconds = start;
                    StatusText = $"Closed bookmark {open.Index} ({open.DurationDisplay})";
                }
                else
                {
                    if (WhyCannotOpenAt(start) is { } refused)
                    {
                        refusal = $"{refused}. Bookmarks are marked going forwards, so a new one has " +
                                  "to start at or after the last mark.";
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
            }

            SaveBookmarks();
            Session.NotifyDurationChanged();
            return;
        }
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
        string? jobSummary = null;

        IsBusy = true; ProgressPercent = 0; StatusText = trimming ? "Trimming…" : "Merging…";
        Job.Begin(trimming ? "Trimming cut" : "Merging cuts");
        Job.SetFile(1, Path.GetFileName(outPath));
        try
        {
            var progress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                ProgressPercent = p.Percent;
                StatusText = p.Message;
                Job.Report(p.Message, p.Percent);

                // Current is the segment being cut, counted against the list
                // handed over just below — so it indexes that list and not the
                // session's, which may hold rows this run is not touching.
                if (p.Current >= 0 && p.Current < toMerge.Count)
                    MarkStep(toMerge[p.Current]);
            });
            await _ffmpeg.MergeBookmarksAsync(Session.VideoPath, outPath, toMerge, progress, default, format);

            StatusText = $"Created {Path.GetFileName(outPath)}";

            // The app now checks what it produced against what was asked for,
            // and separately whether the audio and video tracks inside it
            // actually land together — a file can pass the length check while
            // still carrying a stream offset, since the container's duration
            // is whichever of the two runs longer.
            var drift = _ffmpeg.LastAudioDriftSeconds;
            var driftNoticeable = drift is { } d && Math.Abs(d) > FFmpegService.AudioSyncToleranceSeconds;
            var driftText = driftNoticeable
                ? $"audio runs {Math.Abs(drift!.Value) * 1000:0}ms {(drift.Value > 0 ? "longer" : "shorter")} than the video"
                : null;

            // Measured either way: the panel says what came out even when
            // something about it is also worth warning over.
            var finishedDuration = await _ffmpeg.GetDurationAsync(outPath);
            var sizeMb = new FileInfo(outPath) is { Exists: true } file ? file.Length / 1024.0 / 1024.0 : 0;
            var made = trimming
                ? $"{Bookmark.FormatTime(finishedDuration)} · {sizeMb:0.#} MB"
                : $"{toMerge.Count} cuts · {Bookmark.FormatTime(finishedDuration)} · {sizeMb:0.#} MB";

            jobSummary = driftNoticeable
                ? $"{made} · {driftText}"
                : $"{made} · audio in sync";

            if (_ffmpeg.LastOutputWarning is { } warning)
            {
                // A held, forced toast either way — something to read, not
                // just notice — with the sync finding folded in rather than
                // fired as a second toast, which would just replace this one.
                var detail = driftText != null ? $"{warning} Also, {driftText}." : warning;
                _toast.Show("Output doesn't match the marked range", detail, "⚠", force: true,
                            hold: TimeSpan.FromSeconds(Math.Max(6, _toast.HoldDuration.TotalSeconds)));
            }
            else if (driftNoticeable)
            {
                _toast.Show("Audio may drift out of sync", char.ToUpperInvariant(driftText![0]) + driftText[1..] +
                            " — most noticeable near the cut points.", "⚠", force: true,
                            hold: TimeSpan.FromSeconds(Math.Max(6, _toast.HoldDuration.TotalSeconds)));
            }
            else
            {
                _toast.Show(trimming ? "Trimmed cut" : "Merged cuts", jobSummary, "🎬");
            }

            succeeded = true;
        }
        catch (Exception ex)
        {
            var what = trimming ? "Trim failed" : "Merge failed";
            StatusText = what;
            ShowError(ex.Message, what);
        }
        finally
        {
            IsBusy = false; ProgressPercent = 0;
            ClearStep();
            if (!succeeded) Job.End();
        }

        // Not awaited: the cleanup prompt below should come up straight away
        // rather than after the finished bar has finished holding.
        if (succeeded)
            Job.Complete(trimming ? "Trimmed cut into" : $"Merged {toMerge.Count} cuts into",
                         Path.GetFileName(outPath), summary: jobSummary);

        // Only after the output is on disk.
        if (succeeded) await RunPostOperationCleanup(source, includeBookmarks: true, justWrote: outPath);
    }

    [RelayCommand(CanExecute = nameof(CanSplitSelected))]
    private async Task SplitSelectedAsync()
    {
        var toSplit = Session.Bookmarks.Where(b => b.IsSelected && b.IsValid).ToList();
        if (toSplit.Count == 0) toSplit = Session.Bookmarks.Where(b => b.IsValid).ToList();
        if (toSplit.Count == 0) { Notify("No valid bookmarks."); return; }

        // Straight into "Save to", like every other action. This used to make a
        // "<name>_clips" folder and put them in there, which meant Split was the
        // one operation whose output did not appear where the user had pointed
        // it — and left a folder behind for a single cut.
        //
        // Nothing is lost by dropping it: the clips are named [done], [done2],
        // [done3]… by index, so they do not collide with each other, and a name
        // already taken by something else goes through the same collision
        // prompt as the rest of the app.
        var outDir = ResolveSaveToDirectory();
        if (string.IsNullOrWhiteSpace(outDir))
        {
            Notify("Could not determine where to save the clips.", "Split",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Directory.CreateDirectory(outDir);

        var format = OutputFormat;
        var source = Session.VideoPath;
        var succeeded = false;
        var written = 0;

        IsBusy = true; ProgressPercent = 0;
        try
        {
            // Linear naming: clip 1 = <name>[done].mp4, clip 2 = <name>[done2].mp4, etc.
            // Collisions are put to the user rather than silently skipped past,
            // so a second split into the same folder is a deliberate choice.
            Job.Begin("Splitting clips", toSplit.Count);
            BeginNameBatch(toSplit.Count);
            int i = 0;
            foreach (var b in toSplit)
            {
                // Names the cut rather than counting, like Convert and Strip
                // audio do. The panel directly above already carries a counter,
                // and it counts what is finished while this counted what was
                // starting — so the two sat there disagreeing, "1/3" above
                // "Splitting 2/3".
                i++; ProgressPercent = (double)i / toSplit.Count * 100;
                MarkStep(b);
                StatusText = $"Splitting {b.StartDisplay} → {b.EndDisplay}";
                _batchRemaining = toSplit.Count - i + 1;
                // i - 1, matching the percentage on the next line: i has
                // already been bumped for the cut about to run, and the counter
                // reports what is finished.
                Job.SetFile(i - 1, Path.GetFileName(Session.VideoPath));
                Job.Report($"Cutting {b.StartDisplay} → {b.EndDisplay}", (double)(i - 1) / toSplit.Count * 100);

                var outPath = await ResolveOutputPathAsync(
                    BuildSplitPath(outDir, Session.VideoFileName, i, format.Extension));
                if (outPath == null) continue;   // canceled this clip

                await _ffmpeg.MergeBookmarksAsync(Session.VideoPath, outPath, new[] { b }, null, default, format);
                written++;
            }
            StatusText = $"Created {written} clip(s) in {outDir}";

            // A run in which every clip was canceled wrote nothing, so there
            // is nothing the originals are redundant to.
            succeeded = written > 0;
        }
        catch (Exception ex) { StatusText = "Split failed"; ShowError(ex.Message, "Split failed"); }
        finally
        {
            IsBusy = false; ProgressPercent = 0;
            ClearStep();
            if (!succeeded) Job.End();
        }

        // Many outputs rather than one, so the destination folder stands in for
        // the created file. Not awaited — the cleanup prompt comes first.
        if (succeeded) Job.Complete($"Split into {written} clip(s) in", outDir);

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
        return Path.Combine(outDir, $"{nameWithoutExt}{SuffixBracket(index)}{extension ?? OutputFormat.Extension}");
    }

    /// <summary>
    /// Splits a single bookmark into its own clip — the save button at the
    /// head of each row in the bookmark list. Output goes to the
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
            out var nameCanceled);
        if (nameCanceled) return;

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

        // The bookmark goes through so a template can use its position and
        // length; without one the extra argument changes nothing.
        var outPath = GetUniqueOutputPath(
            Path.Combine(outDir, correctedStem + format.Extension),
            format.Extension,
            outputDirectory: outDir,
            bookmark: bookmark);

        IsBusy = true;
        ProgressPercent = 0;
        var succeeded = false;
        try
        {
            // Through the job panel, like every other operation that writes a
            // file. This used to be a line of status text and a percentage
            // nailed at 50 — so the one-click save was the only thing in the
            // program that started, said nothing more, and finished without
            // the bar, the clock, the chime or any sign it had worked.
            Job.Begin("Quick save", 1);
            Job.SetFile(0, Path.GetFileName(outPath));
            Job.Report("Cutting " + bookmark.StartDisplay + " → " + bookmark.EndDisplay, 0);

            MarkStep(bookmark);
            StatusText = $"Splitting clip {bookmark.Index} → {Path.GetFileName(outPath)}";

            // Real progress from ffmpeg rather than a figure chosen to look
            // like something was happening.
            var progress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                ProgressPercent = p.Percent;
                Job.Report(string.IsNullOrWhiteSpace(p.Message) ? "Cutting" : p.Message, p.Percent);
            });

            // Reuse the same FFmpeg path that SplitSelected uses — it
            // honours Speed and IsFlipped on the bookmark.
            await _ffmpeg.MergeBookmarksAsync(Session.VideoPath, outPath, new[] { bookmark }, progress, default, format);

            ProgressPercent = 100;
            StatusText = $"Created clip → {outPath}";
            succeeded = true;

            // The row's button becomes a check mark. Set here rather than
            // after the try, so a throw on the way out of the write cannot
            // leave a row claiming a clip that was never finished.
            bookmark.QuickSaved = true;

            if (_ffmpeg.LastOutputWarning is { } warning)
                _toast.Show("Output doesn't match the marked range", warning, "⚠", force: true,
                            hold: TimeSpan.FromSeconds(Math.Max(6, _toast.HoldDuration.TotalSeconds)));
            else
                _toast.Show($"Clip {bookmark.Index} saved",
                            Path.GetFileName(outPath),
                            "🎬");
        }
        catch (Exception ex)
        {
            // ShowError rather than a bare MessageBox: it carries the error
            // icon, which is what makes Windows play the failure sound, and it
            // colours the status strip on the way past.
            StatusText = "Split failed";
            ShowError(ex.Message, "Quick save failed");
        }
        finally
        {
            IsBusy = false;
            ProgressPercent = 0;
            ClearStep();
            if (!succeeded) Job.End();
        }

        // After the finally, like the other operations: Complete holds the
        // finished panel on screen for a few seconds, and nothing that has to
        // happen on the way out should be waiting behind it.
        if (succeeded) Job.Complete("Quick saved clip " + bookmark.Index + " to", outPath);
    }

    /// <summary>
    /// Converts picked images to <paramref name="formatKey"/>, following the
    /// same naming tag, filename policy and collision handling as every
    /// other action.
    /// </summary>
    /// <param name="formatKey">
    /// One of <see cref="ImageConversionService.Formats"/>, supplied as the
    /// CommandParameter of the menu item that was clicked — or null from the
    /// toolbar button, which has no parameter to carry and asks instead.
    /// </param>
    [RelayCommand]
    private async Task ConvertImages(string? formatKey)
    {
        // A key that was supplied and is not one of ours is a slip in the
        // markup, not something to ask the user about.
        if (!string.IsNullOrWhiteSpace(formatKey)
            && ImageConversionService.FindFormat(formatKey) is null)
        {
            StatusText = $"Unknown image format '{formatKey}'.";
            return;
        }

        // Images first, format and destination second: the picture is the
        // part of this decision the user already has in mind, so it should
        // not wait behind a question that only matters once some are chosen.
        var ofd = new OpenFileDialog
        {
            Filter = $"Image Files|{ImageFilePattern()}|All Files|*.*",
            Multiselect = true,
            Title = "Select images to convert"
        };
        // A plain return here left the bar reporting whatever the previous
        // operation finished with — reading like this cancelled run was the
        // one that produced it. The status bar's whole job is "what just
        // happened", so a cancel has to say so rather than leave stale text.
        if (ofd.ShowDialog() != true)
        {
            StatusText = "Image conversion cancelled.";
            return;
        }

        var dlg = new ConvertImagesDialog(formatKey ?? _lastImageFormatKey,
                                          ResolveSaveToDirectory(), _lastImageFolder)
        {
            Owner = DialogOwner
        };
        if (dlg.ShowDialog() != true)
        {
            StatusText = "Image conversion cancelled.";
            return;
        }

        var format = dlg.Format;
        _lastImageFormatKey = format.Key;
        _lastImageFolder = dlg.OutputDirectory;

        IsBusy = true;
        int done = 0;
        var errors = new List<string>();

        // Sources that converted cleanly, and so are safe to offer for
        // deletion afterwards. Skipped, canceled and failed files never make
        // it in, so a failure can never cost the original.
        var convertedSources = new List<string>();

        // What was written, so the summary can name the folder even when the
        // destination was "beside each picture" and there is no one folder
        // decided in advance.
        var written = new List<string>();

        Job.Begin($"Converting images to {format.Display}", ofd.FileNames.Length);
        BeginNameBatch(ofd.FileNames.Length);

        foreach (var file in ofd.FileNames)
        {
            _batchRemaining = ofd.FileNames.Length - done;
            Job.SetFile(done, Path.GetFileName(file));
            // Bare "Writing": the action already names the format, and the panel
            // shows the two joined as "Converting images to PNG · Writing".
            Job.Report("Writing", (double)done / ofd.FileNames.Length * 100);
            StatusText = $"Converting {Path.GetFileName(file)}…";

            var outPath = await ResolveOutputPathAsync(
                GetSuffixedOutputPath(file, format.Extension, _lastImageFolder));
            if (outPath == null) { done++; continue; }

            try
            {
                // Quick, but pushed off the UI thread so a large batch cannot
                // freeze the window mid-run.
                await Task.Run(() => _images.Convert(file, outPath, format));
                written.Add(outPath);

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
            Job.Report("Writing", ProgressPercent);
        }

        IsBusy = false; ProgressPercent = 0;

        Job.Complete(errors.Count == 0
            ? $"Converted {done} image(s) to {format.Display} in"
            : $"Converted with {errors.Count} error(s) — output in",
            OutputFolderPicker.Describe(_lastImageFolder, written));

        StatusText = errors.Count == 0
            ? $"Converted {done} image(s) to {format.Display}"
            : $"Finished with {errors.Count} error(s)";

        if (errors.Count > 0)
            MessageBox.Show(string.Join("\n", errors), "Image conversion",
                MessageBoxButton.OK, MessageBoxImage.Warning);

        OfferToDeleteSources(convertedSources);
    }

    /// <summary>
    /// Shows what picked image files say about themselves.
    /// </summary>
    /// <remarks>
    /// Reads nothing and writes nothing — the one operation here that only
    /// looks. Several files at once because the question is usually asked of a
    /// folder rather than a file: which of these came off the camera, which
    /// have been through something else, which carry where they were taken.
    /// </remarks>
    [RelayCommand]
    private async Task ShowImageInfo()
    {
        var ofd = new OpenFileDialog
        {
            Filter = $"Image Files|{ImageFilePattern()}|All Files|*.*",
            Multiselect = true,
            Title = "Select images to inspect"
        };
        if (ofd.ShowDialog() != true) return;

        await ShowImageFacts(ofd.FileNames);
    }

    /// <summary>
    /// Reads the given pictures and shows what they say about themselves.
    /// </summary>
    /// <remarks>
    /// Separate from the command because the files do not always come from a
    /// picker: dropping a picture on the window asks the same question, and
    /// answering it twice in two places is how the two answers start to differ.
    /// </remarks>
    private async Task ShowImageFacts(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        StatusText = paths.Count == 1
            ? "Reading the image…"
            : $"Reading {paths.Count} images…";

        // Off the UI thread: decoding is per file, and a folderful of large
        // photographs would otherwise hold the window while it worked.
        var facts = await Task.Run(() => paths.Select(_imageInfo.Read).ToList());

        var located = facts.Count(f => f.Location is not null);

        new ImageInfoDialog(facts) { Owner = DialogOwner }.ShowDialog();

        StatusText = located == 0
            ? $"Read {facts.Count} image(s)"
            : $"Read {facts.Count} image(s) — {located} say where they were taken";
    }

    /// <summary>Whether this is a picture the program can read.</summary>
    private static bool IsImageFile(string path) =>
        ImageConversionService.ReadableExtensions.Contains(
            Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The file-dialog filter for pictures: every extension that can be read.
    /// </summary>
    private static string ImageFilePattern() =>
        string.Join(";", ImageConversionService.ReadableExtensions.Select(e => "*" + e));

    /// <summary>
    /// The format the last conversion produced, so the picker comes up on it.
    /// Set by the menu entries too — picking JPG there and then reaching for
    /// the toolbar button should not land on PNG.
    /// </summary>
    private string? _lastImageFormatKey;

    /// <summary>
    /// Where the last image operation wrote: a folder, or an empty string for
    /// beside each picture. Shared by Convert and Resize, because it is one
    /// habit rather than two, and per session like the rest of these.
    /// </summary>
    private string _lastImageFolder = string.Empty;

    /// <summary>
    /// The size and shape rule the last resize ran with, so a second run is one
    /// click. Per session, like the Strip audio choice — a size remembered
    /// across launches would quietly decide the next batch.
    /// </summary>
    private int _resizeWidth = 1920;
    private int _resizeHeight = 1080;
    private ImageConversionService.ResizeFit _resizeFit = ImageConversionService.ResizeFit.Inside;

    /// <summary>
    /// Writes picked images out at a chosen size, following the same naming
    /// tag, filename policy and collision handling as every other action.
    /// </summary>
    /// <remarks>
    /// One file or forty: the picker is the same, and the size is asked once
    /// for the batch rather than once per file.
    ///
    /// Options first, then the files — the order Convert images uses, so the
    /// two image operations are the same three steps in the same sequence.
    ///
    /// Each picture keeps its own format, so a folder of mixed JPGs and PNGs
    /// comes back as the same mixture. WebP is the exception — it can be read
    /// and not written — and those come back as PNG, which the summary says.
    /// </remarks>
    [RelayCommand]
    private async Task ResizeImages()
    {
        // Images first, size and destination second: the picture is the part
        // of this decision the user already has in mind, so it should not
        // wait behind a question that only matters once some are chosen.
        var ofd = new OpenFileDialog
        {
            Filter = $"Image Files|{ImageFilePattern()}|All Files|*.*",
            Multiselect = true,
            Title = "Select images to resize"
        };
        // A plain return here left the bar reporting whatever the previous
        // operation finished with — reading like this cancelled run was the
        // one that produced it. The status bar's whole job is "what just
        // happened", so a cancel has to say so rather than leave stale text.
        if (ofd.ShowDialog() != true)
        {
            StatusText = "Image resize cancelled.";
            return;
        }

        var dlg = new ResizeImagesDialog(_resizeWidth, _resizeHeight, _resizeFit,
                                         ResolveSaveToDirectory(), _lastImageFolder)
        {
            Owner = DialogOwner
        };
        if (dlg.ShowDialog() != true)
        {
            StatusText = "Image resize cancelled.";
            return;
        }

        _resizeWidth = dlg.PixelWidth;
        _resizeHeight = dlg.PixelHeight;
        _resizeFit = dlg.Fit;
        _lastImageFolder = dlg.OutputDirectory;

        var size = $"{_resizeWidth} × {_resizeHeight}";

        IsBusy = true;
        int done = 0;
        var errors = new List<string>();

        // Only the files that came through cleanly, so a failure can never cost
        // the original.
        var resizedSources = new List<string>();

        // What was written, so the summary can name the folder even when the
        // destination was "beside each picture".
        var written = new List<string>();

        // Files that could be read but not written back in their own format.
        var reformatted = 0;

        Job.Begin($"Resizing images to {size}", ofd.FileNames.Length);
        BeginNameBatch(ofd.FileNames.Length);

        foreach (var file in ofd.FileNames)
        {
            _batchRemaining = ofd.FileNames.Length - done;
            Job.SetFile(done, Path.GetFileName(file));
            Job.Report("Writing", (double)done / ofd.FileNames.Length * 100);
            StatusText = $"Resizing {Path.GetFileName(file)}…";

            var format = ImageConversionService.FormatForFile(file);
            if (!string.Equals(format.Extension, Path.GetExtension(file), StringComparison.OrdinalIgnoreCase))
                reformatted++;

            var outPath = await ResolveOutputPathAsync(
                GetSuffixedOutputPath(file, format.Extension, _lastImageFolder));
            if (outPath == null) { done++; continue; }

            try
            {
                // Off the UI thread: decoding and scaling a large batch would
                // otherwise freeze the window for the length of the run.
                await Task.Run(() => _images.Resize(file, outPath!, _resizeWidth, _resizeHeight, _resizeFit));
                written.Add(outPath);

                if (!string.Equals(file, outPath, StringComparison.OrdinalIgnoreCase))
                    resizedSources.Add(file);
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }

            done++;
            ProgressPercent = (double)done / ofd.FileNames.Length * 100;
            Job.Report("Writing", ProgressPercent);
        }

        IsBusy = false; ProgressPercent = 0;

        Job.Complete(errors.Count == 0
            ? $"Resized {done} image(s) to {size} in"
            : $"Resized with {errors.Count} error(s) — output in",
            OutputFolderPicker.Describe(_lastImageFolder, written));

        StatusText = errors.Count == 0
            ? $"Resized {done} image(s) to {size}"
              + (reformatted > 0 ? $" — {reformatted} written as PNG" : "")
            : $"Finished with {errors.Count} error(s)";

        if (errors.Count > 0)
            MessageBox.Show(string.Join("\n", errors), "Resize images",
                MessageBoxButton.OK, MessageBoxImage.Warning);

        OfferToDeleteSources(resizedSources);
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
    /// skipped or canceled file must never reach here — a cleanup that can
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
                ? $"Delete the original video?\n\n{Path.GetFileName(videos[0])}\n\n{DeletionNote(1)}"
                : $"Delete the {videos.Count} original video files this operation used?\n\n{DeletionNote(videos.Count)}";

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
            var prompt = $"Delete the bookmarks file?\n\n{Path.GetFileName(csv!)}\n\n{DeletionNote(1)}";

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
                ? DeletedNote(Path.GetFileName(deleted[0]))
                : DeletedNote($"{deleted.Count} file(s)");

        if (failed.Count > 0)
            MessageBox.Show(string.Join("\n", failed), "Delete after operation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// How a deletion about to be offered should be described, according to
    /// where the files will actually go.
    /// </summary>
    /// <remarks>
    /// These prompts used to promise the Recycle Bin unconditionally, which
    /// stopped being true the moment that became a setting: with it off, the
    /// dialog the user clicks Yes on was telling them a permanent deletion was
    /// recoverable. A confirmation that misdescribes what it is confirming is
    /// worse than no confirmation.
    /// </remarks>
    private string DeletionNote(int count) =>
        _settings.Current.DeleteToRecycleBin
            ? (count == 1 ? "It will go to the Recycle Bin."
                          : "They will go to the Recycle Bin.")
            : (count == 1 ? "It will be deleted permanently and cannot be recovered."
                          : "They will be deleted permanently and cannot be recovered.");

    /// <summary>Past-tense counterpart of <see cref="DeletionNote"/>, for the status bar.</summary>
    private string DeletedNote(string what) =>
        _settings.Current.DeleteToRecycleBin
            ? $"Moved {what} to the Recycle Bin"
            : $"Permanently deleted {what}";

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
    /// failed, was skipped or was canceled can never be lost. This keeps its
    /// dialog — deleting the user's images is precisely the sort of thing a
    /// status-bar line should not decide silently, and it defaults to No.
    /// </remarks>
    private void OfferToDeleteSources(List<string> sources)
    {
        if (sources.Count == 0) return;

        // Just the question. It named the count and the format it had converted
        // to — "Delete the 12 original image(s) that were converted to JPEG?" —
        // which is a recap of what just happened rather than part of the
        // decision, and the operation had only just reported both. What is
        // being deleted, and that it goes to the Recycle Bin, is on the line
        // below.
        var prompt = (sources.Count == 1
                         ? "Delete the original image?"
                         : "Delete the original images?")
                     + "\n\n" + DeletionNote(sources.Count);

        if (!Confirm(prompt, "Delete originals")) return;

        var deletedPaths = new List<string>();
        var failed = new List<string>();

        foreach (var file in sources)
            Recycle(file, deletedPaths, failed);

        int deleted = deletedPaths.Count;

        StatusText = failed.Count == 0
            ? DeletedNote($"{deleted} original image(s)")
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
        // deletion afterwards. Skipped, canceled and failed files never make
        // it in, so a failure can never cost the original.
        var convertedSources = new List<string>();

        IsBusy = true; int done = 0; var errors = new List<string>();
        Job.Begin("Converting video", ofd.FileNames.Length);
        BeginNameBatch(ofd.FileNames.Length);
        foreach (var file in ofd.FileNames)
        {
            _batchRemaining = ofd.FileNames.Length - done;
            Job.SetFile(done, Path.GetFileName(file));
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
        IsBusy = false; ProgressPercent = 0;

        Job.Complete(errors.Count == 0
            ? $"Converted {done} file(s) to {label} in"
            : $"Converted with {errors.Count} error(s) — output in",
            ResolveSaveToDirectory());

        StatusText = errors.Count == 0 ? $"Converted {done} file(s) to {label}" : $"Finished with {errors.Count} error(s)";
        if (errors.Count > 0) MessageBox.Show(string.Join("\n", errors));

        // Convert has no bookmark file of its own — the loaded session's CSV
        // belongs to a different video and must not be swept up here.
        await RunPostOperationCleanup(convertedSources, includeBookmarks: false);
    }

    /// <summary>
    /// What the last Strip audio run produced, preselected on the next one.
    /// </summary>
    /// <remarks>
    /// A field rather than a setting. Somebody pulling the audio out of a
    /// folder of files is usually doing it to all of them, so asking twice with
    /// the answer already filled in is the least the dialog can do; persisting
    /// it would mean a choice made weeks ago deciding a run silently.
    /// </remarks>
    private StripAudioOutputs _lastStripOutputs = StripAudioOutputs.Audio;

    /// <summary>
    /// The silent copy's name, which must not be the MP3's name with a
    /// different extension.
    /// </summary>
    /// <remarks>
    /// This is not decoration. MPC-HC — and most players — auto-load an audio
    /// file sitting beside a video whose name starts with the video's, and
    /// attach it as an external track. Writing <c>clip[done].mp3</c> next to
    /// <c>clip[done].mp4</c> hands the player a matched pair, so the silent
    /// copy plays with the very sound that was taken out of it. The file is
    /// genuinely silent — ffprobe shows one video stream — but nobody watching
    /// it would believe that, and they would be right not to: what they asked
    /// for was a video that makes no noise.
    ///
    /// Only the video moves. The player searches for audio whose name begins
    /// with the <em>video's</em> name, so lengthening the video's stem breaks
    /// the match in the one direction that matters; renaming the MP3 instead
    /// would leave it still starting with the video's name.
    ///
    /// Applied whether or not an MP3 is being written this run. A silent copy
    /// made today and an MP3 made from the same source next week would pair up
    /// just as well, and the marker also says which file is which in a folder
    /// of near-identical names.
    /// </remarks>
    private static string SilentStem(string stem) => stem.TrimEnd('-', '_', ' ') + "-silent";

    [RelayCommand]
    private async Task StripAudioAsync()
    {
        var ofd = new OpenFileDialog { Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm|All Files|*.*", Multiselect = true };
        if (ofd.ShowDialog() != true) return;

        // Asked after the files are picked, so the silent copy can name the
        // extension it would actually carry.
        var ask = new StripAudioDialog(_lastStripOutputs, Path.GetExtension(ofd.FileNames[0]))
        {
            Owner = DialogOwner
        };
        if (ask.ShowDialog() != true) return;
        _lastStripOutputs = ask.Outputs;

        var wantAudio = _lastStripOutputs.HasFlag(StripAudioOutputs.Audio);
        var wantVideo = _lastStripOutputs.HasFlag(StripAudioOutputs.SilentVideo);

        // Each file is one ffmpeg run per output asked for, and the progress
        // bar has to divide by the total rather than by the file count.
        var perFile = (wantAudio ? 1 : 0) + (wantVideo ? 1 : 0);
        var totalSteps = ofd.FileNames.Length * perFile;
        var step = 0;

        IsBusy = true;
        int done = 0;
        int wroteAudio = 0, wroteVideo = 0;

        // A run that threw, as against one the user cancelled at a name prompt.
        // Only the first is a failure; the second is them changing their mind,
        // and reporting it as a failure would be calling the user an error.
        var anyFailed = false;
        var stripped = new List<string>();
        Job.Begin("Stripping audio", ofd.FileNames.Length);
        BeginNameBatch(ofd.FileNames.Length);

        foreach (var file in ofd.FileNames)
        {
            _batchRemaining = ofd.FileNames.Length - done;
            StatusText = $"Extracting: {Path.GetFileName(file)}";
            ProgressPercent = (double)step / totalSteps * 100;
            Job.SetFile(done, Path.GetFileName(file));

            // The name is settled once per file, before either output is
            // written. Both outputs share a stem and differ only in extension,
            // so a source whose name breaks FileNameRules — spaces and brackets
            // are common in video filenames — used to raise the same rename
            // prompt twice for the same name. The second one reads as a
            // duplicate and gets dismissed, which silently dropped whichever
            // output it belonged to.
            var named = EnforceFileNamePolicy(
                GetSuffixedOutputPath(file, Path.GetExtension(file), ResolveSaveToDirectory()),
                out var nameCanceled);

            // Its runs are not going to happen, so the bar has to be told —
            // otherwise the denominator counts steps nothing will ever fill and
            // the job finishes short of 100%.
            if (nameCanceled) { step += perFile; done++; continue; }

            var outDir = Path.GetDirectoryName(named) ?? "";
            var outStem = Path.GetFileNameWithoutExtension(named);

            // One ffmpeg run. Returns false when the name was not resolved or
            // the run failed, so a file only counts as consumed — and only
            // becomes eligible for cleanup — once everything asked of it worked.
            async Task<bool> Write(string extension, string label, bool silentCopy,
                                   Func<string, IProgress<FFmpegProgressEventArgs>, Task> run)
            {
                var stem = silentCopy ? SilentStem(outStem) : outStem;
                var candidate = Path.Combine(outDir, stem + extension);

                // The silent copy carries the source's own extension, so it is
                // the one output that can be handed the name of the file it is
                // reading — with the naming tag set to None there is no bracket
                // to tell them apart. ffmpeg refuses to write in place and
                // leaves the original as it was, which looks exactly like an
                // operation that ran and did nothing.
                if (string.Equals(candidate, file, StringComparison.OrdinalIgnoreCase))
                    candidate = NextFreeName(candidate);

                var outPath = await ResolveOutputPathAsync(candidate, enforcePolicy: false);
                if (outPath == null) return false;

                // The action already says "audio"; joined it reads
                // "Stripping audio · Extracting to MP3".
                Job.Report(label, (double)step / totalSteps * 100);

                var slice = 100.0 / totalSteps;
                var basePct = step * slice;
                var fileProgress = new Progress<FFmpegProgressEventArgs>(p =>
                {
                    Job.Report(p.Message, basePct + p.Percent / 100.0 * slice);
                    ProgressPercent = basePct + p.Percent / 100.0 * slice;
                });

                try { await run(outPath, fileProgress); }
                catch (Exception ex)
                {
                    anyFailed = true;
                    ShowError(ex.Message, "Strip audio failed");
                    return false;
                }
                finally { step++; }

                return true;
            }

            var ok = true;
            if (wantAudio)
            {
                var wrote = await Write(".mp3", "Extracting to MP3", silentCopy: false,
                                        (o, p) => _ffmpeg.StripAudioAsync(file, o, p));
                if (wrote) wroteAudio++;
                ok &= wrote;
            }
            if (wantVideo)
            {
                var wrote = await Write(Path.GetExtension(file), "Removing the audio track",
                                        silentCopy: true,
                                        (o, p) => _ffmpeg.RemoveAudioAsync(file, o, p));
                if (wrote) wroteVideo++;
                ok &= wrote;
            }

            if (ok) stripped.Add(file);
            done++;
        }

        IsBusy = false; ProgressPercent = 0;

        // Counted, not assumed. Asking for both and getting one — because a
        // name prompt was cancelled or a run failed — used to be reported as
        // though both had been written.
        var produced = (wantAudio, wantVideo) switch
        {
            (true, true) => $"Extracted the audio from {wroteAudio} file(s) and silenced {wroteVideo}",
            (false, true) => $"Removed the audio from {wroteVideo} file(s)",
            _ => $"Extracted the audio from {wroteAudio} file(s)"
        };

        // Nothing written is not a finished job. Completing regardless is what
        // had a run that failed — and said so in a dialog — go on to fill the
        // bar, report what it had produced, and play the finished chime over
        // the top of the error the user was still reading.
        //
        // Counted rather than taken from the last run's result: with several
        // files picked, some can be written and others fail, and the job is
        // only a failure when none of them got through.
        if (anyFailed || (wroteAudio == 0 && wroteVideo == 0))
        {
            var what = wantVideo && !wantAudio ? "Audio removal failed" : "Audio extraction failed";

            // What did get written is still said. "Failed" on its own would
            // hide three files that were written because a fourth was not, and
            // leave the user hunting for output they already have.
            var detail = wroteAudio > 0 || wroteVideo > 0 ? $"{what} — {produced}" : what;

            Job.End(detail);
            StatusText = detail;
        }
        else
        {
            Job.Complete(produced + " to", ResolveSaveToDirectory());
            StatusText = wantVideo && !wantAudio ? "Audio removal finished" : "Audio extraction finished";
        }

        // Same rule as Convert: these are files the user picked here, so the
        // session's bookmark file — which belongs to a different video — is not
        // swept up with them.
        await RunPostOperationCleanup(stripped, includeBookmarks: false);
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
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            // Said apart, because the two have different fixes: one needs a
            // folder choosing, the other needs the drive it was on.
            Notify(string.IsNullOrEmpty(folder)
                ? "Set a playlist folder first, or use Playlist ▸ Load playlist… to load one."
                : $"The playlist folder is no longer there:\n{folder}\n\nSet it again, or use Playlist ▸ Load playlist… to load one.");
            return;
        }
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
    /// Reads a .pls playlist's entries in order, each paired with whether its
    /// file is present, confirmed gone, or unreachable because its drive is
    /// detached. Exposed so MainWindow.xaml.cs can build the Playlist menu's
    /// per-entry sub-items without taking a direct dependency on
    /// <see cref="PlaylistService"/>.
    /// </summary>
    public List<PlaylistEntry> ClassifyPlaylistEntriesForMenu(string plsPath)
    {
        var result = new List<PlaylistEntry>();
        _stalls.Time($"ClassifyPlaylistEntries({Path.GetFileName(plsPath)})",
            () => result = _playlists.ClassifyEntries(plsPath));
        return result;
    }

    /// <summary>
    /// Counts the entries listed in a .pls, for the count shown beside the
    /// playlist's name in the Playlist menu.
    /// </summary>
    /// <remarks>
    /// This is the number of entries the playlist <em>lists</em>, not the
    /// number whose files still exist. The distinction is deliberate: reading
    /// the .pls is one small local file per playlist, while confirming each
    /// video is a stat per entry against whatever drive it lives on — the
    /// per-video cost that keeps the submenu below lazy. A count that made the
    /// menu wait on a spun-down drive would be a bad trade for a number in a
    /// header.
    /// </remarks>
    public int CountPlaylistEntriesForMenu(string plsPath)
    {
        int count = 0;
        _stalls.Time($"CountPlaylistEntries({Path.GetFileName(plsPath)})",
            () => count = _playlists.ReadEntries(plsPath).Count);
        return count;
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
            // "Not found" covers two different facts. Announcing that a file
            // no longer exists when its drive is merely unplugged is a claim
            // the application cannot support — and one that talks the user
            // into discarding a perfectly good entry.
            var root = DriveAvailability.RootOf(path);
            if (!new DriveAvailability().IsAvailable(root))
            {
                MessageBox.Show(
                    $"{root} is not connected, so this file cannot be reached:\n\n{path}\n\n" +
                    "Whether it still exists is unknown. Reconnect the drive and try again.",
                    "Drive not connected", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = $"{root} is not connected — this entry cannot be checked.";
                return;
            }

            // This was a Yes/No prompt offering removal, which the command has
            // no way to carry out: it receives a path, not a playlist and an
            // index. Both answers arrived here anyway, so it now states what
            // is true and points at the item that can actually act.
            MessageBox.Show(
                "This video file is gone — its drive is connected and the file is not " +
                $"there:\n\n{path}",
                "File not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText = "File is gone — use 'Remove' on the playlist menu item.";
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
    /// Removes every entry in a playlist whose video file cannot be found,
    /// then raises <see cref="PlaylistsChanged"/> so the menu — and the entry
    /// count beside the playlist's name — rebuilds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs immediately, with no confirmation — the same as the per-entry
    /// Remove beside it, which has never asked either. The status bar reports
    /// what happened afterwards.
    /// </para>
    /// <para>
    /// What keeps that safe is not a dialog but
    /// <see cref="PlaylistService.RemoveMissingEntries"/>, which will not touch
    /// an entry whose drive is disconnected however it is called. A playlist
    /// pointing at an external disk therefore survives this action intact while
    /// that disk is away, rather than relying on the user reading a warning in
    /// time. Only files confirmed gone — drive present, file absent — are
    /// removed.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private void RemoveMissingPlaylistEntries(string? plsPath)
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

        var name = Path.GetFileName(plsPath);

        // One pass. There is no prompt to go stale between deciding and
        // acting, so the classification the service makes as it writes is the
        // only one anybody needs.
        var result = default(PlaylistCleanup);
        _stalls.Time($"RemoveMissingEntries({name})",
            () => result = _playlists.RemoveMissingEntries(plsPath));

        var removed = result.Removed.Count;
        var skipped = result.Unverifiable.Count;

        // Entries left behind because their drive is away are always named.
        // Reporting "removed 3" while silently passing over another forty
        // would leave the user with a false picture of the playlist.
        var aside = skipped == 0
            ? string.Empty
            : $" — {skipped} left alone, {JoinRoots(RootsOf(result.Unverifiable))} not connected";

        StatusText = removed > 0
            ? $"Removed {removed} {Entries(removed)} from {name}{aside}"
            : skipped > 0
                ? $"{name}: nothing confirmed gone{aside}"
                : $"{name}: every entry's file is present.";

        PlaylistsChanged?.Invoke();
    }

    /// <summary>
    /// Scans the loaded video for clip boundaries and adds whatever the user
    /// accepts.
    /// </summary>
    /// <remarks>
    /// Nothing is applied by scanning alone. The dialog proposes and the user
    /// disposes — detection is a first guess, and one that quietly rewrote a
    /// bookmark list would be worse than no detection at all.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasActiveVideo))]
    private void DetectBookmarks()
    {
        if (string.IsNullOrWhiteSpace(Session.VideoPath)) return;

        var dlg = new DetectBookmarksDialog(_ffmpeg, Session.VideoPath, Session.Bookmarks.Count > 0,
                                            SnapToEditPoints)
        {
            Owner = Application.Current?.MainWindow,
            Report = s => StatusText = s
        };
        var accepted = dlg.ShowDialog() == true;

        // Harvested whichever way the dialog closed. The scan has already
        // paid for a full decode, and where it found the video changing is
        // worth keeping even when none of its proposals were wanted.
        TakeSnapPoints(dlg.Scanned);
        SnapToEditPoints = dlg.SnapToPoints && HasSnapPoints;

        if (!accepted) return;

        if (dlg.ReplaceExisting) Session.Bookmarks.Clear();

        // A scan's own proposals never overlap each other, but added to a list
        // that is already there they can land on top of it. Those are skipped
        // rather than refusing the lot: the rest of the scan is still worth
        // having, and the cuts already on the list are the ones that were
        // placed deliberately.
        var skipped = 0;
        foreach (var r in dlg.Accepted)
        {
            if (OverlappingCut(r.Start, r.End) is not null)
            {
                skipped++;
                continue;
            }

            Session.Bookmarks.Add(new Bookmark
            {
                Index = Session.Bookmarks.Count + 1,
                StartSeconds = r.Start,
                EndSeconds = r.End
            });
        }

        // Indexes are positional, so anything appended after a replace needs
        // them rewritten rather than continued.
        int i = 1;
        foreach (var b in Session.Bookmarks) b.Index = i++;

        HookSession(Session);
        Session.NotifyDurationChanged();

        // A scan creates the bookmark file the same way the first hand-placed
        // timestamp does. Leaving it unwritten left "loaded" false over a full
        // list, and everything gated on it — remove selected, select all/none,
        // play selected, merge, split, flip — stayed disabled no matter how
        // many rows were checked.
        SaveBookmarks();

        RefreshCommandStates();

        var added = dlg.Accepted.Count - skipped;
        ReportSuccess(skipped == 0
            ? $"Added {added} {Entries(added)} from the scan"
            : $"Added {added} {Entries(added)} from the scan — skipped {skipped} that " +
              $"overlapped cuts already in the list");
    }

    /// <summary>
    /// Replaces <see cref="SnapPoints"/> with the edges of the ranges given.
    /// </summary>
    /// <remarks>
    /// A range's start and end are each a point where the video changed, so
    /// both go in; the two that fall on the very start and end of the file do
    /// not, since nothing can be dragged past them anyway and a tick there is
    /// a tick on the frame of the bar. Near-duplicates are collapsed — one
    /// range's end and the next one's start are usually the same instant seen
    /// twice.
    /// </remarks>
    private void TakeSnapPoints(IEnumerable<DetectedRange> ranges)
    {
        var duration = Session.VideoDurationSeconds;

        var points = ranges
            .SelectMany(r => new[] { r.Start, r.End })
            .Where(s => s > 0.05 && (duration <= 0 || s < duration - 0.05))
            .OrderBy(s => s)
            .ToList();

        SnapPoints.Clear();

        var last = double.NegativeInfinity;
        foreach (var p in points)
        {
            if (p - last < 0.05) continue;
            SnapPoints.Add(p);
            last = p;
        }
    }

    /// <summary>
    /// Scans for edit points without proposing any cuts, purely to give the
    /// timeline something for its brackets to snap to.
    /// </summary>
    /// <remarks>
    /// The same dialog that proposes cuts, since choosing what to look for and
    /// how hard is the same question either way — the result is simply kept
    /// rather than applied. Someone who wants the cuts as well already has
    /// "Find clips automatically", which now fills these in on its way past.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasActiveVideo))]
    private void ScanEditPoints()
    {
        if (string.IsNullOrWhiteSpace(Session.VideoPath)) return;

        var dlg = new DetectBookmarksDialog(_ffmpeg, Session.VideoPath, Session.Bookmarks.Count > 0,
                                            SnapToEditPoints)
        {
            Owner = Application.Current?.MainWindow,
            Report = s => StatusText = s
        };
        dlg.ShowDialog();

        TakeSnapPoints(dlg.Scanned);

        // Only if the scan actually left something. Ticking the box and then
        // finding nothing would otherwise switch on a behaviour with no marks
        // to act on.
        SnapToEditPoints = dlg.SnapToPoints && HasSnapPoints;

        // The dialog has already said what the scan found. Nothing to add: a
        // second line here would only restate it in different words a moment
        // later, which reads as a correction rather than as detail.
    }

    /// <summary>
    /// Fills in a frame for every cut the storyboard will draw a card for.
    /// </summary>
    /// <remarks>
    /// One at a time rather than all at once: each is an ffmpeg call, and a
    /// dozen of them started together would fight each other for the disk and
    /// arrive no sooner. Cards fill in from the top as they come, which also
    /// reads as progress.
    ///
    /// Already-drawn cuts are skipped, so toggling the view back and forth
    /// costs nothing the second time.
    /// </remarks>
    private async Task RefreshStoryboardThumbnailsAsync()
    {
        var video = Session.VideoPath;
        if (string.IsNullOrWhiteSpace(video)) return;

        var wantsEnd = StoryboardLayout == StoryboardLayout.Filmstrip;

        foreach (var b in Session.Bookmarks.ToList())
        {
            if (!b.IsValid) continue;
            if (b.Thumbnail != null && (!wantsEnd || b.ThumbnailEnd != null)) continue;

            // A different video can be loaded while this runs, which makes the
            // rest of the pass frames of the wrong film.
            if (video != Session.VideoPath) return;

            try
            {
                b.Thumbnail ??= await _thumbnails.GetAsync(video, b.StartSeconds);

                // A hair inside the cut rather than on its closing mark: the
                // end is the first instant the clip no longer covers, and a
                // frame fetched exactly there belongs to whatever follows.
                if (wantsEnd && b.ThumbnailEnd is null)
                    b.ThumbnailEnd = await _thumbnails.GetAsync(video, Math.Max(b.StartSeconds, b.EndSeconds - 0.25));
            }
            catch
            {
                // A frame that will not decode leaves an empty card, which is
                // a great deal better than taking the view down with it.
            }
        }
    }

    /// <summary>
    /// Writes the whole video out once with the bookmarks attached as chapters,
    /// instead of cutting it into pieces.
    /// </summary>
    /// <remarks>
    /// For material you want to keep whole but navigate — the bookmarks become
    /// chapter marks a player can jump between. Streams are copied, so it is
    /// quick and lossless whatever the cut-accuracy setting says.
    /// </remarks>
    /// <summary>
    /// Writes the cut list in a format another tool can read.
    /// </summary>
    /// <remarks>
    /// None of these carries what a cut actually is here — the speed, the
    /// rotation, the crop, the fades all stay behind, because a list of start
    /// and end times has nowhere to put them. The program's own CSV is still
    /// the only format that keeps a cut whole, which is why this writes
    /// somewhere else rather than offering to replace it.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanExportCutList))]
    private async Task ExportCutList()
    {
        var usable = Session.Bookmarks.Where(b => b.IsValid).ToList();
        if (usable.Count == 0)
        {
            ReportFailure("No complete cuts to export.");
            return;
        }

        var stem = string.IsNullOrWhiteSpace(Session.VideoPath)
            ? "cuts"
            : Path.GetFileNameWithoutExtension(Session.VideoPath);

        var sfd = new SaveFileDialog
        {
            Title = "Export bookmarks",
            Filter = "CSV — start, end, name|*.csv|" +
                     "YouTube chapters|*.txt|" +
                     "EDL — edit decision list|*.edl",
            FileName = stem + ".csv",
            InitialDirectory = string.IsNullOrWhiteSpace(Session.VideoPath)
                ? null
                : Path.GetDirectoryName(Session.VideoPath)
        };
        if (sfd.ShowDialog() != true) return;

        try
        {
            // FilterIndex is one-based and says which of the three was chosen,
            // which matters because two of them are plain text and the name
            // alone cannot tell them apart.
            var text = sfd.FilterIndex switch
            {
                2 => CutListFormats.WriteYouTube(usable),
                3 => CutListFormats.WriteEdl(usable, await SourceFrameRate(), stem),
                _ => CutListFormats.WriteCsv(usable)
            };

            await File.WriteAllTextAsync(sfd.FileName, text);

            var what = sfd.FilterIndex switch
            {
                2 => "chapters",
                3 => "EDL records",
                _ => "rows"
            };

            ReportSuccess($"Exported {usable.Count} {what} → {Path.GetFileName(sfd.FileName)}");

            // Said after the write, not instead of it. The file is correct
            // either way; YouTube is simply particular about when it will show
            // chapters at all, and finding that out from a silent video page
            // later is worse than hearing it now.
            if (sfd.FilterIndex == 2 && CutListFormats.YouTubeProblems(usable) is { Count: > 0 } problems)
                ShowInfo("The file is written, but YouTube will not turn these into chapters:\n\n• " +
                         string.Join("\n• ", problems),
                         "Export bookmarks");
        }
        catch (Exception ex)
        {
            StatusText = "Export failed";
            ShowError(ex.Message, "Export bookmarks");
        }
    }

    /// <summary>
    /// Reads a cut list written somewhere else.
    /// </summary>
    /// <remarks>
    /// The format is worked out from what is in the file rather than from its
    /// name: two of the three are plain text, and a cut list arrives called
    /// whatever the tool that wrote it chose.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanImportCutList))]
    private async Task ImportCutList()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Import bookmarks",
            Filter = "Cut lists|*.csv;*.txt;*.edl|All files|*.*",
            InitialDirectory = string.IsNullOrWhiteSpace(Session.VideoPath)
                ? null
                : Path.GetDirectoryName(Session.VideoPath)
        };
        if (ofd.ShowDialog() != true) return;

        List<CutListFormats.Entry> entries;
        CutListFormats.Kind kind;

        try
        {
            var text = await File.ReadAllTextAsync(ofd.FileName);
            kind = CutListFormats.Detect(text);
            entries = CutListFormats.Read(text, await SourceFrameRate(), Session.VideoDurationSeconds);
        }
        catch (Exception ex)
        {
            StatusText = "Import failed";
            ShowError(ex.Message, "Import bookmarks");
            return;
        }

        if (entries.Count == 0)
        {
            // The one format with a real dependency on the video. A chapter
            // list says where each heading starts and never where the last one
            // ends, so without a duration to run it to there is nothing to
            // give the final cut — and on a one-chapter list that is the whole
            // file. Worth naming, because "nothing readable" would be a lie
            // about a file that parsed perfectly well.
            if (kind == CutListFormats.Kind.YouTube && Session.VideoDurationSeconds <= 0)
                ReportFailure("Chapter lists need the video's length to size the last chapter, " +
                              "and it is not known yet.");
            else
                ReportFailure($"Nothing readable in {Path.GetFileName(ofd.FileName)}.");

            return;
        }

        if (kind == CutListFormats.Kind.YouTube && Session.VideoDurationSeconds <= 0)
            ShowInfo("The last chapter has no end, because the video's length is not known yet. " +
                     "Everything before it was imported.", "Import bookmarks");

        // Only asked when there is something to lose. See RelocatePlaylistEntries
        // for the same reasoning about a question that answers itself.
        var replace = false;
        if (Session.Bookmarks.Count > 0)
        {
            var answer = MessageBox.Show(
                DialogOwner!,
                $"{Path.GetFileName(ofd.FileName)} holds {entries.Count} " +
                $"{(entries.Count == 1 ? "cut" : "cuts")}, read as {Describe(kind)}.\n\n" +
                "Replace the cuts currently loaded? Choosing No adds them to the list instead.",
                "Import bookmarks",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel) return;
            replace = answer == MessageBoxResult.Yes;
        }

        var before = CaptureBookmarks();
        if (replace) Session.Bookmarks.Clear();

        var skipped = 0;
        foreach (var e in entries)
        {
            if (OverlappingCut(e.Start, e.End) is not null) { skipped++; continue; }

            Session.Bookmarks.Add(new Bookmark
            {
                Index = Session.Bookmarks.Count + 1,
                StartSeconds = e.Start,
                EndSeconds = e.End,
                Label = e.Label
            });
        }

        int i = 1;
        foreach (var b in Session.Bookmarks) b.Index = i++;

        _history.Push("the import", before);
        NotifyHistoryChanged();

        HookSession(Session);
        Session.NotifyDurationChanged();
        SaveBookmarks();
        RefreshCommandStates();

        var added = entries.Count - skipped;
        ReportSuccess(skipped == 0
            ? $"Imported {added} {Bookmarks(added)} from {Path.GetFileName(ofd.FileName)}"
            : $"Imported {added} {Bookmarks(added)} — skipped {skipped} that overlapped cuts already in the list");
    }

    private static string Describe(CutListFormats.Kind kind) => kind switch
    {
        CutListFormats.Kind.YouTube => "YouTube chapters",
        CutListFormats.Kind.Edl => "an edit decision list",
        _ => "a CSV"
    };

    /// <summary>
    /// The source's frame rate, for the one format that counts in frames.
    /// </summary>
    /// <remarks>
    /// 25 when it cannot be read, which is a guess but a declared one: an EDL
    /// has to name a rate, and the editor reading it conforms against the real
    /// file anyway.
    /// </remarks>
    private async Task<double> SourceFrameRate()
    {
        if (string.IsNullOrWhiteSpace(Session.VideoPath)) return 25;

        try
        {
            var (_, value) = await _ffmpeg.GetFrameRateRationalAsync(Session.VideoPath);
            return value > 0 ? value : 25;
        }
        catch
        {
            return 25;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportChapters))]
    private async Task ExportChapters()
    {
        var usable = Session.Bookmarks.Where(b => b.IsValid).ToList();
        if (usable.Count == 0 || string.IsNullOrWhiteSpace(Session.VideoPath))
        {
            StatusText = "Need a video and at least one complete bookmark.";
            return;
        }

        // MKV first: it carries chapters more reliably than MP4 across players.
        var sfd = new SaveFileDialog
        {
            Title = "Save video with chapters",
            Filter = "Matroska|*.mkv|MP4|*.mp4",
            FileName = Path.GetFileNameWithoutExtension(Session.VideoPath) + "[chapters].mkv",
            InitialDirectory = Path.GetDirectoryName(Session.VideoPath)
        };
        if (sfd.ShowDialog() != true) return;

        IsBusy = true;
        Job.Begin("Writing chapters");
        Job.SetFile(1, Path.GetFileName(sfd.FileName));
        var succeeded = false;
        try
        {
            var progress = new Progress<FFmpegProgressEventArgs>(e => Job.Report(e.Message, e.Current));
            await _ffmpeg.ExportChaptersAsync(Session.VideoPath, sfd.FileName, usable, progress);
            StatusText = $"Wrote {usable.Count} chapters → {Path.GetFileName(sfd.FileName)}";
            succeeded = true;
        }
        catch (Exception ex)
        {
            ReportFailure("Chapter export failed");
            MessageBox.Show(ex.Message, "Chapter export failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
            if (!succeeded) Job.End();
        }

        if (succeeded)
            Job.Complete($"Wrote {usable.Count} chapters into", Path.GetFileName(sfd.FileName));
    }

    private bool CanExportChapters() =>
        HasActiveVideo && Session.Bookmarks.Any(b => b.IsValid);

    /// <summary>
    /// Writing the list out needs a list, and nothing else.
    /// </summary>
    /// <remarks>
    /// Not <see cref="HasActiveVideo"/>, which both of these were gated on at
    /// first and neither has any business asking for: that predicate wants
    /// MPC-HC to be running, and exporting a text file does not involve the
    /// player at all. A bookmark CSV opened on its own, with no video anywhere,
    /// is a perfectly good thing to convert to an EDL.
    /// </remarks>
    private bool CanExportCutList() => Session.Bookmarks.Any(b => b.IsValid);

    /// <summary>
    /// Reading a list in needs somewhere to put it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CsvPath rather than a video, because that is the thing actually
    /// required. It is bound whenever a video is loaded and whenever a
    /// bookmark file is opened on its own, and it is what SaveBookmarks writes
    /// to — without one an import would appear to work, save nothing, and
    /// leave the cuts to evaporate.
    /// </para>
    /// <para>
    /// This is also why there is no "import into nothing". Loading a video
    /// clears the list and reads that video's own CSV over it, so cuts brought
    /// in beforehand would be discarded the moment one opened. Open the video
    /// or its bookmark file first, and the import has somewhere to live.
    /// </para>
    /// </remarks>
    private bool CanImportCutList() => !string.IsNullOrEmpty(Session.CsvPath);

    /// <summary>
    /// Finds the files of confirmed-gone entries somewhere else and repoints
    /// the playlist at them, instead of deleting the entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart to removal, and usually the one wanted: a file that is
    /// not where the playlist says has more often been moved than deleted.
    /// </para>
    /// <para>
    /// Asks which drive first, and tries the cheap answer before any searching:
    /// a disk that came back on a different letter has every file exactly where
    /// it was, so swapping the root and asking whether it is there costs one
    /// existence check per entry. That is the common case, and it finishes
    /// before the dialog has finished closing.
    /// </para>
    /// <para>
    /// Only what that leaves is worth a search, and the search is offered
    /// rather than started: reading every folder on a drive takes minutes, and
    /// is not something to begin on the user's behalf because they clicked a
    /// menu item that had already done something else.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The title every box this command raises carries — the menu entry that
    /// opened it, so the answer is attributable to the question.
    /// </summary>
    private const string FindMovedFiles = "Find moved files";

    [RelayCommand]
    private async Task RelocatePlaylistEntries(string? plsPath)
    {
        if (string.IsNullOrWhiteSpace(plsPath) || !File.Exists(plsPath))
        {
            StatusText = "Playlist file not found.";
            ShowError("That playlist file is no longer there.", FindMovedFiles);
            return;
        }

        var name = Path.GetFileName(plsPath);
        var classified = _playlists.ClassifyEntries(plsPath);

        // Everything not where the playlist says, whichever reason. The two
        // reasons matter very much to removal and not at all here: a file on a
        // drive that is no longer connected is the whole reason somebody opens
        // this, and counting only the confirmed-gone ones answered "nothing to
        // look for" to a playlist where every entry had lost its drive letter.
        var unreachable = classified.Count(e => e.Status != PlaylistEntryStatus.Present);

        if (unreachable == 0)
        {
            StatusText = $"{name}: every entry's file is where it says it is.";
            ShowInfo($"Every one of {name}'s entries points at a file that is there. " +
                     "Nothing needs finding.", FindMovedFiles);
            return;
        }

        var picker = new DriveSelectDialog(
            $"{name} has {unreachable} {Entries(unreachable)} whose files are not where it says " +
            "they are. If the drive holding them came back under a different letter, pick it here " +
            "and they will be found straight away.")
        {
            Owner = Application.Current?.MainWindow
        };
        if (picker.ShowDialog() != true) return;

        var chosen = picker.SelectedPath;
        var relettered = 0;

        // The filename search that may follow is narrower than the re-letter
        // pass on purpose — see RelocateMissingEntries — so its own count is
        // the confirmed-gone ones only.
        var gone = classified.Count(e => e.Status == PlaylistEntryStatus.Missing);

        // A folder was picked instead, so there is no drive to re-letter and
        // nothing to try before searching it.
        if (picker.IsWholeDrive)
        {
            var swapped = _playlists.RelocateToRoot(plsPath, chosen);
            relettered = swapped.Count;

            if (relettered > 0)
            {
                PlaylistsChanged?.Invoke();
                unreachable -= relettered;

                ReportSuccess(unreachable == 0
                    ? $"Found all {relettered} {Entries(relettered)} on {chosen} — {name} now points at them"
                    : $"Found {relettered} {Entries(relettered)} on {chosen}, {unreachable} still missing");

                if (unreachable == 0)
                {
                    ShowInfo($"All {relettered} {Entries(relettered)} were on {chosen} under the same " +
                             $"paths.\n\n{name} now points at them.", FindMovedFiles);
                    return;
                }

                // Re-lettering rewrote the file, so the statuses gathered
                // before it are stale — and an entry it moved onto the chosen
                // drive is now Present rather than something to search for.
                gone = _playlists.ClassifyEntries(plsPath)
                    .Count(e => e.Status == PlaylistEntryStatus.Missing);
            }
        }

        if (gone == 0)
        {
            StatusText = $"{name}: {unreachable} {Entries(unreachable)} are on a drive that is not " +
                         "connected, so there is nothing that can be searched for.";
            ShowInfo($"{unreachable} {Entries(unreachable)} were not found on {chosen}, and the drive " +
                     "they name is not connected — so there is no way to tell whether those files " +
                     "still exist.\n\nNothing was changed. Connect that drive, or pick the one the " +
                     "files are on now.", FindMovedFiles);
            return;
        }

        if (!AskToSearch(name, gone, chosen, relettered)) return;

        await SearchForMissingEntries(plsPath, name, gone, chosen, picker.IsWholeDrive);
    }

    /// <summary>
    /// Asks before committing the user to a search that reads every folder
    /// under what they picked.
    /// </summary>
    private static bool AskToSearch(string name, int gone, string where, int alreadyFound)
    {
        var opening = alreadyFound > 0
            ? $"{gone} {Entries(gone)} are not under {where} by their old paths."
            : $"None of {name}'s {gone} missing {Entries(gone)} are under {where} by their old paths.";

        var question = $"{opening}\n\n" +
                       $"Search {where} for them by filename instead? This reads every folder on it " +
                       "and can take a while on a large drive.";

        // Through DialogOwner rather than straight at MainWindow: the drive
        // picker has just closed, and an unowned box can end up behind the
        // window it disabled.
        var owner = DialogOwner;

        return (owner is not null
            ? MessageBox.Show(owner, question, FindMovedFiles,
                              MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(question, FindMovedFiles,
                              MessageBoxButton.YesNo, MessageBoxImage.Question))
            == MessageBoxResult.Yes;
    }

    /// <summary>
    /// The deep search: index every filename under the chosen place, then
    /// repoint whatever matches.
    /// </summary>
    private async Task SearchForMissingEntries(
        string plsPath, string name, int gone, string where, bool wholeDrive)
    {
        // The chosen place plus wherever the surviving entries already live: a
        // library that was reorganized usually moved files between folders it
        // is already using. Ones that sit inside the chosen place are dropped
        // by the index itself rather than walked twice.
        var roots = new List<string> { where };
        roots.AddRange(_playlists.ClassifyEntries(plsPath)
            .Where(e => e.Status == PlaylistEntryStatus.Present)
            .Select(e => Path.GetDirectoryName(e.Path) ?? string.Empty)
            .Where(d => d.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));

        // A whole drive is searched to the bottom. The four-level default
        // suits a folder someone pointed at, where the files are expected to
        // be near the top; a drive root reaches almost nothing at four, and
        // the user has just been told this one takes a while. The cap that
        // remains is only there so a junction pointing at its own ancestor
        // cannot spin forever.
        var depth = wholeDrive ? 64 : 4;

        // Constructed here, on the UI thread, so its callbacks come back to
        // the UI thread — the search itself runs on a worker and must not
        // touch StatusText directly.
        var progress = new Progress<PlaylistService.SearchProgress>(p =>
        {
            if (p.Folder.Length == 0) return;
            StatusText = $"Searching {where} — {p.Files:N0} files in {p.Folders:N0} folders";
        });

        StatusText = $"Searching {where}…";
        IsBusy = true;
        try
        {
            List<PlaylistService.RelocatedEntry> moved = new();
            await Task.Run(() => moved = _playlists.RelocateMissingEntries(
                plsPath, roots, depth, CancellationToken.None, progress));

            // Found nothing is not a failure — the search ran and answered.
            // It is simply not worth a flare either way.
            if (moved.Count == 0)
                StatusText = $"{name}: none of the {gone} missing {Entries(gone)} could be found under {where}.";
            else
                ReportSuccess($"Relocated {moved.Count} of {gone} {Entries(gone)} in {name}");

            if (moved.Count > 0) PlaylistsChanged?.Invoke();

            ShowInfo(
                moved.Count == 0
                    ? $"None of the {gone} missing {Entries(gone)} could be found anywhere under " +
                      $"{where}.\n\n{name} was left as it was."
                    : moved.Count == gone
                        ? $"All {gone} missing {Entries(gone)} were found under {where}.\n\n" +
                          $"{name} now points at them."
                        : $"{moved.Count} of the {gone} missing {Entries(gone)} were found under " +
                          $"{where}.\n\nThe other {gone - moved.Count} could not be, and were left " +
                          "pointing where they were.",
                FindMovedFiles);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>"entry" or "entries", so the prompt and status line read.</summary>
    private static string Entries(int count) => count == 1 ? "entry" : "entries";

    /// <summary>"bookmark" or "bookmarks", for the same reason.</summary>
    private static string Bookmarks(int count) => count == 1 ? "bookmark" : "bookmarks";

    /// <summary>The distinct drives a set of paths lives on, in order.</summary>
    private static List<string> RootsOf(IEnumerable<string> paths)
        => paths
            .Select(DriveAvailability.RootOf)
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Names drives for a sentence — <c>"H:\"</c>, <c>"H:\ and I:\"</c>,
    /// <c>"F:\, H:\ and I:\"</c> — so the status line can say which
    /// disconnected volume is responsible instead of gesturing at "some drive".
    /// </summary>
    private static string JoinRoots(List<string> roots) => roots.Count switch
    {
        0 => "their location",
        1 => roots[0],
        _ => string.Join(", ", roots.Take(roots.Count - 1)) + " and " + roots[^1]
    };

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
            Notify(string.IsNullOrEmpty(folder)
                    ? "Set a playlist folder first (Playlist ▸ Playlist folder…)."
                    : $"The playlist folder is no longer there:\n{folder}\n\nSet it again under Playlist ▸ Playlist folder….",
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
        var dlg = new CaptureHotkeyDialog(current) { Owner = DialogOwner };
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
            // The video goes in the file's header, which is what ties it to
            // this video rather than to its own filename. Empty for a CSV
            // opened on its own, and the save then keeps the header it had.
            _bookmarks.SaveToCsv(Session.CsvPath, Session.Bookmarks, Session.VideoPath);

            RememberBookmarkFile();

            // Writing the first timestamp is what brings the bookmark file
            // into existence, so this is where "loaded" becomes true.
            IsBookmarkFileLoaded = File.Exists(Session.CsvPath);
            RefreshBookmarksFileDisplay();
        }
        catch (Exception ex) { ReportFailure("Save failed: " + ex.Message); }
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
            Filter = "All supported|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.ts;*.m4v;*.csv;*.pls;*.m3u8;*.m3u|" +
                     "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.ts;*.m4v|" +
                     "Bookmark CSV|*.csv|" +
                     "Playlist|*.pls;*.m3u8;*.m3u|" +
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
    /// Opens a video, and nothing else.
    /// </summary>
    /// <remarks>
    /// <see cref="OpenFile"/> takes any of the three kinds of file and works
    /// out which it is from the extension, which is right for a File menu. The
    /// side panel's empty "Current video" line is a more specific question —
    /// there is no video and that line is where one would be named — so the
    /// picker asks for one rather than offering CSVs and playlists beside it.
    /// </remarks>
    [RelayCommand]
    private async Task OpenVideoFile()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.ts;*.m4v|" +
                     "All Files|*.*",
            Title = "Open a video"
        };

        if (ofd.ShowDialog() != true) return;

        var path = ofd.FileName;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "File not found.";
            return;
        }

        _mpc.LaunchVideo(path);
        await LoadVideoAsync(path);
    }

    /// <summary>
    /// Opens a bookmark CSV, and nothing else.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="OpenVideoFile"/>, for the panel's empty
    /// "Current bookmarks" line. Loading one may bring a video with it: a CSV
    /// is written beside the video it describes and named after it, which
    /// <see cref="LoadBookmarksFromCsv"/> takes advantage of.
    /// </remarks>
    [RelayCommand]
    private void OpenBookmarkFile()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Bookmark CSV|*.csv|All Files|*.*",
            Title = "Open a bookmark file"
        };

        if (ofd.ShowDialog() != true) return;

        var path = ofd.FileName;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "File not found.";
            return;
        }

        LoadBookmarksFromCsv(path);
    }

    /// <summary>
    /// Opens a playlist, and nothing else.
    /// </summary>
    /// <remarks>
    /// Every playlist format the program reads, which is more than the one the
    /// menu's own "Load playlist…" offers: .pls is what it writes, and .m3u and
    /// .m3u8 are what it will read from elsewhere.
    ///
    /// Named for loading rather than opening, because
    /// <see cref="OpenPlaylistFile"/> already means something else here — it
    /// hands the file to whatever Windows opens playlists with, which is a
    /// different thing from making it the one this program adds videos to.
    /// </remarks>
    [RelayCommand]
    private void LoadPlaylistFile()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Playlists|*.pls;*.m3u8;*.m3u|All Files|*.*",
            Title = "Open a playlist"
        };

        if (ofd.ShowDialog() != true) return;

        var path = ofd.FileName;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "File not found.";
            return;
        }

        LoadPlaylist(path);
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
        RememberBookmarkFile();

        // Opening a bookmark file replaces the list wholesale — see the note in
        // LoadVideoAsync for why that has to be the end of the history.
        ResetHistory();

        // Load the video these bookmarks were made against, if it can be
        // worked out. The file's header names it outright — that is what the
        // header is for, and it holds wherever the CSV has been moved to.
        var owner = _bookmarks.GetVideoPathFor(csvPath);
        if (!string.IsNullOrEmpty(owner) && File.Exists(owner))
        {
            _ = LoadVideoAsync(owner);
        }
        else
        {
            // No header, or it names a video that has since moved. Fall back to
            // the old convention — a video of the same name beside the CSV —
            // which is still how a file carried over from an earlier build, or
            // dropped in from a video folder, identifies itself.
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
                Filter = "Playlist|*.pls;*.m3u8;*.m3u|All Files|*.*",
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
            // Reported as the failure it is. Notify writes to the status bar
            // and nothing else, so clicking a dead entry put a line of text
            // somewhere the user had no reason to be looking — they had just
            // clicked a menu item and were waiting for a video to open.
            //
            // The bar keeps the whole path, which is what says which copy of a
            // name has gone; the toast carries the name alone, being four
            // inches wide.
            ReportFailure($"File not found — removed from History: {path}");
            ShowRefusal("Removed from History",
                        $"{Path.GetFileName(path)} no longer exists, so it has been taken off the list.");
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
        var dlg = new ManageShortcutsDialog(Shortcuts) { Owner = DialogOwner };
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
    /// <summary>
    /// Redraws every toolbar button small or flat, without saving anything.
    /// </summary>
    /// <remarks>
    /// The items carry both of these, so this is the whole of the change —
    /// each button rebinds its own picture, padding and label from them. Used
    /// by the live preview in Settings, by the cancel that undoes it, and by
    /// the save that makes it permanent, so the three cannot drift.
    /// </remarks>
    private void ApplyToolbarIconStyle(bool small, bool flat)
    {
        foreach (var item in ToolbarItems)
        {
            item.IsCompact = small;
            item.IsFlat = flat;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var dlg = new SettingsDialog(_settings.Current, AutoSwitchViews, _ffmpeg)
        {
            Owner = Application.Current?.MainWindow,
            PreviewToolbarIcons = ApplyToolbarIconStyle,
            PreviewChapterNames = PreviewChapterNames
        };

        var closed = dlg.ShowDialog();

        // Dropped first thing, either way. While it stands, UseChapterNames
        // answers for the dialog rather than for the settings, and from here
        // on the settings are the truth again — ApplyChapterNaming below is
        // what moves the list, and it raises the property itself.
        EndChapterNamesPreview();

        if (closed != true)
        {
            // Cancelled, so the preview has to come back off. The settings
            // were never written, which makes them the record of how the
            // toolbar looked before the dialog opened.
            ApplyToolbarIconStyle(_settings.Current.SmallButtonIcons,
                                  _settings.Current.FlatButtonIcons);
            return;
        }

        var s = _settings.Current;

        s.DefaultVideoFormat = VideoFormats.FromKey(dlg.VideoFormatKey).Key;
        s.DeleteOriginalVideo = dlg.DeleteOriginalVideo;
        s.DeleteBookmarksFile = dlg.DeleteBookmarksFile;
        s.DeleteToRecycleBin = dlg.DeleteToRecycleBin;
        s.Quality = dlg.Quality;
        s.VideoEncoder = dlg.VideoEncoder;
        s.PreciseCuts = dlg.PreciseCuts;
        s.NormalizeAudio = dlg.NormalizeAudio;
        s.FadeSeconds = dlg.FadeSeconds;
        s.NameTemplate = dlg.NameTemplate;

        // The dialog already applied one of these live so it could be seen;
        // this is what makes it survive a restart. ThemeKey is saved either
        // way — it is what "Follow system theme" reverts to once turned off.
        s.ThemeKey = dlg.ThemeKey;
        s.FollowSystemTheme = dlg.FollowSystemTheme;
        ThemeService.ApplyFromKey(dlg.FollowSystemTheme ? SystemTheme.ResolveThemeKey() : dlg.ThemeKey);
        s.OnNameCollision = dlg.OnNameCollision;
        s.PollSpeed = dlg.PollSpeed;
        s.MpcWebInterfacePort = dlg.MpcWebInterfacePort;
        s.AutoDetectMpcWebInterface = dlg.AutoDetectMpcWebInterface;
        s.FfmpegFolder = dlg.FfmpegFolder;
        s.ToastsEnabled = dlg.ToastsEnabled;
        s.CompletionSound = dlg.CompletionSound;

        // The items carry this, so changing it means rebuilding the row.
        var iconsChanged = s.SmallButtonIcons != dlg.SmallButtonIcons
                           || s.FlatButtonIcons != dlg.FlatButtonIcons;
        s.SmallButtonIcons = dlg.SmallButtonIcons;
        s.FlatButtonIcons = dlg.FlatButtonIcons;
        s.ToastSeconds = dlg.ToastSeconds;
        s.CheckForUpdates = dlg.CheckForUpdates;

        // Captured before the assignment: turning the waveform back on needs
        // to know it was off, since nothing was decoded for it while it was.
        var waveformWasOff = !s.ShowSeekbarWaveform;
        s.ShowSeekbar = dlg.ShowSeekbar;
        s.ShowSeekbarWaveform = dlg.ShowSeekbarWaveform;

        // Captured before the assignment: applying it needs to know which way
        // it moved, and turning it off throws names away.
        var chapterNamesWereOn = s.UseChapterNames;
        s.UseChapterNames = dlg.UseChapterNames;
        s.RememberSaveToFolder = dlg.RememberSaveToFolder;
        s.OverlayCorner = dlg.OverlayCorner;

        var runModeChanged = s.RunMode != dlg.RunMode;
        s.RunMode = dlg.RunMode;
        s.AllowMultipleInstances = dlg.AllowMultipleInstances;
        s.OverlayOpacity = dlg.OverlayOpacity;
        s.OverlayClickable = dlg.OverlayClickable;
        s.MaxHistory = dlg.MaxHistory;

        // Turning "remember" on adopts whatever folder is pinned right now,
        // rather than waiting for the user to pick one again. Turning it off
        // forgets the stored folder so it cannot come back on a later restart.
        s.SaveToFolder = dlg.RememberSaveToFolder ? _pinnedSaveToFolder : "";

        _settings.Save();

        // After the save, so the list and the file on disk agree with the
        // setting that has just been written.
        ApplyChapterNaming(chapterNamesWereOn, dlg.UseChapterNames);

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
        OnPropertyChanged(nameof(ShowSeekbar));
        OnPropertyChanged(nameof(HideSeekbar));
        OnPropertyChanged(nameof(ShowSeekbarWaveform));

        // Nothing was decoded while it was off, so turning it back on needs a
        // fresh render rather than whatever stale picture Waveform still holds.
        if (waveformWasOff && dlg.ShowSeekbarWaveform)
            _ = RefreshWaveformAsync(Session.VideoPath);

        OnPropertyChanged(nameof(OverlayCorner));
        OnPropertyChanged(nameof(OverlayOpacity));
        OnPropertyChanged(nameof(OverlayClickable));

        // The View owns the tray icon, so it is told rather than asked.
        if (runModeChanged)
        {
            OnPropertyChanged(nameof(RunMode));
            RunModeChanged?.Invoke();
        }

        // Flipped on the items that are already there rather than rebuilt from
        // the saved layout: a rebuild would reset the select button's caption,
        // which tracks the current selection and not the stored toolbar.
        if (iconsChanged) ApplyToolbarIconStyle(s.SmallButtonIcons, s.FlatButtonIcons);

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

    // ------------------------------------------------------------------
    // Update checks
    // ------------------------------------------------------------------

    /// <summary>
    /// The check made at launch. Silent unless there is something to act on.
    /// </summary>
    /// <remarks>
    /// Awaited by nobody — startup does not wait on the network, and a machine
    /// that is offline when the program opens has not encountered a problem
    /// worth reporting. Only a genuinely newer release produces a window.
    /// </remarks>
    public async Task RunStartupUpdateCheckAsync()
    {
        if (!_settings.Current.CheckForUpdates) return;

        var result = await UpdateCheckService.CheckAsync();
        if (result.Status != UpdateCheckStatus.UpdateAvailable) return;

        ShowUpdateAvailable(result);
    }

    /// <summary>
    /// Help ▸ Check for updates. Answers either way, because someone who asked
    /// is owed a reply — silence would read as a broken menu item.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        StatusText = "Checking for a new version…";

        var result = await UpdateCheckService.CheckAsync();

        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                StatusText = $"Version {result.LatestVersion} is available.";
                ShowUpdateAvailable(result);
                break;

            case UpdateCheckStatus.UpToDate:
                StatusText = $"Up to date — {AppVersion.Display} is the latest release.";
                MessageBox.Show(
                    $"You are running {AppVersion.Display}, which is the latest release.",
                    "Check for updates", MessageBoxButton.OK, MessageBoxImage.Information);
                break;

            default:
                StatusText = "Could not check for a new version.";
                MessageBox.Show(
                    "The check could not be completed.\n\n" +
                    $"{result.Error}\n\n" +
                    "The releases page is at:\n" + UpdateCheckService.ReleasesPageUrl,
                    "Check for updates", MessageBoxButton.OK, MessageBoxImage.Warning);
                break;
        }
    }

    /// <summary>
    /// Puts the notice on screen and persists the opt-out if it is taken.
    /// </summary>
    /// <remarks>
    /// The window is shown without an owner when the main window is hidden —
    /// the view follows focus, and in tray mode there may be nothing on screen
    /// to own it. An owner that is not visible would center the notice on
    /// nothing and, worse, could place it behind the player.
    /// </remarks>
    private void ShowUpdateAvailable(UpdateCheckResult result)
    {
        var dlg = new UpdateAvailableDialog(
            result.LatestVersion ?? "", AppVersion.Display, result.ReleaseUrl, result.Highlights);

        if (Application.Current?.MainWindow is { IsVisible: true } owner)
            dlg.Owner = owner;
        else
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        dlg.Closed += (_, _) =>
        {
            if (!dlg.StopChecking) return;

            _settings.Current.CheckForUpdates = false;
            _settings.Save();
            StatusText = "Update checks are off. Settings ▸ General ▸ Updates turns them back on.";
        };

        dlg.Show();
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
        var text = PromptForSuffixText("Add tag", "Enter the tag text (letters and numbers only, max 50 chars).\n\nIt will be wrapped in brackets in filenames, e.g. [done]", "");
        if (text == null) return;

        if (_settings.AddSuffix(text))
        {
            Suffixes.Add(new SuffixEntry(text));
            UpdateActiveSuffixDisplay();
            StatusText = $"Tag added: [{text}]";
        }
        else
        {
            Notify("That tag already exists.", "Add tag",
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
            Notify("Select a tag to rename, or use Manage tags.",
                "Rename tag", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var text = PromptForSuffixText("Rename tag",
            $"New text for:\n{entry.Display}\n\nLetters and numbers only, max 50 chars.",
            entry.Text);
        if (text == null || text == entry.Text) return;

        if (_settings.RenameSuffix(entry.Text, text))
        {
            entry.Text = text;
            UpdateActiveSuffixDisplay();

            // The button is keyed on the name; the service has just pointed
            // the saved layout at the new one, and this redraws the row from
            // it. See ManageSuffixes for the same pair.
            BuildToolbar();
            StatusText = $"Tag renamed to: [{text}]";
        }
        else
        {
            Notify("Could not rename — a tag with that text already exists.",
                "Rename tag", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            Notify("Select a tag to remove, or use Manage tags.",
                "Remove tag", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_settings.RemoveSuffix(entry.Text))
        {
            var match = Suffixes.FirstOrDefault(s =>
                string.Equals(s.Text, entry.Text, StringComparison.OrdinalIgnoreCase));
            if (match != null) Suffixes.Remove(match);
            UpdateActiveSuffixDisplay();

            // Takes the deleted tag's button off the row with it.
            BuildToolbar();
            StatusText = $"Removed tag: {entry.Display}";
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
            StatusText = $"Active tag: {entry.Display}";
        }
    }

    /// <summary>
    /// Stops output names carrying a naming tag. The "None" entry in the Options
    /// menu.
    /// </summary>
    /// <remarks>
    /// A tag was mandatory: the list always held at least one and something in it
    /// was always active, so every file this program wrote gained a bracket
    /// whether that was wanted or not.
    ///
    /// Choosing None leaves the previously-selected tag in the list and selected,
    /// so picking it up again is one click rather than a hunt.
    ///
    /// Worth saying plainly at the point of choosing, because it changes what a
    /// collision means: with no tag, output written beside its source wants the
    /// source's own name, and the overwrite prompt is then the only thing between
    /// a split and the original video. Everything still goes through that prompt
    /// — see ResolveOutputPathAsync — so nothing is replaced silently.
    /// </remarks>
    [RelayCommand]
    private void ClearActiveSuffix()
    {
        _settings.ClearActiveSuffix();
        UpdateActiveSuffixDisplay();
        StatusText = "No naming tag — output keeps the source's name, " +
                     "and you will be asked before anything is replaced";
    }

    /// <summary>
    /// Opens the full Manage Suffixes dialog (add / rename / remove /
    /// drag-reorder). Persists the final order on close.
    /// </summary>
    [RelayCommand]
    private void ManageSuffixes()
    {
        // Who was called what on the way in. The dialog edits these entries in
        // place rather than going through the service, so a rename made in
        // there never reaches RenameSuffix — and the object identity is the
        // only thing that tells a rename from a removal followed by an add.
        var namesBefore = Suffixes.Select(e => (Entry: e, Text: e.Text)).ToList();

        var dlg = new ManageSuffixesDialog(Suffixes) { Owner = DialogOwner };
        dlg.ShowDialog();

        // A tag's toolbar button is keyed on the tag's text, so a rename has
        // to be carried into the saved layout or the key stops matching and
        // the button silently leaves the toolbar.
        foreach (var (entry, was) in namesBefore)
            if (Suffixes.Contains(entry) && !string.Equals(was, entry.Text, StringComparison.Ordinal))
                _settings.RenameToolbarTag(was, entry.Text);

        // Removals need no such help — the key simply stops matching anything
        // and the build drops it.
        _settings.ReorderSuffixes(Suffixes);
        UpdateActiveSuffixDisplay();

        // The toolbar is rebuilt from the layout against the catalogue, which
        // is read from the tag list. Nothing did this when the tags changed:
        // a deleted tag kept its button until the next launch, and the
        // Customize dialog went on offering it as something to place.
        BuildToolbar();

        StatusText = $"{Suffixes.Count} tag(s) saved";
    }

    /// <summary>
    /// Validation + input loop for suffix text. Returns the validated
    /// text, or null if the user canceled. Enforces:
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

    // ------------------------------------------------------------------
    // Undo / redo
    // ------------------------------------------------------------------

    private readonly UndoHistory<IReadOnlyList<BookmarkState>> _history = new(50);

    /// <summary>The list as it stood after the last recorded change.</summary>
    /// <remarks>
    /// Kept so that recording never has to reconstruct "before" at the moment
    /// of the change — by then the change has already happened. Every push uses
    /// this, and then replaces it.
    /// </remarks>
    private IReadOnlyList<BookmarkState> _historyBaseline = Array.Empty<BookmarkState>();

    /// <summary>Set while the history itself is rewriting the list.</summary>
    private bool _suspendHistory;

    /// <summary>When the last entry was pushed, for coalescing.</summary>
    private DateTime _lastRecordedAt = DateTime.MinValue;

    /// <summary>
    /// Changes closer together than this join the entry already on the stack.
    /// </summary>
    /// <remarks>
    /// Dragging the speed slider raises a change per pixel, and deleting three
    /// cuts raises three collection events; without this, one gesture would
    /// need dozens of undos to reverse. Distinct actions by a human are seconds
    /// apart, so the window can be generous enough to catch a whole gesture and
    /// still never merge two intentions.
    /// </remarks>
    private static readonly TimeSpan HistoryCoalesceWindow = TimeSpan.FromMilliseconds(600);

    public bool CanUndoEdit => _history.CanUndo;
    public bool CanRedoEdit => _history.CanRedo;

    /// <summary>Menu text, so the entry names what it will actually reverse.</summary>
    public string UndoEditHeader => _history.NextUndo is { } d ? $"Undo {d}" : "Undo";
    public string RedoEditHeader => _history.NextRedo is { } d ? $"Redo {d}" : "Redo";

    /// <summary>
    /// How many changes either side of the present the history list shows.
    /// </summary>
    /// <remarks>
    /// The stack holds fifty. All fifty in a menu is a column taller than most
    /// screens, and the far end of it is somewhere nobody navigates to by
    /// reading. Twenty each way is more than a session's worth of fiddling with
    /// one cut, and Ctrl+Z still walks past the end of what is listed.
    /// </remarks>
    private const int HistoryEntriesShown = 20;

    /// <summary>
    /// The history as a list to read, newest first.
    /// </summary>
    /// <remarks>
    /// Undo on its own only ever offers the one step, so stepping back six
    /// changes means pressing it six times and watching the list jump, with no
    /// way to see where you are going until you arrive. This says what each
    /// step was and lets one click cross all of them.
    ///
    /// No row for the present. It was there to mark the middle of a flat list
    /// read in two directions, and every row already says which way it goes —
    /// "Undo the crop" above, "Redo the fade" below — so the marker was
    /// telling the reader something the rows had already told them.
    /// </remarks>
    public IReadOnlyList<HistoryEntry> HistoryEntries
    {
        get
        {
            var rows = new List<HistoryEntry>();

            foreach (var step in _history.Timeline)
            {
                if (Math.Abs(step.Offset) > HistoryEntriesShown) continue;

                var offset = step.Offset;
                rows.Add(new HistoryEntry(
                    offset < 0 ? $"Undo {step.Description}" : $"Redo {step.Description}",
                    offset)
                {
                    Command = new RelayCommand(() => GoToHistoryStep(offset))
                });
            }

            return rows;
        }
    }

    /// <summary>Whether there is any history worth showing a list of.</summary>
    public bool HasHistory => _history.CanUndo || _history.CanRedo;

    /// <summary>
    /// Jumps to a point in the history, however many changes away it is.
    /// </summary>
    /// <remarks>
    /// Every change crossed is pushed onto the other side as it goes, so a jump
    /// of six is six ordinary steps and is reversed by stepping forward again.
    /// Nothing about arriving in one click makes the way back any harder.
    /// </remarks>
    private void GoToHistoryStep(int offset)
    {
        if (offset == 0) return;
        if (!_history.TryMove(offset, CaptureBookmarks(), out var restore)) return;

        RestoreBookmarks(restore);
        NotifyHistoryChanged();

        var n = Math.Abs(offset);
        var changes = n == 1 ? "1 change" : $"{n} changes";
        StatusText = offset < 0 ? $"Stepped back {changes}" : $"Stepped forward {changes}";
    }

    /// <summary>
    /// Every bookmark currently subscribed to, so subscribing is idempotent
    /// and nothing is left attached.
    /// </summary>
    /// <remarks>
    /// Needed for two reasons that both bite silently. <c>Clear()</c> raises a
    /// Reset carrying no OldItems, so there is no way to unsubscribe the
    /// departing bookmarks from the event args alone — without this set they
    /// would stay attached to a ViewModel that outlives them. And restoring a
    /// snapshot adds bookmarks that the collection handler subscribes too, so
    /// an unguarded <c>+=</c> would attach a second handler on every undo, each
    /// one recording the same change again.
    /// </remarks>
    private readonly HashSet<Bookmark> _watchedBookmarks = new();

    private void WatchBookmark(Bookmark b)
    {
        if (_watchedBookmarks.Add(b)) b.PropertyChanged += Bookmark_PropertyChangedForHistory;
    }

    private void UnwatchBookmark(Bookmark b)
    {
        if (_watchedBookmarks.Remove(b)) b.PropertyChanged -= Bookmark_PropertyChangedForHistory;
    }

    private void UnwatchAllBookmarks()
    {
        foreach (var b in _watchedBookmarks) b.PropertyChanged -= Bookmark_PropertyChangedForHistory;
        _watchedBookmarks.Clear();
    }

    /// <summary>Starts watching the bookmark list for anything worth recording.</summary>
    private void StartWatchingForHistory()
    {
        Session.Bookmarks.CollectionChanged += Bookmarks_CollectionChangedForHistory;
        foreach (var b in Session.Bookmarks) WatchBookmark(b);
        _historyBaseline = CaptureBookmarks();
    }

    private void Bookmarks_CollectionChangedForHistory(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            UnwatchAllBookmarks();
            foreach (var b in Session.Bookmarks) WatchBookmark(b);
        }
        else
        {
            if (e.OldItems != null)
                foreach (Bookmark b in e.OldItems) UnwatchBookmark(b);

            if (e.NewItems != null)
                foreach (Bookmark b in e.NewItems) WatchBookmark(b);
        }

        RecordChange(e.Action switch
        {
            NotifyCollectionChangedAction.Add => "add",
            NotifyCollectionChangedAction.Remove => "delete",
            NotifyCollectionChangedAction.Reset => "replace the cuts",
            _ => "edit the cuts"
        });
    }

    private void Bookmark_PropertyChangedForHistory(object? sender, PropertyChangedEventArgs e)
    {
        // Index is assigned by Renumber as a consequence of some other change,
        // and the derived display properties are not state at all.
        switch (e.PropertyName)
        {
            case nameof(Bookmark.Index):
            case null:
                return;

            // Selection is carried in the snapshot so that undo restores what
            // was ticked, but ticking a box is not itself worth a step on the
            // stack: it changes nothing about the output. The baseline moves
            // with it so the next real edit records the selection as it is now.
            case nameof(Bookmark.IsSelected):
                if (!_suspendHistory) _historyBaseline = CaptureBookmarks();
                return;

            case nameof(Bookmark.StartSeconds):
            case nameof(Bookmark.EndSeconds):
                RecordChange("the timing");
                return;

            case nameof(Bookmark.Speed):
                RecordChange("the speed");
                return;

            case nameof(Bookmark.Label):
                RecordChange("the name");
                return;

            case nameof(Bookmark.IsFlipped):
                RecordChange("the flip");
                return;

            case nameof(Bookmark.Rotation):
                RecordChange("the rotation");
                return;

            case nameof(Bookmark.IsMuted):
                RecordChange("the mute");
                return;

            case nameof(Bookmark.FadeInSeconds):
            case nameof(Bookmark.FadeOutSeconds):
                RecordChange("the fade");
                return;

            // All four arrive together from SetCrop, and the coalescing window
            // joins them into the one step a single rectangle deserves.
            case nameof(Bookmark.CropX):
            case nameof(Bookmark.CropY):
            case nameof(Bookmark.CropWidth):
            case nameof(Bookmark.CropHeight):
                RecordChange("the crop");
                return;

            // Everything else is derived — Prefix, RowMarkers, the display
            // strings — and recording those would push a step for every one of
            // the several notifications a single change raises.
            default:
                return;
        }
    }

    /// <summary>
    /// Pushes the pre-change state, unless this change belongs with the one
    /// before it.
    /// </summary>
    private void RecordChange(string description)
    {
        if (_suspendHistory) return;

        var now = DateTime.UtcNow;
        if (now - _lastRecordedAt < HistoryCoalesceWindow)
        {
            // Same gesture. The entry already on the stack holds the state from
            // before it started, which is where undo should land, so only the
            // baseline moves.
            _lastRecordedAt = now;
            _historyBaseline = CaptureBookmarks();
            return;
        }

        _history.Push(description, _historyBaseline);
        _historyBaseline = CaptureBookmarks();
        _lastRecordedAt = now;
        NotifyHistoryChanged();
    }

    private List<BookmarkState> CaptureBookmarks() =>
        Session.Bookmarks.Select(BookmarkState.From).ToList();
    /// <summary>
    /// Says why a split did not happen, in the bar and over the picture.
    /// </summary>
    /// <remarks>
    /// A toast as well as the status bar, because of where this is pressed
    /// from. Split time is on the toolbar and in a menu, but it is aimed at
    /// the playhead — so it is used while watching, with the player in front
    /// and this window behind it. A refusal written only into a status bar
    /// nobody can see is a button that did nothing.
    ///
    /// Through ShowRefusal, which the hotkey's own refusals use: forced past
    /// "toasts off" and held long enough to read, for the same reason. The
    /// setting asks for less noise from an action that worked.
    /// </remarks>
    private void RefuseSplit(string reason)
    {
        ReportFailure(reason);
        ShowRefusal("Could not split", reason);
    }

    /// <summary>
    /// Puts a boundary in the cut list wherever the player is.
    /// </summary>
    /// <remarks>
    /// The arithmetic is <see cref="PlayheadSplit"/>; this is the part that has
    /// to talk to the player, the list and the file. Applied through
    /// <see cref="RestoreBookmarks"/>, which already knows how to swap the
    /// whole list out without the history recording a change per row — the
    /// result here is a new list rather than an edit to one, for the same
    /// reason an undo is.
    ///
    /// One history entry, pushed by hand. Left to itself the collection handler
    /// would describe this as "add", which is true of the mechanism and not of
    /// what the user did.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasActiveVideo))]
    private void SplitAtPlayhead()
    {
        if (IsBusy)
        {
            RefuseSplit("Another operation is running — wait for it to finish.");
            return;
        }

        var (playhead, playerDuration) = _mpc.GetPlaybackPosition();
        if (playhead <= 0)
        {
            RefuseSplit("The player is not reporting a position to split at.");
            return;
        }

        // Either source will do. ffprobe fills this in on load and the poll
        // keeps it in step with the player, but both can be a moment behind a
        // video that has only just opened.
        var duration = Session.VideoDurationSeconds > 0
            ? Session.VideoDurationSeconds
            : playerDuration;

        var before = CaptureBookmarks();
        var result = PlayheadSplit.At(before, playhead, duration);
        var at = Bookmark.FormatTime(playhead);

        if (result.Outcome is not (PlayheadSplit.Outcome.Divided or PlayheadSplit.Outcome.Filled))
        {
            RefuseSplit(result.Outcome switch
            {
                PlayheadSplit.Outcome.AlreadySplit =>
                    $"There is already a bookmark boundary at {at}",
                PlayheadSplit.Outcome.TooShort =>
                    $"Splitting at {at} would leave a piece too short to be a clip",
                PlayheadSplit.Outcome.OutOfRange =>
                    $"{at} is outside the part of the video that can be marked",
                _ =>
                    "The video's length is not known yet — try again in a moment"
            });
            return;
        }

        // Recorded against the list as it was, so one undo puts it back.
        _history.Push("the split", before);
        NotifyHistoryChanged();

        RestoreBookmarks(result.Cuts);

        // A split on a video with no cuts at all is what brings the bookmark
        // file into existence, the same way the first hand-placed timestamp
        // does — and RestoreBookmarks only saves a file that already exists.
        if (!IsBookmarkFileLoaded) SaveBookmarks();

        RefreshCommandStates();
        ReportSuccess($"Split at {at} — {Session.Bookmarks.Count} {Bookmarks(Session.Bookmarks.Count)} now");
    }

    /// <summary>Rebuilds the list from a snapshot, without recording it.</summary>
    private void RestoreBookmarks(IReadOnlyList<BookmarkState> state)
    {
        _suspendHistory = true;
        try
        {
            // The collection's own handler subscribes what goes in, so nothing
            // is attached by hand here — see WatchBookmark's remarks.
            Session.Bookmarks.Clear();

            int i = 1;
            foreach (var s in state)
                Session.Bookmarks.Add(s.ToBookmark(i++));

            _historyBaseline = CaptureBookmarks();

            // Not through Renumber: that would reapply the chapter-naming rules
            // and rename cuts the snapshot deliberately restored.
            Session.NotifyDurationChanged();
            RefreshCommandStates();
        }
        finally
        {
            _suspendHistory = false;
        }

        if (IsBookmarkFileLoaded) SaveBookmarks();
    }

    /// <summary>
    /// Forgets the history. Called when the list stops being the same list.
    /// </summary>
    private void ResetHistory()
    {
        _history.Clear();
        _historyBaseline = CaptureBookmarks();
        _lastRecordedAt = DateTime.MinValue;
        NotifyHistoryChanged();
    }

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndoEdit));
        OnPropertyChanged(nameof(CanRedoEdit));
        OnPropertyChanged(nameof(UndoEditHeader));
        OnPropertyChanged(nameof(RedoEditHeader));
        OnPropertyChanged(nameof(HistoryEntries));
        OnPropertyChanged(nameof(HasHistory));
        UndoEditCommand.NotifyCanExecuteChanged();
        RedoEditCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndoEdit))]
    private void UndoEdit()
    {
        var description = _history.NextUndo;
        if (!_history.TryUndo(CaptureBookmarks(), out var restore)) return;

        RestoreBookmarks(restore);
        NotifyHistoryChanged();
        StatusText = $"Undid {description}";
    }

    [RelayCommand(CanExecute = nameof(CanRedoEdit))]
    private void RedoEdit()
    {
        var description = _history.NextRedo;
        if (!_history.TryRedo(CaptureBookmarks(), out var restore)) return;

        RestoreBookmarks(restore);
        NotifyHistoryChanged();
        StatusText = $"Redid {description}";
    }

    /// <summary>
    /// What the Settings dialog is showing while it is open, when that is not
    /// yet what the settings say. Null at every other moment.
    /// </summary>
    private bool? _previewChapterNames;

    /// <summary>
    /// Whether ranges are named. Bound by the row template to show or hide the
    /// name box, so the column appears the moment the box is ticked.
    /// </summary>
    public bool UseChapterNames => _previewChapterNames ?? _settings.Current.UseChapterNames;

    /// <summary>
    /// Shows or hides the name column while the Settings dialog is open.
    /// </summary>
    /// <remarks>
    /// The column only. Turning the setting off for real clears every name,
    /// hand-written ones included, and writes the bookmark file — see
    /// <see cref="ApplyChapterNaming"/> — and a preview may do none of that,
    /// because the dialog can still be cancelled and a cleared name is not
    /// something Cancel could give back.
    ///
    /// So the answer is held beside the setting rather than in it, and the
    /// rows are told to look again. Turning it on previews empty boxes: the
    /// names are made on Save, by the same path that makes them when the
    /// setting is changed any other way.
    /// </remarks>
    private void PreviewChapterNames(bool on)
    {
        if (_previewChapterNames == on) return;

        _previewChapterNames = on;
        OnPropertyChanged(nameof(UseChapterNames));
    }

    /// <summary>
    /// Drops the preview, whichever way the dialog was closed.
    /// </summary>
    private void EndChapterNamesPreview()
    {
        if (_previewChapterNames is null) return;

        _previewChapterNames = null;
        OnPropertyChanged(nameof(UseChapterNames));
    }

    /// <summary>The name a range takes when nobody has given it one.</summary>
    private static string DefaultChapterName(int index) => $"Chapter {index}";

    /// <summary>
    /// Matches a name this app assigned itself, so it can be told apart from
    /// one the user typed.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex AutoChapterName =
        new(@"^Chapter \d+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Reassigns row numbers, and with them the automatic chapter names.
    /// </summary>
    /// <remarks>
    /// Only names this app generated are re-derived. Deleting clip 1 should
    /// renumber "Chapter 2" to "Chapter 1", but a clip the user renamed to
    /// "the good bit" keeps that name wherever it lands — the point of letting
    /// them rename it is that the name is theirs.
    /// </remarks>
    private void Renumber()
    {
        int i = 1;
        var naming = _settings.Current.UseChapterNames;

        foreach (var b in Session.Bookmarks)
        {
            b.Index = i++;

            if (!naming) continue;
            if (!b.HasLabel || AutoChapterName.IsMatch(b.Label!))
                b.Label = DefaultChapterName(b.Index);
        }
    }

    /// <summary>
    /// Brings the open list into line after the chapter-names setting changes.
    /// </summary>
    /// <remarks>
    /// Turning it off clears every name, hand-written ones included. That is
    /// what the setting says it does, and what the dialog warns about — the
    /// alternative, keeping invisible names on disk that reappear later, is the
    /// kind of state nobody can reason about.
    /// </remarks>
    private void ApplyChapterNaming(bool wasOn, bool isOn)
    {
        if (wasOn == isOn) return;

        OnPropertyChanged(nameof(UseChapterNames));

        if (isOn)
        {
            Renumber();
            StatusText = "Chapter names on — every completed range now carries one.";
        }
        else
        {
            foreach (var b in Session.Bookmarks) b.Label = null;
            StatusText = "Chapter names off — the names have been cleared.";
        }

        if (IsBookmarkFileLoaded) SaveBookmarks();
    }

    // ------------------------------------------------------------------
    // Per-clip adjustments
    // ------------------------------------------------------------------

    /// <summary>
    /// Turns every checked cut a quarter further round.
    /// </summary>
    /// <remarks>
    /// One target for the whole selection, taken from the first cut in it.
    /// Advancing each clip from its own current rotation would leave a mixed
    /// selection permanently out of step, and no amount of clicking would ever
    /// bring them back together — the same reasoning as
    /// <see cref="ToggleFlip"/>'s "turn them all on".
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanToggleFlip))]
    private void RotateSelected()
    {
        var selected = ModifierTargets();
        if (selected.Count == 0 && NothingToModify()) return;

        var next = Bookmark.NextRotation(selected[0].Rotation);
        foreach (var b in selected) b.Rotation = next;

        Session.NotifyDurationChanged();
        if (IsBookmarkFileLoaded) SaveBookmarks();

        // Qualified: System.Windows.Media.Imaging has a Rotation of its own,
        // and both namespaces are in scope here.
        StatusText = next == Models.Rotation.None
            ? $"{selected.Count} cut(s) back to their original orientation"
            : $"{selected.Count} cut(s) {Bookmark.DescribeRotation(next)}";
    }

    /// <summary>
    /// Silences every checked cut, or restores the audio if they are all
    /// already silent.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleFlip))]
    private void ToggleMute()
    {
        var selected = ModifierTargets();
        if (selected.Count == 0 && NothingToModify()) return;

        var turningOn = !selected.All(b => b.IsMuted);
        foreach (var b in selected) b.IsMuted = turningOn;

        Session.NotifyDurationChanged();
        if (IsBookmarkFileLoaded) SaveBookmarks();

        StatusText = turningOn
            ? $"{selected.Count} cut(s) will be written silent"
            : $"{selected.Count} cut(s) will keep their audio";
    }

    /// <summary>
    /// Fades every checked cut up at the start and down at the end, or takes
    /// the fades off if they all already have them.
    /// </summary>
    /// <remarks>
    /// Both ends at once, at the length in Settings ▸ Output. See
    /// <see cref="ApplyFade"/> for the other two.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanToggleFlip))]
    private void ToggleFade() => ApplyFade(FadeEnds.Both);

    /// <summary>Fades the start of the selected cuts, and only the start.</summary>
    [RelayCommand(CanExecute = nameof(CanToggleFlip))]
    private void FadeIn() => ApplyFade(FadeEnds.In);

    /// <summary>Fades the end of the selected cuts, and only the end.</summary>
    [RelayCommand(CanExecute = nameof(CanToggleFlip))]
    private void FadeOut() => ApplyFade(FadeEnds.Out);

    /// <summary>Which ends of a cut a fade is wanted on.</summary>
    private enum FadeEnds { In, Out, Both }

    /// <summary>
    /// Puts a fade on the ends named, and takes it off the ends not named.
    /// </summary>
    /// <remarks>
    /// Three buttons where there was one. The old comment here argued against
    /// exactly that — a pair that "nearly always get pressed together" — and
    /// said a cut fading at one end only could be written by hand into the
    /// bookmark file. That was true while every fade was something a person
    /// placed deliberately. Splitting a cut produces one-ended fades on its
    /// own, as the only way to divide a faded clip without dipping to nothing
    /// in the middle of it, so the state needs to be reachable from the
    /// interface that can now create it.
    ///
    /// The model already carried the two lengths separately and FadeDisplay
    /// already read them out as "fade in 1s" — the capability was there and
    /// only the commands were missing.
    ///
    /// Each button sets its own mode outright rather than toggling one end
    /// independently: "fade in" means in and not out, which is what makes the
    /// three of them readable as three answers to one question. Pressing the
    /// one a selection is already in clears the fades, which is the only way
    /// back to no fade at all.
    ///
    /// "All of them are already exactly this" rather than per-cut toggling,
    /// the same rule as <see cref="ToggleFlip"/> and <see cref="ToggleMute"/>:
    /// advancing each cut from its own state would leave a mixed selection
    /// permanently out of step.
    /// </remarks>
    private void ApplyFade(FadeEnds ends)
    {
        var selected = ModifierTargets();
        if (selected.Count == 0 && NothingToModify()) return;

        var length = _settings.Current.FadeSeconds;
        var wantIn = ends != FadeEnds.Out;
        var wantOut = ends != FadeEnds.In;

        var turningOff = selected.All(b =>
            (b.FadeInSeconds > 0) == wantIn && (b.FadeOutSeconds > 0) == wantOut);

        // Turning them on with the length set to zero would report a fade and
        // apply nothing. The setting is the thing to change, so say so.
        if (!turningOff && length <= 0)
        {
            Notify("The fade length is set to zero — see Settings ▸ Output.");
            return;
        }

        foreach (var b in selected)
        {
            b.FadeInSeconds = !turningOff && wantIn ? length : 0;
            b.FadeOutSeconds = !turningOff && wantOut ? length : 0;
        }

        Session.NotifyDurationChanged();
        if (IsBookmarkFileLoaded) SaveBookmarks();

        var what = ends switch
        {
            FadeEnds.In => $"fade in over {length:0.##}s",
            FadeEnds.Out => $"fade out over {length:0.##}s",
            _ => $"fade in and out over {length:0.##}s"
        };

        StatusText = turningOff
            ? $"{selected.Count} cut(s) will start and end hard"
            : $"{selected.Count} cut(s) will {what}";
    }

    /// <summary>
    /// Opens a still from the first chosen cut and lets a box be drawn on it,
    /// then gives that rectangle to every chosen cut.
    /// </summary>
    /// <remarks>
    /// One rectangle for the selection rather than one each. Cropping a set of
    /// cuts is nearly always the same decision applied to all of them — black
    /// bars, or a shape to publish in — and asking per cut would turn a batch
    /// into a queue of near-identical dialogs. A single cut is still a
    /// selection of one, so cropping just one works the same way.
    ///
    /// The still comes from the cut's own start, carrying its flip and
    /// rotation, so the box is drawn on the picture that cut will produce.
    /// </remarks>
    [RelayCommand]
    private async Task CropSelected()
    {
        // The loaded video if there is one, otherwise whichever is picked. A
        // crop needs a picture and nothing else, so having no session is a
        // reason to ask rather than a reason to refuse.
        var video = Session.VideoPath;
        var loaded = !string.IsNullOrWhiteSpace(video) && File.Exists(video);

        if (!loaded)
        {
            var pick = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.ts;*.m4v|All Files|*.*",
                Title = "Select a video to crop"
            };
            if (pick.ShowDialog() != true) return;

            video = pick.FileName;
        }

        // Cuts belong to the loaded video, so a file picked just now has none
        // whatever the list happens to be holding.
        var chosen = loaded ? ModifierTargets() : new List<Bookmark>();
        var everyCut = loaded ? Session.Bookmarks.Where(b => b.IsValid).ToList() : new List<Bookmark>();

        // What the cuts option would do, or nothing when there are no cuts to
        // put a rectangle on. Deliberately picked cuts are the default; "all of
        // them" is offered but not assumed, being a bigger claim than the user
        // has made.
        var targets = chosen.Count > 0 ? chosen : everyCut;
        var cutsOption = targets.Count == 0 ? null
            : chosen.Count > 0
                ? $"Apply to the {Cuts(chosen.Count)} chosen"
                : $"Apply to all {Cuts(everyCut.Count)}";

        // The frame to draw on: the first cut's start when there are cuts,
        // otherwise a tenth of the way in. Not the very first frame, which on
        // a great many videos is black and tells you nothing about where to
        // put the box.
        var first = targets.FirstOrDefault();
        var duration = loaded ? Session.VideoDurationSeconds : await SafeDurationAsync(video);
        var at = first?.StartSeconds ?? (duration > 0 ? duration * 0.1 : 0);

        StatusText = "Fetching a frame to crop…";

        // Full height: the still is drawn several hundred pixels across and a
        // 76-pixel thumbnail would be too coarse to aim at.
        var png = await _ffmpeg.ExtractFrameAsync(video, at, height: 0, orientLike: first);
        var frame = png is null ? null : LoadImage(png);

        if (frame is null)
        {
            StatusText = "Could not read a frame from the video.";
            return;
        }

        // The still already carries the cut's rotation, so its own size is the
        // one the rectangle is measured against — no need to ask the file and
        // swap the sides by hand.
        var dlg = new CropDialog(frame, (int)frame.Width, (int)frame.Height,
                                 first?.CropX ?? 0, first?.CropY ?? 0,
                                 first?.CropWidth ?? 1, first?.CropHeight ?? 1,
                                 cutsOption, cutsByDefault: chosen.Count > 0)
        {
            Owner = DialogOwner
        };

        if (dlg.ShowDialog() != true)
        {
            StatusText = string.Empty;
            return;
        }

        if (dlg.CropsWholeVideo)
        {
            await CropWholeVideoAsync(video, dlg.CropX, dlg.CropY, dlg.CropWidth, dlg.CropHeight);
            return;
        }

        foreach (var b in targets)
            b.SetCrop(dlg.CropX, dlg.CropY, dlg.CropWidth, dlg.CropHeight);

        if (IsBookmarkFileLoaded) SaveBookmarks();

        var kept = $"{dlg.CropWidth * 100:0}% × {dlg.CropHeight * 100:0}%";

        StatusText = targets[0].HasCrop
            ? $"{Cuts(targets.Count)} cropped to {kept} of the frame"
            : $"{Cuts(targets.Count)} back to the whole frame";
    }

    /// <summary>"1 cut" or "3 cuts".</summary>
    private static string Cuts(int count) => count == 1 ? "1 cut" : $"{count} cuts";

    /// <summary>
    /// Writes a cropped copy of a whole video, with the naming, collision
    /// handling and progress panel every other write goes through.
    /// </summary>
    /// <remarks>
    /// No cleanup prompt. A cropped copy looks like a replacement but is not
    /// one — the part trimmed off exists nowhere else — so the original is left
    /// alone whatever the Cleanup setting says, as it is for the animations and
    /// the thumbnails.
    /// </remarks>
    private async Task CropWholeVideoAsync(string video, double x, double y, double w, double h)
    {
        var outDir = ResolveSaveToDirectory();
        if (string.IsNullOrWhiteSpace(outDir)) outDir = Path.GetDirectoryName(video) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(outDir))
        {
            Notify("Could not work out where to write the cropped video.", "Crop",
                   MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Directory.CreateDirectory(outDir);

        var outPath = await ResolveOutputPathAsync(
            GetSuffixedOutputPath(video, OutputFormat.Extension, outDir));
        if (outPath == null) return;

        IsBusy = true; ProgressPercent = 0;
        Job.Begin("Cropping video", 1);
        Job.SetFile(0, Path.GetFileName(video));
        BeginNameBatch(1);

        var written = false;

        try
        {
            var progress = new Progress<FFmpegProgressEventArgs>(p =>
            {
                Job.Report(p.Message, p.Percent);
                ProgressPercent = p.Percent;
            });

            await _ffmpeg.CropVideoAsync(video, outPath, x, y, w, h, progress);
            written = true;

            StatusText = $"Cropped to {w * 100:0}% × {h * 100:0}% — wrote {Path.GetFileName(outPath)}";
        }
        catch (Exception ex)
        {
            ReportFailure("Crop failed");
            MessageBox.Show(ex.Message, "Crop", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false; ProgressPercent = 0;
            if (!written) Job.End();
        }

        if (written) Job.Complete("Cropped video written to", outDir);
    }

    /// <summary>A PNG in memory as something the view can draw.</summary>
    private static BitmapImage? LoadImage(byte[] png)
    {
        try
        {
            using var stream = new MemoryStream(png);

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;

            // Loaded outright rather than kept against the stream, which is
            // disposed on the way out of this method.
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Export cuts as animations
    // ------------------------------------------------------------------

    /// <summary>The last animation choice, preselected on the next export.</summary>
    private AnimationChoice _lastAnimationChoice = new(Webp: false, Fps: 15, Width: 480);

    /// <summary>
    /// Writes each checked cut as an animated GIF or WebP.
    /// </summary>
    /// <remarks>
    /// Shares <see cref="CanSplitSelected"/>'s requirements — a video, a
    /// bookmark file and at least one complete cut — because it is the same
    /// operation with a different encoder on the end. It writes from the checked
    /// cuts, and falls back to all of them when none are checked, the way Split
    /// does.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSplitSelected))]
    private async Task ExportAnimationAsync()
    {
        var cuts = Session.Bookmarks.Where(b => b.IsSelected && b.IsValid).ToList();
        if (cuts.Count == 0) cuts = Session.Bookmarks.Where(b => b.IsValid).ToList();
        if (cuts.Count == 0) { Notify("No complete cuts to export."); return; }

        var outDir = ResolveSaveToDirectory();
        if (string.IsNullOrWhiteSpace(outDir))
        {
            Notify("Could not determine where to save the animations.", "Export as animation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Directory.CreateDirectory(outDir);

        // Named so the dialog can say what the animation will inherit. An export
        // that quietly applied a rotation the user had forgotten about is a
        // surprise; one that says so first is a feature.
        var carried = cuts.Where(b => b.Prefix.Length > 0).Select(b => b.Prefix).Distinct().ToList();
        var appliedNote = carried.Count switch
        {
            0 => string.Empty,
            1 => $"The cut's own settings are applied: {carried[0]}.",
            _ => "Each cut's own flip, rotation, speed and fades are applied."
        };

        var dlg = new ExportAnimationDialog(_lastAnimationChoice, cuts.Count, appliedNote)
        {
            Owner = DialogOwner
        };
        if (dlg.ShowDialog() != true) return;
        _lastAnimationChoice = dlg.Choice;

        var choice = dlg.Choice;
        var written = 0;
        var label = choice.Webp ? "WebP" : "GIF";

        IsBusy = true; ProgressPercent = 0;
        Job.Begin($"Exporting {label}", cuts.Count);
        BeginNameBatch(cuts.Count);

        try
        {
            var i = 0;
            foreach (var b in cuts)
            {
                i++;
                _batchRemaining = cuts.Count - i + 1;
                StatusText = $"Exporting {b.StartDisplay} → {b.EndDisplay} as {label}";
                Job.SetFile(i - 1, Path.GetFileName(Session.VideoPath));

                var outPath = await ResolveOutputPathAsync(
                    BuildSplitPath(outDir, Session.VideoFileName, i, choice.Extension));
                if (outPath == null) continue;

                var slice = 100.0 / cuts.Count;
                var basePct = (i - 1) * slice;
                var progress = new Progress<FFmpegProgressEventArgs>(p =>
                {
                    Job.Report(p.Message, basePct + p.Percent / 100.0 * slice);
                    ProgressPercent = basePct + p.Percent / 100.0 * slice;
                });

                await _ffmpeg.ExportAnimationAsync(Session.VideoPath, outPath, b,
                                                   choice.Webp, choice.Fps, choice.Width, progress);
                written++;
            }

            StatusText = $"Wrote {written} {label} file(s) to {outDir}";
        }
        catch (Exception ex)
        {
            ReportFailure($"{label} export failed");
            ShowError(ex.Message, $"{label} export failed");
        }
        finally
        {
            IsBusy = false; ProgressPercent = 0;
            if (written == 0) Job.End();
        }

        // No cleanup call. The source is still the only copy of the footage —
        // an animation is a derivative to send somewhere, not a replacement for
        // the video, so deleting the original after one would be wrong whatever
        // the Cleanup setting says.
        if (written > 0) Job.Complete($"Exported {written} {label} file(s) to", outDir);
    }

    // ------------------------------------------------------------------
    // Thumbnails through the whole video
    // ------------------------------------------------------------------

    /// <summary>
    /// A video's length, or zero when it cannot be read. Only the count shown
    /// in the dialog depends on it, so a file ffprobe chokes on is worth
    /// carrying on with rather than refusing.
    /// </summary>
    private async Task<double> SafeDurationAsync(string path)
    {
        try { return await _ffmpeg.GetDurationAsync(path); }
        catch { return 0; }
    }

    /// <summary>The last thumbnail choice, preselected on the next run.</summary>
    private double _thumbEverySeconds = 10;
    private int _thumbWidth = 320;

    /// <summary>Columns on a sheet, or zero for a picture per frame.</summary>
    private int _thumbColumns;

    /// <summary>
    /// Takes a frame every so many seconds through a video and writes them out,
    /// either as numbered pictures or tiled onto sheets.
    /// </summary>
    /// <remarks>
    /// The interval is what is asked for, not a number of thumbnails: "one
    /// every ten seconds" means the same thing on a two-minute clip and a
    /// two-hour film.
    ///
    /// Never disabled. It works off a file rather than off the session, so with
    /// a video loaded it acts on that one and with nothing loaded it asks for
    /// one — a button greyed out because no video is open would be refusing to
    /// do something it is perfectly able to do.
    ///
    /// One ffmpeg pass writes the lot — the file is decoded once and the
    /// <c>fps</c> filter picks the frames out of it, rather than seeking to
    /// each in turn.
    /// </remarks>
    [RelayCommand]
    private async Task SaveThumbnails()
    {
        var video = Session.VideoPath;
        var loaded = !string.IsNullOrWhiteSpace(video) && File.Exists(video);

        if (!loaded)
        {
            var pick = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.ts;*.m4v|All Files|*.*",
                Title = "Select a video to take thumbnails from"
            };
            if (pick.ShowDialog() != true) return;

            video = pick.FileName;
        }

        // The session's duration belongs to the session's video. For one picked
        // here it has to be read, and it is only wanted for the count the
        // dialog shows — so a file ffprobe cannot read still opens the dialog,
        // without the count.
        var duration = loaded
            ? Session.VideoDurationSeconds
            : await SafeDurationAsync(video);

        var dlg = new ThumbnailsDialog(duration, _thumbEverySeconds,
                                       _thumbWidth, _thumbColumns,
                                       ResolveSaveToDirectory(), _lastImageFolder)
        {
            Owner = DialogOwner
        };
        if (dlg.ShowDialog() != true) return;

        _thumbEverySeconds = dlg.EverySeconds;
        _thumbWidth = dlg.ThumbnailWidth;
        _thumbColumns = dlg.Columns;
        _lastImageFolder = dlg.OutputDirectory;

        // "Beside each picture" means beside the video here — there is only one
        // source, and it is the one thing these came out of.
        var outDir = string.IsNullOrWhiteSpace(_lastImageFolder)
            ? Path.GetDirectoryName(video) ?? string.Empty
            : _lastImageFolder;

        if (string.IsNullOrWhiteSpace(outDir))
        {
            Notify("Could not work out where to write the thumbnails.", "Save thumbnails",
                   MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Directory.CreateDirectory(outDir);

        var tiling = _thumbColumns > 0;
        var stem = Path.GetFileNameWithoutExtension(video) + SuffixBracket()
                   + (tiling ? "-sheet-" : "-thumb-");

        // ffmpeg fills the counter in, so the number of files does not have to
        // be known in advance — which it is not: the frame count comes out of
        // the decode, not out of the duration metadata.
        var pattern = Path.Combine(outDir, stem + "%03d.png");

        // A second run with the same settings would overwrite the first without
        // a word, and ffmpeg's -y is not a question. So it is asked here.
        var existing = Directory.GetFiles(outDir, stem + "*.png");
        if (existing.Length > 0)
        {
            var answer = MessageBox.Show(
                $"{existing.Length} file(s) from a previous run are already there and will be replaced.\n\nGo ahead?",
                "Save thumbnails", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes) return;
        }

        // Enough rows for everything, up to the cap; past that ffmpeg starts
        // another sheet, which is why the counter is in the pattern either way.
        var frames = duration > 0
            ? Math.Max(1, (int)(duration / _thumbEverySeconds))
            : ThumbnailsDialog.MaxRows * Math.Max(1, _thumbColumns);

        var rows = tiling ? ThumbnailsDialog.RowsPerSheet(frames, _thumbColumns) : 0;

        IsBusy = true; ProgressPercent = 0;

        // A sheet is one picture, written at the end, so there is no honest
        // percentage to show and no remaining time to estimate — the panel says
        // so in words instead. A run writing a picture per interval has real
        // progress and keeps the bar.
        Job.Begin(tiling ? "Building thumbnail sheets" : "Saving thumbnails", 1,
                  showsProgress: !tiling);
        Job.SetFile(0, Path.GetFileName(video));

        if (tiling) Job.Report("This will take a moment. Please wait.", 0);

        var written = 0;

        try
        {
            // Nothing to report for a sheet: the step is the sentence above, and
            // a percentage arriving behind a hidden bar would only overwrite it.
            var progress = tiling ? null : new Progress<FFmpegProgressEventArgs>(p =>
            {
                Job.Report(p.Message, p.Percent);
                ProgressPercent = p.Percent;
            });

            await _ffmpeg.SaveThumbnailsAsync(video, pattern, _thumbEverySeconds,
                                              _thumbWidth, _thumbColumns, rows, progress);

            written = Directory.GetFiles(outDir, stem + "*.png").Length;

            StatusText = written == 0
                ? "No thumbnails were produced — try a shorter interval."
                : $"Wrote {written} {(tiling ? "sheet(s)" : "thumbnail(s)")} to {outDir}";
        }
        catch (Exception ex)
        {
            ReportFailure("Thumbnails failed");
            MessageBox.Show(ex.Message, "Save thumbnails",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false; ProgressPercent = 0;
            if (written == 0) Job.End();
        }

        // No cleanup call: thumbnails are a derivative, and the video they came
        // from is still the only copy of the footage.
        //
        // Held longer than the usual three seconds. A run with no progress bar
        // gives nothing away while it works, so the summary at the end is the
        // only thing that says what happened and where it went.
        if (written > 0)
            Job.Complete($"Wrote {written} {(tiling ? "sheet(s)" : "thumbnail(s)")} to",
                         outDir, holdMs: 5000);
    }

    // ------------------------------------------------------------------
    // Save the frame on screen
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes the frame the player is sitting on to a PNG beside the other
    /// output.
    /// </summary>
    /// <remarks>
    /// At the frame's own size, not the 76-pixel thumbnail the panel asks for —
    /// the point of saving a still is to keep the whole thing.
    ///
    /// The position is read from the player rather than taken from
    /// <c>Session.CurrentTimeSeconds</c>, which is only as fresh as the last
    /// poll: at the slowest poll speed that can be a second and a half stale,
    /// and a still is asked for while looking at one particular frame. The
    /// cached value is the fallback for when the player cannot be reached.
    ///
    /// The time goes in the filename because these are taken in runs. A plain
    /// suffix would mean the collision dialog on every grab after the first,
    /// and answering "increment" to reach [done2], [done3] is a worse way to
    /// say "a different frame" than the timestamp is.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSaveCurrentFrame))]
    private async Task SaveCurrentFrameAsync()
    {
        var video = Session.VideoPath;
        if (string.IsNullOrWhiteSpace(video) || !File.Exists(video))
        {
            Notify("No video to take a frame from.");
            return;
        }

        var (live, _) = _mpc.GetPlaybackPosition();
        var seconds = live > 0 ? live : Session.CurrentTimeSeconds;

        // Spaces and colons are not filename material; the spoken form with the
        // gaps closed up reads as a time and survives as a name — "1m35s".
        var stamp = Bookmark.FormatSpoken(seconds).Replace(" ", "");

        var candidate = Path.Combine(
            ResolveSaveToDirectory() is { Length: > 0 } dir ? dir : Path.GetDirectoryName(video) ?? "",
            $"{Path.GetFileNameWithoutExtension(video)} {stamp}.png");

        var outPath = await ResolveOutputPathAsync(candidate);
        if (outPath == null) return;

        StatusText = $"Saving the frame at {Bookmark.FormatTime(seconds)}…";

        try
        {
            var png = await _ffmpeg.ExtractFrameAsync(video, seconds, height: 0);
            if (png is null || png.Length == 0)
            {
                StatusText = "That frame could not be read";
                Notify($"No frame could be read at {Bookmark.FormatTime(seconds)}.");
                return;
            }

            await File.WriteAllBytesAsync(outPath, png);
            StatusText = $"Saved {Path.GetFileName(outPath)}";
            _toast.Show("Frame saved", Path.GetFileName(outPath));
        }
        catch (Exception ex)
        {
            StatusText = "The frame could not be saved";
            ShowError(ex.Message, "Could not save the frame");
        }
    }

    // ------------------------------------------------------------------
    // Frame nudging
    // ------------------------------------------------------------------

    /// <summary>
    /// Moves one end of a cut by one second. The parameter names which end and
    /// which direction — "start-", "start+", "end-", "end+".
    /// </summary>
    /// <remarks>
    /// A whole second, and every timestamp is a whole number of them. The arrows
    /// moved a single frame at first, which was the wrong unit twice over: a
    /// frame is 0.04s at 25fps, so the row — which shows whole seconds — did not
    /// visibly change for twenty-four presses out of twenty-five, and the CSV
    /// holds whole seconds anyway, so the fraction was discarded on the next save
    /// regardless. A second moves the mark by the smallest amount the rest of the
    /// program can actually represent.
    ///
    /// A cut that is a second late is the common correction, and before these
    /// arrows the only way to make it was to retype the whole timestamp. The
    /// bookmark is taken from the command parameter rather than the selection so
    /// a row's own buttons act on that row.
    ///
    /// The two marks are kept a second apart: a range that closes before it
    /// opens is the state <see cref="Bookmark.IsIncomplete"/> exists to describe,
    /// and nudging into it would silently drop the row out of every selection.
    /// </remarks>
    /// <summary>
    /// The clearance a nudged timestamp keeps from whatever bounds it, which is
    /// also the distance one press moves it.
    /// </summary>
    private const double NudgeGapSeconds = 1.0;

    /// <summary>Slack for comparing two times that floating point has been near.</summary>
    private const double NudgeEpsilon = 1e-6;

    /// <summary>
    /// The existing cut that <paramref name="start"/> to <paramref name="end"/>
    /// would run into, or <c>null</c> when that span is clear.
    /// </summary>
    /// <param name="ignore">
    /// The cut being edited, which must not be found overlapping itself.
    /// </param>
    /// <remarks>
    /// <para>
    /// Cuts may not overlap. Two that share any of the file are two that will
    /// both be written out carrying the same footage, and the list stops being
    /// a description of an edit and becomes a set of claims about it that
    /// cannot all hold.
    /// </para>
    /// <para>
    /// The comparison is half-open — touching is not overlapping — so a cut may
    /// start exactly where the one before it ended. A bookmark still waiting
    /// for its closing timestamp occupies only the instant it was opened at,
    /// since it has no span yet to defend.
    /// </para>
    /// </remarks>
    private Bookmark? OverlappingCut(double start, double end, Bookmark? ignore = null)
    {
        foreach (var other in Session.Bookmarks)
        {
            if (ReferenceEquals(other, ignore)) continue;

            var otherStart = other.StartSeconds;
            var otherEnd = other.IsValid ? other.EndSeconds : other.StartSeconds;

            if (start < otherEnd && otherStart < end) return other;
        }

        return null;
    }

    /// <summary>
    /// The existing cut that <paramref name="time"/> falls inside, or
    /// <c>null</c> when nothing covers that instant.
    /// </summary>
    /// <remarks>
    /// A single time is not a span, and asking <see cref="OverlappingCut"/>
    /// about an empty one silently answered wrong. Its test is
    /// <c>start &lt; otherEnd &amp;&amp; otherStart &lt; end</c>, and with
    /// start and end both the same instant the second half is
    /// <c>otherStart &lt; time</c> — false at exactly a cut's opening. So a
    /// new bookmark opened on the very first second of an existing cut passed
    /// the overlap check and was added, leaving a second cut starting inside
    /// the first with nothing to say it could not be there.
    ///
    /// Half-open, matching the rule everywhere else: the opening instant is
    /// inside the cut, the closing instant is not. Landing exactly where a cut
    /// ends is how two cuts are made to touch, which is allowed.
    ///
    /// An incomplete bookmark covers nothing at all — it has no span yet to
    /// defend, and <c>time &lt; other.StartSeconds</c> is false for the one
    /// instant it does occupy.
    /// </remarks>
    private Bookmark? CutContaining(double time, Bookmark? ignore = null)
    {
        foreach (var other in Session.Bookmarks)
        {
            if (ReferenceEquals(other, ignore) || !other.IsValid) continue;

            if (time >= other.StartSeconds && time < other.EndSeconds) return other;
        }

        return null;
    }

    /// <summary>
    /// The latest time any bookmark has claimed — the end of the last cut, or
    /// the opening of one still waiting to be closed. Zero when the list is
    /// empty.
    /// </summary>
    private double LatestMarkedTime() =>
        Session.Bookmarks.Count == 0
            ? 0
            : Session.Bookmarks.Max(b => b.IsValid ? b.EndSeconds : b.StartSeconds);

    /// <summary>
    /// Why a new cut may not open at <paramref name="time"/>, or null when it
    /// may.
    /// </summary>
    /// <remarks>
    /// Cuts are marked going forwards. A new one has to open at or after the
    /// latest time already marked — not merely outside the existing cuts, which
    /// was the old rule and let a mark land in a gap behind the work already
    /// done.
    ///
    /// Being inside a cut is reported as its own case even though the forward
    /// rule already covers it. "That is inside cut 2" and "the last mark is at
    /// 4s" are answers to different questions, and the one that fits tells the
    /// user what to do next.
    ///
    /// Landing exactly on the latest time is allowed, because that is how one
    /// cut is made to start where the last one ended.
    /// </remarks>
    private string? WhyCannotOpenAt(double time)
    {
        if (CutContaining(time) is { } inside)
            return $"{Bookmark.FormatTime(time)} is inside cut {inside.Index} " +
                   $"({inside.StartDisplay} – {inside.EndDisplay})";

        var latest = LatestMarkedTime();
        if (time < latest)
            return $"{Bookmark.FormatTime(time)} is behind the last mark at " +
                   $"{Bookmark.FormatTime(latest)}";

        return null;
    }

    /// <summary>
    /// How far one end of a cut may be nudged before it would run into a
    /// neighbouring cut.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cuts may touch, so a neighbour's timestamp is a place a mark is allowed
    /// to land on, not one it has to stop short of. Along the file the times run
    /// start, end, start, end, and each is fenced by the two beside it in that
    /// run:
    /// </para>
    /// <list type="bullet">
    ///   <item>a start may reach the previous cut's end exactly, and stops a second below its own end;</item>
    ///   <item>an end may reach the next cut's start exactly, and stops a second above its own start.</item>
    /// </list>
    /// <para>
    /// The second of clearance is only ever between the two ends of one cut,
    /// which is the one pair of times that cannot meet: a cut has to be long
    /// enough to be a cut. Against a neighbour there is no clearance at all,
    /// because 1s–10s followed by 10s–20s is a legal pair and an arrow that
    /// vanished at 11s would be refusing to let you build it.
    /// </para>
    /// <para>
    /// An end is never measured against another end. Two ends are never
    /// adjacent — there is always a start between them — so the only end that
    /// could bound one is a cut further away, and stopping short of that one
    /// would leave the cut in between free to be overlapped anyway.
    /// </para>
    /// <para>
    /// Neighbours are taken in time order rather than row order. A bookmark set
    /// from the player is appended to the list and only sorted when the file is
    /// written, so the row above is not reliably the cut before.
    /// </para>
    /// <para>
    /// The video's length caps everything, less a second, so the last cut cannot
    /// end on the final instant of the file. An unknown length lifts the cap
    /// rather than setting it to zero: the duration arrives a moment after the
    /// video does, and clamping to it before it is known would pin every
    /// timestamp to the start of the file. A start with no closing timestamp
    /// yet has only that cap above it — there is no end to stay behind, which
    /// is the whole state of an open bookmark.
    /// </para>
    /// <para>
    /// Both bounds come back on a whole second — the floor rounded up, the
    /// ceiling rounded down — so a clamp against one can never leave a fraction
    /// behind. The video's length is the only fractional number in here, and
    /// a file 100.5 seconds long would otherwise cap an end at 99.5.
    /// </para>
    /// </remarks>
    private (double Floor, double Ceiling) NudgeRange(Bookmark b, bool movingStart)
    {
        var duration = Session.VideoDurationSeconds;
        var cap = duration > 0 ? duration - NudgeGapSeconds : double.PositiveInfinity;

        var ordered = Session.Bookmarks.OrderBy(x => x.StartSeconds).ToList();
        var index = ordered.IndexOf(b);

        double floor, ceiling;

        if (movingStart)
        {
            var previous = index > 0 ? ordered[index - 1] : null;

            // The cut before's own end, reachable exactly — or its start when it
            // is still open, which is the whole of the instant it occupies.
            floor = previous is null
                ? 0
                : previous.IsValid ? previous.EndSeconds : previous.StartSeconds;

            ceiling = b.IsValid ? Math.Min(cap, b.EndSeconds - NudgeGapSeconds) : cap;
        }
        else
        {
            var next = index >= 0 && index < ordered.Count - 1 ? ordered[index + 1] : null;

            floor = b.StartSeconds + NudgeGapSeconds;
            ceiling = next is null ? cap : Math.Min(cap, next.StartSeconds);
        }

        // Ceiling(floor) and Floor(ceiling): rounding each bound inwards to a
        // whole second keeps both of them somewhere a timestamp is allowed to be.
        // Infinity survives Math.Floor unchanged, which is what the unknown-length
        // case needs.
        return (Math.Ceiling(floor), Math.Floor(ceiling));
    }

    // ------------------------------------------------------------------
    // Dragging a bracket on the seekbar
    // ------------------------------------------------------------------

    /// <summary>
    /// The least clearance a dragged bracket ever keeps from its own other
    /// end. The visible brackets themselves shrink to fit a cramped cut
    /// rather than overlap (see <see cref="Helpers.TimelineRanges.DrawBracket"/>),
    /// so this is not sized against that — it is the shortest cut dragging is
    /// meant to be able to create at all, the same one second
    /// <see cref="NudgeGapSeconds"/> already holds the nudge arrows to.
    /// </summary>
    private const double DragMinGapSeconds = NudgeGapSeconds;

    /// <summary>
    /// The same floor and ceiling <see cref="NudgeRange"/> computes, but
    /// unrounded: a drag reads continuously off the pointer, where a nudge
    /// steps a whole second at a time, and rounding this to whole seconds
    /// would make a bracket jump between them rather than track the mouse.
    /// </summary>
    /// <remarks>
    /// Exposed so the View can clamp a drag in progress without touching the
    /// bookmark itself — see <see cref="CommitBracketDrag"/> for the one
    /// place a drag actually changes anything.
    /// </remarks>
    public (double Floor, double Ceiling) GetBracketDragRange(Bookmark b, bool movingStart)
    {
        var gap = DragMinGapSeconds;
        var duration = Session.VideoDurationSeconds;
        var cap = duration > 0 ? duration - gap : double.PositiveInfinity;

        var ordered = Session.Bookmarks.OrderBy(x => x.StartSeconds).ToList();
        var index = ordered.IndexOf(b);

        if (movingStart)
        {
            var previous = index > 0 ? ordered[index - 1] : null;
            var floor = previous is null
                ? 0
                : previous.IsValid ? previous.EndSeconds : previous.StartSeconds;
            var ceiling = b.IsValid ? Math.Min(cap, b.EndSeconds - gap) : cap;
            return (floor, ceiling);
        }
        else
        {
            var next = index >= 0 && index < ordered.Count - 1 ? ordered[index + 1] : null;
            var floor = b.StartSeconds + gap;
            var ceiling = next is null ? cap : Math.Min(cap, next.StartSeconds);
            return (floor, ceiling);
        }
    }

    /// <summary>
    /// Moves one end of a cut to where a bracket was dragged to, and saves.
    /// </summary>
    /// <remarks>
    /// The one place a drag actually touches the bookmark. While the drag is
    /// in progress the View only moves a visual mark — see
    /// <see cref="Helpers.TimelineRanges.DragBookmark"/> — so the clip preview
    /// and the undo stack each see one change for the whole gesture instead
    /// of one for every pixel the pointer crossed.
    /// </remarks>
    public void CommitBracketDrag(Bookmark b, bool movingStart, double proposedSeconds)
    {
        var (floor, ceiling) = GetBracketDragRange(b, movingStart);
        if (floor > ceiling) return;

        var clamped = Math.Clamp(proposedSeconds, Math.Max(0, floor), ceiling);

        if (movingStart) b.StartSeconds = clamped;
        else b.EndSeconds = clamped;

        Session.NotifyDurationChanged();
        if (IsBookmarkFileLoaded) SaveBookmarks();
    }

    /// <summary>
    /// Works out which of each row's four arrows still has room to move, so the
    /// ones that do not can be taken off the row.
    /// </summary>
    private void RefreshNudgeLimits()
    {
        foreach (var b in Session.Bookmarks)
        {
            var (startFloor, startCeiling) = NudgeRange(b, movingStart: true);
            b.CanNudgeStartBack = b.StartSeconds > startFloor + NudgeEpsilon;
            b.CanNudgeStartForward = b.StartSeconds < startCeiling - NudgeEpsilon;

            // A bookmark with no closing timestamp has no closing arrows; the
            // row does not draw that half at all.
            if (!b.IsValid)
            {
                b.CanNudgeEndBack = false;
                b.CanNudgeEndForward = false;
                continue;
            }

            var (endFloor, endCeiling) = NudgeRange(b, movingStart: false);
            b.CanNudgeEndBack = b.EndSeconds > endFloor + NudgeEpsilon;
            b.CanNudgeEndForward = b.EndSeconds < endCeiling - NudgeEpsilon;
        }
    }

    /// <summary>
    /// Moves one end of <paramref name="b"/> by one second, clamped to the room
    /// <see cref="NudgeRange"/> allows.
    /// </summary>
    /// <remarks>
    /// The bookmark is passed in rather than read from the selection. The
    /// arrows appear on the row the pointer is over, which is not necessarily
    /// the row that is selected — so taking the selection meant hovering any
    /// other row gave you four arrows that moved a different cut, or, with
    /// nothing selected at all, four arrows that did nothing.
    ///
    /// Clamped rather than refused at the boundary: the last press before a
    /// limit should land on the limit rather than be ignored for overshooting it.
    /// </remarks>
    public void NudgeSecond(Bookmark? b, string? request)
    {
        if (b is null || string.IsNullOrWhiteSpace(request)) return;

        var forward = request.EndsWith('+');
        var movingStart = request.StartsWith("start", StringComparison.OrdinalIgnoreCase);

        if (!movingStart && !b.IsValid) return;

        var (floor, ceiling) = NudgeRange(b, movingStart);
        if (floor > ceiling) return;

        var current = movingStart ? b.StartSeconds : b.EndSeconds;

        // Floor then +1 going forward, Ceiling then -1 going back. For a whole
        // second — which every timestamp is — that is exactly one second either
        // way. For a fractional one, which only a hand-edited CSV can produce, it
        // lands on the next whole second in the direction pressed rather than
        // carrying the fraction along for the rest of the file's life.
        var proposed = forward ? Math.Floor(current) + NudgeGapSeconds
                               : Math.Ceiling(current) - NudgeGapSeconds;

        proposed = Math.Clamp(proposed, Math.Max(0, floor), ceiling);

        if (Math.Abs(proposed - current) < NudgeEpsilon) return;

        // Clamping a step towards a limit that is already behind the timestamp
        // would send it the other way — a press of "earlier" landing later. The
        // arrow is hidden in that state, so this only guards the path, but a
        // button whose name is the opposite of what it does is worth ruling out
        // rather than relying on nobody reaching it.
        if (forward != proposed > current) return;

        if (movingStart) b.StartSeconds = proposed;
        else b.EndSeconds = proposed;

        Session.NotifyDurationChanged();
        if (IsBookmarkFileLoaded) SaveBookmarks();

        var which = movingStart ? "Start" : "End";
        StatusText = $"{which} of cut {b.Index} moved " +
                     $"{(forward ? "later" : "earlier")} — now " +
                     $"{Bookmark.FormatTime(proposed)}";
    }

    // ------------------------------------------------------------------
    // Reveal in Explorer
    // ------------------------------------------------------------------

    /// <summary>
    /// Whether the loaded video is still on disk to be shown.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="HasActiveVideo"/>, which also asks whether
    /// MPC-HC is running. Finding a file in Explorer has nothing to do with
    /// the player being up.
    /// </remarks>
    public bool CanRevealVideo => Session.HasVideo;

    /// <summary>Opens Explorer with the loaded video selected.</summary>
    [RelayCommand(CanExecute = nameof(CanRevealVideo))]
    private void RevealVideo()
    {
        if (!CanRevealVideo) return;
        RevealInExplorer(Session.VideoPath);
    }

    /// <summary>
    /// Opens Explorer with <paramref name="path"/> selected.
    /// </summary>
    /// <remarks>
    /// <c>/select,</c> needs the path quoted exactly like this; Explorer
    /// parses its own command line and mis-reads the usual quoting. That is
    /// the whole reason this is one method rather than repeated per caller —
    /// it is the kind of detail that gets "tidied" back into being broken.
    /// </remarks>
    private void RevealInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open Explorer — {ex.Message}";
        }
    }

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
