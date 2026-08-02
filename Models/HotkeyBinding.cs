using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MpcHcVideoEditor.Models;

/// <summary>
/// A user-configurable hotkey for the "set bookmark timestamp" action.
/// Can be either a mouse button (MButton / XButton1 / XButton2) or a
/// keyboard combo (an optional set of modifier keys plus a single key).
/// Immutable. Serialized to <c>settings.json</c> as a short string like
/// <c>"MButton"</c> or <c>"Ctrl+Shift+T"</c> via
/// <see cref="HotkeyBindingJsonConverter"/>.
/// </summary>
public sealed class HotkeyBinding
{
    /// <summary>
    /// Which kind of input triggers the hotkey. <see cref="Kind.None"/>
    /// means the hotkey is disabled.
    /// </summary>
    public enum HotkeyKind { None, Mouse, Keyboard }

    /// <summary>Mouse button values that can be used as a hotkey.</summary>
    public enum MouseButtonKind { MButton, XButton1, XButton2 }

    public HotkeyKind Kind { get; }
    public MouseButtonKind? Mouse { get; }
    public ModifierKeys Modifiers { get; }
    public Key? Key { get; }

    /// <summary>A binding with <see cref="HotkeyKind.None"/> — disabled.</summary>
    public static HotkeyBinding None { get; } = new(HotkeyKind.None);

    /// <summary>The default binding: middle mouse button.</summary>
    public static HotkeyBinding DefaultMouse { get; } =
        new(HotkeyKind.Mouse, MouseButtonKind.MButton);

    private HotkeyBinding(HotkeyKind kind, MouseButtonKind? mouse = null,
        ModifierKeys modifiers = ModifierKeys.None, Key? key = null)
    {
        Kind = kind;
        Mouse = mouse;
        Modifiers = modifiers;
        Key = key;
    }

    public static HotkeyBinding FromMouse(MouseButtonKind button) =>
        new(HotkeyKind.Mouse, button);

    public static HotkeyBinding FromKeyboard(ModifierKeys modifiers, Key key) =>
        new(HotkeyKind.Keyboard, null, modifiers, key);

    /// <summary>
    /// Human-readable form shown in the status bar and menu:
    /// <c>"MButton"</c>, <c>"XButton1"</c>, <c>"Ctrl+Shift+T"</c>,
    /// <c>"F8"</c>, or <c>"(disabled)"</c>.
    /// </summary>
    public string Display
    {
        get
        {
            switch (Kind)
            {
                case HotkeyKind.None: return "(disabled)";
                case HotkeyKind.Mouse: return Mouse?.ToString() ?? "(unknown mouse)";
                case HotkeyKind.Keyboard:
                    var parts = new List<string>();
                    if ((Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
                    if ((Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
                    if ((Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
                    if ((Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
                    if (Key.HasValue) parts.Add(KeyToString(Key.Value));
                    return parts.Count == 0 ? "(disabled)" : string.Join("+", parts);
            }
            return "(disabled)";
        }
    }

    /// <summary>Compact string form persisted to settings.json.</summary>
    public string ToSettingsString() => Display;

    private static string KeyToString(Key key)
    {
        // Normalize a few common ones for readability; everything else
        // just uses the enum name (F8, D1, Space, etc.).
        // Fully qualified: the class has an instance property named Key,
        // so inside this static method an unqualified `Key.Space` resolves
        // to the property (which is invalid in a static context → CS0120).
        return key switch
        {
            System.Windows.Input.Key.Space => "Space",
            System.Windows.Input.Key.OemTilde => "`",
            System.Windows.Input.Key.OemMinus => "-",
            System.Windows.Input.Key.OemPlus => "+",
            _ => key.ToString()
        };
    }

    /// <summary>
    /// Parses a settings-string back into a <see cref="HotkeyBinding"/>.
    /// Accepts: <c>null</c>/<c>""</c>/<c>"none"</c>/<c>"(disabled)"</c> → None;
    /// <c>"MButton"</c>/<c>"XButton1"</c>/<c>"XButton2"</c> → mouse binding;
    /// anything else parsed as <c>[Ctrl+][Shift+][Alt+][Win+]Key</c>.
    /// Returns <see cref="DefaultMouse"/> if parsing fails entirely,
    /// so a corrupt settings file never breaks the hotkey feature.
    /// </summary>
    public static HotkeyBinding Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return None;
        var trimmed = text.Trim();

        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("(disabled)", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("(none)", StringComparison.OrdinalIgnoreCase))
            return None;

        // Mouse buttons
        if (trimmed.Equals("MButton", StringComparison.OrdinalIgnoreCase))
            return FromMouse(MouseButtonKind.MButton);
        if (trimmed.Equals("XButton1", StringComparison.OrdinalIgnoreCase))
            return FromMouse(MouseButtonKind.XButton1);
        if (trimmed.Equals("XButton2", StringComparison.OrdinalIgnoreCase))
            return FromMouse(MouseButtonKind.XButton2);

        // Keyboard combo: split on '+'
        var tokens = trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .ToList();
        if (tokens.Count == 0) return None;

        ModifierKeys mods = ModifierKeys.None;
        Key? key = null;
        foreach (var tok in tokens)
        {
            if (tok.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                tok.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Control;
            else if (tok.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Shift;
            else if (tok.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
                     tok.Equals("Menu", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Alt;
            else if (tok.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                     tok.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Windows;
            else
            {
                if (Enum.TryParse<Key>(tok, ignoreCase: true, out var k))
                    key = k;
                else
                {
                    // Map a few human-readable aliases back to the enum.
                    // Fully qualified — see KeyToString() for the reason.
                    key = tok.ToLowerInvariant() switch
                    {
                        "space" => System.Windows.Input.Key.Space,
                        "`" => System.Windows.Input.Key.OemTilde,
                        "-" => System.Windows.Input.Key.OemMinus,
                        "+" => System.Windows.Input.Key.OemPlus,
                        _ => key
                    };
                }
            }
        }

        if (key == null) return None;
        return FromKeyboard(mods, key.Value);
    }

    public override string ToString() => Display;
}

/// <summary>
/// Serializes <see cref="HotkeyBinding"/> as a short string like
/// <c>"MButton"</c> or <c>"Ctrl+Shift+T"</c>, instead of a full object.
/// On read, accepts either a string (preferred) or a legacy
/// <c>{ "Kind": "...", ... }</c> object form.
/// </summary>
public class HotkeyBindingJsonConverter : JsonConverter<HotkeyBinding>
{
    public override HotkeyBinding? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return HotkeyBinding.Parse(reader.GetString());
        if (reader.TokenType == JsonTokenType.Null)
            return HotkeyBinding.None;
        // Tolerate an unexpected object token by skipping — Parse(null) returns None.
        reader.Skip();
        return HotkeyBinding.None;
    }

    public override void Write(Utf8JsonWriter writer, HotkeyBinding value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToSettingsString());
}
