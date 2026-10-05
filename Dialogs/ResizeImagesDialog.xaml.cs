using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asked once, before a resize run, for the whole batch.
/// </summary>
/// <remarks>
/// A size on its own does not say what to do with a picture that is not that
/// shape, and most of them are not: a phone photo is 3:4 and a screenshot is
/// whatever the window was. So the shape question is asked beside the size
/// rather than decided quietly, with the answer that cannot spoil a picture —
/// fit inside — as the one already chosen.
/// </remarks>
public partial class ResizeImagesDialog : Window
{
    /// <summary>The list entry that puts the two boxes in reach.</summary>
    private const string CustomEntry = "Custom…";

    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }
    public ImageConversionService.ResizeFit Fit { get; private set; }

    /// <summary>
    /// Where to write, or an empty string for "beside each picture".
    /// </summary>
    public string OutputDirectory { get; private set; } = string.Empty;

    /// <param name="width">The size to preselect — the last one used this
    /// session, so a second run is one click.</param>
    /// <param name="saveToFolder">The program's current Save to folder.</param>
    /// <param name="initialDirectory">The destination last used.</param>
    public ResizeImagesDialog(int width, int height, ImageConversionService.ResizeFit fit,
                              string saveToFolder, string? initialDirectory)
    {
        InitializeComponent();

        Where.SaveTo = saveToFolder;
        Where.Preselect(initialDirectory);
        Where.ChoiceChanged += RefreshOk;

        foreach (var r in ImageConversionService.Resolutions)
            SizeCombo.Items.Add(r.Display);

        SizeCombo.Items.Add(CustomEntry);

        var known = Array.FindIndex(ImageConversionService.Resolutions,
                                    r => r.Width == width && r.Height == height);

        if (known >= 0)
        {
            SizeCombo.SelectedIndex = known;
        }
        else
        {
            // A size that is not on the list is one that was typed, so the
            // dialog comes back up on Custom with it still in the boxes.
            SizeCombo.SelectedItem = CustomEntry;
        }

        WidthBox.Text = width.ToString(CultureInfo.CurrentCulture);
        HeightBox.Text = height.ToString(CultureInfo.CurrentCulture);

        switch (fit)
        {
            case ImageConversionService.ResizeFit.Crop: FillCrop.IsChecked = true; break;
            case ImageConversionService.ResizeFit.Stretch: StretchExact.IsChecked = true; break;
            default: FitInside.IsChecked = true; break;
        }
    }

    private bool IsCustom => Equals(SizeCombo.SelectedItem, CustomEntry);

    private void Size_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Null on the first SelectionChanged, which runs while the window is
        // still being built.
        if (CustomPanel is null) return;

        CustomPanel.IsEnabled = IsCustom;

        if (!IsCustom && SizeCombo.SelectedIndex >= 0)
        {
            var chosen = ImageConversionService.Resolutions[SizeCombo.SelectedIndex];
            WidthBox.Text = chosen.Width.ToString(CultureInfo.CurrentCulture);
            HeightBox.Text = chosen.Height.ToString(CultureInfo.CurrentCulture);
        }

        RefreshOk();
    }

    private void Custom_Changed(object sender, TextChangedEventArgs e) => RefreshOk();

    /// <summary>
    /// Continue stays out of reach until the boxes hold a real size and the
    /// destination is settled.
    /// </summary>
    private void RefreshOk()
    {
        if (OkButton is null) return;
        OkButton.IsEnabled = TryReadSize(out _, out _) && Where.IsReady;
    }

    private bool TryReadSize(out int width, out int height)
    {
        height = 0;

        return int.TryParse(WidthBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out width)
               && int.TryParse(HeightBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out height)
               && width is > 0 and <= 20000
               && height is > 0 and <= 20000;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSize(out var width, out var height) || !Where.IsReady) return;

        PixelWidth = width;
        PixelHeight = height;
        OutputDirectory = Where.Directory;

        Fit = FillCrop.IsChecked == true ? ImageConversionService.ResizeFit.Crop
            : StretchExact.IsChecked == true ? ImageConversionService.ResizeFit.Stretch
            : ImageConversionService.ResizeFit.Inside;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
