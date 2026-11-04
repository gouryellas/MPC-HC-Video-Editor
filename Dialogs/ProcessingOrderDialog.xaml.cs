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
    private readonly bool _useNames;

    /// <summary>
    /// The order chosen, or null for none — which is what an empty box means.
    /// </summary>
    public IReadOnlyList<Bookmark>? Order { get; private set; }

    /// <param name="useNames">
    /// Whether the cuts are being named, so the read-back can say which clip
    /// each number is rather than only where it comes in the run. Off, the
    /// names are either absent or not what the user is reading the list by.
    /// </param>
    public ProcessingOrderDialog(
        IReadOnlyList<Bookmark> cuts, IReadOnlyList<Bookmark>? current, bool useNames)
    {
        InitializeComponent();

        _cuts = cuts;
        _useNames = useNames;
        AvailableLine.Text = cuts.Count == 1
            ? "There is one clip, numbered 1."
            : $"Clips 1 to {cuts.Count}, each exactly once.";

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

        // An empty box is not an error — it is how the order is removed from
        // here, and saying "nothing entered" in red at somebody who has just
        // emptied the field on purpose is scolding them for it. There was a
        // No order button beside Save doing the same job; clearing the box
        // and saving is the same gesture with one less thing to explain.
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

        // The numbers, and the names beside them when the cuts are named.
        // A name is what the user is picking clips by in that state, so it is
        // the thing worth confirming; a start time is not — it read as a
        // second set of numbers to check against the first, which is the work
        // the line was supposed to save.
        ReadingLine.Text = string.Join("  →  ", order.Select(Describe));
        ReadingLine.Foreground = (System.Windows.Media.Brush)FindResource("StatusOk");
        SaveButton.IsEnabled = true;
    }

    /// <summary>One clip in the read-back: its number, and its name if named.</summary>
    /// <remarks>
    /// A named clip with no name of its own falls back to the number alone
    /// rather than to a gap — the cuts are named from the moment the setting
    /// is on, but one can lose its name to a hand-edited CSV.
    /// </remarks>
    private string Describe(Bookmark b) =>
        _useNames && b.HasLabel ? $"#{b.Index} {b.Label}" : $"#{b.Index}";

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
}
