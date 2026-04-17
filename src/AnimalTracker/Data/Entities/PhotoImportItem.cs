using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class PhotoImportItem
{
    public int Id { get; set; }

    public int BatchId { get; set; }
    public PhotoImportBatch Batch { get; set; } = null!;

    [Required, MaxLength(512)]
    public string OriginalFileName { get; set; } = "";

    [MaxLength(64)]
    public string? ContentSha256Hex { get; set; }

    public PhotoImportItemStatus Status { get; set; }

    public int? SpeciesId { get; set; }

    public double? CandidateConfidence { get; set; }

    [MaxLength(300)]
    public string? RecognizedLabel { get; set; }

    [MaxLength(64)]
    public string? ClusterId { get; set; }

    public int? SightingId { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }
}
