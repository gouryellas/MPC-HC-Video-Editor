using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>How a line of the Advanced tab is edited.</summary>
public enum AdvancedKind
{
    /// <summary>A box to type in.</summary>
    Text,

    /// <summary>A drop-down list.</summary>
    List,

    /// <summary>On or off: a drop-down holding just 1 and 0.</summary>
    Flag,

    /// <summary>A box to type a folder in, with a button to browse for one.</summary>
    Folder,

    /// <summary>The timestamp hotkey: set by pressing it, then applied or cleared.</summary>
    Hotkey,

    /// <summary>
    /// A colour: follow the theme, no colour, or a custom colour changed on a
    /// colour wheel.
    /// </summary>
    Color
}

/// <summary>
/// Settings the Advanced tab edits that have no control on the other tabs:
/// they are changed from the main window's menus. Filled in by whoever opens
/// the dialog, changed here, and read back on Save.
/// </summary>
public sealed class AdvancedExtras
{
    public string SaveToFolder { get; set; } = "";
    public string QuickSaveFolder { get; set; } = "";
    public string PlaylistFolder { get; set; } = "";
    public HotkeyBinding Hotkey { get; set; } = HotkeyBinding.None;

    /// <summary>"list", or a <see cref="StoryboardLayout"/> name for the storyboard.</summary>
    public string View { get; set; } = "list";

    public string SeekbarColor { get; set; } = SeekbarAppearance.FollowTheme;
    public string SeekbarBorderColor { get; set; } = SeekbarAppearance.FollowTheme;
    public string SeekbarTrackColor { get; set; } = SeekbarAppearance.FollowTheme;
    public string SeekbarMarkColor { get; set; } = SeekbarAppearance.FollowTheme;
    public string SeekbarPlayheadColor { get; set; } = SeekbarAppearance.FollowTheme;
    public string SeekbarWaveformColor { get; set; } = SeekbarAppearance.FollowTheme;
    public Dictionary<string, string> ButtonColors { get; set; } = new();
}

/// <summary>One entry of a line shown as a drop-down list.</summary>
public sealed record AdvancedChoice(string Key, string Label)
{
    /// <summary>
    /// The label, for a list that shows the item itself rather than going
    /// through DisplayMemberPath, as the 1/0 list's own template does.
    /// </summary>
    public override string ToString() => Label;
}

/// <summary>One line of the Advanced tab: a setting's name and its value.</summary>
/// <remarks>
/// Holds no value of its own. <see cref="Read"/> asks the control on the
/// other tab, and <see cref="Write"/> sets that control, so the two can never
/// disagree and Save and Cancel treat a change made here exactly like one made
/// there.
/// </remarks>
public sealed class AdvancedSetting : INotifyPropertyChanged
{
    private readonly Func<string> _read;
    private readonly Func<string, bool> _write;
    private readonly Func<string, bool>? _allows;
    private string _value = "";

    public AdvancedSetting(string name, string accepts, Func<string> read, Func<string, bool> write,
                           IReadOnlyList<AdvancedChoice>? choices = null, Func<string, bool>? allows = null)
    {
        Name = name;
        Accepts = accepts;
        Choices = choices;
        _allows = allows;
        Kind = choices is null ? AdvancedKind.Text : AdvancedKind.List;
        _read = read;
        _write = write;
        Refresh();
    }

    public string Name { get; }

    /// <summary>What the value may be, shown on hover.</summary>
    public string Accepts { get; }

    /// <summary>
    /// The entries to pick from when the line is a drop-down list rather than
    /// a box to type in; null for a box.
    /// </summary>
    public IReadOnlyList<AdvancedChoice>? Choices { get; }

    /// <summary>How the line is edited.</summary>
    public AdvancedKind Kind { get; init; }

    /// <summary>The colour of a <see cref="AdvancedKind.Color"/> line, to paint its swatch.</summary>
    public Brush? Swatch =>
        Kind == AdvancedKind.Color && ColorWheelPicker.TryParse(Value, out var c) ? new SolidColorBrush(c) : null;

    public const string ModeTheme = "theme";
    public const string ModeNone = "none";
    public const string ModeCustom = "custom";

