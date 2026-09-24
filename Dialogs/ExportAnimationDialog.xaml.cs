using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>What an animated export should produce.</summary>
public sealed record AnimationChoice(bool Webp, int Fps, int Width)
{
    /// <summary>The extension this choice writes.</summary>
    public string Extension => Webp ? ".webp" : ".gif";
}

/// <summary>
/// Asked once, before exporting the checked cuts as animations.
/// </summary>
/// <remarks>
/// Frame rate and width are asked rather than settled in Settings because they
/// are decided per clip, not per install: a two-second reaction shot and a
/// thirty-second sequence want different answers, and the two numbers are the
/// whole difference between a file worth sending and one that is too big to.
/// </remarks>
public partial class ExportAnimationDialog : Window
{
    public AnimationChoice Choice { get; private set; } = new(false, 15, 480);

    /// <param name="initial">The last choice made this session.</param>
    /// <param name="cutCount">How many cuts are about to be written.</param>
    /// <param name="appliedNote">
    /// What the cuts already carry that the animation will inherit — a flip, a
    /// rotation, a speed, a fade. Empty when they carry nothing.
    /// </param>
    public ExportAnimationDialog(AnimationChoice initial, int cutCount, string appliedNote)
    {
        InitializeComponent();

        Heading.Text = cutCount == 1
            ? "Export this cut as an animation"
            : $"Export {cutCount} cuts as animations";

        if (initial.Webp) FormatWebp.IsChecked = true;
        else FormatGif.IsChecked = true;

        FpsBox.Text = initial.Fps.ToString(CultureInfo.CurrentCulture);
        WidthBox.Text = initial.Width.ToString(CultureInfo.CurrentCulture);

        AppliedNote.Text = appliedNote;
        AppliedNote.Visibility = string.IsNullOrEmpty(appliedNote)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Upper bounds rather than none: a GIF at 120 frames a second is a
        // mistake rather than a request, and 8000 pixels of it is a mistake that
        // takes a long time to find out about.
        if (!TryRead(FpsBox, "frames a second", 1, 60, out var fps)) return;
        if (!TryRead(WidthBox, "width", 0, 4000, out var width)) return;

        Choice = new AnimationChoice(FormatWebp.IsChecked == true, fps, width);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private bool TryRead(TextBox box, string what, int min, int max, out int value)
    {
        if (int.TryParse(box.Text?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
            && value >= min && value <= max)
            return true;

        MessageBox.Show(this, $"Enter a {what} between {min} and {max}.",
                        "Export as animation", MessageBoxButton.OK, MessageBoxImage.Warning);
        box.Focus();
        box.SelectAll();
        return false;
    }
}
