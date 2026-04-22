using AnimalTracker.Services;

namespace AnimalTracker.Tests;

public sealed class PhotoBurstClusteringTests
{
    [Fact]
    public void Cluster_merges_same_species_within_time_and_distance()
    {
        var t0 = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddSeconds(30);

        var items = new List<ImportWorkItem>
        {
            CreateItem("a.jpg", 1, t0, 51.0, -1.0),
            CreateItem("b.jpg", 1, t1, 51.0001, -1.0001),
        };

        var clusters = PhotoBurstClustering.Cluster(items, timeWindowSeconds: 120, distanceMeters: 200);
        Assert.Single(clusters);
        Assert.Equal(2, clusters[0].Count);
    }

    [Fact]
    public void Cluster_splits_when_species_differs()
    {
        var t = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var items = new List<ImportWorkItem>
        {
            CreateItem("a.jpg", 1, t, 51, -1),
            CreateItem("b.jpg", 2, t, 51, -1),
        };

        var clusters = PhotoBurstClustering.Cluster(items, 120, 200);
        Assert.Equal(2, clusters.Count);
    }

    [Fact]
    public void Cluster_time_only_when_gps_missing()
    {
        var t0 = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddSeconds(60);
        var items = new List<ImportWorkItem>
        {
            CreateItem("a.jpg", 1, t0, null, null),
            CreateItem("b.jpg", 1, t1, null, null),
        };

        var clusters = PhotoBurstClustering.Cluster(items, 120, 75);
        Assert.Single(clusters);
    }

    [Fact]
    public void Cluster_splits_when_time_exceeds_window_with_gps()
    {
        var t0 = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddSeconds(500);
        var items = new List<ImportWorkItem>
        {
            CreateItem("a.jpg", 1, t0, 51.0, -1.0),
            CreateItem("b.jpg", 1, t1, 51.0001, -1.0001),
        };

        var clusters = PhotoBurstClustering.Cluster(items, timeWindowSeconds: 120, distanceMeters: 200);
        Assert.Equal(2, clusters.Count);
    }

    [Fact]
    public void Cluster_splits_when_distance_exceeds_threshold()
    {
        var t = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var items = new List<ImportWorkItem>
        {
            CreateItem("a.jpg", 1, t, 51.0, -1.0),
            CreateItem("b.jpg", 1, t, 52.0, -1.0),
        };

        var clusters = PhotoBurstClustering.Cluster(items, timeWindowSeconds: 120, distanceMeters: 200);
        Assert.Equal(2, clusters.Count);
    }

    [Fact]
    public void Cluster_orders_items_by_occurred_time()
    {
        var tEarly = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var tLate = tEarly.AddSeconds(30);
        var items = new List<ImportWorkItem>
        {
            CreateItem("late.jpg", 1, tLate, null, null),
            CreateItem("early.jpg", 1, tEarly, null, null),
        };

        var clusters = PhotoBurstClustering.Cluster(items, 120, 200);
        Assert.Single(clusters);
        Assert.Equal("early.jpg", clusters[0][0].OriginalFileName);
        Assert.Equal("late.jpg", clusters[0][1].OriginalFileName);
    }

    private static ImportWorkItem CreateItem(
        string name,
        int speciesId,
        DateTime utc,
        double? lat,
        double? lng) =>
        new()
        {
            OriginalFileName = name,
            SourceBytes = [],
            SourceSha256Hex = "x",
            SpeciesId = speciesId,
            OccurredAtUtc = utc,
            ExifCaptureUtc = utc,
            Latitude = lat,
            Longitude = lng,
            RecognitionConfidence = 0.9,
            RecognizedLabel = "Test",
            NeedsReview = false
        };
}
