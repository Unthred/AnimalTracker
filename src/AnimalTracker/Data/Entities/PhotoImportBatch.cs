using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class PhotoImportBatch
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string OwnerUserId { get; set; } = "";

    public PhotoImportBatchStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int CreatedSightings { get; set; }
    public int SkippedDuplicates { get; set; }
    public int NeedsReviewCount { get; set; }

    [MaxLength(4000)]
    public string? LastError { get; set; }

    public List<PhotoImportItem> Items { get; set; } = [];
}
