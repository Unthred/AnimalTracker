namespace AnimalTracker.Services;

public sealed class ImportWorkItem
{
    public required string OriginalFileName { get; init; }
    public required byte[] SourceBytes { get; init; }
    public required string SourceSha256Hex { get; init; }
    public required int SpeciesId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public DateTime? ExifCaptureUtc { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? RecognitionConfidence { get; init; }
    public string? RecognizedLabel { get; init; }
    public bool NeedsReview { get; init; }
}

public static class PhotoBurstClustering
{
    public static List<List<ImportWorkItem>> Cluster(
        IReadOnlyList<ImportWorkItem> items,
        int timeWindowSeconds,
        double distanceMeters)
    {
        var sorted = items.OrderBy(i => i.OccurredAtUtc).ToList();
        var clusters = new List<List<ImportWorkItem>>();
        foreach (var item in sorted)
        {
            var placed = false;
            foreach (var cluster in clusters)
            {
                var rep = cluster[0];
                if (rep.SpeciesId != item.SpeciesId)
                    continue;
                if (!WithinCluster(rep, item, timeWindowSeconds, distanceMeters))
                    continue;
                cluster.Add(item);
                placed = true;
                break;
            }

            if (!placed)
                clusters.Add([item]);
        }

        return clusters;
    }

    private static bool WithinCluster(ImportWorkItem a, ImportWorkItem b, int timeWindowSeconds, double distanceMeters)
    {
        var dt = Math.Abs((b.OccurredAtUtc - a.OccurredAtUtc).TotalSeconds);
        if (dt > timeWindowSeconds)
            return false;

        var la = a.Latitude;
        var loa = a.Longitude;
        var lb = b.Latitude;
        var lob = b.Longitude;

        if (la is null || loa is null || lb is null || lob is null)
            return true;

        return HaversineMeters(la.Value, loa.Value, lb.Value, lob.Value) <= distanceMeters;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusMeters = 6371000;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(DegreesToRadians(lat1)) *
                Math.Cos(DegreesToRadians(lat2)) *
                Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double value) => value * (Math.PI / 180);
}
