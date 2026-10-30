using System.Globalization;
using System.Text;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Builds an output filename from a pattern such as
/// <c>{name}_{number2}{tag}</c>.
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
    /// <summary>The default, which reproduces the old behavior exactly.</summary>
    public const string Default = "{name}{tag}";

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
    /// Built per call rather than held as a list, so the tag's example is the
    /// tag actually in use. It was a fixed "[done]" — the tag this program
    /// ships with — which read as a statement about what {tag} expands to
    /// rather than an example, and was simply wrong for anybody who had
    /// chosen a different one.
    ///
    /// Returned rather than bound to, so the dialog cannot drift from what
    /// <see cref="Build"/> understands.
    /// </remarks>
    /// <param name="tag">
    /// The active tag, brackets and all, or empty when none is selected.
    /// </param>
    public static IReadOnlyList<TemplateVariable> VariablesFor(string tag) => new[]
    {
        new TemplateVariable("{name}",     "holiday-2026", "The source video's filename, without its extension."),
        new TemplateVariable("{tag}",      TagExample(tag), "The naming tag currently selected under Tags."),
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
    /// How the tag reads in the variable list.
    /// </summary>
    /// <remarks>
    /// With no tag selected the expansion really is nothing, and an empty cell
    /// in a table of examples reads as a row that failed to load rather than
    /// as an answer. Saying so is the answer.
    /// </remarks>
    private static string TagExample(string tag) =>
        string.IsNullOrWhiteSpace(tag) ? "(nothing)" : tag;

    /// <summary>
    /// Patterns worth starting from, shown under the variable list.
    /// </summary>
    /// <param name="tag">As <see cref="VariablesFor"/> — the tag in use.</param>
    public static IReadOnlyList<TemplateVariable> ExamplesFor(string tag) => new[]
    {
        new TemplateVariable("{name}{tag}", $"holiday-2026{tag}.mp4",
            "The default — the source name with the naming tag on the end."),
        new TemplateVariable("{name}_{number2}{tag}", $"holiday-2026_03{tag}.mp4",
            "Numbered, so a batch of clips stays in order in the folder."),
        new TemplateVariable("{name}_{start}-{end}{tag}", $"holiday-2026_0-01-23-0-01-45{tag}.mp4",
            "Timestamped, so each clip says where in the source it came from."),
        new TemplateVariable("{number2}_{duration}s_{name}{tag}", $"03_22s_holiday-2026{tag}.mp4",
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
    public static string Build(string? template, string sourceName, string tag, Bookmark? bookmark)
    {
        if (string.IsNullOrWhiteSpace(template)) template = Default;

        var sb = new StringBuilder(template);
        sb.Replace("{name}", sourceName);
        sb.Replace("{tag}", tag ?? string.Empty);

        // These were called suffixes until the whole program settled on
        // "tag". Kept working, undocumented, for the same reason {index} is
        // below: a pattern saved under an earlier release would otherwise
        // start writing the literal text "{suffix}" into every filename.
        sb.Replace("{suffix}", tag ?? string.Empty);
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
    /// <param name="tag">
    /// The naming tag in force, bracket and all. Passed in rather than made up:
    /// the preview used a hardcoded "[done]" while the row above it promised
    /// "the naming tag currently selected under Tags", so the one line on
    /// screen showing what files will be called was wrong for anyone who had
    /// picked a different tag.
    /// </param>
    public static string Preview(string? template, string tag = "[done]")
    {
        var sample = new Bookmark { Index = 3, StartSeconds = 83, EndSeconds = 105 };
        return Build(template, "holiday-2026", tag, sample) + ".mp4";
    }
}
