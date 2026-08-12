using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;
using MpcHcVideoEditor.ViewModels;

namespace MpcHcVideoEditor.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
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
    }

    // ------------------------------------------------------------------
    // Run mode: application vs system tray
    // ------------------------------------------------------------------

    private TrayIconService? _tray;

    /// <summary>
    /// Set when the user has chosen Exit from the tray menu, so the close that
    /// follows is allowed through instead of being turned back into a hide.
    /// </summary>
    private bool _exitConfirmed;

    /// <summary>Whether the tray is currently the configured behaviour.</summary>
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
    /// In tray mode, minimising hides the window rather than parking it on the
    /// taskbar — otherwise it would be in both places at once.
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
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
    /// Whether the restored window should also be brought to the front. False
    /// when the restore happened because focus moved to some other
    /// application — showing the window is wanted, stealing focus back from
    /// whatever the user just switched to is not.
    /// </param>
    private void SetMinimalView(bool minimal, bool activate)
    {
        if (minimal)
        {
            if (_minimal == null)
            {
                _minimal = new MinimalWindow { DataContext = DataContext };
                _minimal.Closed += (_, _) => _minimal = null;
            }

            _minimal.Show();

            // Appearance is re-applied on every show, not just on creation, so
            // a change in Settings takes effect the next time the overlay
            // appears rather than only after a restart.
            _minimal.SetBackgroundOpacity(_vm?.OverlayOpacity ?? 1.0);
            _minimal.PositionInCorner(_vm?.OverlayCorner ?? OverlayCorner.TopRight);

            Hide();
            return;
        }

        _minimal?.Hide();
        Show();
        WindowState = WindowState.Normal;

        if (activate) Activate();
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
    /// Accepts a dropped video, bookmark CSV or .pls playlist. Dispatch is by
    /// extension, matching File ▸ Open…; only the first file is used.
    /// </summary>
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_vm == null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths) return;

        e.Handled = true;
        _vm.OpenDroppedFileCommand.Execute(paths[0]);
    }

    /// <summary>Guards <see cref="MainWindow_Loaded"/> against running twice.</summary>
    /// <remarks>
    /// A WPF Window raises Loaded again when it is hidden and shown, which
    /// this window now does constantly — the view follows focus, and in tray
    /// mode minimising hides it too. Every subscription below would otherwise
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
        var fileMenu = FindMenuItem(HeaderMenu, "_File");
        if (fileMenu == null) return;

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
        startSep.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        endSep.Visibility = Visibility.Visible;

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
        var shortcutsMenu = FindMenuItem(HeaderMenu, "Sho_rtcuts");
        if (shortcutsMenu == null) return;

        var startSep = FindNamed(shortcutsMenu.Items, "ShortcutsMenuStartSeparator") as Separator;
        if (startSep == null) return;

        int startIdx = shortcutsMenu.Items.IndexOf(startSep);

        // Remove everything after the start separator — that range is
        // entirely dynamic (no trailing end separator anymore).
        while (shortcutsMenu.Items.Count > startIdx + 1)
            shortcutsMenu.Items.RemoveAt(startIdx + 1);

        bool hasAny = _vm!.Shortcuts.Count > 0;
        startSep.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;

        int insertAt = startIdx + 1;
        foreach (var entry in _vm.Shortcuts)
            shortcutsMenu.Items.Insert(insertAt++, BuildShortcutsMenuItem(entry));
    }

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
            Header = $"📁  {header}",
            ToolTip = entry.Path,
            Tag = entry
        };

        var set = new MenuItem { Header = "Set", ToolTip = $"Quick save to {entry.Path}" };
        set.Click += (_, _) => _vm?.SetQuickSaveShortcutCommand.Execute(entry);

        var remove = new MenuItem { Header = "Remove" };
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
            ToolTip = entry.Path,
            Tag = entry
        };

        var open = new MenuItem { Header = "Open", Tag = entry };
        open.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.OpenShortcutCommand.Execute(entry);
        };

        var rename = new MenuItem { Header = "Rename…", Tag = entry };
        rename.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.RenameShortcutCommand.Execute(entry);
        };

        var remove = new MenuItem { Header = "Remove", Tag = entry };
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

    private static MenuItem? FindMenuItem(ItemsControl parent, string header)
    {
        foreach (var item in parent.Items)
            if (item is MenuItem mi && string.Equals(mi.Header as string, header, StringComparison.Ordinal))
                return mi;
        return null;
    }

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
        startSep.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        endSep.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
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
            ToolTip = path,
            Tag = path
        };

        // Single-click on the parent also opens — convenient for the
        // common case. Sub-items are for the less-frequent "remove" action.
        parent.Click += (_, _) => _vm?.OpenRecentVideoCommand.Execute(path);

        var open = new MenuItem { Header = "Open", Tag = path };
        open.Click += (_, _) => _vm?.OpenRecentVideoCommand.Execute(path);

        var remove = new MenuItem { Header = "Remove from list", Tag = path };
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

        // "_Options" — the menu was renamed from "_Suffix". Looking up the old
        // header found nothing and this whole rebuild silently did nothing, so
        // newly added naming styles never appeared in the menu.
        var suffixMenu = FindMenuItem(HeaderMenu, "_Options");
        if (suffixMenu == null) return;

        var startSep = FindNamed(suffixMenu.Items, "SuffixListStartSeparator") as Separator;
        var endSep = FindNamed(suffixMenu.Items, "SuffixListEndSeparator") as Separator;
        if (startSep == null || endSep == null) return;

        int startIdx = suffixMenu.Items.IndexOf(startSep);
        int endIdx = suffixMenu.Items.IndexOf(endSep);

        for (int i = endIdx - 1; i > startIdx; i--)
            suffixMenu.Items.RemoveAt(i);

        int insertAt = startIdx + 1;
        foreach (var entry in _vm.Suffixes)
        {
            // Compare against the active text directly. This used to scrape
            // it out of ActiveSuffixDisplay, which broke the moment that
            // label's wording changed.
            var isActive = string.Equals(entry.Text, _vm.ActiveSuffixText,
                                         StringComparison.OrdinalIgnoreCase);
            // Plain text, not entry.Display — the brackets are an output
            // detail shown by the "Example:" line, not part of the style name.
            var header = isActive ? $"✓  {entry.Text}" : $"    {entry.Text}";
            var item = new MenuItem
            {
                Header = header,
                ToolTip = $"Click to set [{entry.Text}] as the active suffix",
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

        var playlistMenu = FindMenuItem(HeaderMenu, "_Playlist");
        if (playlistMenu == null) return;

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

        // Read the playlist folder and list .pls files.
        var folder = _vm.PlaylistFolderDisplay;
        // PlaylistFolderDisplay is "Playlist folder: <path>" or "(not set)";
        // extract the actual path. If it's "(not set)", treat as empty.
        string plsFolder = string.Empty;
        const string prefix = "Playlist folder: ";
        if (folder.StartsWith(prefix, StringComparison.Ordinal))
            plsFolder = folder[prefix.Length..];

        bool hasFolder = !string.IsNullOrEmpty(plsFolder) && System.IO.Directory.Exists(plsFolder);
        bool hasAny = false;

        if (hasFolder)
        {
            var playlists = System.IO.Directory.GetFiles(plsFolder, "*.pls")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            hasAny = playlists.Count > 0;

            int insertAt = startIdx + 1;
            foreach (var plsPath in playlists)
                playlistMenu.Items.Insert(insertAt++, BuildPlaylistMenuItem(plsPath));
        }

        // Show/hide separators + placeholder.
        startSep.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        endSep.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        if (emptyPlaceholder != null)
        {
            emptyPlaceholder.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;
            emptyPlaceholder.Header = hasFolder
                ? "(no playlists in folder)"
                : "(no playlist folder set)";
        }
    }

    /// <summary>
    /// Builds a single playlist's parent MenuItem for the Playlist menu.
    /// Header is the playlist's filename (e.g. <c>"my_videos.pls"</c>);
    /// expands into the action items first (Open playlist file /
    /// Load this playlist / Add current video / Delete playlist), then
    /// a separator, then the numbered video entries (1, 2, 3, …).
    /// </summary>
    private MenuItem BuildPlaylistMenuItem(string plsPath)
    {
        var plsName = System.IO.Path.GetFileName(plsPath);
        var parent = new MenuItem
        {
            Header = $"📋  {plsName}",
            ToolTip = plsPath,
            Tag = plsPath
        };

        // Action items first — at the top of the submenu so they're always
        // reachable without scrolling past a long list of video entries.
        // View playlist content (shell-execute the .pls in default handler)
        var openFile = new MenuItem { Header = "View playlist content", Tag = plsPath };
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
        var loadThis = new MenuItem { Header = "Load this playlist", Tag = plsPath };
        loadThis.Click += (_, e) =>
        {
            e.Handled = true;
            // Loading also starts playback at the first entry that still exists.
            _vm?.LoadPlaylistAndPlayCommand.Execute(plsPath);
        };
        parent.Items.Add(loadThis);

        // Add current video to this playlist
        var addCurrent = new MenuItem { Header = "Add current video", Tag = plsPath };
        addCurrent.Click += (_, e) =>
        {
            e.Handled = true;
            _vm?.AddCurrentToPlaylistNamedCommand.Execute(plsPath);
        };
        parent.Items.Add(addCurrent);

        // Delete playlist
        var delete = new MenuItem { Header = "Delete playlist…", Tag = plsPath };
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

            List<string> entries;
            try
            {
                entries = _vm != null
                    ? _vm.ReadPlaylistEntriesForMenu(plsPath)
                    : new List<string>();
            }
            catch
            {
                entries = new List<string>();
            }

            parent.Items.Remove(placeholder);

            if (entries.Count == 0)
            {
                parent.Items.Add(new MenuItem { Header = "(empty)", IsEnabled = false });
                return;
            }

            int position = 1;
            foreach (var videoPath in entries)
                parent.Items.Add(BuildPlaylistEntryItem(plsPath, position++, videoPath));
        };

        return parent;
    }

    /// <summary>
    /// Builds a single video-entry MenuItem for a playlist's submenu.
    /// Header shows the entry's 1-based position and the video's filename;
    /// tooltip is the full path. Clicking the parent opens the video in
    /// MPC-HC; a "Remove entry" sub-item removes it from the .pls file.
    /// </summary>
    private MenuItem BuildPlaylistEntryItem(string plsPath, int index, string videoPath)
    {
        var fileName = System.IO.Path.GetFileName(videoPath);
        var fileExists = System.IO.File.Exists(videoPath);
        // A missing entry gets a "Remove" sub-item only — there is nothing to
        // open, and offering it just produces an error dialog.
        var header = fileExists
            ? $"{index}.  {fileName}"
            : $"{index}.  {fileName}  (missing)";
        var parent = new MenuItem
        {
            Header = header,
            ToolTip = videoPath,
            Tag = videoPath,
            // NOTE: do NOT use `null` here — assigning null to a dependency
            // property becomes an explicit local value that overrides the
            // implicit MenuItem style's Foreground=Black, making the header
            // text invisible. Use an explicit Brushes.Black so existing-file
            // entries render the same as every other menu item, while missing
            // files keep the Salmon warning color.
            Foreground = fileExists ? Brushes.Black : Brushes.Salmon
        };
        // Clicking a missing entry must not try to play it.
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
            var open = new MenuItem { Header = "Open", Tag = videoPath };
            open.Click += (_, e) =>
            {
                e.Handled = true;
                _vm?.OpenPlaylistEntryCommand.Execute(videoPath);
            };
            parent.Items.Add(open);
        }

        var remove = new MenuItem { Header = "Remove", Tag = (plsPath, index) };
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

}
