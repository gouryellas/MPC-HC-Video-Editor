using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Lays out a release note's markdown as themed WPF text.
/// </summary>
/// <remarks>
/// <para>
/// A deliberately small reader, not a markdown engine. Release notes in this
/// project are headings, bullets and paragraphs, and have been for forty-odd
/// versions — so this handles those three and treats anything else as a
/// paragraph. Pulling in a markdown library to render text the same hand
/// writes every time would be a dependency bought for one dialog.
/// </para>
/// <para>
/// Inline <c>**bold**</c> and <c>`code`</c> are honoured because they appear
/// in the older notes. Everything else is left as written: a stray asterisk
/// shows as an asterisk, which is better than silently eating a character
/// somebody meant.
/// </para>
/// </remarks>
public static class NoteRenderer
{
    /// <summary>Builds the blocks for one version's notes.</summary>
    public static IEnumerable<UIElement> Render(string notes)
    {
        foreach (var raw in notes.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.Length == 0)
            {
                // A blank line is spacing, not an empty paragraph — an empty
                // TextBlock still takes a line's height and the gaps double.
                yield return new Border { Height = 6 };
                continue;
            }

            if (trimmed.StartsWith("###"))
            {
                yield return Heading(trimmed.TrimStart('#').Trim());
                continue;
            }

            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("-"))
            {
                yield return Bullet(trimmed.TrimStart('-', '*', ' '));
                continue;
            }

            yield return Paragraph(trimmed);
        }
    }

    private static UIElement Heading(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 4)
        };

        block.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        return block;
    }

    /// <summary>
    /// A bullet and its text, as two columns rather than one wrapped line, so
    /// a note that runs to three lines stays aligned under its own first
    /// word instead of sliding back under the dot.
    /// </summary>
    private static UIElement Bullet(string text)
    {
        var grid = new Grid { Margin = new Thickness(2, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new TextBlock { Text = "•", FontSize = 12, VerticalAlignment = VerticalAlignment.Top };
        dot.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");

        var body = Paragraph(text);
        Grid.SetColumn(body, 1);

        grid.Children.Add(dot);
        grid.Children.Add(body);
        return grid;
    }

    private static TextBlock Paragraph(string text)
    {
        var block = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextDim");

        foreach (var run in Inline(text)) block.Inlines.Add(run);
        return block;
    }

    /// <summary>
    /// Splits a line on <c>**bold**</c> and <c>`code`</c>, leaving unmatched
    /// markers alone.
    /// </summary>
    internal static IEnumerable<Run> Inline(string text)
    {
        int i = 0;

        while (i < text.Length)
        {
            var bold = text.IndexOf("**", i, StringComparison.Ordinal);
            var code = text.IndexOf('`', i);

            // Whichever marker comes first, if either has a closing partner.
            var next = -1;
            var marker = "";

            if (bold >= 0 && text.IndexOf("**", bold + 2, StringComparison.Ordinal) > 0)
            {
                next = bold;
                marker = "**";
            }

            if (code >= 0 && text.IndexOf('`', code + 1) > 0 && (next < 0 || code < next))
            {
                next = code;
                marker = "`";
            }

            if (next < 0)
            {
                yield return new Run(text[i..]);
                yield break;
            }

            if (next > i) yield return new Run(text[i..next]);

            var contentStart = next + marker.Length;
            var close = text.IndexOf(marker, contentStart, StringComparison.Ordinal);
            var content = text[contentStart..close];

            var run = new Run(content);
            if (marker == "**") run.FontWeight = FontWeights.SemiBold;
            else
            {
                run.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                run.SetResourceReference(TextElement.ForegroundProperty, "ValueYellow");
            }

            yield return run;
            i = close + marker.Length;
        }
    }
}
