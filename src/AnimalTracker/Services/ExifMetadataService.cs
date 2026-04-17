using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Rational = SixLabors.ImageSharp.Rational;

namespace AnimalTracker.Services;

public sealed record ExifExtractResult(
    DateTime? CaptureUtc,
    double? Latitude,
    double? Longitude);

public sealed class ExifMetadataService
{
    public async Task<ExifExtractResult> TryExtractAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        if (imageStream.CanSeek)
            imageStream.Position = 0;

        try
        {
            var info = await Image.IdentifyAsync(imageStream, cancellationToken);
            var exif = info?.Metadata?.ExifProfile;
            if (exif is null)
                return new ExifExtractResult(null, null, null);

            DateTime? captureUtc = TryParseCaptureUtc(exif);

            double? lat = null;
            double? lng = null;
            if (exif.TryGetValue(ExifTag.GPSLatitudeRef, out IExifValue<string>? latRefValue)
                && exif.TryGetValue(ExifTag.GPSLongitudeRef, out IExifValue<string>? lonRefValue)
                && exif.TryGetValue(ExifTag.GPSLatitude, out IExifValue<Rational[]>? latValue)
                && exif.TryGetValue(ExifTag.GPSLongitude, out IExifValue<Rational[]>? lonValue))
            {
                if (TryParseGpsCoordinate(latValue.Value, latRefValue.Value, out var la)
                    && TryParseGpsCoordinate(lonValue.Value, lonRefValue.Value, out var lo))
                {
                    lat = la;
                    lng = lo;
                }
            }

            return new ExifExtractResult(captureUtc, lat, lng);
        }
        catch
        {
            return new ExifExtractResult(null, null, null);
        }
    }

    private static DateTime? TryParseCaptureUtc(ExifProfile exif)
    {
        if (exif.TryGetValue(ExifTag.DateTimeOriginal, out IExifValue<string>? original) && original?.Value is { Length: > 0 } s1)
        {
            if (TryParseExifDateTime(s1, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        }

        if (exif.TryGetValue(ExifTag.DateTimeDigitized, out IExifValue<string>? digitized) && digitized?.Value is { Length: > 0 } s2)
        {
            if (TryParseExifDateTime(s2, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        }

        if (exif.TryGetValue(ExifTag.DateTime, out IExifValue<string>? dtTag) && dtTag?.Value is { Length: > 0 } s3)
        {
            if (TryParseExifDateTime(s3, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        }

        return null;
    }

    private static bool TryParseExifDateTime(string raw, out DateTime local)
    {
        local = default;
        raw = raw.Trim();
        // "2024:04:17 14:30:00"
        if (raw.Length >= 19 && raw[4] == ':' && raw[7] == ':')
        {
            var y = int.Parse(raw.AsSpan(0, 4));
            var mo = int.Parse(raw.AsSpan(5, 2));
            var d = int.Parse(raw.AsSpan(8, 2));
            var h = int.Parse(raw.AsSpan(11, 2));
            var mi = int.Parse(raw.AsSpan(14, 2));
            var sec = int.Parse(raw.AsSpan(17, 2));
            local = new DateTime(y, mo, d, h, mi, sec, DateTimeKind.Local);
            return true;
        }

        return DateTime.TryParse(raw, out local);
    }

    private static bool TryParseGpsCoordinate(object? rawValue, string? direction, out double coordinate)
    {
        coordinate = 0;
        if (!TryReadDms(rawValue, out var degrees, out var minutes, out var seconds))
            return false;

        coordinate = Math.Abs(degrees) + minutes / 60d + seconds / 3600d;
        if (string.Equals(direction, "S", StringComparison.OrdinalIgnoreCase)
            || string.Equals(direction, "W", StringComparison.OrdinalIgnoreCase))
            coordinate = -coordinate;

        return true;
    }

    private static bool TryReadDms(object? rawValue, out double degrees, out double minutes, out double seconds)
    {
        degrees = 0;
        minutes = 0;
        seconds = 0;

        if (rawValue is not System.Collections.IEnumerable values)
            return false;

        var parts = new List<double>(3);
        foreach (var value in values)
        {
            if (!TryConvertExifPartToDouble(value, out var numeric))
                continue;
            parts.Add(numeric);
            if (parts.Count == 3)
                break;
        }

        if (parts.Count < 3)
            return false;

        degrees = parts[0];
        minutes = parts[1];
        seconds = parts[2];
        return true;
    }

    private static bool TryConvertExifPartToDouble(object? value, out double numeric)
    {
        numeric = 0;
        if (value is null)
            return false;

        if (value is Rational rational)
        {
            numeric = rational.Denominator == 0 ? 0 : (double)rational.Numerator / rational.Denominator;
            return true;
        }

        try
        {
            numeric = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
