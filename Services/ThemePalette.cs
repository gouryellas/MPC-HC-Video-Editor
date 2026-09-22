namespace MpcHcVideoEditor.Services;

/// <summary>
/// A complete set of colors for the interface.
/// </summary>
/// <remarks>
/// <para>
/// Every color the XAML uses resolves to one of these names. The application
/// previously spelled out 55 distinct hex values across twelve files, which
/// meant a change of appearance was a find-and-replace exercise and a new
/// theme was not realistically possible at all.
/// </para>
/// <para>
/// Names describe the <em>role</em>, not the color — <c>Accent</c> rather than
/// <c>Teal</c> — because the whole point is that the value changes per theme
/// while the meaning does not.
/// </para>
/// </remarks>
/// <param name="Key">Stable identifier persisted in settings.json.</param>
/// <param name="Display">Shown in the settings dialog.</param>
/// <param name="IsLight">
/// Whether this is a light theme. The menu strip and a few controls follow the
/// system's own light chrome, so they need the opposite treatment.
/// </param>
public sealed record ThemePalette(
    string Key,
    string Display,
    bool IsLight,

    // Surfaces, darkest to lightest.
    string TimelineTrack,
    string InsetBackground,
    string WindowBackground,
    string PanelBackground,
    string ControlBackground,
    string ControlHover,
    string RowHover,
    string RowSelected,

    // The menu strip, which sits on system chrome rather than the app's canvas.
    string MenuBarBackground,
    string MenuBarForeground,

    string BorderBrush,

    // Text, strongest to faintest.
    string TextPrimary,
    string TextBody,
    string TextDim,
    string TextSecondary,
    string TextMuted,

    // Accents and values.
    string Accent,
    string AccentBright,
    string PrimaryButton,
    string PrimaryButtonBorder,

    /// <summary>
    /// Playback position on the timeline, drawn over
    /// <see cref="TimelineTrack"/>.
    /// </summary>
    /// <remarks>
    /// Its own role rather than a second use of <see cref="PrimaryButton"/>,
    /// which is what it used to borrow. The two answer different questions — one
    /// is "which button commits the action", the other "where is the playhead" —
    /// and sharing a value meant the position could not be recolored without
    /// moving every primary button with it.
    ///
    /// Blue in every theme, and deliberately not drawn from the theme's accent:
    /// the cut marks are drawn on the same track in fixed colors — white, and
    /// red for one still waiting to be closed — so a position taking its color
    /// from the theme would eventually be handed one of them.
    /// </remarks>
    string TimelinePosition,

    /// <summary>
    /// Text sitting on <see cref="PrimaryButton"/> and the colored action
    /// buttons. A role rather than a literal "White", so a theme with a pale
    /// primary button is possible without hunting through the XAML.
    /// </summary>
    string OnAccent,
    string LinkBlue,
    string ValueYellow,
    string ValueGreen,
    string StatusOk,
    string StatusError,
    string StatusWarn,

    // Overlay windows, which float over the player and carry their own alpha.
    string OverlayBackground,
    string ToastBackground,

    // The icon, drawn from these rather than shipped per theme.
    string IconBackground,
    string IconBody,
    string IconDetail)
{
    // The colored action buttons on the toolbar. The same four colors in every
    // theme, which is why they are here rather than on the constructor above.
    //
    // They are not decoration: each one is how its operation is found on a row
    // of buttons, and somebody who reaches for the olive one to split has to
    // learn the row again if it turns blue with the theme. The roles that do
    // vary are the ones describing a surface or a piece of text, which have to
    // suit the theme around them; a filled button carries its own background
    // and its own label, so it does not.
    //
    // Every label clears 6:1 against its button in all five themes, checked
    // against each theme's OnAccent rather than assuming white.
    public string MergeBackground => "#7A5320";
    public string MergeBorder => "#A87423";
    public string SplitBackground => "#4A5A2C";
    public string SplitBorder => "#5D7038";
    public string ConvertBackground => "#6B3A2E";
    public string ConvertBorder => "#8A4A3A";
    // Slate blue, and the only cool one of the four. It was a dark gold, which
    // put it a shade away from Merge on the same row — two warm browns telling
    // two unrelated operations apart. Merge, Split and Convert are amber, olive
    // and rust, close enough in temperature that a fourth warm color had
    // nowhere to sit.
    public string AudioBackground => "#3F5A8A";
    public string AudioBorder => "#5A7BB5";

    /// <summary>Every theme, in the order the settings dialog lists them.</summary>
    /// <remarks>
    /// Computed on access rather than stored in a field. As a field initialized
    /// here it ran before the three palettes below were constructed — static
    /// initializers execute in declaration order — so it captured three nulls,
    /// which would have left the settings list empty and every lookup falling
    /// back to the default.
    ///
    /// Dark first, then light, each group in the order it was added. The
    /// settings dialog lists them exactly as they come out of here, so the two
    /// kinds are not interleaved — somebody picking a dark theme should not
    /// have to step over a light one to compare two of them.
    /// </remarks>
    public static IReadOnlyList<ThemePalette> All =>
        new[] { Graphite, Midnight, Obsidian, Daylight, Parchment };

    /// <summary>Falls back to <see cref="Graphite"/> for an unknown key.</summary>
    public static ThemePalette FromKey(string? key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Graphite;

    /// <summary>Warm charcoal with an amber accent. The default.</summary>
    public static ThemePalette Graphite { get; } = new(
        Key: "graphite", Display: "Graphite — warm charcoal, amber accent", IsLight: false,
        TimelineTrack: "#141212", InsetBackground: "#151312",
        WindowBackground: "#1A1817", PanelBackground: "#221F1D",
        ControlBackground: "#332F2B", ControlHover: "#3E3934",
        RowHover: "#2C2823", RowSelected: "#5A4520",
        MenuBarBackground: "#F2EFEA", MenuBarForeground: "#221F1D",
        BorderBrush: "#3A3530",
        TextPrimary: "#EFEAE3", TextBody: "#DCD6CE", TextDim: "#C6BFB6",
        TextSecondary: "#A79E93", TextMuted: "#8A8076",
        Accent: "#E0A253", AccentBright: "#F2BE7C",
        PrimaryButton: "#8A5D22", PrimaryButtonBorder: "#B27B33",
        TimelinePosition: "#4A90D9",
        OnAccent: "#FFF6EA",
        LinkBlue: "#D8A96A", ValueYellow: "#E8C87E", ValueGreen: "#BFCF95",
        StatusOk: "#A9C98A", StatusError: "#E4736B", StatusWarn: "#E0A253",
        OverlayBackground: "#F01A1817", ToastBackground: "#FF171514",
        IconBackground: "#221F1D", IconBody: "#E0A253", IconDetail: "#C9853A");

    /// <summary>Cool blue-gray with a cyan accent.</summary>
    public static ThemePalette Midnight { get; } = new(
        Key: "midnight", Display: "Midnight — cool blue-gray, cyan accent", IsLight: false,
        TimelineTrack: "#0B1017", InsetBackground: "#0D131B",
        WindowBackground: "#0F151E", PanelBackground: "#161F2B",
        ControlBackground: "#233246", ControlHover: "#2C3E56",
        RowHover: "#1E2C3D", RowSelected: "#1F4E63",
        MenuBarBackground: "#EDF1F6", MenuBarForeground: "#161F2B",
        BorderBrush: "#2A3A50",
        TextPrimary: "#E3EBF5", TextBody: "#C7D3E3", TextDim: "#AFBCCE",
        TextSecondary: "#8FA3BC", TextMuted: "#6E819A",
        Accent: "#5AC8E0", AccentBright: "#86DCEE",
        PrimaryButton: "#1F5F7A", PrimaryButtonBorder: "#2E7E9E",
        TimelinePosition: "#4A9EE0",
        OnAccent: "#F0FAFD",
        LinkBlue: "#6FB3E8", ValueYellow: "#E8D48E", ValueGreen: "#9FD8B8",
        StatusOk: "#8FD4A8", StatusError: "#E8706E", StatusWarn: "#E8B366",
        OverlayBackground: "#F00F151E", ToastBackground: "#FF0C1119",
        IconBackground: "#161F2B", IconBody: "#5AC8E0", IconDetail: "#3E9DB5");

    /// <summary>Near-black neutral with a blue accent.</summary>
    /// <remarks>
    /// The darkest of the three darks, and the only one with no temperature to
    /// it: Graphite leans warm and Midnight leans blue, so a plain grey was the
    /// hole in the set. What separates it from Midnight is the surfaces rather
    /// than the accent — neutral grey against a blue-grey — so the two are told
    /// apart by the thing that covers most of the window.
    ///
    /// The accent is a cornflower blue, lighter and flatter than the playback
    /// position it shares the window with, and the index beside it is pushed
    /// towards sky so a row is not two shades of one color.
    /// </remarks>
    public static ThemePalette Obsidian { get; } = new(
        Key: "obsidian", Display: "Obsidian — near-black neutral, blue accent", IsLight: false,
        TimelineTrack: "#0A0A0C", InsetBackground: "#0D0D10",
        WindowBackground: "#121214", PanelBackground: "#1A1A1D",
        ControlBackground: "#27272C", ControlHover: "#32323A",
        RowHover: "#222228", RowSelected: "#24416B",
        MenuBarBackground: "#F0F0F3", MenuBarForeground: "#1A1A1D",
        BorderBrush: "#33333A",
        TextPrimary: "#ECECF0", TextBody: "#D6D6DC", TextDim: "#BFBFC7",
        TextSecondary: "#9999A2", TextMuted: "#7A7A84",
        Accent: "#6C9CF0", AccentBright: "#93B7F7",
        PrimaryButton: "#2B4C85", PrimaryButtonBorder: "#3C67AD",
        TimelinePosition: "#4A90D9",
        OnAccent: "#F2F6FF",
        LinkBlue: "#79CCEE", ValueYellow: "#E5CE85", ValueGreen: "#9ED9A8",
        StatusOk: "#86D19A", StatusError: "#E8706E", StatusWarn: "#E3B266",
        OverlayBackground: "#F0121214", ToastBackground: "#FF0F0F12",
        IconBackground: "#1A1A1D", IconBody: "#6C9CF0", IconDetail: "#4A74C4");

    /// <summary>Light surfaces with an indigo accent.</summary>
    public static ThemePalette Daylight { get; } = new(
        Key: "daylight", Display: "Daylight — light surfaces, indigo accent", IsLight: true,
        TimelineTrack: "#D8DCE6", InsetBackground: "#E9ECF2",
        WindowBackground: "#FAFBFD", PanelBackground: "#F2F3F7",
        ControlBackground: "#E4E7EE", ControlHover: "#D6DAE4",
        RowHover: "#E8EBF2", RowSelected: "#C9D4EE",
        MenuBarBackground: "#E8EAF0", MenuBarForeground: "#2B3040",
        BorderBrush: "#C9CDD8",
        TextPrimary: "#1B1E27", TextBody: "#2B3040", TextDim: "#3D4354",
        TextSecondary: "#5A6070", TextMuted: "#7B8190",
        Accent: "#3B4CA8", AccentBright: "#5566C4",
        PrimaryButton: "#3B4CA8", PrimaryButtonBorder: "#2E3D8C",
        TimelinePosition: "#2C6BB8",
        OnAccent: "#FFFFFF",
        LinkBlue: "#2C6BB8", ValueYellow: "#8A6A18", ValueGreen: "#3F6B2E",
        StatusOk: "#2E7D4F", StatusError: "#B3312C", StatusWarn: "#9A6708",
        OverlayBackground: "#F0FAFBFD", ToastBackground: "#FFFFFFFF",
        IconBackground: "#2B3040", IconBody: "#7C8CE0", IconDetail: "#5566C4");

    /// <summary>Warm paper with a teal accent.</summary>
    /// <remarks>
    /// The second light theme, and warm where Daylight is cool, so the pair
    /// differ in the thing a light theme is actually chosen for — a white that
    /// is too blue to sit in front of all evening is the usual complaint, and
    /// picking the other one is now the answer.
    ///
    /// Teal rather than another blue: the accent lands on text as often as on a
    /// button, and an indigo accent beside <see cref="LinkBlue"/> made a
    /// timestamp and a link the same color at a glance.
    /// </remarks>
    public static ThemePalette Parchment { get; } = new(
        Key: "parchment", Display: "Parchment — warm paper, teal accent", IsLight: true,
        TimelineTrack: "#DCD6C9", InsetBackground: "#EFE9DD",
        WindowBackground: "#FCF9F2", PanelBackground: "#F5F0E6",
        ControlBackground: "#EAE3D5", ControlHover: "#DCD4C4",
        RowHover: "#EFEADD", RowSelected: "#C8DED8",
        MenuBarBackground: "#EFEADF", MenuBarForeground: "#2E2A24",
        BorderBrush: "#D0C8B8",
        TextPrimary: "#221F19", TextBody: "#2E2A24", TextDim: "#443F36",
        TextSecondary: "#635C50", TextMuted: "#837B6D",
        // Only a little brighter than Accent. A light theme has nowhere to go
        // upwards without the hover state fading into the paper behind it;
        // the obvious teal reads at 3.4:1 against the panel, which is below
        // what the rest of the set manages.
        Accent: "#1E6F6B", AccentBright: "#227A74",
        PrimaryButton: "#1E6F6B", PrimaryButtonBorder: "#175754",
        TimelinePosition: "#2C6BB8",
        OnAccent: "#FFFFFF",
        LinkBlue: "#2C6BB8", ValueYellow: "#8A6A18", ValueGreen: "#3F6B2E",
        StatusOk: "#2E7D4F", StatusError: "#B3312C", StatusWarn: "#9A6708",
        OverlayBackground: "#F0FCF9F2", ToastBackground: "#FFFDFBF5",
        IconBackground: "#2E2A24", IconBody: "#3FA5A0", IconDetail: "#2A8F8A");
}
