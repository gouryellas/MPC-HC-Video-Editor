using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Chooses how a bookmark's pair of timestamps is marked on the seekbar.
/// </summary>
public partial class CustomizeSeekbarDialog : Window
{
    private static readonly (SeekbarBracketStyle Style, string Name, string Glyph, string Description)[] Options =
    {
        (SeekbarBracketStyle.Classic, "Classic", "[ 1 ]",
         "A bracket at each end, feet pointing into the cut. The default."),
        (SeekbarBracketStyle.Lines, "Lines", "| 1 |",
         "A plain upright at each end, no feet."),
        (SeekbarBracketStyle.Chevrons, "Chevrons", "< 1 >",
         "An angled mark at each end, pointing into the cut."),
        (SeekbarBracketStyle.None, "None", "",
         "Nothing drawn. The cut list is the only place a cut shows."),
    };

    private SeekbarBracketStyle _chosen;
    private double _opacity;
    private bool _barEnabled;

    private readonly Dictionary<SeekbarBracketStyle, RadioButton> _radios = new();
    private Slider _opacitySlider = null!;
    private CheckBox _barEnabledCheck = null!;

    /// <summary>The style to store, once the dialog returns true.</summary>
    public SeekbarBracketStyle BracketStyle { get; private set; }

    /// <summary>Opacity of the filled position wash, 0.1–1.0.</summary>
    public double SeekbarOpacity { get; private set; }

    /// <summary>Whether the filled position bar draws at all.</summary>
    public bool SeekbarBarEnabled { get; private set; }

    public CustomizeSeekbarDialog(AppSettings current)
    {
        InitializeComponent();

        _chosen = current.SeekbarBracketStyle;
        _opacity = current.SeekbarOpacity;
        _barEnabled = current.SeekbarBarEnabled;

        BracketStyle = _chosen;
        SeekbarOpacity = _opacity;
        SeekbarBarEnabled = _barEnabled;

        foreach (var option in Options)
            Rows.Children.Add(Row(option));

        Extras.Children.Add(OpacityControl());
        Extras.Children.Add(BarEnabledControl());

        Repaint();
    }

    private UIElement Row((SeekbarBracketStyle Style, string Name, string Glyph, string Description) option)
    {
        var card = new Border
        {
            Background = ThemeService.Brush(nameof(ThemePalette.PanelBackground)),
            BorderBrush = ThemeService.Brush(nameof(ThemePalette.BorderBrush)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        card.Child = row;

        var radio = new RadioButton
        {
            Content = option.Name,
            GroupName = "BracketStyle",
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextPrimary)),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        radio.Click += (_, _) => { _chosen = option.Style; Repaint(); };
        _radios[option.Style] = radio;

        Grid.SetColumn(radio, 0);
        row.Children.Add(radio);

        var description = new TextBlock
        {
            Text = option.Description,
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextSecondary)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 10, 0)
        };
        Grid.SetColumn(description, 1);
        row.Children.Add(description);

        // No glyph for "None" — showing one would be a lie about what the
        // option actually does.
        var preview = new TextBlock
        {
            Text = string.IsNullOrEmpty(option.Glyph) ? "(nothing)" : option.Glyph,
            FontFamily = new FontFamily("Consolas"),
            FontStyle = string.IsNullOrEmpty(option.Glyph) ? FontStyles.Italic : FontStyles.Normal,
            Foreground = ThemeService.Brush(string.IsNullOrEmpty(option.Glyph)
                ? nameof(ThemePalette.TextMuted) : nameof(ThemePalette.TextPrimary)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(preview, 2);
        row.Children.Add(preview);

        // Clicking anywhere on the card selects it, not only the radio's own
        // small hit area.
        card.MouseLeftButtonUp += (_, _) => { _chosen = option.Style; Repaint(); };
        card.Cursor = System.Windows.Input.Cursors.Hand;

        return card;
    }

    private UIElement OpacityControl()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(new TextBlock { Text = "TRANSPARENCY", Style = (Style)FindResource("Heading") });

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var slider = _opacitySlider = new Slider
        {
            Minimum = 10,
            Maximum = 100,
            Value = _opacity * 100,
            TickFrequency = 10,
            IsSnapToTickEnabled = false,
            VerticalAlignment = VerticalAlignment.Center
        };

        var label = new TextBlock
        {
            Width = 48,
            TextAlignment = TextAlignment.Right,
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextPrimary)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        slider.ValueChanged += (_, _) =>
        {
            _opacity = slider.Value / 100.0;
            label.Text = $"{slider.Value:0}%";
        };

        label.Text = $"{slider.Value:0}%";

        Grid.SetColumn(slider, 0);
        Grid.SetColumn(label, 1);
        row.Children.Add(slider);
        row.Children.Add(label);

        panel.Children.Add(row);
        return panel;
    }

    private UIElement BarEnabledControl()
    {
        var panel = new StackPanel();

        var check = _barEnabledCheck = new CheckBox
        {
            Content = "Show the filled position bar",
            IsChecked = _barEnabled,
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextPrimary)),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        check.Checked += (_, _) => _barEnabled = true;
        check.Unchecked += (_, _) => _barEnabled = false;

        panel.Children.Add(check);

        panel.Children.Add(new TextBlock
        {
            Text = "Off shows only the playhead position.",
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextSecondary)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(22, 2, 0, 0)
        });

        return panel;
    }

    private void Repaint()
    {
        foreach (var (style, radio) in _radios)
            radio.IsChecked = style == _chosen;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _chosen = SeekbarBracketStyle.Classic;
        _opacity = 0.5;
        _barEnabled = true;

        // The slider and checkbox hold their own state rather than reading
        // the fields back, so they are told directly. Setting the slider's
        // Value fires ValueChanged, which keeps _opacity and its label in
        // step without repeating that logic here.
        _opacitySlider.Value = _opacity * 100;
        _barEnabledCheck.IsChecked = _barEnabled;

        Repaint();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        BracketStyle = _chosen;
        SeekbarOpacity = _opacity;
        SeekbarBarEnabled = _barEnabled;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
