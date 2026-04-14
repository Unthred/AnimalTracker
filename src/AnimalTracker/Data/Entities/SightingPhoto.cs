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
}

