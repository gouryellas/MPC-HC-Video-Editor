using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Chooses what the seekbar's filled position bar, its border, its track, its
/// brackets and its waveform are painted.
/// </summary>
/// <remarks>
/// Five subjects rather than <see cref="ButtonColorsDialog"/>'s four
/// families, so it has no need of that dialog's per-family loop — but it is
/// built the same way, in code, because the preview has to repaint as a
/// choice changes just as a button's sample does.
/// </remarks>
public partial class SeekbarColorsDialog : Window
{
    /// <summary>The same spread <see cref="ButtonColorsDialog"/> offers, so a
    /// colour picked for a button and one picked for the seekbar come from
    /// the same wheel.</summary>
    private static readonly string[] Presets =
    {
        "#D9A82E", "#C9972A", "#B8792C", "#8A4A18", "#99302E", "#8C3A52",
        "#E07BA8", "#A8406B", "#64357F", "#5E4C86", "#255FA8", "#1F5AA8",
        "#1B4A87", "#2A5B8C", "#1C6E6E", "#246F66", "#15704A", "#3A6B55",
        "#4F4B46", "#2A2724", "#141414", "#F2F0EC",
    };

    /// <summary>What a bracket, a cut's number and its tick are painted before anyone chooses otherwise.</summary>
    private const string DefaultMark = "#FFFFFF";

    /// <summary>What the waveform is painted before anyone chooses otherwise.</summary>
    private const string DefaultWaveform = "#8C8C8C";

    private string _fill;
    private string _playhead = SeekbarAppearance.FollowTheme;
    private string _border;
    private string _track;
    private string _mark;
    private string _waveform;

    // Not edited here any more — Customize seekbar owns both — but the
    // preview still has to show an honest bar, so the values travel along
    // for that and nothing else.
    private readonly double _opacity;
    private readonly bool _barEnabled;

    private Border _previewWash = null!;
    private Border _previewTrack = null!;
    private Grid _previewArea = null!;

    private Action? _repaintPreview;

    /// <summary>What the position bar is painted: "theme" or a hex colour.</summary>
    public string SeekbarColor { get; private set; }

    /// <summary>What the border is painted: "theme" or a hex colour.</summary>
    public string SeekbarBorderColor { get; private set; }

    /// <summary>What the track (the bar's background) is painted: "theme" or a hex colour.</summary>
    public string SeekbarTrackColor { get; private set; }

    /// <summary>What a bracket, a cut's number and its tick are painted: "theme" or a hex colour.</summary>
    public string SeekbarMarkColor { get; private set; }

    /// <summary>What the playhead is painted: "theme" or a hex colour.</summary>
    public string SeekbarPlayheadColor { get; private set; }

    /// <summary>What the waveform is painted: "theme" or a hex colour.</summary>
    public string SeekbarWaveformColor { get; private set; }

    public SeekbarColorsDialog(AppSettings current)
    {
        InitializeComponent();

        _fill = current.SeekbarColor;
        _border = current.SeekbarBorderColor;
        _track = current.SeekbarTrackColor;
        _mark = current.SeekbarMarkColor;
        _playhead = current.SeekbarPlayheadColor;
        _waveform = current.SeekbarWaveformColor;
        _opacity = current.SeekbarOpacity;
        _barEnabled = current.SeekbarBarEnabled;

        SeekbarColor = _fill;
        SeekbarBorderColor = _border;
        SeekbarTrackColor = _track;
        SeekbarMarkColor = _mark;
        SeekbarPlayheadColor = _playhead;
        SeekbarWaveformColor = _waveform;

        Build();
    }

    // ------------------------------------------------------------------
    // Layout
    // ------------------------------------------------------------------

    private void Build()
    {
        Body.Children.Add(Preview());
        Body.Children.Add(ColorPicker("POSITION BAR COLOR",
            () => _fill, v => { _fill = v; Repaint(); }));
        Body.Children.Add(ColorPicker("BORDER COLOR",
            () => _border, v => { _border = v; Repaint(); }));
        Body.Children.Add(ColorPicker("BACKGROUND COLOR",
            () => _track, v => { _track = v; Repaint(); }));
        Body.Children.Add(ColorPicker("BRACKET COLOR",
            () => _mark, v => { _mark = v; Repaint(); }));
        Body.Children.Add(ColorPicker("PLAYHEAD COLOR",
            () => _playhead, v => { _playhead = v; Repaint(); }));
        Body.Children.Add(ColorPicker("WAVEFORM COLOR",
            () => _waveform, v => { _waveform = v; Repaint(); }));

        Repaint();
    }

