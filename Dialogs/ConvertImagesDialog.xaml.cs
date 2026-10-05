using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asks what a conversion should produce, and where it should land.
/// </summary>
/// <remarks>
/// Shown for every conversion, including the menu entries that name a format:
/// the format they carry preselects the list, and the destination question is
/// one nothing in the menu can answer. The toolbar button, which carries no
/// format at all, is the reason the list is here rather than implied.
/// </remarks>
public partial class ConvertImagesDialog : Window
{
    /// <summary>The format chosen, valid once the dialog returns true.</summary>
    public ImageConversionService.Format Format { get; private set; } =
        ImageConversionService.Formats[0];

    /// <summary>
    /// Where to write, or an empty string for "beside each picture".
    /// </summary>
    public string OutputDirectory { get; private set; } = string.Empty;

    /// <param name="initialFormat">
    /// The format key to preselect — the one the menu entry named, or the last
    /// one converted to.
    /// </param>
    /// <param name="saveToFolder">The program's current Save to folder.</param>
    /// <param name="initialDirectory">
    /// The destination last used, so a second run is one click.
    /// </param>
    public ConvertImagesDialog(string? initialFormat, string saveToFolder, string? initialDirectory)
    {
        InitializeComponent();

        foreach (var f in ImageConversionService.Formats)
            FormatCombo.Items.Add(f.Display);

        var known = Array.FindIndex(ImageConversionService.Formats,
                                    f => string.Equals(f.Key, initialFormat, StringComparison.OrdinalIgnoreCase));

        FormatCombo.SelectedIndex = known >= 0 ? known : 0;

        Where.SaveTo = saveToFolder;
        Where.Preselect(initialDirectory);
        Where.ChoiceChanged += () => OkButton.IsEnabled = Where.IsReady;
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
        if (FormatCombo.SelectedIndex < 0 || !Where.IsReady) return;

        Format = ImageConversionService.Formats[FormatCombo.SelectedIndex];
        OutputDirectory = Where.Directory;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
