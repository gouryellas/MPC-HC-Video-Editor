using System.IO;
using System.Text;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Removes the descriptive metadata from JPEG, PNG and WebP images without
/// touching a single pixel.
/// </summary>
/// <remarks>
/// <para>
/// Done by walking the file's own structure and leaving out the blocks that
/// describe rather than draw — not by decoding and re-saving, which would
/// cost a JPEG a generation of quality for the sake of deleting some text.
/// </para>
/// <para>
/// What goes is what says something about the person or the device: EXIF
/// (camera, serial number, date, GPS position), XMP, IPTC captions, comments,
/// and the text chunks image generators write their prompts into. What stays
/// is what the picture needs to look right: colour profiles, JPEG's Adobe
/// colour-transform marker, and the orientation — a phone photo stripped of
/// its EXIF would otherwise come out on its side, so that one value is
/// written back on its own.
/// </para>
/// </remarks>
public static class MetadataStripper
{
    /// <summary>Extensions this can clean.</summary>
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>Whether a path is an image this can clean.</summary>
    public static bool IsSupportedImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Writes a cleaned copy of an image.
    /// </summary>
    /// <returns>How many metadata blocks were left out.</returns>
    /// <exception cref="InvalidDataException">The file is not the format its name says.</exception>
    public static int StripImage(string inputPath, string outputPath)
    {
        var data = File.ReadAllBytes(inputPath);

        var (cleaned, removed) = Path.GetExtension(inputPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => StripJpeg(data),
            ".png" => StripPng(data),
            ".webp" => StripWebp(data),
            _ => throw new InvalidDataException("Only JPEG, PNG and WebP images can be cleaned.")
        };

        File.WriteAllBytes(outputPath, cleaned);
        return removed;
    }

    // ------------------------------------------------------------------
    // JPEG
    // ------------------------------------------------------------------

    /// <summary>
    /// Copies a JPEG's markers, leaving out the descriptive ones.
    /// </summary>
    /// <remarks>
    /// Kept: APP0 (JFIF, which some decoders read for pixel density), APP2
    /// when it is an ICC profile, and APP14 (Adobe, which says whether the
    /// data is RGB or YCbCr — dropping it turns some CMYK files to garbage).
    /// Left out: every other APPn, which is EXIF, XMP, IPTC, maker notes and
    /// thumbnails, and COM.
    /// </remarks>
    internal static (byte[] Data, int Removed) StripJpeg(byte[] data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
            throw new InvalidDataException("This is not a JPEG file.");

        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2);

        var removed = 0;
        ushort? orientation = null;
        var orientationWritten = false;
        var pos = 2;

        while (pos + 4 <= data.Length)
        {
            if (data[pos] != 0xFF)
                throw new InvalidDataException("The JPEG's structure could not be read.");

            var marker = data[pos + 1];

            // Fill bytes before a marker are legal and mean nothing.
            if (marker == 0xFF) { pos++; continue; }

            // Start of scan: everything from here to the end is picture.
            // The orientation goes in just before it, after anything kept.
            if (marker == 0xDA)
            {
                if (orientation is { } o && !orientationWritten) output.Write(OrientationApp1(o));
                output.Write(data, pos, data.Length - pos);
                return (output.ToArray(), removed);
            }

            var length = (data[pos + 2] << 8) | data[pos + 3];
            if (length < 2 || pos + 2 + length > data.Length)
                throw new InvalidDataException("The JPEG's structure could not be read.");

            var payloadStart = pos + 4;
            var payloadLength = length - 2;

            var keep = marker switch
            {
                0xE0 => true,
                0xE2 => StartsWith(data, payloadStart, payloadLength, "ICC_PROFILE\0"),
                0xEE => true,
                0xFE => false,
                >= 0xE1 and <= 0xEF => false,
                _ => true
            };

            if (!keep)
            {
                if (marker == 0xE1 && StartsWith(data, payloadStart, payloadLength, "Exif\0\0"))
                    orientation ??= ReadExifOrientation(data, payloadStart + 6, payloadLength - 6);

                removed++;
            }
            else
            {
                // The orientation goes back where EXIF sat: after the
                // application blocks, ahead of the tables. Every reader looks
                // for it there, and EXIF always comes before the tables, so by
                // the first one its value is known.
                var isApp = marker is >= 0xE0 and <= 0xEF;
                if (!isApp && orientation is { } o && !orientationWritten)
                {
                    output.Write(OrientationApp1(o));
                    orientationWritten = true;
                }

                output.Write(data, pos, 2 + length);
            }

            pos += 2 + length;
        }

