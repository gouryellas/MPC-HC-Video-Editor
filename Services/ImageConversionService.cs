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
    /// The format to write a file of this extension back as, or PNG when
    /// nothing here can write that extension.
    /// </summary>
    /// <remarks>
    /// Resizing keeps the format it was given — a resized JPG should still be a
    /// JPG. WebP is the one readable format with no encoder, so a resized WebP
    /// comes back as a PNG rather than failing; it is the only lossless thing
    /// to do with it, and the summary says so.
    /// </remarks>
    public static Format FormatForFile(string path)
    {
        var ext = Path.GetExtension(path);

        // .tif and .tiff are the same format under two spellings; the entry is
        // keyed on the longer one.
        if (string.Equals(ext, ".tif", StringComparison.OrdinalIgnoreCase))
            return Formats.First(f => f.Key == "tiff");

        return Formats.FirstOrDefault(f =>
                   string.Equals(f.Extension, ext, StringComparison.OrdinalIgnoreCase))
               ?? Formats.First(f => f.Key == "png");
    }

    /// <summary>A size the user can pick, or one they typed.</summary>
    /// <param name="Note">What it is normally called, for the list.</param>
    public sealed record Resolution(int Width, int Height, string Note)
    {
        public string Display => $"{Width} × {Height}" + (Note.Length > 0 ? $"  ({Note})" : "");
    }

    /// <summary>
    /// The sizes offered, smallest first.
    /// </summary>
    /// <remarks>
    /// The common display and video sizes, plus the two square ones that get
    /// asked for by anything wanting an avatar or an icon. Anything else is
    /// typed in.
    /// </remarks>
    public static readonly Resolution[] Resolutions =
    {
        new(640, 480, "VGA"),
        new(800, 600, "SVGA"),
        new(1024, 768, "XGA"),
        new(1280, 720, "720p"),
        new(1366, 768, "laptop"),
        new(1600, 900, "HD+"),
        new(1920, 1080, "1080p"),
        new(2560, 1440, "1440p"),
        new(3840, 2160, "4K UHD"),
        new(512, 512, "square"),
        new(1024, 1024, "square"),
    };

    /// <summary>What to do when the image is not the shape of the target.</summary>
    public enum ResizeFit
    {
        /// <summary>
        /// Scale until it fits inside, keeping its shape. One side lands on the
        /// target and the other comes up short.
        /// </summary>
        Inside,

        /// <summary>
        /// Scale until it covers the target, keeping its shape, then trim the
        /// overflow evenly from both sides. The result is exactly the size
        /// asked for.
        /// </summary>
        Crop,

        /// <summary>
        /// Exactly the size asked for, by squashing. Distorts anything that was
        /// not already the right shape.
        /// </summary>
        Stretch
    }

    /// <summary>
    /// Writes <paramref name="inputPath"/> to <paramref name="outputPath"/> at
    /// the requested size, in the format the output extension names.
    /// </summary>
    /// <returns>The size actually written, which differs from the request under
    /// <see cref="ResizeFit.Inside"/>.</returns>
    public (int Width, int Height) Resize(string inputPath, string outputPath,
                                          int width, int height, ResizeFit fit)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "A size must be at least 1 × 1.");

        var frame = LoadFirstFrame(inputPath);

        var scale = fit switch
        {
            // The smaller ratio is the one that fits; the larger is the one
            // that covers.
            ResizeFit.Inside => Math.Min((double)width / frame.PixelWidth, (double)height / frame.PixelHeight),
            ResizeFit.Crop => Math.Max((double)width / frame.PixelWidth, (double)height / frame.PixelHeight),
            _ => 0
        };

        BitmapSource source;

        if (fit == ResizeFit.Stretch)
        {
            source = Scale(frame, (double)width / frame.PixelWidth, (double)height / frame.PixelHeight);
        }
        else
        {
            source = Scale(frame, scale, scale);

            if (fit == ResizeFit.Crop)
            {
                // Rounding can leave the covering scale a pixel short of the
                // target, and a crop rectangle that runs past the edge throws.
                var x = Math.Max(0, (source.PixelWidth - width) / 2);
                var y = Math.Max(0, (source.PixelHeight - height) / 2);
                var w = Math.Min(width, source.PixelWidth - x);
                var h = Math.Min(height, source.PixelHeight - y);

                var cropped = new CroppedBitmap(source, new System.Windows.Int32Rect(x, y, w, h));
                cropped.Freeze();
                source = cropped;
            }
        }

        var written = BitmapFrame.Create(source);
        written.Freeze();

        var format = FormatForFile(outputPath);

        if (string.Equals(format.Key, "ico", StringComparison.OrdinalIgnoreCase))
            WriteIcon(written, outputPath);
        else
            Encode(written, outputPath, format);

        return (source.PixelWidth, source.PixelHeight);
    }

    /// <summary>Scales a frame, rounded to whole pixels by WPF.</summary>
    private static BitmapSource Scale(BitmapSource source, double x, double y)
    {
        var scaled = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(x, y));
        scaled.Freeze();
        return scaled;
    }

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

        Encode(frame, outputPath, format);
    }

    /// <summary>Writes one frame out in the given format.</summary>
    private static void Encode(BitmapFrame frame, string outputPath, Format format)
    {
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
        writer.Write((byte)0);     // palette size, 0 for truecolor
        writer.Write((byte)0);     // reserved
        writer.Write((ushort)1);   // color planes
        writer.Write((ushort)32);  // bits per pixel
        writer.Write(png.Length);
        writer.Write(22);          // payload offset: 6-byte dir + 16-byte entry

        writer.Write(png);
    }
}
