using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Chooses what each family of operations is painted.
/// </summary>
/// <remarks>
/// Four rows, because there are four families and nothing is gained by letting
/// every button be recoloured on its own: the colour is there to say what kind
/// of thing a button does, and twenty-seven separate answers would say nothing.
///
/// The label is never chosen here. Given a fill there is one sensible answer,
/// so it is computed — and where no answer is comfortable, the row says so
/// rather than refusing the colour. See <see cref="ButtonColors.InkFor"/>.
/// </remarks>
public partial class ButtonColorsDialog : Window
{
    /// <summary>
    /// The colours a family can be given without opening anything: a spread
    /// across the wheel, all dark or light enough to carry a label.
    /// </summary>
    private static readonly string[] Presets =
    {
        "#D9A82E", "#C9972A", "#B8792C", "#8A4A18", "#99302E", "#8C3A52",
        "#E07BA8", "#A8406B", "#64357F", "#5E4C86", "#255FA8", "#1F5AA8",
        "#1B4A87", "#2A5B8C", "#1C6E6E", "#246F66", "#15704A", "#3A6B55",
        "#4F4B46", "#2A2724", "#141414", "#F2F0EC",
    };

    private readonly Dictionary<ButtonFamily, string> _chosen = new();
    private readonly Dictionary<ButtonFamily, Action> _repaint = new();

    /// <summary>The settings to store, once the dialog returns true.</summary>
    public Dictionary<string, string> Result { get; } = new();

    public ButtonColorsDialog(IReadOnlyDictionary<string, string>? current)
    {
        InitializeComponent();

        // Four families of swatches stack up taller than a 1366x768 screen, or
        // a 1080p one at 125%. The cards scroll; this is what makes the window
        // stop growing so that the buttons under them stay on screen. Done once
        // the window has a handle, so the monitor asked about is the one it is
        // actually appearing on.
        SourceInitialized += (_, _) => ScreenFit.Cap(this);

        foreach (var family in ButtonColors.Families)
        {
            _chosen[family] = current is not null
                              && current.TryGetValue(ButtonColors.KeyOf(family), out var value)
                ? value
                : ButtonColors.DefaultFor(family);

            Rows.Items.Add(Row(family));
        }
    }

    // ------------------------------------------------------------------
    // One family
    // ------------------------------------------------------------------

    /// <summary>
    /// One family: what it covers, what it currently looks like, and the three
    /// things it can be set to.
    /// </summary>
    /// <remarks>
    /// Laid out as a card with the question on the left and the answer on the
    /// right. The sample used to sit at the start of the row, where it read as
    /// a button to press rather than as a picture of one — so it is now on its
    /// own, under the word Preview, and refuses the mouse.
    /// </remarks>
    private UIElement Row(ButtonFamily family)
    {
        var card = new Border
        {
            Background = ThemeService.Brush(nameof(ThemePalette.PanelBackground)),
            BorderBrush = ThemeService.Brush(nameof(ThemePalette.BorderBrush)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var panel = new StackPanel();
        card.Child = panel;

        // ---- the name, and what is in the family ----
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titles = new StackPanel();

        titles.Children.Add(new TextBlock
        {
            Text = ButtonColors.NameOf(family),
            Foreground = ThemeService.Brush(nameof(ThemePalette.Accent)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        });

        titles.Children.Add(new TextBlock
        {
            Text = ButtonColors.Describe(family),
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextSecondary)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 12, 0)
        });

        header.Children.Add(titles);

        // ---- the sample, captioned so it cannot be mistaken for a control ----
        var sample = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            MinWidth = 132,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };

        var sampleText = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        sample.Child = sampleText;

        var previewBlock = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };

