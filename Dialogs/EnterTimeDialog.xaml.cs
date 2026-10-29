using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asks for a time, or a range of two, and says what it made of the answer as
/// it is typed.
/// </summary>
/// <remarks>
/// This was a generic InputDialog with the accepted formats listed in its
/// prompt, and every rejection re-opened it with a longer prompt. The list was
/// accurate and still hard to use: a reader had to match what they wanted to
/// type against five patterns, and if they were wrong they found out by
/// pressing OK.
///
/// Here the forms are examples that type themselves in, and the line under the
/// box says what the program understood — so the question "will this do what I
/// mean" is answered before OK rather than after it. OK is held closed while
/// the text cannot be read at all.
/// </remarks>
public partial class EnterTimeDialog : Window
{
    /// <summary>What was typed, once OK has been pressed.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>What was typed, already read. Valid whenever Value is.</summary>
    public TimeInput.Parsed Parsed { get; private set; }

    /// <param name="seed">The value to start with, usually the playhead.</param>
    /// <param name="error">
    /// Why a previous attempt was refused, when the caller is re-asking. These
    /// are the caller's own reasons — an overlap, a time before the last mark —
    /// not anything about how the time was written.
    /// </param>
    public EnterTimeDialog(string seed, string? error = null)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(error))
        {
            ErrorText.Text = error;
            ErrorBanner.Visibility = Visibility.Visible;
        }

        InputBox.Text = seed;
        UpdateReading();

        // Selected rather than merely focused: the seed is a complete answer
        // for whoever wants the playhead, and something to type over for
        // everybody else.
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => UpdateReading();

    /// <summary>
    /// Reads the box and reports what it means, or what is wrong with it.
    /// </summary>
    private void UpdateReading()
    {
        // Runs from the constructor, before the rest of the window exists.
        if (ReadingText is null || OkButton is null) return;

        if (TimeInput.TryParse(InputBox.Text, out var parsed, out var error))
        {
            Parsed = parsed;
            OkButton.IsEnabled = true;
            ReadingText.Foreground = (Brush)FindResource("StatusOk");

            ReadingText.Text = parsed.End is { } end
                ? $"Bookmark from {Bookmark.FormatTime(parsed.Start)} to {Bookmark.FormatTime(end)}" +
                  $"   ·   {TimeInput.Describe(end - parsed.Start)} long"
                : $"One time at {Bookmark.FormatTime(parsed.Start)}   ·   {TimeInput.Describe(parsed.Start)}";
            return;
        }

        OkButton.IsEnabled = false;
        ReadingText.Foreground = (Brush)FindResource("StatusError");
        ReadingText.Text = error;
    }

    /// <summary>Puts the clicked example in the box, ready to accept or edit.</summary>
    private void Example_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Hyperlink link) return;

        var text = new TextRange(link.ContentStart, link.ContentEnd).Text.Trim();
        if (text.Length == 0) return;

        InputBox.Text = text;
        InputBox.Focus();
        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // The button is disabled while the text cannot be read, so this only
        // has to catch Enter arriving before the box has been re-read.
        if (!TimeInput.TryParse(InputBox.Text, out var parsed, out _)) return;

        Value = InputBox.Text.Trim();
        Parsed = parsed;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