    /// <summary>The three answers a colour line can give.</summary>
    public static IReadOnlyList<AdvancedChoice> ColourModes { get; } = new[]
    {
        new AdvancedChoice(ModeTheme, "Follow theme"),
        new AdvancedChoice(ModeNone, "No colour"),
        new AdvancedChoice(ModeCustom, "Custom"),
    };

    /// <summary>Which of <see cref="ColourModes"/> a colour line is set to.</summary>
    public string Mode =>
        ButtonColors.IsColour(Value) ? ModeCustom
        : string.Equals(Value, ModeNone, StringComparison.OrdinalIgnoreCase) ? ModeNone
        : ModeTheme;

    /// <summary>Whether a colour line has a custom colour, and so a swatch to click.</summary>
    public bool IsCustom => Mode == ModeCustom;

    /// <summary>
    /// What the theme paints a colour line's part, for a custom colour to
    /// start from: nothing on screen changes until the wheel moves.
    /// </summary>
    public Func<string>? ThemeColour { get; init; }

    /// <summary>
    /// The custom colour last used, kept while the window is open so that
    /// leaving Custom and coming back finds it again.
    /// </summary>
    public string? LastCustom { get; set; }

    private bool _capturing;
    private string? _pending;

    /// <summary>Whether a hotkey line is waiting for the key to be pressed.</summary>
    public bool Capturing
    {
        get => _capturing;
        set { _capturing = value; Notify(nameof(Capturing)); Notify(nameof(Shown)); }
    }

    /// <summary>A hotkey pressed but not yet applied.</summary>
    public string? Pending
    {
        get => _pending;
        set { _pending = value; Notify(nameof(Pending)); Notify(nameof(Shown)); Notify(nameof(CanApply)); }
    }

    /// <summary>Whether there is a pressed hotkey to apply.</summary>
    public bool CanApply => _pending is not null;

    /// <summary>What a hotkey line's box says.</summary>
    public string Shown => _capturing ? "Press a key or mouse button…" : _pending ?? Value;

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Value
    {
        get => _value;
        private set
        {
            if (_value == value) return;
            _value = value;
            Notify(nameof(Value));
            Notify(nameof(Swatch));
            Notify(nameof(Mode));
            Notify(nameof(IsCustom));
            Notify(nameof(Shown));
        }
    }

    /// <summary>
    /// Whether the box may hold <paramref name="text"/> while it is being
    /// typed, so only what the setting can take gets in. Text partway to a
    /// value, such as an empty box or "0." on the way to "0.5", is allowed.
    /// </summary>
    public bool Allows(string text) => _allows?.Invoke(text) ?? true;

    /// <summary>Whether the setting takes free text, spaces included.</summary>
    public bool TakesAnyText => _allows is null;

    /// <summary>Rereads the value from its control.</summary>
    public void Refresh() => Value = _read();

