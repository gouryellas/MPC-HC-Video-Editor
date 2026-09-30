using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MpcHcVideoEditor.Models;

/// <summary>What a toolbar entry is.</summary>
public enum ToolbarItemKind
{
    /// <summary>An action.</summary>
    Button,

    /// <summary>A fixed gap, for separating one group of buttons from the next.</summary>
    Spacer,

    /// <summary>
    /// A gap that takes whatever width is going, pushing what follows it to the
    /// right — or splitting the leftover evenly when there is more than one.
    /// </summary>
    Expander,

    /// <summary>
    /// Ends the row. Everything after it is drawn on the next line of the same
    /// toolbar.
    /// </summary>
    /// <remarks>
    /// Stored like anything else, because a break that is not in the list is a
    /// break that cannot be saved or drawn. It is the one kind with nothing to
    /// show at runtime: the row simply ends.
    /// </remarks>
    RowBreak
}

/// <summary>
/// One entry in the action toolbar.
/// </summary>
/// <remarks>
/// The toolbar used to be thirteen buttons written out in the markup. They are
/// data now, because an order the user can change is an order the program has
/// to be able to rebuild — from a list of keys in the settings file, in a
/// sequence nobody wrote down in advance.
///
/// Everything that varies between the buttons lives here: the label, the
/// command, the tooltip, the fixed width of the icon-only ones, and the key of
/// the style that colours the four operation buttons. Nothing here is a brush.
/// A brush captured at build time would not follow a theme change, so the item
/// carries the <em>name</em> of a style and the template looks it up — the
/// DynamicResource references inside that style keep working as they did when
/// each button was written by hand.
/// </remarks>
public sealed class ToolbarItem : INotifyPropertyChanged
{
    /// <summary>
    /// Stable identifier, written to settings. Never shown, and never changed
    /// once shipped — it is what a saved order is a list of.
    /// </summary>
    /// <remarks>
    /// Unique among the buttons, and deliberately not among the rest: a layout
    /// can hold any number of spacers, expanders and row breaks, so those share
    /// one key each and are told apart by position alone.
    /// </remarks>
    public required string Key { get; init; }

    public ToolbarItemKind Kind { get; init; } = ToolbarItemKind.Button;

    /// <summary>True for everything that is not an action.</summary>
    public bool IsButton => Kind == ToolbarItemKind.Button;

    /// <summary>
    /// The caption. Settable, because one button renames itself — Select all
    /// becomes Select none once everything is checked — and the view binds
    /// straight to this rather than to a second path that only one item would
    /// use.
    /// </summary>
    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    private string _label = string.Empty;

    /// <summary>
    /// Which menu this action lives in, for grouping the Customize dialog's
    /// list. Empty for the spacers and breaks, which belong to no menu.
    /// </summary>
    public string Group { get; init; } = string.Empty;

    public ICommand? Command { get; init; }
    public string? ToolTip { get; init; }

    /// <summary>Fixed width for the icon-only buttons; 0 means "as wide as the text".</summary>
    public double Width { get; init; }

    /// <summary>Smallest width, so a button that renames itself does not shuffle the row.</summary>
    public double MinWidth { get; init; }

    /// <summary>
    /// Resource key of the style that paints this button, or null for the
    /// ordinary ones.
    /// </summary>
    public string? StyleKey { get; init; }

    /// <summary>
    /// True while this item is being dragged, so the view can show which one is
    /// moving. The row reorders live under the pointer, and without this the
    /// item being carried is indistinguishable from the ones getting out of its
    /// way.
    /// </summary>
    public bool IsDragging
    {
        get => _isDragging;
        set { _isDragging = value; OnPropertyChanged(); }
    }

    private bool _isDragging;

    /// <summary>A copy of this entry, for the Customize dialog's working list.</summary>
    /// <remarks>
    /// The dialog edits copies so Cancel can mean it. Spacers and expanders are
    /// copied rather than shared for a second reason: a layout may hold several,
    /// and two list entries that are the same object cannot be reordered
    /// independently.
    /// </remarks>
    public ToolbarItem Clone() => new()
    {
        Key = Key,
        Kind = Kind,
        Label = Label,
        Group = Group,
        Command = Command,
        ToolTip = ToolTip,
        Width = Width,
        MinWidth = MinWidth,
        StyleKey = StyleKey
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
