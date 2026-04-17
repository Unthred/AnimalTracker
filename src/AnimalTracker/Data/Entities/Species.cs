using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class Species
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(200)]
    public string? ScientificName { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    [MaxLength(200)]
    public string? ImageLicense { get; set; }

    [MaxLength(500)]
    public string? ImageAttribution { get; set; }

    [MaxLength(100)]
    public string? CatalogSource { get; set; }

    [MaxLength(100)]
    public string? CatalogSourceId { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public List<Animal> Animals { get; set; } = [];
    public List<Sighting> Sightings { get; set; } = [];
}

