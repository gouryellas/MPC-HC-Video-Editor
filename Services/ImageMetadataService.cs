using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace MpcHcVideoEditor.Services;

/// <summary>One fact about an image.</summary>
public sealed record MetaRow(string Name, string Value);

/// <summary>A heading and the facts under it.</summary>
public sealed record MetaGroup(string Name, IReadOnlyList<MetaRow> Rows);

/// <summary>Everything known about one image file.</summary>
/// <param name="Location">
/// Where the photograph was taken, when it says so. Separate from the groups
/// because it is the one piece of metadata worth knowing about before a file
/// is sent anywhere.
/// </param>
/// <param name="Problem">
/// Why little or nothing could be read, when that is the case. Null on success.
/// </param>
public sealed record ImageFacts(
    string Path,
    string FileName,
    IReadOnlyList<MetaGroup> Groups,
    string? Location,
    string? Problem)
{
    /// <summary>Everything, as text, for the clipboard.</summary>
    public string AsText()
    {
        var lines = new List<string> { FileName };

        foreach (var group in Groups)
        {
            lines.Add(string.Empty);
            lines.Add(group.Name);
            lines.AddRange(group.Rows.Select(r => $"  {r.Name}: {r.Value}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Reads what an image file says about itself.
/// </summary>
/// <remarks>
/// WPF's own decoders, which already serve the conversion and resizing, rather
/// than a metadata library: EXIF, XMP and IPTC all arrive through
/// <see cref="BitmapMetadata"/> for the formats that carry them, and a portable
/// app is better off with one fewer dependency.
///
/// Every read is defensive. Metadata is a bag of optional tags written by
/// hundreds of different cameras and editors, and asking for one that is not
/// there throws rather than returning null — so each query is wrapped, and a
/// file that answers nothing produces an empty group instead of an error.
/// </remarks>
public class ImageMetadataService
{
    public ImageFacts Read(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        var groups = new List<MetaGroup>();

        // The file itself, which is knowable whether or not the picture can be
        // decoded at all.
        try
        {
            var file = new FileInfo(path);
            groups.Add(new MetaGroup("File", new List<MetaRow>
            {
                new("Name", file.Name),
                new("Folder", file.DirectoryName ?? string.Empty),
                new("Size", DescribeSize(file.Length)),
                new("Modified", file.LastWriteTime.ToString("f", CultureInfo.CurrentCulture)),
            }));
        }
        catch (Exception ex)
        {
            return new ImageFacts(path, name, groups, null, ex.Message);
        }

        BitmapFrame frame;
        BitmapDecoder decoder;

        try
        {
            using var stream = File.OpenRead(path);
            decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat,
                                           BitmapCacheOption.OnLoad);
            frame = decoder.Frames[0];
        }
        catch (Exception ex)
        {
            // Two quite different reasons, and nothing here can tell them
            // apart: Windows decodes WebP only when the optional codec is
            // installed, and a file can simply not be the picture its name
            // claims. Both are said, because asserting the wrong one sends
            // someone looking for a codec they do not need.
            var ext = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            return new ImageFacts(path, name, groups, null,
                $"Could not read this {ext} — either Windows has no decoder for it, or the file is " +
                $"not the picture its name says it is. Only the file's own details are shown. " +
                ex.Message);
        }

        groups.Add(new MetaGroup("Picture", Picture(frame, decoder)));

        var metadata = frame.Metadata as BitmapMetadata;
        if (metadata is not null)
        {
            var camera = Camera(metadata);
            if (camera.Count > 0) groups.Add(new MetaGroup("Camera", camera));

            var described = Described(metadata);
            if (described.Count > 0) groups.Add(new MetaGroup("Description", described));
        }

        var location = metadata is null ? null : Location(metadata);
        if (location is not null)
            groups.Add(new MetaGroup("Location", new List<MetaRow> { new("Coordinates", location) }));

        return new ImageFacts(path, name, groups, location, null);
    }

    private static List<MetaRow> Picture(BitmapFrame frame, BitmapDecoder decoder)
    {
        var rows = new List<MetaRow>
        {
            new("Size", $"{frame.PixelWidth} × {frame.PixelHeight} pixels"),
            new("Megapixels", (frame.PixelWidth * (long)frame.PixelHeight / 1_000_000.0).ToString("0.0")),
            new("Resolution", $"{frame.DpiX:0} × {frame.DpiY:0} DPI"),
            new("Colour", $"{frame.Format.BitsPerPixel}-bit {frame.Format}"),
        };

        // Only worth saying for the formats that can hold more than one.
        if (decoder.Frames.Count > 1)
            rows.Add(new MetaRow("Frames", decoder.Frames.Count.ToString(CultureInfo.CurrentCulture)));

        return rows;
    }

    private static List<MetaRow> Camera(BitmapMetadata m)
    {
        var rows = new List<MetaRow>();

        Add(rows, "Make", Try(() => m.CameraManufacturer));
        Add(rows, "Model", Try(() => m.CameraModel));
        Add(rows, "Taken", Try(() => m.DateTaken));
        Add(rows, "Software", Try(() => m.ApplicationName));

        // The EXIF tags WPF has no named property for. The numbers are the
        // tag ids from the EXIF specification; the paths are WIC's spelling of
        // "the EXIF block inside a JPEG".
        Add(rows, "Exposure", Exposure(Query(m, "/app1/ifd/exif:{uint=33434}")));
        Add(rows, "Aperture", Aperture(Query(m, "/app1/ifd/exif:{uint=33437}")));
        Add(rows, "ISO", Query(m, "/app1/ifd/exif:{uint=34855}")?.ToString());
        Add(rows, "Focal length", Millimetres(Query(m, "/app1/ifd/exif:{uint=37386}")));
        Add(rows, "Orientation", Orientation(Query(m, "/app1/ifd/{ushort=274}")));

        return rows;
    }

    private static List<MetaRow> Described(BitmapMetadata m)
    {
        var rows = new List<MetaRow>();

        Add(rows, "Title", Try(() => m.Title));
        Add(rows, "Subject", Try(() => m.Subject));
        Add(rows, "Comment", Try(() => m.Comment));
        Add(rows, "Author", Try(() => m.Author is { Count: > 0 } a ? string.Join(", ", a) : null));
        Add(rows, "Copyright", Try(() => m.Copyright));
        Add(rows, "Keywords", Try(() => m.Keywords is { Count: > 0 } k ? string.Join(", ", k) : null));
        Add(rows, "Rating", Try(() => m.Rating > 0 ? m.Rating.ToString(CultureInfo.CurrentCulture) : null));

        return rows;
    }

    /// <summary>
    /// Where the picture was taken, as degrees, or null when it does not say.
    /// </summary>
    private static string? Location(BitmapMetadata m)
    {
        var lat = Degrees(Query(m, "/app1/ifd/gps/{ushort=2}"));
        var lon = Degrees(Query(m, "/app1/ifd/gps/{ushort=4}"));

        if (lat is null || lon is null) return null;

        var northSouth = Query(m, "/app1/ifd/gps/{ushort=1}")?.ToString()?.Trim('\0') ?? "N";
        var eastWest = Query(m, "/app1/ifd/gps/{ushort=3}")?.ToString()?.Trim('\0') ?? "E";

        return $"{lat:0.#####}° {northSouth}, {lon:0.#####}° {eastWest}";
    }

    // ------------------------------------------------------------------
    // Reading the awkward shapes EXIF stores things in
    // ------------------------------------------------------------------

    /// <summary>A metadata query, or null if it is not there or throws.</summary>
    private static object? Query(BitmapMetadata m, string query)
    {
        try { return m.GetQuery(query); }
        catch { return null; }
    }

    /// <summary>A named property, or null if reading it throws.</summary>
    private static string? Try(Func<string?> read)
    {
        try { return read(); }
        catch { return null; }
    }

    private static void Add(List<MetaRow> rows, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) rows.Add(new MetaRow(name, value.Trim()));
    }

    /// <summary>
    /// An EXIF rational as a number.
    /// </summary>
    /// <remarks>
    /// Rationals arrive packed into one integer: the numerator in the low half,
    /// the denominator in the high half. Signed ones come as <c>long</c> and
    /// unsigned as <c>ulong</c>, so both are accepted.
    /// </remarks>
    private static double? Rational(object? value)
    {
        ulong packed;

        switch (value)
        {
            case ulong u: packed = u; break;
            case long l: packed = unchecked((ulong)l); break;
            default: return null;
        }

        var numerator = (uint)(packed & 0xFFFFFFFF);
        var denominator = (uint)(packed >> 32);

        return denominator == 0 ? null : numerator / (double)denominator;
    }

    /// <summary>Degrees, minutes and seconds — three rationals — as degrees.</summary>
    private static double? Degrees(object? value)
    {
        if (value is not Array array || array.Length < 3) return null;

        var d = Rational(array.GetValue(0));
        var m = Rational(array.GetValue(1));
        var s = Rational(array.GetValue(2));

        if (d is null || m is null || s is null) return null;

        return d + m / 60 + s / 3600;
    }

    /// <summary>A shutter speed, as the fraction photographers read.</summary>
    private static string? Exposure(object? value)
    {
        var seconds = Rational(value);
        if (seconds is null or <= 0) return null;

        return seconds >= 1
            ? $"{seconds:0.#}s"
            : $"1/{Math.Round(1 / seconds.Value)}s";
    }

    private static string? Aperture(object? value) =>
        Rational(value) is { } f and > 0 ? $"f/{f:0.#}" : null;

    private static string? Millimetres(object? value) =>
        Rational(value) is { } mm and > 0 ? $"{mm:0.#} mm" : null;

    /// <summary>The EXIF orientation flag, in words.</summary>
    private static string? Orientation(object? value)
    {
        if (value is not ushort flag) return null;

        return flag switch
        {
            1 => "Normal",
            2 => "Mirrored",
            3 => "Upside down",
            4 => "Mirrored, upside down",
            5 => "Mirrored, turned left",
            6 => "Turned right",
            7 => "Mirrored, turned right",
            8 => "Turned left",
            _ => null
        };
    }

    private static string DescribeSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.##} MB"
    };
}
