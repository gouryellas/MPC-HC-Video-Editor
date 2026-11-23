using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asked once before Add watermark, for the mark every picked file gets.
/// </summary>
public partial class WatermarkDialog : Window
{
    /// <summary>The text size and image size kept apart, because 5% of the
    /// height is a good line of text and a tiny logo.</summary>
    private double _textSize, _imageSize;
    private string? _imagePath;
    private bool _ready;

    public FFmpegService.WatermarkSpec Spec { get; private set; }

    /// <param name="initial">The last mark used this session, or null.</param>
    public WatermarkDialog(FFmpegService.WatermarkSpec? initial)
    {
        InitializeComponent();

        Spec = initial ?? new FFmpegService.WatermarkSpec(null, null,
            FFmpegService.WatermarkPosition.BottomRight, 5, 0.7);

        _textSize = Spec.Text is not null || Spec.ImagePath is null ? Spec.SizePercent : 5;
        _imageSize = Spec.ImagePath is not null ? Spec.SizePercent : 15;

        MarkTextBox.Text = Spec.Text ?? string.Empty;
        _imagePath = Spec.ImagePath;
        if (_imagePath is not null) ImageText.Text = Path.GetFileName(_imagePath);

        if (Spec.ImagePath is not null && Spec.Text is null) UseImage.IsChecked = true;
        else UseText.IsChecked = true;

        (Spec.Position switch
        {
            FFmpegService.WatermarkPosition.TopLeft => TopLeft,
            FFmpegService.WatermarkPosition.TopRight => TopRight,
            FFmpegService.WatermarkPosition.BottomLeft => BottomLeft,
            FFmpegService.WatermarkPosition.Center => Center,
            _ => BottomRight
        }).IsChecked = true;

        OpacityBox.Text = (Spec.Opacity * 100).ToString("0", CultureInfo.CurrentCulture);

        _ready = true;
        ShowSize();

        Loaded += (_, _) => { if (UseText.IsChecked == true) MarkTextBox.Focus(); };
    }

    /// <summary>Typing words is choosing words.</summary>
    private void Text_Focused(object sender, KeyboardFocusChangedEventArgs e) => UseText.IsChecked = true;

    private void Kind_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        // Whatever was typed for the other kind is kept for if they come back.
        if (sender == UseImage && Read(SizeBox.Text, out var text)) _textSize = text;
        if (sender == UseText && Read(SizeBox.Text, out var image)) _imageSize = image;

        ShowSize();
    }

    private void ShowSize()
    {
        var image = UseImage.IsChecked == true;
        SizeBox.Text = (image ? _imageSize : _textSize).ToString("0.##", CultureInfo.CurrentCulture);
        SizeUnit.Text = image ? "% of the picture's width" : "% of the picture's height";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*",
            Title = "Choose the watermark image"
        };
        if (ofd.ShowDialog(this) != true) return;

        _imagePath = ofd.FileName;
        ImageText.Text = Path.GetFileName(_imagePath);
        UseImage.IsChecked = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var image = UseImage.IsChecked == true;

        if (!image && string.IsNullOrWhiteSpace(MarkTextBox.Text))
        {
            Fail("Type the text to put on the picture.");
            return;
        }

        if (image && (_imagePath is null || !File.Exists(_imagePath)))
        {
            Fail("Choose an image with Browse.");
            return;
        }

        if (!Read(SizeBox.Text, out var size) || size < 1 || size > (image ? 100 : 50))
        {
            Fail(image ? "The size has to be between 1 and 100." : "The size has to be between 1 and 50.");
            return;
        }

        if (!Read(OpacityBox.Text, out var opacity) || opacity < 1 || opacity > 100)
        {
            Fail("The opacity has to be between 1 and 100.");
            return;
        }

        var where = TopLeft.IsChecked == true ? FFmpegService.WatermarkPosition.TopLeft
                  : TopRight.IsChecked == true ? FFmpegService.WatermarkPosition.TopRight
                  : BottomLeft.IsChecked == true ? FFmpegService.WatermarkPosition.BottomLeft
                  : Center.IsChecked == true ? FFmpegService.WatermarkPosition.Center
                  : FFmpegService.WatermarkPosition.BottomRight;

        Spec = new FFmpegService.WatermarkSpec(
            image ? null : MarkTextBox.Text.Trim(),
            image ? _imagePath : null,
            where, size, opacity / 100.0);

        DialogResult = true;
    }

    private static bool Read(string text, out double value) =>
        double.TryParse(text.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(text.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
