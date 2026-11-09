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
    /// <remarks>
    /// Works in blocks rather than lines, which is what markdown means and
    /// what makes the older notes read like the newer ones. Releases up to
    /// 5.7 are written as prose hard-wrapped at about eighty columns; taking
    /// each source line as its own paragraph turned one wrapped sentence into
    /// six blocks, each re-wrapping on its own inside a panel a third that
    /// width. The result was ragged lines breaking in the wrong places, and
    /// the continuation of a wrapped bullet sitting at the margin as though
    /// it were a new one.
    ///
    /// Consecutive non-blank lines are therefore one paragraph, and a blank
    /// line ends it — so the panel decides where lines break, which is the
    /// only thing that can, knowing its own width.
    /// </remarks>
    public static IEnumerable<UIElement> Render(string notes)
    {
        var lines = notes.Replace("\r\n", "\n").Split('\n');

        var run = new List<string>();      // the block being gathered
        var bullet = false;                // ...and whether it is a bullet
        var fenced = false;                // inside a ``` block
        var code = new List<string>();

        IEnumerable<UIElement> Flush()
        {
            if (run.Count == 0) yield break;

            var text = string.Join(" ", run).Trim();
            run.Clear();

            if (text.Length == 0) yield break;
            yield return bullet ? Bullet(text) : Paragraph(text);
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            // Code fences are taken verbatim — they are the one thing in
            // these notes whose line breaks and spacing carry meaning.
            if (trimmed.StartsWith("```"))
            {
                if (fenced)
                {
                    yield return CodeBlock(string.Join("\n", code));
                    code.Clear();
                    fenced = false;
                }
                else
                {
                    foreach (var block in Flush()) yield return block;
                    fenced = true;
                }
                continue;
            }

            if (fenced) { code.Add(line); continue; }

            if (trimmed.Length == 0)
            {
                foreach (var block in Flush()) yield return block;

                // Spacing, not an empty paragraph: an empty TextBlock still
                // takes a line's height and the gaps double.
                yield return new Border { Height = 6 };
                continue;
            }

            if (trimmed.StartsWith("#"))
            {
                foreach (var block in Flush()) yield return block;
                yield return Heading(trimmed.TrimStart('#').Trim());
                continue;
            }

            if (IsBulletStart(trimmed))
            {
                foreach (var block in Flush()) yield return block;
                bullet = true;
                run.Add(trimmed.Substring(1).TrimStart());
                continue;
            }

            // Anything else continues whatever is open. A line following a
            // bullet belongs to that bullet, which is how the wrapped ones
            // in the older notes were written.
            run.Add(trimmed);
        }

        if (fenced && code.Count > 0) yield return CodeBlock(string.Join("\n", code));
        foreach (var block in Flush()) yield return block;
    }

    /// <summary>
    /// Whether a line opens a bullet. Both markers, because the earliest
    /// releases used <c>*</c> and everything since has used <c>-</c>.
    /// </summary>
    /// <remarks>
    /// The marker must be followed by a space. Without that check a line
    /// beginning with an em dash or with <c>**bold**</c> would be eaten as a
    /// bullet and lose its first character.
    /// </remarks>
    private static bool IsBulletStart(string trimmed) =>
        trimmed.Length > 1 && (trimmed[0] == '-' || trimmed[0] == '*') && trimmed[1] == ' ';

    /// <summary>
    /// A fenced block, kept as written and in a fixed-width face.
    /// </summary>
    private static UIElement CodeBlock(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0, 2, 0, 2)
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextDim");

        var shell = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 4, 0, 6)
        };
        shell.SetResourceReference(Border.BackgroundProperty, "InsetBackground");

        // Scrolls rather than wraps: a diagram or a pasted panel means
        // nothing once its columns have been folded.
        shell.Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = block
        };

        return shell;
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
