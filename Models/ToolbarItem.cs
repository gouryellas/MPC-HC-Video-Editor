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
        set { _label = value; OnPropertyChanged(); OnPropertyChanged(nameof(Caption)); OnPropertyChanged(nameof(Hint)); }
    }

    private string _label = string.Empty;

    /// <summary>
    /// The glyph alone, for the icons-only toolbar. Null for a button that has
    /// no sensible picture — the naming tags, which are each other's twin in
    /// every way but their text.
    /// </summary>
    /// <remarks>
    /// Settable for the same reason the label is: the select button turns into
    /// its own opposite, and a ☑ that stays put while the name underneath it
    /// changes would be telling the user the wrong thing.
    /// </remarks>
    public string? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); OnPropertyChanged(nameof(Caption)); }
    }

    private string? _icon;

    /// <summary>
    /// True when the toolbar is drawing icons only. Set on every item as the
    /// toolbar is built, from the one setting.
    /// </summary>
    /// <remarks>
    /// Carried per item rather than read from the settings by the template,
    /// because a binding needs something to listen to: flipping the setting has
    /// to redraw a row that is already on screen.
    /// </remarks>
    public bool IsCompact
    {
        get => _isCompact;
        set
        {
            _isCompact = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Caption));
            OnPropertyChanged(nameof(ButtonWidth));
            OnPropertyChanged(nameof(ButtonMinWidth));
        }
    }

    private bool _isCompact;

    /// <summary>What the toolbar button actually shows.</summary>
    public string Caption => _isCompact && Icon is { Length: > 0 } ? Icon : Label;

    /// <summary>Square while compact, and whatever it asked for otherwise.</summary>
    public double ButtonWidth => _isCompact && Icon is { Length: > 0 } ? 36 : Width;

    /// <summary>
    /// The minimum width, dropped while compact — one button reserves room for
    /// its longer name, which an icon does not need.
    /// </summary>
    public double ButtonMinWidth => _isCompact && Icon is { Length: > 0 } ? 0 : MinWidth;

    /// <summary>
    /// The tooltip, falling back to the button's own name. With the labels off
    /// the tooltip is the only thing that says what a button does, so every
    /// button has to have one.
    /// </summary>
    public string Hint => string.IsNullOrWhiteSpace(ToolTip) ? Label : ToolTip!;

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
    /// <remarks>
    /// Settable, because one group of buttons changes how it is painted while
    /// the program runs: the naming tags, of which exactly one is the active
    /// one and says so by being lit.
    /// </remarks>
    public string? StyleKey
    {
        get => _styleKey;
        set { _styleKey = value; OnPropertyChanged(); }
    }

    private string? _styleKey;

    /// <summary>
    /// True while this item is being dragged, so the view can show which one is
    /// moving. The row reorders live under the pointer, and without this the
    /// item being carried is indistinguishable from the ones getting out of its
    /// way.
    /// </summary>
    public bool IsDragging
    {
        get => _isDragging;
        set { _isDragging = value; OnPropertyChanged(); OnPropertyChanged(nameof(ChipOpacity)); }
    }

    private bool _isDragging;

    /// <summary>
    /// True for a list entry whose action is already on the toolbar. Drawn
    /// faded, and refused if dropped — it is there to be found, not to be added
    /// twice.
    /// </summary>
    public bool IsPlaced
    {
        get => _isPlaced;
        set { _isPlaced = value; OnPropertyChanged(); OnPropertyChanged(nameof(ChipOpacity)); }
    }

    private bool _isPlaced;

    /// <summary>
    /// How solid this entry is drawn: faded while it is being carried, and
    /// faded in the list when it is already on the toolbar.
    /// </summary>
    /// <remarks>
    /// A property rather than a converter because two reasons feed one number,
    /// and a binding cannot multiply two booleans without a multi-converter
    /// that would exist to answer this one question.
    /// </remarks>
    public double ChipOpacity => IsDragging || IsPlaced ? 0.4 : 1.0;

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
        Icon = Icon,
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
