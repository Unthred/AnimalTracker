using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class Sighting
{
    public int Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int SpeciesId { get; set; }
    public Species Species { get; set; } = null!;

    public int? AnimalId { get; set; }
    public Animal? Animal { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    [Required, MaxLength(450)]
    public string OwnerUserId { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<SightingPhoto> Photos { get; set; } = [];
}

