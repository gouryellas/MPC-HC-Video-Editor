using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asks which format a conversion should produce.
/// </summary>
/// <remarks>
/// The menu does not need this — each of its entries names a format and carries
/// it as the command's parameter, which is one gesture rather than two. A
/// toolbar button has no parameter to carry, so the button that used to run the
/// command with nothing to convert to asks here instead.
/// </remarks>
public partial class ChooseImageFormatDialog : Window
{
    /// <summary>The format chosen, valid once the dialog returns true.</summary>
    public ImageConversionService.Format Format { get; private set; } =
        ImageConversionService.Formats[0];

    /// <param name="initial">
    /// The key to preselect — the last one converted to this session.
    /// </param>
    public ChooseImageFormatDialog(string? initial)
    {
        InitializeComponent();

        foreach (var f in ImageConversionService.Formats)
            FormatCombo.Items.Add(f.Display);

        var known = Array.FindIndex(ImageConversionService.Formats,
                                    f => string.Equals(f.Key, initial, StringComparison.OrdinalIgnoreCase));

        FormatCombo.SelectedIndex = known >= 0 ? known : 0;
    }

    private void Format_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Null on the first SelectionChanged, which runs while the window is
        // still being built.
        if (FormatNote is null || FormatCombo.SelectedIndex < 0) return;

        var format = ImageConversionService.Formats[FormatCombo.SelectedIndex];

        FormatNote.Text = format.Key switch
        {
            "ico" => "Scaled down to 256px if larger — the ICO format cannot express more.",
            "jpg" or "jpeg" => "Lossy, and loses any transparency. JPG and JPEG are the same picture; "
                             + "only the extension differs.",
            "gif" => "256 colours, so photographs lose detail.",
            "bmp" => "Uncompressed, so the files are large.",
            _ => "Lossless, and keeps any transparency."
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (FormatCombo.SelectedIndex < 0) return;

        Format = ImageConversionService.Formats[FormatCombo.SelectedIndex];
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