        throw new InvalidDataException("The JPEG ended before its picture began.");
    }

    /// <summary>
    /// The orientation tag from an EXIF block, or null when there is none or
    /// it says the picture is already upright.
    /// </summary>
    private static ushort? ReadExifOrientation(byte[] data, int start, int length)
    {
        try
        {
            if (length < 8) return null;

            var little = data[start] == 'I' && data[start + 1] == 'I';
            if (!little && !(data[start] == 'M' && data[start + 1] == 'M')) return null;

            ushort U16(int at) => little
                ? (ushort)(data[at] | (data[at + 1] << 8))
                : (ushort)((data[at] << 8) | data[at + 1]);

            uint U32(int at) => little
                ? (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24))
                : (uint)((data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3]);

            var ifd = start + (int)U32(start + 4);
            if (ifd + 2 > start + length) return null;

            var count = U16(ifd);
            for (var i = 0; i < count; i++)
            {
                var entry = ifd + 2 + i * 12;
                if (entry + 12 > start + length) return null;

                if (U16(entry) == 0x0112)
                {
                    var value = U16(entry + 8);
                    return value is >= 2 and <= 8 ? value : null;
                }
            }
        }
        catch (IndexOutOfRangeException)
        {
            // A malformed EXIF block loses its orientation, not the file.
        }

        return null;
    }

    /// <summary>
    /// An EXIF block holding the orientation and nothing else.
    /// </summary>
    private static byte[] OrientationApp1(ushort orientation)
    {
        var block = new byte[]
        {
            0xFF, 0xE1, 0x00, 0x22,                     // APP1, length 34
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A,           // big-endian TIFF
            0x00, 0x00, 0x00, 0x08,                     // first IFD at 8
            0x00, 0x01,                                 // one entry
            0x01, 0x12, 0x00, 0x03,                     // Orientation, SHORT
            0x00, 0x00, 0x00, 0x01,                     // count 1
            (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00                      // no next IFD
        };

        return block;
    }

    // ------------------------------------------------------------------
    // PNG
    // ------------------------------------------------------------------

    /// <summary>Chunks that describe rather than draw.</summary>
    private static readonly HashSet<string> PngTextChunks = new(StringComparer.Ordinal)
    {
        "tEXt", "zTXt", "iTXt", "eXIf", "tIME"
    };

    /// <summary>
    /// Copies a PNG's chunks, leaving out the text, EXIF and timestamp ones.
    /// </summary>
    /// <remarks>
    /// Chunk CRCs cover only their own chunk, so dropping one needs nothing
    /// else rewritten. Colour chunks — iCCP, sRGB, gAMA, cHRM — stay.
    /// </remarks>
    internal static (byte[] Data, int Removed) StripPng(byte[] data)
    {
        ReadOnlySpan<byte> signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (data.Length < 8 || !data.AsSpan(0, 8).SequenceEqual(signature))
            throw new InvalidDataException("This is not a PNG file.");

        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 8);

        var removed = 0;
        var pos = 8;

        while (pos + 12 <= data.Length)
        {
            var length = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            if (length < 0 || pos + 12 + length > data.Length)
                throw new InvalidDataException("The PNG's structure could not be read.");

            var type = Encoding.ASCII.GetString(data, pos + 4, 4);

            if (PngTextChunks.Contains(type)) removed++;
            else output.Write(data, pos, 12 + length);

            pos += 12 + length;
            if (type == "IEND") break;
        }

        return (output.ToArray(), removed);
    }

    // ------------------------------------------------------------------
    // WebP
    // ------------------------------------------------------------------

    /// <summary>
    /// Copies a WebP's chunks, leaving out EXIF and XMP and clearing the
    /// header flags that announced them.
    /// </summary>
    internal static (byte[] Data, int Removed) StripWebp(byte[] data)
    {
        if (data.Length < 12 || Encoding.ASCII.GetString(data, 0, 4) != "RIFF" ||
            Encoding.ASCII.GetString(data, 8, 4) != "WEBP")
            throw new InvalidDataException("This is not a WebP file.");

        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 12);

        var removed = 0;
        var pos = 12;

        while (pos + 8 <= data.Length)
        {
            var type = Encoding.ASCII.GetString(data, pos, 4);
            var size = data[pos + 4] | (data[pos + 5] << 8) | (data[pos + 6] << 16) | (data[pos + 7] << 24);
            var padded = size + (size & 1);
            if (size < 0 || pos + 8 + padded > data.Length)
            {
                // Some writers leave off the final pad byte.
                if (size >= 0 && pos + 8 + size == data.Length) padded = size;
                else throw new InvalidDataException("The WebP's structure could not be read.");
            }

            if (type is "EXIF" or "XMP ")
            {
                removed++;
            }
            else
            {
                var start = (int)output.Position;
                output.Write(data, pos, 8 + padded);

                // VP8X carries a flag per optional chunk; a flag left set for a
                // chunk that is gone makes strict readers refuse the file.
                if (type == "VP8X")
                {
                    var buffer = output.GetBuffer();
                    buffer[start + 8] &= unchecked((byte)~(0x08 | 0x04));
                }
            }

            pos += 8 + padded;
        }

        var result = output.ToArray();
        var riffSize = result.Length - 8;
        result[4] = (byte)riffSize;
        result[5] = (byte)(riffSize >> 8);
        result[6] = (byte)(riffSize >> 16);
        result[7] = (byte)(riffSize >> 24);

        return (result, removed);
    }

    private static bool StartsWith(byte[] data, int start, int length, string text)
    {
        if (length < text.Length) return false;
        for (var i = 0; i < text.Length; i++)
            if (data[start + i] != text[i]) return false;
        return true;
    }
}
