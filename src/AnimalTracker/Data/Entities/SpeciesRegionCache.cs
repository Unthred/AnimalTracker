namespace AnimalTracker.Data.Entities;

public sealed class SpeciesRegionCache
{
    public int Id { get; set; }
    public string RegionKey { get; set; } = "";
    public string RegionName { get; set; } = "";
    public int SpeciesId { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public Species Species { get; set; } = default!;
}
