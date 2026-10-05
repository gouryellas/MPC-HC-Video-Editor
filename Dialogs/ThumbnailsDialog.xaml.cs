using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asks how often to take a frame, how big it should be, and whether the
/// frames land as separate pictures or tiled onto sheets.
/// </summary>
/// <remarks>
/// The interval is the setting, not the number of thumbnails: "one every ten
/// seconds" means the same thing on a two-minute clip and a two-hour film,
/// where "twenty of them" means something different on each.
///
/// The video's length is known, so the dialog says how many that will be
/// rather than leaving it to be discovered — four hundred PNGs is worth seeing
/// before it happens, not afterwards.
/// </remarks>
public partial class ThumbnailsDialog : Window
{
    private readonly double _durationSeconds;

    public double EverySeconds { get; private set; }
    public int ThumbnailWidth { get; private set; }

    /// <summary>Thumbnails across a sheet, or zero for separate pictures.</summary>
    public int Columns { get; private set; }

    /// <summary>
    /// Where to write, or an empty string for "beside the video".
    /// </summary>
    public string OutputDirectory { get; private set; } = string.Empty;

    /// <param name="durationSeconds">
    /// The video's length, for the count. Zero when it is not known, which
    /// leaves the count unsaid rather than guessed.
    /// </param>
    public ThumbnailsDialog(double durationSeconds, double everySeconds, int width, int columns,
                            string saveToFolder, string? initialDirectory)
    {
        InitializeComponent();

        _durationSeconds = durationSeconds;

        EverySecondsBox.Text = everySeconds.ToString("0.###", CultureInfo.CurrentCulture);
        WidthBox.Text = width.ToString(CultureInfo.CurrentCulture);
        ColumnsBox.Text = (columns > 0 ? columns : 4).ToString(CultureInfo.CurrentCulture);

        if (columns > 0) ContactSheet.IsChecked = true;

        Where.DescribeBeside("Beside the video",
                             "In the same folder as the video they were taken from.");
        Where.SaveTo = saveToFolder;
        Where.Preselect(initialDirectory);
        Where.ChoiceChanged += Refresh;

        Refresh();
    }

    private bool Tiling => ContactSheet.IsChecked == true;

    private void Layout_Changed(object sender, RoutedEventArgs e)
    {
        if (ColumnsPanel is null) return;

        ColumnsPanel.IsEnabled = Tiling;
        Refresh();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Refresh();

    /// <summary>
    /// Restates what the answers add up to, and keeps Continue out of reach
    /// until they are all readable.
    /// </summary>
    private void Refresh()
    {
        if (OkButton is null || CountNote is null) return;

        var ok = TryRead(out var every, out var width, out var columns);
        OkButton.IsEnabled = ok && Where.IsReady;

        if (!ok || _durationSeconds <= 0)
        {
            CountNote.Text = ok ? string.Empty : "Type a number of seconds greater than zero.";
            return;
        }

        var frames = Math.Max(1, (int)(_durationSeconds / every));

        CountNote.Text = Tiling
            ? $"About {frames} frames, {columns} across — {Sheets(frames, columns)}."
            : $"About {frames} picture{(frames == 1 ? "" : "s")}, each {width}px wide.";
    }

    /// <summary>
    /// How many sheets that many frames fill. A sheet is capped at a sane
    /// number of rows: tiling four hundred frames into one picture produces
    /// something no program will open.
    /// </summary>
    private static string Sheets(int frames, int columns)
    {
        var rows = RowsPerSheet(frames, columns);
        var perSheet = columns * rows;
        var sheets = (int)Math.Ceiling((double)frames / perSheet);

        return sheets == 1
            ? $"one sheet {columns} × {rows}"
            : $"{sheets} sheets of {columns} × {rows}";
    }

    /// <summary>Rows on a sheet: enough for everything, up to the cap.</summary>
    public static int RowsPerSheet(int frames, int columns) =>
        Math.Clamp((int)Math.Ceiling((double)frames / columns), 1, MaxRows);

    /// <summary>
    /// The tallest a sheet gets. Twelve rows of 320px thumbnails is already a
    /// picture four thousand pixels tall.
    /// </summary>
    public const int MaxRows = 12;

    private bool TryRead(out double every, out int width, out int columns)
    {
        width = 0;
        columns = 0;

        if (!double.TryParse(EverySecondsBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out every)
            || every <= 0 || every > 36000)
            return false;

        if (!int.TryParse(WidthBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out width)
            || width is < 16 or > 4000)
            return false;

        if (!Tiling) return true;

        return int.TryParse(ColumnsBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out columns)
               && columns is > 0 and <= 20;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var every, out var width, out var columns) || !Where.IsReady) return;

        EverySeconds = every;
        ThumbnailWidth = width;
        Columns = Tiling ? columns : 0;
        OutputDirectory = Where.Directory;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
