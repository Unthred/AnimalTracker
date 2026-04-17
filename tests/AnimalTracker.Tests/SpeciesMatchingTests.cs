using AnimalTracker.Data.Entities;
using AnimalTracker.Services;

namespace AnimalTracker.Tests;

public sealed class SpeciesMatchingTests
{
    [Fact]
    public void TryMatchSpeciesId_matches_name()
    {
        var species = new List<Species>
        {
            new() { Id = 1, Name = "Red Fox" },
            new() { Id = 2, Name = "Badger" }
        };

        var id = SpeciesMatching.TryMatchSpeciesId("red fox", species);
        Assert.Equal(1, id);
    }

    [Fact]
    public void GetBestRecognitionCandidate_prefers_detection()
    {
        var r = new RecognitionResponse
        {
            Detections =
            [
                new RecognitionDetection
                {
                    TopCandidates = [new RecognitionCandidate { Label = "Fox", Confidence = 0.9 }]
                }
            ],
            ImageLevelCandidates = [new RecognitionCandidate { Label = "Dog", Confidence = 0.5 }]
        };

        var (label, conf) = SpeciesMatching.GetBestRecognitionCandidate(r);
        Assert.Equal("Fox", label);
        Assert.Equal(0.9, conf);
    }
}