        previewBlock.Children.Add(new TextBlock
        {
            Text = "PREVIEW",
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextMuted)),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3)
        });

        previewBlock.Children.Add(sample);

        Grid.SetColumn(previewBlock, 1);
        header.Children.Add(previewBlock);

        panel.Children.Add(header);

        // ---- the three answers ----
        var choices = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var group = "family" + family;

        var theme = Radio("Follow the theme", group, () => Set(family, ButtonColors.FollowTheme));
        var none = Radio("No colour", group, () => Set(family, ButtonColors.NoColour));
        var pick = Radio("Pick one:", group, () => Set(family, FirstPreset(family)));

        choices.Children.Add(theme);
        choices.Children.Add(none);
        choices.Children.Add(pick);

        panel.Children.Add(choices);

        // ---- the swatches ----
        var swatches = new WrapPanel { Margin = new Thickness(22, 6, 0, 0) };
        var marks = new Dictionary<string, Border>();

        foreach (var hex in Presets)
        {
            var mark = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = Fill(hex),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 0, 5, 5),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = hex
            };

            mark.MouseLeftButtonUp += (_, _) => Set(family, hex);

            marks[hex] = mark;
            swatches.Children.Add(mark);
        }

        panel.Children.Add(swatches);

        var warning = new TextBlock
        {
            Foreground = ThemeService.Brush(nameof(ThemePalette.StatusWarn)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(22, 2, 0, 0),
            Visibility = Visibility.Collapsed
        };

        panel.Children.Add(warning);

        // Everything the card shows comes from the one setting, so a single
        // method repaints the lot and it can never be half updated.
        _repaint[family] = () =>
        {
            var setting = _chosen[family];
            var custom = ButtonColors.IsColour(setting);
            var look = ButtonColors.Resolve(family, Settings(), ThemeService.Current);

            sample.Background = Fill(look.Fill);
            sample.BorderBrush = Fill(look.Edge);
            sampleText.Foreground = Fill(look.Ink);
            sampleText.Text = Glyph(family) + "  " + Example(family);

            theme.IsChecked = setting == ButtonColors.FollowTheme;
            none.IsChecked = setting == ButtonColors.NoColour;
            pick.IsChecked = custom;

            // The swatches are only live once "Pick one" is the answer, and the
            // chosen one wears a ring. Without it nothing on the card said
            // which of the twenty-two was in use.
            swatches.Opacity = custom ? 1.0 : 0.45;

            foreach (var (hex, mark) in marks)
                mark.BorderBrush = custom && string.Equals(hex, setting, StringComparison.OrdinalIgnoreCase)
                    ? ThemeService.Brush(nameof(ThemePalette.AccentBright))
                    : Fill(hex);

            // Said, not forbidden. It is their program, and a mid grey is a
            // legitimate thing to want even though nothing reads well on it.
            var best = ButtonColors.BestInkContrast(look.Fill);
            warning.Visibility = best >= 4.5 ? Visibility.Collapsed : Visibility.Visible;
            warning.Text = $"Hard to read — the best a label can manage on this colour is {best:0.0}:1. " +
                           "Try something darker or lighter.";
        };

        _repaint[family]();
        return card;
    }

    /// <summary>One of the three answers.</summary>
    private RadioButton Radio(string text, string group, Action chosen)
    {
        var radio = new RadioButton
        {
            Content = text,
            GroupName = group,
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextPrimary)),
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        // Checked rather than Click: setting IsChecked in code must not look
        // like a choice, and only a real one raises this from the pointer.
        radio.Click += (_, _) => chosen();
        return radio;
    }

    /// <summary>
    /// What "Pick one" starts from: whatever the family is wearing now, so
    /// choosing it changes nothing until a swatch is clicked.
    /// </summary>
    private string FirstPreset(ButtonFamily family)
    {
        var current = _chosen[family];
        if (ButtonColors.IsColour(current)) return current;

        return ButtonColors.Resolve(family, null, ThemeService.Current).Fill;
    }

    /// <summary>A short label for the sample, so it reads as a real button.</summary>
    private static string Example(ButtonFamily family) => family switch
    {
        ButtonFamily.Video => "Merge",
        ButtonFamily.Audio => "Strip audio",
        ButtonFamily.Image => "Resize images",
        _ => "Set timestamp"
    };

    private void Set(ButtonFamily family, string value)
    {
        _chosen[family] = value;
        _repaint[family]();
    }

    /// <summary>The choices as the resolver wants them.</summary>
    private Dictionary<string, string> Settings() =>
        _chosen.ToDictionary(p => ButtonColors.KeyOf(p.Key), p => p.Value);

    /// <summary>A glyph standing for the family, taken from its own operations.</summary>
    private static string Glyph(ButtonFamily family) => family switch
    {
        ButtonFamily.Video => "🎬",
        ButtonFamily.Audio => "🔊",
        ButtonFamily.Image => "🖼",
        _ => "📍"
    };

    private static SolidColorBrush Fill(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return new SolidColorBrush(Colors.Gray); }
    }

    // ------------------------------------------------------------------
    // Buttons
    // ------------------------------------------------------------------

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        foreach (var family in ButtonColors.Families) Set(family, ButtonColors.DefaultFor(family));
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result.Clear();

        foreach (var (family, value) in _chosen)
        {
            // A family left at its default is stored as nothing at all, so a
            // settings file only ever holds the choices actually made — and a
            // later change to what the default is reaches everybody who never
            // chose.
            if (value == ButtonColors.DefaultFor(family)) continue;
            Result[ButtonColors.KeyOf(family)] = value;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
