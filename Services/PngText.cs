using System.IO;
using System.IO.Compression;
using System.Text;

namespace MpcHcVideoEditor.Services;

/// <summary>A key and its text, as stored in a PNG.</summary>
public sealed record PngTextChunk(string Key, string Text);

/// <summary>
/// Reads the text a PNG carries alongside its pixels.
/// </summary>
/// <remarks>
/// PNG keeps arbitrary text in three chunk types — <c>tEXt</c> for Latin-1,
/// <c>zTXt</c> for the same thing deflated, and <c>iTXt</c> for UTF-8, which may
/// also be deflated. None of it is EXIF, which is why a reader looking only at
/// <see cref="System.Windows.Media.Imaging.BitmapMetadata"/> finds a PNG silent
/// however much it is actually carrying.
///
/// Parsed here rather than asked of WPF. Its PNG decoder will return a tEXt
/// chunk, but only one you name — and the whole point is to find out what a
/// file holds without knowing in advance. Walking the chunks is also format,
/// not codec: it works whether or not Windows can decode the picture.
///
/// This is where image generators put what they did. ComfyUI writes its entire
/// node graph under <c>prompt</c> and <c>workflow</c>; Automatic1111 writes a
/// flat block under <c>parameters</c>.
/// </remarks>
public static class PngText
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    /// <summary>
    /// The largest single chunk that will be read, so a corrupt length field
    /// cannot ask for a gigabyte.
    /// </summary>
    private const int MaxChunk = 64 * 1024 * 1024;

    /// <summary>
    /// Every text chunk in the file, in the order they appear. Empty for
    /// anything that is not a PNG, or that carries no text.
    /// </summary>
    public static IReadOnlyList<PngTextChunk> Read(string path)
    {
        var found = new List<PngTextChunk>();

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            var signature = reader.ReadBytes(Signature.Length);
            if (!signature.SequenceEqual(Signature)) return found;

            while (stream.Position + 8 <= stream.Length)
            {
                var length = ReadBigEndian(reader);
                var type = Encoding.ASCII.GetString(reader.ReadBytes(4));

                if (length < 0 || length > MaxChunk) break;
                if (type == "IEND") break;

                // Only the three text types are worth reading; the rest are
                // skipped over by their own length, which is what makes this
                // safe on a file holding megabytes of pixels.
                if (type is "tEXt" or "zTXt" or "iTXt")
                {
                    var data = reader.ReadBytes(length);
                    var chunk = type switch
                    {
                        "tEXt" => PlainText(data),
                        "zTXt" => Deflated(data),
                        _ => International(data)
                    };

                    if (chunk is not null) found.Add(chunk);
                }
                else
                {
                    stream.Seek(length, SeekOrigin.Current);
                }

                // The CRC, which is not checked: a wrong one would mean a
                // damaged file, and reporting the text that did parse is more
                // use than refusing the lot.
                stream.Seek(4, SeekOrigin.Current);
            }
        }
        catch
        {
            // A truncated or malformed file gives back whatever was read
            // before it went wrong, which is the honest answer.
        }

        return found;
    }

    /// <summary>PNG stores its lengths most significant byte first.</summary>
    private static int ReadBigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length < 4) return -1;

        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    /// <summary>keyword, NUL, text.</summary>
    private static PngTextChunk? PlainText(byte[] data)
    {
        var split = Array.IndexOf(data, (byte)0);
        if (split <= 0) return null;

        var key = Latin1(data, 0, split);
        var text = Latin1(data, split + 1, data.Length - split - 1);

        return new PngTextChunk(key, text);
    }

    /// <summary>keyword, NUL, compression method, deflated text.</summary>
    private static PngTextChunk? Deflated(byte[] data)
    {
        var split = Array.IndexOf(data, (byte)0);
        if (split <= 0 || split + 2 > data.Length) return null;

        var key = Latin1(data, 0, split);
        var text = Inflate(data, split + 2, Encoding.Latin1);

        return text is null ? null : new PngTextChunk(key, text);
    }

    /// <summary>
    /// keyword, NUL, compression flag, compression method, language tag, NUL,
    /// translated keyword, NUL, UTF-8 text.
    /// </summary>
    private static PngTextChunk? International(byte[] data)
    {
        var split = Array.IndexOf(data, (byte)0);
        if (split <= 0 || split + 2 >= data.Length) return null;

        var key = Latin1(data, 0, split);
        var compressed = data[split + 1] == 1;

        // Past the language tag and the translated keyword, both of which are
        // NUL-terminated and neither of which is wanted here.
        var at = split + 3;
        for (var skipped = 0; skipped < 2 && at < data.Length; skipped++)
        {
            var end = Array.IndexOf(data, (byte)0, at);
            if (end < 0) return null;
            at = end + 1;
        }

        if (at > data.Length) return null;

        var text = compressed
            ? Inflate(data, at, Encoding.UTF8)
            : Encoding.UTF8.GetString(data, at, data.Length - at);

        return text is null ? null : new PngTextChunk(key, text);
    }

    /// <summary>Zlib-deflated bytes as text, or null if they will not inflate.</summary>
    private static string? Inflate(byte[] data, int offset, Encoding encoding)
    {
        try
        {
            using var source = new MemoryStream(data, offset, data.Length - offset);
            using var zlib = new ZLibStream(source, CompressionMode.Decompress);
            using var output = new MemoryStream();

            zlib.CopyTo(output);
            return encoding.GetString(output.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static string Latin1(byte[] data, int offset, int count) =>
        count <= 0 ? string.Empty : Encoding.Latin1.GetString(data, offset, count).TrimEnd('\0');
}