    /// <summary>
    /// Applies <paramref name="text"/> to the control. False when the setting
    /// cannot take it, in which case nothing changed.
    /// </summary>
    public bool Write(string text) => _write(text.Trim());

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class SettingsDialog
{
    private List<AdvancedSetting>? _advanced;

    /// <summary>
    /// The settings changed from the main window rather than on these tabs,
    /// for the Advanced tab to list. Null leaves them off it.
    /// </summary>
    public AdvancedExtras? Extras { get; init; }

    /// <summary>
    /// Builds the Advanced tab's lines, in the order the settings appear on
    /// the other tabs.
    /// </summary>
    private void BuildAdvancedList()
    {
        _advanced = new List<AdvancedSetting>
        {
            // General
            // Picking a theme here means wanting it, so it stops following
            // Windows, which would otherwise ignore the pick. Unticking the
            // box re-enables the list and is undone by Cancel like any other.
            Choice("Theme", ThemeCombo, ThemePalette.All.Select(t => t.Key).ToArray(),
                   ThemePalette.All.Select(t => t.Display).ToArray(),
                   unlock: () => FollowSystemThemeCheck.IsChecked = false),
            Flag("WindowsLightDarkMode", FollowSystemThemeCheck),
            Flag("SmallButtonIcons", SmallIconsCheck),
            Flag("NoButtonBackground", FlatIconsCheck),
            Flag("ShowSeekbar", ShowSeekbarCheck),
            Flag("ShowSeekbarWaveform", ShowSeekbarWaveformCheck),
            Flag("RunInSystemTray", ModeTray, ModeApplication),
            Flag("AllowMultipleInstances", InstanceMultiple, InstanceSingle),
            Flag("SwitchViewsAutomatically", AutoSwitchCheck),
            Flag("BookmarkToast", ToastsCheck),
            Number("ToastSeconds", ToastSecondsBox, 0.5, 10, "0.#"),
            Flag("CompletionSound", CompletionSoundCheck),
            Flag("UseChapterNames", UseChapterNamesCheck),
            Flag("CheckForUpdates", CheckForUpdatesCheck),
            Number("RecentVideos", MaxHistoryBox, 1, 50, "0"),

            // Output
            Choice("OutputFormat", FormatCombo, VideoFormats.All.Select(f => f.Key).ToArray(),
                   VideoFormats.All.Select(f => f.Display).ToArray()),
            Options("EncodingQuality", ("Fast", QualityFast), ("Balanced", QualityBalanced),
                    ("High quality", QualityHigh)),
            Options("H264Encoder", ("Software (x264)", EncoderSoftware), ("NVIDIA (NVENC)", EncoderNvenc),
                    ("Intel (Quick Sync)", EncoderQsv), ("AMD (AMF)", EncoderAmf)),
            Options("CutAccuracy", ("Fast", CutFast), ("Precise", CutPrecise), ("Smart", CutSmart)),
            Text("OutputFilename", NameTemplateBox, "The filename pattern, as on the Output tab."),
            Flag("NormalizeAudio", NormalizeAudioCheck),
            Number("FadeSeconds", FadeSecondsBox, 0, 10, "0.##"),
            Options("NameCollision", ("Ask each time", CollisionAsk), ("Number it", CollisionIncrement),
                    ("Overwrite", CollisionOverwrite)),
            Flag("RememberSaveToFolder", RememberSaveToCheck),

            // Cleanup
            Options("DeleteOriginalVideo", ("Keep it", VideoNever), ("Ask", VideoAsk),
                    ("Delete without asking", VideoAlways)),
            Options("DeleteBookmarksFile", ("Keep it", CsvNever), ("Ask", CsvAsk),
                    ("Delete without asking", CsvAlways)),
            Flag("DeleteToRecycleBin", RecycleBinCheck),

            // Player
            Flag("DetectPortAutomatically", AutoDetectPortCheck),
            Number("WebInterfacePort", PortBox, 1, 65535, "0"),
            Options("Polling", ("Responsive, 150 ms", PollResponsive), ("Balanced, 300 ms", PollBalanced),
                    ("Light, 750 ms", PollLight)),
            Text("FfmpegFolder", FfmpegBox, "The folder holding ffmpeg.exe, or blank to search for it."),

            // Overlay
            Options("OverlayCorner", ("Top right", CornerTopRight), ("Top left", CornerTopLeft),
                    ("Bottom right", CornerBottomRight), ("Bottom left", CornerBottomLeft)),
            Percent("OverlayOpacity", OpacitySlider),
            Flag("OverlayClickable", OverlayClickableCheck),
        };

        if (Extras is { } x)
        {
            _advanced.AddRange(new[]
            {
                // Folders
                Folder("SaveToFolder", () => x.SaveToFolder, v => x.SaveToFolder = v),
                Folder("QuickSaveFolder", () => x.QuickSaveFolder, v => x.QuickSaveFolder = v),
                Folder("PlaylistFolder", () => x.PlaylistFolder, v => x.PlaylistFolder = v),

                Hotkey("TimestampHotkey", x),

                // The View menu's layouts, under the names it gives them.
                Extra("View", () => x.View, v => x.View = v,
                      ("list", "List"),
                      (nameof(StoryboardLayout.Cards), "Slides"),
                      (nameof(StoryboardLayout.ContactSheet), "Compact"),
                      (nameof(StoryboardLayout.Detail), "Film"),
                      (nameof(StoryboardLayout.Filmstrip), "Detailed")),

                // Seekbar colours
                Colour("SeekbarBarColor", () => x.SeekbarColor, v => x.SeekbarColor = v,
                       () => ThemeService.Current.TimelinePosition),
                Colour("SeekbarBorderColor", () => x.SeekbarBorderColor, v => x.SeekbarBorderColor = v,
                       () => ThemeService.Current.BorderBrush),
                Colour("SeekbarBgColor", () => x.SeekbarTrackColor, v => x.SeekbarTrackColor = v,
                       () => ThemeService.Current.TimelineTrack),
                Colour("SeekbarMarkColor", () => x.SeekbarMarkColor, v => x.SeekbarMarkColor = v,
                       () => SeekbarAppearance.DefaultMark),

                // Left to the theme, the playhead takes the marks' colour.
                Colour("SeekbarPlayheadColor", () => x.SeekbarPlayheadColor, v => x.SeekbarPlayheadColor = v,
                       () => ButtonColors.IsColour(x.SeekbarMarkColor) ? x.SeekbarMarkColor : SeekbarAppearance.DefaultMark),
                Colour("SeekbarWaveformColor", () => x.SeekbarWaveformColor, v => x.SeekbarWaveformColor = v,
                       () => SeekbarAppearance.DefaultWaveform),
            });

            // Button colours, one per family.
            foreach (var family in ButtonColors.Families)
            {
                var key = ButtonColors.KeyOf(family);
                _advanced.Add(Colour($"{family}ButtonColor",
                    () => x.ButtonColors.TryGetValue(key, out var v) ? v : ButtonColors.DefaultFor(family),
                    v => x.ButtonColors[key] = v,
                    () => ThemeButtonColour(family)));
            }
        }

        AdvancedList.ItemsSource = _advanced;
    }

    private void RefreshAdvanced()
    {
        if (_advanced is null) return;
        foreach (var setting in _advanced) setting.Refresh();
    }

    /// <summary>
    /// Rereads every line when the tab is opened, so it shows what was changed
    /// on the other tabs since.
    /// </summary>
    private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // A ComboBox inside a tab raises the same event, and it bubbles here.
        if (!ReferenceEquals(e.OriginalSource, SettingsTabs)) return;

        // Built on first opening rather than in the constructor, by which time
        // every control it reads has its value. The first selection happens
        // while the window is still being built, so AdvancedTab can be null.
        if (AdvancedTab is null || !ReferenceEquals(SettingsTabs.SelectedItem, AdvancedTab)) return;

        if (_advanced is null) BuildAdvancedList();
        else RefreshAdvanced();
    }

