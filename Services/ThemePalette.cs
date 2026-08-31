namespace MpcHcVideoEditor.Services;

/// <summary>
/// A complete set of colours for the interface.
/// </summary>
/// <remarks>
/// <para>
/// Every colour the XAML uses resolves to one of these names. The application
/// previously spelled out 55 distinct hex values across twelve files, which
/// meant a change of appearance was a find-and-replace exercise and a new
/// theme was not realistically possible at all.
/// </para>
/// <para>
/// Names describe the <em>role</em>, not the colour — <c>Accent</c> rather than
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
    /// Text sitting on <see cref="PrimaryButton"/> and the coloured action
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

    // The coloured action buttons on the toolbar.
    string MergeBackground,
    string MergeBorder,
    string SplitBackground,
    string SplitBorder,
    string ConvertBackground,
    string ConvertBorder,
    string AudioBackground,
    string AudioBorder,

    // Overlay windows, which float over the player and carry their own alpha.
    string OverlayBackground,
    string ToastBackground,

    // The icon, drawn from these rather than shipped per theme.
    string IconBackground,
    string IconBody,
    string IconDetail)
{
    /// <summary>Every theme, in the order the settings dialog lists them.</summary>
    /// <remarks>
    /// Computed on access rather than stored in a field. As a field initialised
    /// here it ran before the three palettes below were constructed — static
    /// initialisers execute in declaration order — so it captured three nulls,
    /// which would have left the settings list empty and every lookup falling
    /// back to the default.
    /// </remarks>
    public static IReadOnlyList<ThemePalette> All => new[] { Graphite, Midnight, Daylight };

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
        OnAccent: "#FFF6EA",
        LinkBlue: "#D8A96A", ValueYellow: "#E8C87E", ValueGreen: "#BFCF95",
        StatusOk: "#A9C98A", StatusError: "#E4736B", StatusWarn: "#E0A253",
        MergeBackground: "#7A5320", MergeBorder: "#A87423",
        SplitBackground: "#4A5A2C", SplitBorder: "#5D7038",
        ConvertBackground: "#6B3A2E", ConvertBorder: "#8A4A3A",
        AudioBackground: "#6B5A20", AudioBorder: "#8A7429",
        OverlayBackground: "#F01A1817", ToastBackground: "#FF171514",
        IconBackground: "#221F1D", IconBody: "#E0A253", IconDetail: "#C9853A");

    /// <summary>Cool blue-grey with a cyan accent.</summary>
    public static ThemePalette Midnight { get; } = new(
        Key: "midnight", Display: "Midnight — cool blue-grey, cyan accent", IsLight: false,
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
        OnAccent: "#F0FAFD",
        LinkBlue: "#6FB3E8", ValueYellow: "#E8D48E", ValueGreen: "#9FD8B8",
        StatusOk: "#8FD4A8", StatusError: "#E8706E", StatusWarn: "#E8B366",
        MergeBackground: "#1F5F7A", MergeBorder: "#2E7E9E",
        SplitBackground: "#1D5C4E", SplitBorder: "#2A7A68",
        ConvertBackground: "#4A3570", ConvertBorder: "#63498F",
        AudioBackground: "#5A4A73", AudioBorder: "#756294",
        OverlayBackground: "#F00F151E", ToastBackground: "#FF0C1119",
        IconBackground: "#161F2B", IconBody: "#5AC8E0", IconDetail: "#3E9DB5");

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
        OnAccent: "#FFFFFF",
        LinkBlue: "#2C6BB8", ValueYellow: "#8A6A18", ValueGreen: "#3F6B2E",
        StatusOk: "#2E7D4F", StatusError: "#B3312C", StatusWarn: "#9A6708",
        MergeBackground: "#3B4CA8", MergeBorder: "#2E3D8C",
        SplitBackground: "#1D7A5F", SplitBorder: "#166049",
        ConvertBackground: "#8A4A9E", ConvertBorder: "#703A82",
        AudioBackground: "#9A6708", AudioBorder: "#7C5206",
        OverlayBackground: "#F0FAFBFD", ToastBackground: "#FFFFFFFF",
        IconBackground: "#2B3040", IconBody: "#7C8CE0", IconDetail: "#5566C4");
}
