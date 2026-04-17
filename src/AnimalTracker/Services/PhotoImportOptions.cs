namespace AnimalTracker.Services;

public sealed class PhotoImportOptions
{
    public const string SectionName = "PhotoImport";

    public int DedupeTimeWindowSeconds { get; set; } = 120;

    public double DedupeDistanceMeters { get; set; } = 75;

    public int ImportChunkSize { get; set; } = 100;
}
