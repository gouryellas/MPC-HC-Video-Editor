using System.IO;
using System.Text;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Reads the small text files this application owns — playlists and bookmark
/// CSVs — working out their encoding rather than assuming one.
/// </summary>
/// <remarks>
/// <para>
/// Both formats are plain text that other tools also write, frequently in the
/// legacy Windows code page where an accented character is a single byte. Read
/// as UTF-8, every one of those bytes becomes U+FFFD, and a path containing
/// U+FFFD matches nothing on disk: the entry shows as missing while the file
/// sits where it always was. Worse, rewriting then bakes the replacement
/// character in permanently.
/// </para>
/// <para>
/// This began life inside <c>PlaylistService</c> after exactly that bug. It
/// lives here because <c>BookmarkService</c> had the same defect for the same
/// reason — and this project is a rewrite of an AutoHotkey predecessor, so
/// CSVs carried over from that era are precisely the ones likely to be
/// affected.
/// </para>
/// </remarks>
public static class TextFile
{
    /// <summary>
    /// Windows-1252's 0x80–0x9F range, the only place it differs from Latin-1.
    /// Five of the 32 are genuinely unassigned and decode to the replacement
    /// character.
    /// </summary>
    private static readonly char[] Cp1252Punctuation =
    {
        '€', '�', '‚', 'ƒ', '„', '…', '†', '‡',
        'ˆ', '‰', 'Š', '‹', 'Œ', '�', 'Ž', '�',
        '�', '‘', '’', '“', '”', '•', '–', '—',
        '˜', '™', 'š', '›', 'œ', '�', 'ž', 'Ÿ'
    };

    /// <summary>
    /// Reads a file as text, detecting its encoding.
    /// </summary>
    /// <remarks>
    /// Honour a byte-order mark when there is one; otherwise try <em>strict</em>
    /// UTF-8, which rejects the byte sequences legacy text produces; and only
    /// when that fails fall back to Windows-1252. Genuine UTF-8 essentially
    /// never fails a strict decode, so the fallback cannot misclaim a file that
    /// was UTF-8 all along.
    /// </remarks>
    public static string ReadAllText(string path)
    {
        var bytes = File.ReadAllBytes(path);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return DecodeWindows1252(bytes);
        }
    }

    /// <summary>Splits <see cref="ReadAllText"/> into lines.</summary>
    public static string[] ReadAllLines(string path)
        => ReadAllText(path).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

    /// <summary>
    /// Decodes bytes as Windows-1252.
    /// </summary>
    /// <remarks>
    /// Hand-rolled because <c>Encoding.GetEncoding(1252)</c> needs the
    /// System.Text.Encoding.CodePages package on modern .NET, and
    /// <see cref="Encoding.Latin1"/> — identical from 0xA0 up, and enough for
    /// the accented letters — leaves 0x80–0x9F as control codes, which is
    /// exactly where Windows-1252 keeps the curly quotes, dashes and ellipsis
    /// that turn up in filenames taken off the web.
    /// </remarks>
    private static string DecodeWindows1252(byte[] bytes)
    {
        var chars = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i] = b < 0x80  ? (char)b                       // ASCII
                     : b < 0xA0  ? Cp1252Punctuation[b - 0x80]   // the divergent range
                     :             (char)b;                      // 0xA0-0xFF match Unicode
        }
        return new string(chars);
    }
}