    /// <summary>
    /// A mock track the width of the dialog, washed halfway across and
    /// bordered the way the real one is — so a choice is seen before it is
    /// saved, the same reason every card in <see cref="ButtonColorsDialog"/>
    /// carries a sample.
    /// </summary>
    private UIElement Preview()
    {
        var block = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

        block.Children.Add(new TextBlock
        {
            Text = "PREVIEW",
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextMuted)),
            FontSize = 9,
            Margin = new Thickness(0, 0, 0, 4)
        });

        _previewTrack = new Border
        {
            Height = 22,
            BorderThickness = new Thickness(1)
        };

        _previewArea = new Grid();
        _previewTrack.Child = _previewArea;

        // A handful of bars standing for the waveform, behind everything
        // else the way the real one sits behind the wash and the brackets.
        // Heights are just varied enough to read as a shape rather than a
        // bar chart — the real thing is ffmpeg's own drawing, which this
        // is not attempting to reproduce, only to show the colour of.
        var waveformBars = new Grid { Opacity = 0.5 };
        double[] barHeights = { 0.3, 0.6, 0.4, 0.9, 0.5, 0.7, 0.35, 0.8, 0.45, 0.65, 0.3, 0.55 };
        var bars = new List<Rectangle>();
        for (var i = 0; i < barHeights.Length; i++)
        {
            var bar = new Rectangle
            {
                Width = 3,
                Height = 22 * barHeights[i],
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(i * 16 + 4, 0, 0, 0)
            };
            bars.Add(bar);
            waveformBars.Children.Add(bar);
        }
        _previewArea.Children.Add(waveformBars);

        _previewWash = new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        _previewArea.Children.Add(_previewWash);

        // A sliver standing for the caret, roughly where the wash leaves off,
        // so the preview reads as the same bar rather than only a tinted box.
        // Filled in the bracket colour and edged in the border colour, the
        // same two resources the real handle reads from.
        var caret = new Border
        {
            Width = 7,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(173, 0, 0, 0)
        };
        _previewArea.Children.Add(caret);

        // A bracket pair standing for a cut, so the mark colour is seen
        // against the track rather than only named in a list of choices.
        var mark = new TextBlock
        {
            Text = "[ 1 ]",
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(60, 0, 0, 0)
        };
        _previewArea.Children.Add(mark);

        _repaintPreview = () =>
        {
            _previewTrack.Background = Fill(Resolve(_track, ThemeService.Current.TimelineTrack));
            _previewTrack.BorderBrush = Fill(Resolve(_border, ThemeService.Current.BorderBrush));
            var fill = Resolve(_fill, ThemeService.Current.TimelinePosition);
            _previewWash.Background = Fill(fill);
            _previewWash.Opacity = _opacity;
            _previewWash.Visibility = _barEnabled ? Visibility.Visible : Visibility.Collapsed;
            _previewWash.Width = _barEnabled ? 180 : 0;
            var markFill = Fill(Resolve(_mark, DefaultMark));

            // The caret in the preview is the playhead. It follows the
            // bracket colour only while the playhead has none of its own,
            // which is what the bar itself does.
            caret.Background = Fill(Resolve(_playhead, Resolve(_mark, DefaultMark)));
            caret.BorderBrush = Fill(Resolve(_border, ThemeService.Current.BorderBrush));
            mark.Foreground = markFill;

            var waveformFill = Fill(Resolve(_waveform, DefaultWaveform));
            foreach (var bar in bars) bar.Fill = waveformFill;
        };

        block.Children.Add(_previewTrack);
        return block;
    }

    /// <summary>
    /// One colour choice: follow the theme, or pick a swatch — the same two
    /// answers a button family has, minus "no colour", which means nothing
    /// for a bar that always needs some fill to be visible at all.
    /// </summary>
    private UIElement ColorPicker(string heading, Func<string> get, Action<string> set)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

        panel.Children.Add(new TextBlock { Text = heading, Style = (Style)FindResource("Heading") });

        var choices = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var group = heading;

        var theme = Radio("Follow the theme", group, () => set(SeekbarAppearance.FollowTheme));
        var pick = Radio("Pick one:", group, () => set(ButtonColors.IsColour(get()) ? get() : Presets[0]));

        choices.Children.Add(theme);
        choices.Children.Add(pick);
        panel.Children.Add(choices);

        var swatches = new WrapPanel { Margin = new Thickness(22, 0, 0, 0) };
        var marks = new Dictionary<string, Border>();

        foreach (var hex in Presets)
        {
            var mark = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(4),
                Background = Fill(hex),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 0, 5, 5),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = hex
            };

            mark.MouseLeftButtonUp += (_, _) => set(hex);
            marks[hex] = mark;
            swatches.Children.Add(mark);
        }

        panel.Children.Add(swatches);

        // Captured so Repaint() below can run every picker's own refresh
        // without this method knowing how many there are.
        _repaints.Add(() =>
        {
            var current = get();
            var custom = ButtonColors.IsColour(current);

            theme.IsChecked = !custom;
            pick.IsChecked = custom;

            swatches.Opacity = custom ? 1.0 : 0.45;
            foreach (var (hex, mark) in marks)
                mark.BorderBrush = custom && string.Equals(hex, current, StringComparison.OrdinalIgnoreCase)
                    ? ThemeService.Brush(nameof(ThemePalette.AccentBright))
                    : Fill(hex);
        });

        return panel;
    }

    private readonly List<Action> _repaints = new();

    private void Repaint()
    {
        foreach (var repaint in _repaints) repaint();
        _repaintPreview?.Invoke();
    }

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

        radio.Click += (_, _) => chosen();
        return radio;
    }

    private static string Resolve(string setting, string themeDefault) =>
        string.IsNullOrEmpty(setting)
        || string.Equals(setting, SeekbarAppearance.FollowTheme, StringComparison.OrdinalIgnoreCase)
        || !ButtonColors.IsColour(setting)
            ? themeDefault
            : setting;

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
        // Colour only — transparency and the bar on/off belong to Customize
        // seekbar now, and resetting a choice this dialog never offered
        // would be a surprise, not a reset.
        _fill = SeekbarAppearance.FollowTheme;
        _border = SeekbarAppearance.FollowTheme;
        _track = SeekbarAppearance.FollowTheme;
        _mark = SeekbarAppearance.FollowTheme;
        _playhead = SeekbarAppearance.FollowTheme;
        _waveform = SeekbarAppearance.FollowTheme;

        Repaint();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SeekbarColor = _fill;
        SeekbarBorderColor = _border;
        SeekbarTrackColor = _track;
        SeekbarMarkColor = _mark;
        SeekbarPlayheadColor = _playhead;
        SeekbarWaveformColor = _waveform;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
