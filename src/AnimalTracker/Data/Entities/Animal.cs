using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class Animal
{
    public int Id { get; set; }

    public int SpeciesId { get; set; }
    public Species Species { get; set; } = null!;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(2000)]
    public string? IdentifyingFeatures { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public AnimalStatus Status { get; set; } = AnimalStatus.Active;

    [Required, MaxLength(450)]
    public string OwnerUserId { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<Sighting> Sightings { get; set; } = [];
}

