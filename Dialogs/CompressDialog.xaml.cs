using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asked once before Compress to size, for the size every picked file has
/// to fit in.
/// </summary>
/// <remarks>
/// Megabytes as Windows counts them — 1,048,576 bytes — because Explorer's
/// figure is the one somebody checks the result against.
/// </remarks>
public partial class CompressDialog : Window
{
    /// <summary>The chosen size, in megabytes.</summary>
    public double TargetMegabytes { get; private set; }

    /// <param name="initialMegabytes">The last size used this session.</param>
    public CompressDialog(double initialMegabytes)
    {
        InitializeComponent();

        TargetMegabytes = initialMegabytes;

        var preset = new[] { Size10, Size25, Size50, Size100 }
            .FirstOrDefault(r => double.Parse((string)r.Tag, CultureInfo.InvariantCulture) == initialMegabytes);

        if (preset is not null)
        {
            preset.IsChecked = true;
        }
        else
        {
            SizeCustom.IsChecked = true;
            CustomBox.Text = initialMegabytes.ToString("0.##", CultureInfo.CurrentCulture);
        }
    }

    /// <summary>Typing a size is choosing it.</summary>
    private void Custom_Focused(object sender, KeyboardFocusChangedEventArgs e) => SizeCustom.IsChecked = true;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        double megabytes;

        if (SizeCustom.IsChecked == true)
        {
            var text = CustomBox.Text.Trim();
            if (!(double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out megabytes) ||
                  double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out megabytes)) ||
                megabytes < 1 || megabytes > 100_000)
            {
                ErrorText.Text = "Type a size of at least 1 MB.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
        }
        else
        {
            var chosen = new[] { Size10, Size25, Size50, Size100 }.First(r => r.IsChecked == true);
            megabytes = double.Parse((string)chosen.Tag, CultureInfo.InvariantCulture);
        }

        TargetMegabytes = megabytes;
        DialogResult = true;
    }
}
