using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Asks which order the operations should take the cuts in.
/// </summary>
/// <remarks>
/// A box of numbers rather than a list to drag. The order being described is
/// short, the user is reading it off row numbers that are already on screen,
/// and "3, 1, 2, 6, 5, 4" is quicker to type than six drags are to perform —
/// and quicker to check afterwards, which matters more.
///
/// Nothing here touches a cut. The dialog hands back a sequence of the cuts it
/// was given, and the caller keeps it beside the list.
/// </remarks>
public partial class ProcessingOrderDialog : Window
{
    private readonly IReadOnlyList<Bookmark> _cuts;

    /// <summary>
    /// The order chosen, or null for none — which is both what Clear produces
    /// and what an empty box produces.
    /// </summary>
    public IReadOnlyList<Bookmark>? Order { get; private set; }

    public ProcessingOrderDialog(IReadOnlyList<Bookmark> cuts, IReadOnlyList<Bookmark>? current)
    {
        InitializeComponent();

        _cuts = cuts;
        AvailableLine.Text = cuts.Count == 1
            ? "There is one clip, numbered 1."
            : $"Clips 1 to {cuts.Count}, each exactly once.";

        ClearButton.IsEnabled = current is { Count: > 0 };

        if (current is { Count: > 0 })
            OrderBox.Text = string.Join(", ", current.Select(b => b.Index));

        OrderBox.Focus();
        OrderBox.SelectAll();
        Refresh();
    }

    private void OrderBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    /// <summary>
    /// Reads the box back and says what it understood, or what is wrong with
    /// it, and leaves Save enabled only when there is something to save.
    /// </summary>
    private void Refresh()
    {
        var text = OrderBox.Text;

        // An empty box is not an error — it is the same answer as Clear, and
        // saying "nothing entered" in red at somebody who has just emptied the
        // field on purpose is scolding them for it.
        if (string.IsNullOrWhiteSpace(text))
        {
            ReadingLine.Text = "No order — the clips will run in the order they appear in the video.";
            ReadingLine.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
            SaveButton.IsEnabled = true;
            return;
        }

        if (!ProcessingOrderInput.TryParse(text, _cuts, out var order, out var problem))
        {
            ReadingLine.Text = problem;
            ReadingLine.Foreground = (System.Windows.Media.Brush)FindResource("StatusError");
            SaveButton.IsEnabled = false;
            return;
        }

        // Read back as the clips themselves, not as the numbers that were just
        // typed. Echoing the input proves nothing; naming the clips it lands on
        // is what catches a number that was meant to be a different one.
        ReadingLine.Text = string.Join("  →  ", order.Select(Describe));
        ReadingLine.Foreground = (System.Windows.Media.Brush)FindResource("StatusOk");
        SaveButton.IsEnabled = true;
    }

    private static string Describe(Bookmark b) =>
        b.HasLabel ? $"{b.Index}. {b.Label}" : $"{b.Index}. {b.StartDisplay}";

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OrderBox.Text))
        {
            Order = null;
            DialogResult = true;
            return;
        }

        if (!ProcessingOrderInput.TryParse(OrderBox.Text, _cuts, out var order, out _)) return;

        Order = order;
        DialogResult = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Order = null;
        DialogResult = true;
    }
}
