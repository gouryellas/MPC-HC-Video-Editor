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

        foreach (var family in ButtonColors.Families)
        {
            _chosen[family] = current is not null
                              && current.TryGetValue(ButtonColors.KeyOf(family), out var value)
                ? value
                : ButtonColors.FollowTheme;

            Rows.Items.Add(Row(family));
        }
    }

    // ------------------------------------------------------------------
    // One family
    // ------------------------------------------------------------------

    private UIElement Row(ButtonFamily family)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

        var top = new StackPanel { Orientation = Orientation.Horizontal };

        var preview = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            MinWidth = 150,
            VerticalAlignment = VerticalAlignment.Center
        };

        var previewText = new TextBlock { FontWeight = FontWeights.SemiBold };
        preview.Child = previewText;

        top.Children.Add(preview);

        var name = new TextBlock
        {
            Text = ButtonColors.NameOf(family),
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextPrimary)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            FontWeight = FontWeights.SemiBold
        };
        top.Children.Add(name);

        var theme = Choice("Theme", () => Set(family, ButtonColors.FollowTheme));
        var none = Choice("None", () => Set(family, ButtonColors.NoColour));

        top.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 0, 0),
            Children = { theme, none }
        });

        panel.Children.Add(top);

        // The swatches. A grid of colours rather than a colour wheel: picking
        // from twenty is what nearly everybody does anyway, and a wheel is a
        // great deal of code to let somebody choose a shade of blue the grid
        // already has.
        var swatches = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };

        foreach (var hex in Presets)
        {
            var swatch = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(3),
                Background = Fill(hex),
                BorderBrush = ThemeService.Brush(nameof(ThemePalette.BorderBrush)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = hex
            };

            swatch.MouseLeftButtonUp += (_, _) => Set(family, hex);
            swatches.Children.Add(swatch);
        }

        panel.Children.Add(swatches);

        var warning = new TextBlock
        {
            Foreground = ThemeService.Brush(nameof(ThemePalette.StatusWarn)),
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0),
            Visibility = Visibility.Collapsed
        };

        panel.Children.Add(warning);

        // Everything the row shows is derived from the one setting, so one
        // method repaints the lot and the row can never be half updated.
        _repaint[family] = () =>
        {
            var setting = _chosen[family];
            var look = ButtonColors.Resolve(family, Settings(), ThemeService.Current);

            preview.Background = Fill(look.Fill);
            preview.BorderBrush = Fill(look.Edge);
            previewText.Foreground = Fill(look.Ink);

            previewText.Text = setting switch
            {
                ButtonColors.FollowTheme => Glyph(family) + "  Theme",
                ButtonColors.NoColour => Glyph(family) + "  None",
                _ => Glyph(family) + "  " + setting
            };

            theme.FontWeight = setting == ButtonColors.FollowTheme ? FontWeights.Bold : FontWeights.Normal;
            none.FontWeight = setting == ButtonColors.NoColour ? FontWeights.Bold : FontWeights.Normal;

            // Said, not forbidden. It is their program, and a mid grey is a
            // legitimate thing to want even though nothing reads well on it.
            var best = ButtonColors.BestInkContrast(look.Fill);
            warning.Visibility = best >= 4.5 ? Visibility.Collapsed : Visibility.Visible;
            warning.Text = $"Hard to read — the best a label can manage on this is {best:0.0}:1. " +
                           "Try something darker or lighter.";
        };

        _repaint[family]();
        return panel;
    }

    /// <summary>A small text button for the two non-colour choices.</summary>
    private Button Choice(string text, Action click)
    {
        var button = new Button { Content = text, MinWidth = 64 };
        button.Click += (_, _) => click();
        return button;
    }

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
        _ => "ℹ"
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
        foreach (var family in ButtonColors.Families) Set(family, ButtonColors.FollowTheme);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result.Clear();

        foreach (var (family, value) in _chosen)
        {
            // A family following the theme is stored as nothing at all, so a
            // settings file only ever holds the choices actually made.
            if (value == ButtonColors.FollowTheme) continue;
            Result[ButtonColors.KeyOf(family)] = value;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
