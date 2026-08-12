using System.IO;
using System.Windows.Media.Imaging;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Converts still images between formats.
/// </summary>
/// <remarks>
/// Uses WPF's own imaging encoders rather than shelling out to ffmpeg. They
/// are already available to this process, handle every format offered here,
/// and avoid spawning one process per file. ICO is the exception — no encoder
/// ships for it, so the container is written by hand around a PNG payload.
/// </remarks>
public class ImageConversionService
{
    /// <summary>A target format the user can pick from the menu.</summary>
    public sealed record Format(string Key, string Extension, string Display);

    /// <summary>
    /// Offered formats, in menu order. JPG and JPEG are the same encoder and
    /// differ only by extension — both are listed because both get asked for.
    /// </summary>
    public static readonly Format[] Formats =
    {
        new("png",  ".png",  "PNG"),
        new("jpg",  ".jpg",  "JPG"),
        new("jpeg", ".jpeg", "JPEG"),
        new("bmp",  ".bmp",  "BMP"),
        new("gif",  ".gif",  "GIF"),
        new("tiff", ".tiff", "TIFF"),
        new("ico",  ".ico",  "ICO"),
    };

    /// <summary>Extensions accepted as input, for the file picker.</summary>
    public static readonly string[] ReadableExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".ico", ".webp" };

    public static Format? FindFormat(string? key) =>
        Formats.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads <paramref name="inputPath"/> and writes it to
    /// <paramref name="outputPath"/> in <paramref name="format"/>.
    /// </summary>
    public void Convert(string inputPath, string outputPath, Format format)
    {
        var frame = LoadFirstFrame(inputPath);

        if (string.Equals(format.Key, "ico", StringComparison.OrdinalIgnoreCase))
        {
            WriteIcon(frame, outputPath);
            return;
        }

        BitmapEncoder encoder = format.Key switch
        {
            "png" => new PngBitmapEncoder(),
            "jpg" or "jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            "bmp" => new BmpBitmapEncoder(),
            "gif" => new GifBitmapEncoder(),
            "tiff" => new TiffBitmapEncoder(),
            _ => throw new NotSupportedException($"No encoder for '{format.Key}'.")
        };

        encoder.Frames.Add(frame);

        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    /// <summary>
    /// Loads the first frame, fully into memory.
    /// </summary>
    /// <remarks>
    /// <c>OnLoad</c> matters: the default keeps the source stream open for the
    /// image's lifetime, which would leave the input file locked — and makes
    /// converting a file onto itself impossible.
    /// </remarks>
    private static BitmapFrame LoadFirstFrame(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(
            stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
            throw new InvalidDataException($"'{Path.GetFileName(path)}' contains no image data.");

        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    /// <summary>
    /// Writes a single-image .ico wrapping a PNG payload.
    /// </summary>
    /// <remarks>
    /// The ICO directory stores width and height in one byte each, so 256 is
    /// the largest expressible size and is encoded as 0. Anything bigger is
    /// scaled down to fit rather than silently truncated to a wrong size.
    /// </remarks>
    private static void WriteIcon(BitmapFrame frame, string outputPath)
    {
        const int maxSide = 256;

        BitmapSource source = frame;
        if (frame.PixelWidth > maxSide || frame.PixelHeight > maxSide)
        {
            var scale = Math.Min((double)maxSide / frame.PixelWidth,
                                 (double)maxSide / frame.PixelHeight);
            var scaled = new TransformedBitmap(frame,
                new System.Windows.Media.ScaleTransform(scale, scale));
            scaled.Freeze();
            source = scaled;
        }

        byte[] png;
        using (var buffer = new MemoryStream())
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(buffer);
            png = buffer.ToArray();
        }

        using var file = File.Create(outputPath);
        using var writer = new BinaryWriter(file);

        // ICONDIR
        writer.Write((ushort)0);   // reserved
        writer.Write((ushort)1);   // type: 1 = icon
        writer.Write((ushort)1);   // image count

        // ICONDIRENTRY — 256 is written as 0.
        writer.Write((byte)(source.PixelWidth >= maxSide ? 0 : source.PixelWidth));
        writer.Write((byte)(source.PixelHeight >= maxSide ? 0 : source.PixelHeight));
        writer.Write((byte)0);     // palette size, 0 for truecolour
        writer.Write((byte)0);     // reserved
        writer.Write((ushort)1);   // colour planes
        writer.Write((ushort)32);  // bits per pixel
        writer.Write(png.Length);
        writer.Write(22);          // payload offset: 6-byte dir + 16-byte entry

        writer.Write(png);
    }
}
