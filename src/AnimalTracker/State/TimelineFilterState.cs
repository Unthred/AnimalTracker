namespace AnimalTracker.State;

public sealed class TimelineFilterState
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int? SpeciesId { get; set; }
    public int? LocationId { get; set; }
    public int? AnimalId { get; set; }
    public bool UnknownOnly { get; set; }

    public int? LastUsedSpeciesId { get; set; }
    public int? LastUsedLocationId { get; set; }
}

