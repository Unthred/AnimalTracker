using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class Location
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required, MaxLength(450)]
    public string OwnerUserId { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<Sighting> Sightings { get; set; } = [];
}

