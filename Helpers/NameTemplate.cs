using System.Globalization;
using System.Text;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Builds an output filename from a pattern such as
/// <c>{name}_{start}-{end}{suffix}</c>.
/// </summary>
/// <remarks>
/// <para>
/// The naming tags already cover "put [done] on the end". A template covers
/// what they cannot: putting the clip's position, its number or its length into
/// the name, which is the difference between twelve files called
/// <c>video[done](1..12)</c> and twelve you can identify without opening them.
/// </para>
/// <para>
/// The result still goes through <see cref="FileNameRules"/>. A template is a
/// convenience, not a way around the project's filename policy — and a user who
/// types a colon into one should get a usable name rather than a path the
/// filesystem rejects.
/// </para>
/// </remarks>
public static class NameTemplate
{
    /// <summary>The default, which reproduces the old behaviour exactly.</summary>
    public const string Default = "{name}{suffix}";

    /// <summary>Every token, with what it expands to, for the settings hint.</summary>
    public static readonly (string Token, string Meaning)[] Tokens =
    {
        ("{name}",     "the source video's filename, without extension"),
        ("{suffix}",   "the active naming tag, e.g. [done]"),
        ("{index}",    "the clip's number in the list, e.g. 3"),
        ("{index2}",   "the same, padded to two digits, e.g. 03"),
        ("{start}",    "the clip's start, as 0-01-23"),
        ("{end}",      "the clip's end, as 0-01-45"),
        ("{duration}", "how long the clip runs, in whole seconds"),
        ("{date}",     "today's date, as 2026-08-24")
    };

    /// <summary>
    /// Expands <paramref name="template"/> for one clip.
    /// </summary>
    /// <param name="bookmark">
    /// The clip being written, or null for whole-file operations — the
    /// clip-specific tokens then expand to nothing rather than to a misleading
    /// zero.
    /// </param>
    public static string Build(string? template, string sourceName, string suffix, Bookmark? bookmark)
    {
        if (string.IsNullOrWhiteSpace(template)) template = Default;

        var sb = new StringBuilder(template);
        sb.Replace("{name}", sourceName);
        sb.Replace("{suffix}", suffix ?? string.Empty);
        sb.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        if (bookmark is null)
        {
            foreach (var token in new[] { "{index}", "{index2}", "{start}", "{end}", "{duration}" })
                sb.Replace(token, string.Empty);
        }
        else
        {
            sb.Replace("{index}", bookmark.Index.ToString(CultureInfo.InvariantCulture));
            sb.Replace("{index2}", bookmark.Index.ToString("00", CultureInfo.InvariantCulture));
            sb.Replace("{start}", Stamp(bookmark.StartSeconds));
            sb.Replace("{end}", Stamp(bookmark.EndSeconds));
            sb.Replace("{duration}",
                ((int)Math.Round(bookmark.EndSeconds - bookmark.StartSeconds)).ToString(CultureInfo.InvariantCulture));
        }

        // Sanitize rather than merely validate: a template is applied to every
        // clip in a batch, so stopping to ask about each one would be useless.
        return FileNameRules.Sanitize(sb.ToString());
    }

    /// <summary>
    /// A timestamp safe for a filename — <c>0-01-23</c> rather than
    /// <c>0:01:23</c>, since a colon cannot appear in a Windows path.
    /// </summary>
    private static string Stamp(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)t.TotalHours}-{t.Minutes:00}-{t.Seconds:00}";
    }

    /// <summary>
    /// What <paramref name="template"/> produces for a made-up clip, for the
    /// live preview in Settings.
    /// </summary>
    public static string Preview(string? template)
    {
        var sample = new Bookmark { Index = 3, StartSeconds = 83, EndSeconds = 105 };
        return Build(template, "holiday-2026", "[done]", sample) + ".mp4";
    }
}
