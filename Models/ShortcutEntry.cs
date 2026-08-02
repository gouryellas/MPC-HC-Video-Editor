using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MpcHcVideoEditor.Models;

/// <summary>
/// A user-defined folder shortcut. The <see cref="Name"/> is what appears
/// in the File menu; <see cref="Path"/> is the absolute folder path that
/// gets opened in Explorer when the shortcut is clicked.
/// Implements <see cref="INotifyPropertyChanged"/> so that bound UI
/// (the Manage Shortcuts dialog and the dynamic File / Shortcuts menus)
/// refreshes immediately when a property changes — most importantly when
/// the user renames a shortcut.
/// </summary>
public class ShortcutEntry : INotifyPropertyChanged
{
    private string _path = "";
    private string _name = "";

    public string Path
    {
        get => _path;
        set { _path = value ?? ""; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value ?? ""; OnPropertyChanged(); }
    }

    public ShortcutEntry() { }

    public ShortcutEntry(string path, string? name = null)
    {
        var p = (path ?? "").TrimEnd('\\', '/');
        _path = string.IsNullOrEmpty(p) ? (path ?? "") : p + "\\";

        if (string.IsNullOrWhiteSpace(name))
        {
            _name = System.IO.Path.GetFileName(p);
            if (string.IsNullOrEmpty(_name)) _name = p;
        }
        else
        {
            _name = name;
        }
    }

    public override string ToString() => $"{Name}  —  {Path}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Allows <see cref="ShortcutEntry"/> to be deserialized from either a
/// plain JSON string (legacy settings.json written by v3.0, where the
/// Shortcuts array was <c>List&lt;string&gt;</c>) or a full object with
/// <c>{ "Path": "...", "Name": "..." }</c>. Always serializes as the
/// object form on write.
/// </summary>
public class ShortcutEntryJsonConverter : JsonConverter<ShortcutEntry>
{
    public override ShortcutEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            return string.IsNullOrEmpty(s) ? null : new ShortcutEntry(s);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            string? path = null;
            string? name = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                var prop = reader.GetString();
                reader.Read();
                var value = reader.GetString();
                if (prop == "Path") path = value;
                else if (prop == "Name") name = value;
            }
            if (string.IsNullOrEmpty(path)) return null;
            return new ShortcutEntry(path, name);
        }

        if (reader.TokenType == JsonTokenType.Null)
            return null;

        // Skip unknown token to avoid corrupting the stream.
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ShortcutEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Path", value.Path);
        writer.WriteString("Name", value.Name);
        writer.WriteEndObject();
    }
}
