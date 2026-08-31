using System.Globalization;
using System.Text;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Builds an output filename from a pattern such as
/// <c>{name}_{number2}{suffix}</c>.
/// </summary>
/// <remarks>
/// <para>
/// The naming tags already cover "put [done] on the end". A pattern covers what
/// they cannot: putting the clip's position, its length or its timestamps into
/// the name, which is the difference between twelve files called
/// <c>video[done](1..12)</c> and twelve you can identify without opening them.
/// </para>
/// <para>
/// The result still goes through <see cref="FileNameRules"/>. A pattern is a
/// convenience, not a way around the project's filename policy.
/// </para>
/// </remarks>
public static class NameTemplate
{
    /// <summary>The default, which reproduces the old behaviour exactly.</summary>
    public const string Default = "{name}{suffix}";

    /// <summary>One variable, a worked example of it, and what it means.</summary>
    /// <remarks>
    /// A record rather than a tuple because the settings dialog binds to these:
    /// a ValueTuple's element names exist only at compile time, so
    /// <c>{Binding Name}</c> against one silently shows nothing.
    /// </remarks>
    public sealed record TemplateVariable(string Name, string Example, string Meaning);

    /// <summary>
    /// Every variable, with a worked example, for the settings dialog.
    /// </summary>
    /// <remarks>
    /// Bound to rather than duplicated in XAML, so the list on screen cannot
    /// drift from the one <see cref="Build"/> actually understands.
    /// </remarks>
    public static readonly IReadOnlyList<TemplateVariable> Variables = new[]
    {
        new TemplateVariable("{name}",     "holiday-2026", "The source video's filename, without its extension."),
        new TemplateVariable("{suffix}",   "[done]",       "The naming tag currently selected under Options."),
        new TemplateVariable("{number}",   "3",            "Which clip this is — 1 for the first in the list, 2 for the second."),
        new TemplateVariable("{number2}",  "03",           "The same number written to two digits. Ten clips then sort 01, 02 … 10 instead of 1, 10, 2."),
        new TemplateVariable("{number3}",  "003",          "Three digits, for lists longer than ninety-nine."),
        new TemplateVariable("{start}",    "0-01-23",      "Where the clip begins, as hours-minutes-seconds."),
        new TemplateVariable("{end}",      "0-01-45",      "Where the clip ends."),
        new TemplateVariable("{duration}", "22",           "How long the clip runs, in whole seconds."),
        // Computed, not written out: a hardcoded date is wrong from the day
        // after it is typed, and an example that lies is worse than none.
        new TemplateVariable("{date}", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "Today's date.")
    };

    /// <summary>
    /// Patterns worth starting from, shown under the variable list.
    /// </summary>
    public static readonly IReadOnlyList<TemplateVariable> Examples = new[]
    {
        new TemplateVariable("{name}{suffix}", "holiday-2026[done].mp4",
            "The default — the source name with the naming tag on the end."),
        new TemplateVariable("{name}_{number2}{suffix}", "holiday-2026_03[done].mp4",
            "Numbered, so a batch of clips stays in order in the folder."),
        new TemplateVariable("{name}_{start}-{end}{suffix}", "holiday-2026_0-01-23-0-01-45[done].mp4",
            "Timestamped, so each clip says where in the source it came from."),
        new TemplateVariable("{number2}_{duration}s_{name}{suffix}", "03_22s_holiday-2026[done].mp4",
            "Number and length first, for skimming a folder at a glance.")
    };

    /// <summary>
    /// Expands <paramref name="template"/> for one clip.
    /// </summary>
    /// <param name="bookmark">
    /// The clip being written, or null for whole-file operations — the
    /// clip-specific variables then expand to nothing rather than to a
    /// misleading zero.
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
            foreach (var v in new[] { "{number}", "{number2}", "{number3}",
                                      "{index}", "{index2}",
                                      "{start}", "{end}", "{duration}" })
                sb.Replace(v, string.Empty);
        }
        else
        {
            var n = bookmark.Index;
            sb.Replace("{number3}", n.ToString("000", CultureInfo.InvariantCulture));
            sb.Replace("{number2}", n.ToString("00", CultureInfo.InvariantCulture));
            sb.Replace("{number}", n.ToString(CultureInfo.InvariantCulture));

            // 4.0 shipped these two names before they were reconsidered. Kept
            // working, undocumented, so a pattern saved under that release does
            // not quietly start producing the literal text "{index}".
            sb.Replace("{index2}", n.ToString("00", CultureInfo.InvariantCulture));
            sb.Replace("{index}", n.ToString(CultureInfo.InvariantCulture));

            sb.Replace("{start}", Stamp(bookmark.StartSeconds));
            sb.Replace("{end}", Stamp(bookmark.EndSeconds));
            sb.Replace("{duration}",
                ((int)Math.Round(bookmark.EndSeconds - bookmark.StartSeconds)).ToString(CultureInfo.InvariantCulture));
        }

        // Sanitize rather than merely validate: a pattern is applied to every
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
