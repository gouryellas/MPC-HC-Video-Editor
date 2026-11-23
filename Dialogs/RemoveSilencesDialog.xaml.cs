using System.Globalization;
using System.Windows;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// The three numbers that decide what Remove silences takes out.
/// </summary>
/// <param name="ThresholdDb">Anything quieter than this is silence. Negative.</param>
/// <param name="MinSilenceSeconds">Pauses shorter than this stay in.</param>
/// <param name="PaddingSeconds">Silence kept either side of what stays.</param>
public readonly record struct SilenceSettings(double ThresholdDb, double MinSilenceSeconds, double PaddingSeconds)
{
    public static SilenceSettings Default => new(-30, 0.5, 0.15);
}

/// <summary>
/// Asked before Remove silences scans, for how quiet and how long a gap has
/// to be before it goes.
/// </summary>
public partial class RemoveSilencesDialog : Window
{
    public SilenceSettings Settings { get; private set; }

    /// <param name="initial">The last values used this session.</param>
    public RemoveSilencesDialog(SilenceSettings initial)
    {
        InitializeComponent();

        Settings = initial;
        ThresholdBox.Text = initial.ThresholdDb.ToString("0.##", CultureInfo.CurrentCulture);
        MinSilenceBox.Text = initial.MinSilenceSeconds.ToString("0.##", CultureInfo.CurrentCulture);
        PaddingBox.Text = initial.PaddingSeconds.ToString("0.##", CultureInfo.CurrentCulture);

        Loaded += (_, _) => { ThresholdBox.Focus(); ThresholdBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!Read(ThresholdBox.Text, out var threshold) || threshold >= 0 || threshold < -90)
        {
            Fail("The loudness has to be a number below zero, such as -30.");
            return;
        }

        if (!Read(MinSilenceBox.Text, out var minimum) || minimum < 0.1 || minimum > 60)
        {
            Fail("The shortest silence has to be between 0.1 and 60 seconds.");
            return;
        }

        if (!Read(PaddingBox.Text, out var padding) || padding < 0 || padding > 5)
        {
            Fail("The time kept around speech has to be between 0 and 5 seconds.");
            return;
        }

        // Padding eats into the silence from both ends, so a gap no longer
        // than twice the padding would have nothing left to remove.
        if (padding * 2 >= minimum)
        {
            Fail("The time kept around speech has to be less than half the shortest silence, or nothing would be removed.");
            return;
        }

        Settings = new SilenceSettings(threshold, minimum, padding);
        DialogResult = true;
    }

    private static bool Read(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
