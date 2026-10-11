using System.Windows;
using System.Windows.Media;

namespace MpcHcVideoEditor.Services;

/// <summary>The groups the coloured operations fall into.</summary>
public enum ButtonFamily
{
    /// <summary>Not a coloured operation — the cut-list controls.</summary>
    Plain,

    /// <summary>Writes or rewrites a video: merge, split, convert, crop, chapters.</summary>
    Video,

    /// <summary>Strip audio.</summary>
    Audio,

    /// <summary>Writes pictures: frames, thumbnails, animations, conversions, resizes.</summary>
    Image,

    /// <summary>Looks rather than writes: image info, find clips.</summary>
    Other
}

/// <summary>
/// What each family of operations is painted, given the theme and whatever the
/// user has chosen.
/// </summary>
/// <remarks>
/// One place decides, and everything that draws an operation reads from here
/// through <c>DynamicResource</c>: the toolbar buttons, and the squares beside
/// the menu entries. Two places working it out separately is how the Playlist
/// menu came to contradict itself, and it would be worse here — the two would
/// simply be different colours.
///
/// The label is computed, never chosen. Given a background there is exactly one
/// sensible answer, and offering the choice would mainly let someone make a
/// button unreadable.
/// </remarks>
public static class ButtonColors
{
    /// <summary>Settings value meaning "whatever the theme says".</summary>
    public const string FollowTheme = "theme";

    /// <summary>Settings value meaning "no colour at all".</summary>
    public const string NoColour = "none";

    /// <summary>Whatever was last applied, for a repaint after a theme change.</summary>
    public static IReadOnlyDictionary<string, string>? Chosen { get; private set; }

    /// <summary>Repaints the families from the current theme and settings.</summary>
    public static void Reapply() => Apply(Chosen, ThemeService.Current);

    /// <summary>The families a user can recolour, in the order they are listed.</summary>
    public static readonly ButtonFamily[] Families =
        { ButtonFamily.Video, ButtonFamily.Audio, ButtonFamily.Image, ButtonFamily.Other };

    /// <summary>The settings key for a family.</summary>
    public static string KeyOf(ButtonFamily family) => family.ToString().ToLowerInvariant();

    /// <summary>What a family is called in the dialog.</summary>
    public static string NameOf(ButtonFamily family) => family switch
    {
        ButtonFamily.Video => "Video",
        ButtonFamily.Audio => "Audio",
        ButtonFamily.Image => "Images",
        ButtonFamily.Other => "Everything else",
        _ => "Plain"
    };

    /// <summary>The operations in each family, by toolbar key.</summary>
    /// <remarks>
    /// Only the operations. The cut-list controls — timestamps, the modifiers,
    /// select all, the naming tags — stay plain: colour is for telling the
    /// handful of things that write files apart, and colouring all twenty-seven
    /// buttons would tell you nothing at all.
    /// </remarks>
    private static readonly Dictionary<string, ButtonFamily> Membership = new()
    {
        ["merge"] = ButtonFamily.Video,
        ["split"] = ButtonFamily.Video,
        ["convert"] = ButtonFamily.Video,
        ["crop"] = ButtonFamily.Video,
        ["export-chapters"] = ButtonFamily.Video,

        ["strip-audio"] = ButtonFamily.Audio,

        ["export-animation"] = ButtonFamily.Image,
        ["save-frame"] = ButtonFamily.Image,
        ["thumbnails"] = ButtonFamily.Image,
        ["convert-images"] = ButtonFamily.Image,
        ["resize-images"] = ButtonFamily.Image,

        ["image-info"] = ButtonFamily.Other,
        ["detect"] = ButtonFamily.Other,
    };

    /// <summary>Which family a toolbar entry belongs to.</summary>
    public static ButtonFamily FamilyOf(string? key) =>
        key is not null && Membership.TryGetValue(key, out var family) ? family : ButtonFamily.Plain;

    /// <summary>The resource key of the style that paints a family's buttons.</summary>
    public static string? StyleKeyOf(ButtonFamily family) =>
        family == ButtonFamily.Plain ? null : $"{family}Button";

