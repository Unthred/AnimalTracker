using AnimalTracker.Data.Entities;

namespace AnimalTracker.Services;

public static class SpeciesMatching
{
    public static int? TryMatchSpeciesId(string? label, IReadOnlyList<Species> species)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var trimmed = label.Trim();

        foreach (var s in species)
        {
            if (string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                return s.Id;
            if (!string.IsNullOrWhiteSpace(s.ScientificName) &&
                string.Equals(s.ScientificName, trimmed, StringComparison.OrdinalIgnoreCase))
                return s.Id;
        }

        foreach (var s in species)
        {
            if (trimmed.Contains(s.Name, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                return s.Id;
        }

        return null;
    }

    public static (string? Label, double Confidence) GetBestRecognitionCandidate(RecognitionResponse? response)
    {
        if (response is null)
            return (null, 0);

        foreach (var d in response.Detections)
        {
            var top = d.TopCandidates.OrderByDescending(x => x.Confidence).FirstOrDefault();
            if (top is not null)
                return (top.Label, top.Confidence);
        }

        var img = response.ImageLevelCandidates.OrderByDescending(x => x.Confidence).FirstOrDefault();
        return img is null ? (null, 0) : (img.Label, img.Confidence);
    }
}
