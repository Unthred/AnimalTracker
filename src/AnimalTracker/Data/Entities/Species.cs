using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class Species
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(2000)]
    public string? Description { get; set; }

    public List<Animal> Animals { get; set; } = [];
    public List<Sighting> Sightings { get; set; } = [];
}