    /// <summary>
    /// Pushes every family's three brushes into the application's resources, so
    /// the styles and the menu squares repaint themselves.
    /// </summary>
    /// <param name="chosen">
    /// The user's settings: a family key to <see cref="FollowTheme"/>,
    /// <see cref="NoColour"/>, or a hex colour. Anything absent follows the
    /// theme.
    /// </param>
    public static void Apply(IReadOnlyDictionary<string, string>? chosen, ThemePalette theme)
    {
        // Kept so a theme change can repaint without the caller having to hand
        // the settings over again — the settings dialog changes the palette
        // live, from a place that has no business knowing about these.
        Chosen = chosen;

        var resources = Application.Current?.Resources;
        if (resources is null) return;

        foreach (var family in Families)
        {
            var look = Resolve(family, chosen, theme);

            resources[$"{family}Fill"] = Frozen(look.Fill);
            resources[$"{family}Ink"] = Frozen(look.Ink);
            resources[$"{family}Edge"] = Frozen(look.Edge);
        }
    }

    /// <summary>A family's fill, the ink that goes on it, and its edge.</summary>
    public sealed record Look(string Fill, string Ink, string Edge);

    /// <summary>
    /// What a family looks like right now.
    /// </summary>
    /// <remarks>
    /// "None" resolves to the ordinary control colours rather than to nothing.
    /// The button keeps its style and simply stops standing out, which means
    /// turning a family's colour off needs no second code path anywhere — and
    /// unlike a structural change, it takes effect the moment the setting moves.
    /// </remarks>
    public static Look Resolve(ButtonFamily family,
                               IReadOnlyDictionary<string, string>? chosen,
                               ThemePalette theme)
    {
        var setting = chosen is not null && chosen.TryGetValue(KeyOf(family), out var value)
            ? value
            : FollowTheme;

        if (string.Equals(setting, NoColour, StringComparison.OrdinalIgnoreCase))
            return new Look(theme.ControlBackground, theme.TextPrimary, theme.BorderBrush);

        var fill = string.Equals(setting, FollowTheme, StringComparison.OrdinalIgnoreCase) || !IsColour(setting)
            ? ThemeColour(family, theme)
            : setting;

        return new Look(fill, InkFor(fill), EdgeFor(fill));
    }

    private static string ThemeColour(ButtonFamily family, ThemePalette theme) => family switch
    {
        ButtonFamily.Video => theme.OperationVideo,
        ButtonFamily.Audio => theme.OperationAudio,
        ButtonFamily.Image => theme.OperationImage,
        ButtonFamily.Other => theme.OperationOther,
        _ => theme.ControlBackground
    };

    /// <summary>Whether a settings value is a colour this can use.</summary>
    public static bool IsColour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] != '#') return false;

        try
        {
            ColorConverter.ConvertFromString(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------
    // Deciding the label
    // ------------------------------------------------------------------

    /// <summary>Near-black, for labels on a light fill.</summary>
    public const string DarkInk = "#141210";

    /// <summary>Near-white, for labels on a dark fill.</summary>
    public const string LightInk = "#F7F4F0";

    /// <summary>
    /// Whichever of the two inks reads better on this fill.
    /// </summary>
    /// <remarks>
    /// Computed rather than chosen. A background has exactly one sensible
    /// answer here, the arithmetic is five lines, and a control for it would
    /// mostly be a way to end up with a button nobody can read.
    /// </remarks>
    public static string InkFor(string fill) =>
        Contrast(fill, LightInk) >= Contrast(fill, DarkInk) ? LightInk : DarkInk;

    /// <summary>
    /// The edge: the fill, lifted or dropped towards whichever end it is
    /// further from, so the button has a rim on any background.
    /// </summary>
    private static string EdgeFor(string fill)
    {
        var c = Parse(fill);
        var lift = Luminance(fill) < 0.45 ? 40 : -40;

        byte Shift(byte v) => (byte)Math.Clamp(v + lift, 0, 255);

        return $"#{Shift(c.R):X2}{Shift(c.G):X2}{Shift(c.B):X2}";
    }

    /// <summary>
    /// How well two colours stand apart, by the WCAG formula. 4.5 is the
    /// threshold below which ordinary text stops being comfortable.
    /// </summary>
    public static double Contrast(string a, string b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);

        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>The best contrast a label can manage on this fill.</summary>
    public static double BestInkContrast(string fill) =>
        Math.Max(Contrast(fill, LightInk), Contrast(fill, DarkInk));

    private static double Luminance(string hex)
    {
        var c = Parse(hex);

        double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static Color Parse(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.Gray; }
    }

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush(Parse(hex));
        brush.Freeze();
        return brush;
    }
}
