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

    public bool EmailEnabled { get; set; }

    [MaxLength(256)]
    public string? EmailHost { get; set; }

    public int? EmailPort { get; set; }

    [MaxLength(1024)]
    public string? EmailUserNameProtected { get; set; }

    [MaxLength(4096)]
    public string? EmailPasswordProtected { get; set; }

    [MaxLength(256)]
    public string? EmailFromEmail { get; set; }

    [MaxLength(200)]
    public string? EmailFromName { get; set; }

    public bool EmailEnableSsl { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