    private void AdvancedValue_KeyDown(object sender, KeyEventArgs e)
    {
        // A space raises no PreviewTextInput, so it is turned away here.
        if (e.Key == Key.Space && sender is TextBox { DataContext: AdvancedSetting { TakesAnyText: false } })
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter) return;

        // Handled, or Enter would also press Save, the dialog's default button.
        e.Handled = true;
        ApplyAdvanced((TextBox)sender);
    }

    /// <summary>Lets in only a keystroke that leaves a value the setting can take.</summary>
    private void AdvancedValue_TextInput(object sender, TextCompositionEventArgs e)
    {
        var box = (TextBox)sender;
        if (box.DataContext is AdvancedSetting setting && !setting.Allows(WouldRead(box, e.Text)))
            e.Handled = true;
    }

    /// <summary>The same for pasted text.</summary>
    private void AdvancedValue_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        var box = (TextBox)sender;
        if (box.DataContext is not AdvancedSetting setting) return;
        var text = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? "";
        if (!setting.Allows(WouldRead(box, text.Trim()))) e.CancelCommand();
    }

    /// <summary>What the box would hold with <paramref name="typed"/> in place of its selection.</summary>
    private static string WouldRead(TextBox box, string typed) =>
        box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, typed);

    private void AdvancedValue_LostFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        ApplyAdvanced((TextBox)sender);

    private void AdvancedChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Kept from bubbling to SettingsTabs_SelectionChanged.
        e.Handled = true;

        var combo = (ComboBox)sender;
        if (combo.DataContext is not AdvancedSetting setting) return;
        if (combo.SelectedValue is not string key || key == setting.Value) return;

        setting.Write(key);
        RefreshAdvanced();

        // Put back the setting's real value if it refused the pick.
        combo.SetCurrentValue(Selector.SelectedValueProperty, setting.Value);
    }

    // ------------------------------------------------------------------
    // Folder, hotkey and colour lines
    // ------------------------------------------------------------------

    private void AdvancedBrowse_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not AdvancedSetting setting) return;

        var dlg = new OpenFolderDialog { Title = setting.Name };
        if (Directory.Exists(setting.Value)) dlg.InitialDirectory = setting.Value;
        if (dlg.ShowDialog(this) != true) return;

        setting.Write(dlg.FolderName);
        RefreshAdvanced();
    }

    private void AdvancedHotkeySet_Click(object sender, RoutedEventArgs e)
    {
        var button = (FrameworkElement)sender;
        if (button.DataContext is not AdvancedSetting setting) return;

        setting.Pending = null;
        setting.Capturing = true;
        (button.Tag as IInputElement)?.Focus();
    }

    private void AdvancedHotkeyApply_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not AdvancedSetting { Pending: { } pending } setting) return;
        setting.Write(pending);
        setting.Pending = null;
        RefreshAdvanced();
    }

    private void AdvancedHotkeyClear_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not AdvancedSetting setting) return;
        setting.Capturing = false;
        setting.Pending = null;
        setting.Write(HotkeyBinding.None.ToSettingsString());
        RefreshAdvanced();
    }

    /// <summary>Takes the key combination pressed while the line is waiting for one.</summary>
    private void AdvancedHotkey_KeyDown(object sender, KeyEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not AdvancedSetting { Capturing: true } setting) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // A modifier on its own is the start of a combination, not the end.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;

        setting.Capturing = false;

        // Escape on its own backs out, leaving the hotkey as it was.
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None) return;

        setting.Pending = HotkeyBinding.FromKeyboard(Keyboard.Modifiers, key).Display;
    }

    /// <summary>Takes the middle or a side mouse button the same way.</summary>
    private void AdvancedHotkey_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not AdvancedSetting { Capturing: true } setting) return;

        HotkeyBinding.MouseButtonKind? button = e.ChangedButton switch
        {
            MouseButton.Middle => HotkeyBinding.MouseButtonKind.MButton,
            MouseButton.XButton1 => HotkeyBinding.MouseButtonKind.XButton1,
            MouseButton.XButton2 => HotkeyBinding.MouseButtonKind.XButton2,
            _ => null
        };
        if (button is null) return;

        e.Handled = true;
        setting.Capturing = false;
        setting.Pending = HotkeyBinding.FromMouse(button.Value).Display;
    }

    private void AdvancedHotkey_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is AdvancedSetting setting) setting.Capturing = false;
    }

    /// <summary>Follow theme, no colour, or custom, picked from a colour line's list.</summary>
    private void AdvancedColourMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Kept from bubbling to SettingsTabs_SelectionChanged.
        e.Handled = true;

        var combo = (ComboBox)sender;
        if (combo.DataContext is not AdvancedSetting setting) return;
        if (combo.SelectedValue is not string mode || mode == setting.Mode) return;

        if (setting.IsCustom) setting.LastCustom = setting.Value;

        setting.Write(mode == AdvancedSetting.ModeCustom
            ? setting.LastCustom ?? setting.ThemeColour?.Invoke() ?? "#808080"
            : mode);
        RefreshAdvanced();
    }

    /// <summary>Opens the colour wheel under a colour line's swatch.</summary>
    private void AdvancedColour_Click(object sender, MouseButtonEventArgs e)
    {
        var target = (FrameworkElement)sender;
        if (target.DataContext is not AdvancedSetting setting) return;

        var wheel = new ColorWheelPicker(setting.Value);
        wheel.ColorChanged += hex =>
        {
            setting.Write(hex);
            setting.Refresh();
        };

        _ = new Popup
        {
            PlacementTarget = target,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = new Border
            {
                Background = ThemeService.Brush(nameof(ThemePalette.PanelBackground)),
                BorderBrush = ThemeService.Brush(nameof(ThemePalette.BorderBrush)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12),
                Child = wheel
            },
            IsOpen = true
        };
        e.Handled = true;
    }

    private void ApplyAdvanced(TextBox box)
    {
        if (box.DataContext is not AdvancedSetting setting) return;
        if (box.Text.Trim() == setting.Value) return;

        // A value the setting cannot take is put back rather than kept: the
        // line must always say what the setting actually is.
        setting.Write(box.Text);

        // Every line, not just this one: some settings move others with them
        // (the waveform follows the seekbar, the theme follows Windows).
        RefreshAdvanced();

        // SetCurrentValue rather than assigning Text: a plain assignment would
        // replace the binding, and the box would stop following its setting.
        // Needed even when the value came back unchanged, since no change
        // means no notification to put the rejected text back.
        box.SetCurrentValue(TextBox.TextProperty, setting.Value);
        box.CaretIndex = box.Text.Length;
    }

    // ------------------------------------------------------------------
    // Kinds of line
    // ------------------------------------------------------------------

    /// <summary>A checkbox, as 1 or 0.</summary>
    private static AdvancedSetting Flag(string name, ToggleButton box) =>
        new(name, "1 for on, 0 for off.",
            () => box.IsChecked == true ? "1" : "0",
            text =>
            {
                if (!box.IsEnabled || text is not ("0" or "1")) return false;
                box.IsChecked = text == "1";
                return true;
            },
            OneAndZero)
        { Kind = AdvancedKind.Flag };

    /// <summary>A pair of radio buttons that amounts to on or off, as 1 or 0.</summary>
    private static AdvancedSetting Flag(string name, RadioButton on, RadioButton off) =>
        new(name, $"1 for “{Caption(on)}”, 0 for “{Caption(off)}”.",
            () => on.IsChecked == true ? "1" : "0",
            text =>
            {
                if (text is not ("0" or "1")) return false;
                var pick = text == "1" ? on : off;
                if (!pick.IsEnabled) return false;
                pick.IsChecked = true;
                return true;
            },
            OneAndZero)
        { Kind = AdvancedKind.Flag };

    /// <summary>
    /// A group of radio buttons, as a drop-down list of their labels. The
    /// list's keys are the buttons' positions, never shown.
    /// </summary>
    private static AdvancedSetting Options(string name, params (string Label, RadioButton Button)[] options) =>
        new(name, "Pick from the list.",
            () =>
            {
                var index = Array.FindIndex(options, o => o.Button.IsChecked == true);
                return index < 0 ? "" : index.ToString(CultureInfo.InvariantCulture);
            },
            text =>
            {
                if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var index)) return false;
                if (index < 0 || index >= options.Length) return false;

                // A GPU encoder this machine does not have is disabled on its
                // tab, and stays unchosen here too.
                var pick = options[index].Button;
                if (!pick.IsEnabled) return false;
                pick.IsChecked = true;
                return true;
            },
            options.Select((o, i) => new AdvancedChoice(i.ToString(CultureInfo.InvariantCulture), o.Label)).ToArray());

    /// <summary>A drop-down list, shown as one here too.</summary>
    /// <remarks>
    /// <paramref name="unlock"/>, when given, runs before a pick is made, to
    /// lift whatever has the list disabled on its own tab.
    /// </remarks>
    private static AdvancedSetting Choice(string name, Selector combo, string[] keys, string[] labels,
                                          Action? unlock = null) =>
        new(name, "Pick from the list.",
            () => combo.SelectedIndex >= 0 && combo.SelectedIndex < keys.Length ? keys[combo.SelectedIndex] : "",
            text =>
            {
                var index = Array.FindIndex(keys, k => string.Equals(k, text, StringComparison.OrdinalIgnoreCase));
                if (index < 0) return false;
                unlock?.Invoke();
                if (!combo.IsEnabled) return false;
                combo.SelectedIndex = index;
                return true;
            },
            keys.Select((k, i) => new AdvancedChoice(k, labels[i])).ToArray());

    /// <summary>A number typed into a box, kept within the range Save accepts.</summary>
    private static AdvancedSetting Number(string name, TextBox box, double min, double max, string format) =>
        new(name, $"A number from {min.ToString(CultureInfo.CurrentCulture)} to {max.ToString(CultureInfo.CurrentCulture)}.",
            () => box.Text.Trim(),
            text =>
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)) return false;
                if (value < min || value > max) return false;
                if (format == "0" && value != Math.Floor(value)) return false;
                box.Text = value.ToString(format, CultureInfo.CurrentCulture);
                return true;
            },
            allows: text =>
            {
                if (text.Length == 0) return true;
                var point = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                var parts = text.Split(point);
                if (parts.Length > 2 || (parts.Length == 2 && format == "0")) return false;
                if (!parts.All(p => p.All(char.IsAsciiDigit))) return false;
                if (text == point) return true;
                return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
                       && value <= max;
            });

    /// <summary>Free text typed into a box.</summary>
    private static AdvancedSetting Text(string name, TextBox box, string accepts) =>
        new(name, accepts,
            () => box.Text,
            text =>
            {
                box.Text = text;
                return true;
            });

    /// <summary>The opacity slider, as a whole percentage.</summary>
    private static AdvancedSetting Percent(string name, RangeBase slider) =>
        new(name, $"A percentage from {slider.Minimum * 100:0} to {slider.Maximum * 100:0}, in steps of 5.",
            () => (slider.Value * 100).ToString("0", CultureInfo.InvariantCulture),
            text =>
            {
                if (!int.TryParse(text.TrimEnd('%'), NumberStyles.None, CultureInfo.InvariantCulture, out var percent))
                    return false;
                var value = percent / 100.0;
                if (value < slider.Minimum - 1e-9 || value > slider.Maximum + 1e-9 || percent % 5 != 0) return false;
                slider.Value = value;
                return true;
            },
            allows: text => text.Length == 0 ||
                            (WholeNumber(text, out var n) && n <= slider.Maximum * 100 + 1e-9));

    /// <summary>The two entries of an on/off line's list.</summary>
    private static readonly AdvancedChoice[] OneAndZero =
    {
        new("1", "1"),
        new("0", "0"),
    };

    /// <summary>Digits only, as a number.</summary>
    private static bool WholeNumber(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    /// <summary>A folder, typed or browsed for. Blank means none is set.</summary>
    private static AdvancedSetting Folder(string name, Func<string> get, Action<string> set) =>
        new(name, "A folder, or blank for none.", get,
            text =>
            {
                if (text.Length == 0)
                {
                    set("");
                    return true;
                }

                // A folder that is not there is refused, as the menus' folder
                // pickers cannot choose one either.
                if (!Directory.Exists(text)) return false;
                set(text.TrimEnd('\\') + "\\");
                return true;
            })
        { Kind = AdvancedKind.Folder };

    /// <summary>The timestamp hotkey, by its name: F8, Ctrl+Shift+T, MButton.</summary>
    private static AdvancedSetting Hotkey(string name, AdvancedExtras extras) =>
        new(name, "Press Set, then the key or mouse button.",
            () => extras.Hotkey.Display,
            text =>
            {
                extras.Hotkey = HotkeyBinding.Parse(text);
                return true;
            })
        { Kind = AdvancedKind.Hotkey };

    /// <summary>A drop-down list of a setting kept outside these tabs.</summary>
    private static AdvancedSetting Extra(string name, Func<string> get, Action<string> set,
                                         params (string Key, string Label)[] options) =>
        new(name, "Pick from the list.", get,
            text =>
            {
                if (!options.Any(o => o.Key == text)) return false;
                set(text);
                return true;
            },
            options.Select(o => new AdvancedChoice(o.Key, o.Label)).ToArray());

    /// <summary>
    /// A colour: "theme", "none", or #RRGGBB. <paramref name="themeColour"/>
    /// is what the theme paints it, for Custom to start from.
    /// </summary>
    private static AdvancedSetting Colour(string name, Func<string> get, Action<string> set,
                                          Func<string> themeColour) =>
        new(name, "",
            () =>
            {
                var value = get();
                if (ButtonColors.IsColour(value)) return value.ToUpperInvariant();
                return string.Equals(value, AdvancedSetting.ModeNone, StringComparison.OrdinalIgnoreCase)
                    ? AdvancedSetting.ModeNone
                    : AdvancedSetting.ModeTheme;
            },
            text =>
            {
                if (text is AdvancedSetting.ModeTheme or AdvancedSetting.ModeNone
                    || ColorWheelPicker.TryParse(text, out _))
                {
                    set(text);
                    return true;
                }
                return false;
            })
        { Kind = AdvancedKind.Color, ThemeColour = themeColour };

    /// <summary>What the theme paints a button family, for Custom to start from.</summary>
    private static string ThemeButtonColour(ButtonFamily family) =>
        ButtonColors.Resolve(family,
                             new Dictionary<string, string> { [ButtonColors.KeyOf(family)] = ButtonColors.FollowTheme },
                             ThemeService.Current).Fill;

    private static string Caption(ContentControl control) =>
        control.Content as string ?? control.Name;
}
