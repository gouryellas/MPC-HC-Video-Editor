using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MpcHcVideoEditor.Models;

/// <summary>
/// A user-defined filename suffix that gets appended to all video
/// operation outputs. The <see cref="Text"/> property holds the raw
/// alphanumeric string (e.g. <c>"done"</c>); when applied to a filename
/// the system wraps it in square brackets (e.g. <c>[done]</c>) and
/// auto-increments on collision (e.g. <c>[done2]</c>, <c>[done3]</c>).
/// </summary>
/// <remarks>
/// <para>
/// Validation rules enforced by the UI layer:
/// </para>
/// <list type="bullet">
///   <item>Alphanumeric characters only (a–z, A–Z, 0–9).</item>
///   <item>Length 1–50 characters.</item>
///   <item>Unique within the user's suffix list (case-insensitive).</item>
/// </list>
/// <para>
/// Implements <see cref="INotifyPropertyChanged"/> so bound UI (the
/// Suffix menu, the Manage Suffixes dialog) refreshes immediately when
/// <see cref="Text"/> changes — most importantly when the user renames
/// a suffix.
/// </para>
/// </remarks>
public class SuffixEntry : INotifyPropertyChanged
{
    private string _text = "";

    /// <summary>
    /// The raw suffix text, alphanumeric only (e.g. <c>"done"</c>).
    /// When displayed in menus it is wrapped in brackets:
    /// <c>[done]</c>.
    /// </summary>
    public string Text
    {
        get => _text;
        set { _text = value ?? ""; OnPropertyChanged(); }
    }

    /// <summary>
    /// Bracketed display form, e.g. <c>[done]</c>. Used by the menu
    /// and dialog to show the user exactly how the suffix will appear
    /// in output filenames.
    /// </summary>
    public string Display => $"[{_text}]";

    public SuffixEntry() { }

    public SuffixEntry(string text)
    {
        _text = text ?? "";
    }

    public override string ToString() => Display;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Serializes <see cref="SuffixEntry"/> as either a plain JSON string
/// (legacy <c>List&lt;string&gt;</c> format) or a full object with
/// <c>{ "Text": "..." }</c>. Always serializes as the object form on write.
/// </summary>
public class SuffixEntryJsonConverter : JsonConverter<SuffixEntry>
{
    public override SuffixEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            return string.IsNullOrEmpty(s) ? null : new SuffixEntry(s);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            string? text = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                var prop = reader.GetString();
                reader.Read();
                var value = reader.GetString();
                if (prop == "Text") text = value;
            }
            if (string.IsNullOrEmpty(text)) return null;
            return new SuffixEntry(text);
        }

        if (reader.TokenType == JsonTokenType.Null)
            return null;

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, SuffixEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Text", value.Text);
        writer.WriteEndObject();
    }
}
