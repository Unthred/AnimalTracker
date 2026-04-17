using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class SightingPhoto
{
    public int Id { get; set; }

    public int SightingId { get; set; }
    public Sighting Sighting { get; set; } = null!;

    [Required, MaxLength(1024)]
    public string StoredPath { get; set; } = "";

    [Required, MaxLength(255)]
    public string OriginalFileName { get; set; } = "";

    [Required, MaxLength(200)]
    public string ContentType { get; set; } = "";

    public long SizeBytes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>SHA-256 of stored file bytes (lowercase hex), for duplicate detection.</summary>
    [MaxLength(64)]
    public string? ContentSha256Hex { get; set; }

    /// <summary>Original capture time from EXIF when available.</summary>
    public DateTime? OriginalCaptureUtc { get; set; }

    public double? OriginalLatitude { get; set; }
    public double? OriginalLongitude { get; set; }
}

