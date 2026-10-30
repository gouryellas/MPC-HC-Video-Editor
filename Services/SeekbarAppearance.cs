using System.Windows;
using System.Windows.Media;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// What the seekbar's filled position bar, its border, its track and its
/// marks are painted, given the theme and whatever the user has chosen — and
/// what its waveform should be rendered in, which works differently.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ButtonColors"/> for a single subject rather than four
/// families: four brushes, each following the theme until a settings value
/// says otherwise, pushed into Application.Resources under their own keys —
/// <c>SeekbarFill</c>, <c>SeekbarBorder</c>, <c>SeekbarTrack</c>,
/// <c>SeekbarMark</c>, <c>SeekbarPlayhead</c> — rather than overwriting <c>TimelinePosition</c>,
/// <c>BorderBrush</c> or <c>TimelineTrack</c>, which other controls still
/// read directly and have no reason to follow a seekbar-only choice.
///
/// The waveform's colour is not a fifth resource: it is baked into a
/// rendered picture rather than drawn live, so there is nothing here for a
/// <c>DynamicResource</c> to point at. <see cref="ResolveWaveformColor"/>
/// is what the render path asks instead.
/// </remarks>
public static class SeekbarAppearance
{
    /// <summary>Settings value meaning "whatever the theme says".</summary>
    public const string FollowTheme = "theme";

    /// <summary>
    /// What a bracket, a cut's number and its tick are painted before anyone
    /// chooses otherwise. Not drawn from the theme — <see cref="TimelineRanges"/>
    /// never has been, so "theme" here means this fixed value rather than a
    /// palette property that does not exist.
    /// </summary>
    private const string DefaultMark = "#FFFFFF";

    /// <summary>
    /// What the waveform is painted before anyone chooses otherwise — the
    /// same mid grey <see cref="FFmpegService.RenderWaveformAsync"/> has
    /// always defaulted to.
    /// </summary>
    public const string DefaultWaveform = "#8C8C8C";

    /// <summary>Whatever was last applied, for a repaint after a theme change.</summary>
    public static AppSettings? Settings { get; private set; }

    /// <summary>Repaints from the current theme and the settings last applied.</summary>
    public static void Reapply() => Apply(Settings, ThemeService.Current);

    /// <summary>
    /// Pushes the seekbar's brushes into the application's resources, so the
    /// bar repaints itself.
    /// </summary>
    public static void Apply(AppSettings? settings, ThemePalette theme)
    {
        Settings = settings;

        var resources = Application.Current?.Resources;
        if (resources is null) return;

        resources["SeekbarFill"] = Frozen(Resolve(settings?.SeekbarColor, theme.TimelinePosition));
        resources["SeekbarBorder"] = Frozen(Resolve(settings?.SeekbarBorderColor, theme.BorderBrush));
        resources["SeekbarTrack"] = Frozen(Resolve(settings?.SeekbarTrackColor, theme.TimelineTrack));
        resources["SeekbarMark"] = Frozen(Resolve(settings?.SeekbarMarkColor, DefaultMark));

        // The playhead follows the brackets until it is given a colour of its
        // own, which is what it was painted before it had a setting — so
        // nobody's bar changes appearance for having gained the option.
        resources["SeekbarPlayhead"] = Frozen(Resolve(
            settings?.SeekbarPlayheadColor,
            Resolve(settings?.SeekbarMarkColor, DefaultMark)));
    }

    private static string Resolve(string? setting, string themeDefault) =>
        string.IsNullOrEmpty(setting)
        || string.Equals(setting, FollowTheme, StringComparison.OrdinalIgnoreCase)
        || !ButtonColors.IsColour(setting)
            ? themeDefault
            : setting;

    /// <summary>
    /// What the waveform should be rendered in, resolved from settings.
    /// </summary>
    /// <remarks>
    /// Public, unlike the other three colours: those are pushed into
    /// resources and read back through <c>DynamicResource</c>, but the
    /// waveform is baked into a PNG by ffmpeg, so whoever asks for that
    /// render has to resolve the hex itself first.
    /// </remarks>
    public static string ResolveWaveformColor(AppSettings? settings) =>
        Resolve(settings?.SeekbarWaveformColor, DefaultWaveform);

    private static SolidColorBrush Frozen(string hex)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        catch
        {
            var fallback = new SolidColorBrush(Colors.Gray);
            fallback.Freeze();
            return fallback;
        }
    }
}
