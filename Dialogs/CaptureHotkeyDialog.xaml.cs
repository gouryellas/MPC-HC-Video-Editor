using System.Windows;
using System.Windows.Input;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Modal dialog for picking the "set bookmark timestamp" hotkey.
/// </summary>
/// <remarks>
/// It captures from the moment it opens: the field shows whatever was last
/// pressed, live, and OK commits it. There is nothing to arm and no separate
/// mouse-versus-keyboard choice to make — the previous version had radio
/// buttons for mouse buttons and a separate click-to-focus capture area,
/// which meant deciding up front which kind of input you wanted before you
/// could express it.
/// </remarks>
public partial class CaptureHotkeyDialog : Window
{
    /// <summary>
    /// The binding chosen by the user, set when they click OK or Disable.
    /// Null if they cancelled.
    /// </summary>
    public HotkeyBinding? Result { get; private set; }

    private HotkeyBinding _captured;

    public CaptureHotkeyDialog(HotkeyBinding current)
    {
        InitializeComponent();

        // Open showing what is already bound, so OK without pressing
        // anything is a no-op rather than a surprise.
        _captured = current;
        CapturedText.Text = current.Kind == HotkeyBinding.HotkeyKind.None
            ? "(none)"
            : current.Display;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Keyboard focus on the window itself, not the OK button — otherwise
        // Space or Enter would press OK instead of being captured.
        Keyboard.Focus(this);
    }

    /// <summary>
    /// Captures every keystroke before anything else can act on it, so keys
    /// that normally drive a dialog (Space, Enter, Tab, arrows) are bindable.
    /// Escape is left alone so the dialog stays cancellable.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape) return;   // let IsCancel close the dialog

        if (IsModifierKey(key))
        {
            // Show the modifiers as they are held, so the combo builds up
            // visibly rather than staying blank until the final key.
            var parts = new List<string>();
            var mods = Keyboard.Modifiers;
            if ((mods & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((mods & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((mods & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((mods & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add("…");

            CapturedText.Text = string.Join("+", parts);
            e.Handled = true;
            return;
        }

        _captured = HotkeyBinding.FromKeyboard(Keyboard.Modifiers, key);
        CapturedText.Text = _captured.Display;
        e.Handled = true;
    }

    /// <summary>
    /// Captures middle and side mouse buttons. Left and right are left alone
    /// so the OK / Cancel buttons remain clickable.
    /// </summary>
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var button = e.ChangedButton switch
        {
            MouseButton.Middle => HotkeyBinding.MouseButtonKind.MButton,
            MouseButton.XButton1 => HotkeyBinding.MouseButtonKind.XButton1,
            MouseButton.XButton2 => HotkeyBinding.MouseButtonKind.XButton2,
            _ => (HotkeyBinding.MouseButtonKind?)null
        };
        if (button == null) return;

        _captured = HotkeyBinding.FromMouse(button.Value);
        CapturedText.Text = _captured.Display;
        e.Handled = true;
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = _captured;
        DialogResult = true;
    }

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        Result = HotkeyBinding.None;
        DialogResult = true;
    }
}
