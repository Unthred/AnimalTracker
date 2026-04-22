using AnimalTracker.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Rational = SixLabors.ImageSharp.Rational;

namespace AnimalTracker.Tests;

public sealed class ExifMetadataServiceTests
{
    [Fact]
    public async Task TryExtractAsync_empty_stream_returns_no_metadata()
    {
        var sut = new ExifMetadataService();
        await using var ms = new MemoryStream();
        var r = await sut.TryExtractAsync(ms);
        Assert.Null(r.CaptureUtc);
        Assert.Null(r.Latitude);
        Assert.Null(r.Longitude);
    }

    [Fact]
    public async Task TryExtractAsync_parses_capture_time_and_gps_from_valid_jpeg_exif()
    {
        var sut = new ExifMetadataService();
        await using var ms = new MemoryStream();

        using (var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(8, 8))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.DateTimeOriginal, "2024:04:17 14:30:00");
            exif.SetValue(ExifTag.GPSLatitudeRef, "N");
            exif.SetValue(ExifTag.GPSLongitudeRef, "W");
            exif.SetValue(ExifTag.GPSLatitude, [new Rational(51), new Rational(30), new Rational(0)]);
            exif.SetValue(ExifTag.GPSLongitude, [new Rational(1), new Rational(15), new Rational(0)]);
            image.Metadata.ExifProfile = exif;
            await image.SaveAsJpegAsync(ms);
        }

        ms.Position = 0;
        var r = await sut.TryExtractAsync(ms);

        Assert.NotNull(r.CaptureUtc);
        Assert.NotNull(r.Latitude);
        Assert.NotNull(r.Longitude);
        Assert.InRange(r.Latitude!.Value, 51.49, 51.51);
        Assert.InRange(r.Longitude!.Value, -1.26, -1.24);
    }
}
