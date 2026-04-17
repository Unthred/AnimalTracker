using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class AppSettings
{
    public int Id { get; set; }

    [Required, MaxLength(16)]
    public string DefaultThemeMode { get; set; } = "system";

    [MaxLength(512)]
    public string? DefaultAuthImageRelativePath { get; set; }

    [MaxLength(100)]
    public string? ActiveSpeciesRegionKey { get; set; }

    [MaxLength(200)]
    public string? ActiveSpeciesRegionName { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
