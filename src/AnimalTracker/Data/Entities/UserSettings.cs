using System.ComponentModel.DataAnnotations;

namespace AnimalTracker.Data.Entities;

public sealed class UserSettings
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string OwnerUserId { get; set; } = "";

    /// <summary>
    /// Accent/highlight color as a hex string like "#22c55e".
    /// </summary>
    [Required, MaxLength(16)]
    public string AccentColorHex { get; set; } = "#0f172a"; // slate-900

    public bool CompactMode { get; set; }

    /// <summary>
    /// Timeline page size (e.g. 25/50/100).
    /// </summary>
    public int TimelinePageSize { get; set; } = 50;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

