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
    public void TryMatchSpeciesId_matches_scientific_name()
    {
        var species = new List<Species>
        {
            new() { Id = 1, Name = "Red Fox", ScientificName = "Vulpes vulpes" },
            new() { Id = 2, Name = "Badger" }
        };

        var id = SpeciesMatching.TryMatchSpeciesId("VULPES VULPES", species);
        Assert.Equal(1, id);
    }

    [Fact]
    public void TryMatchSpeciesId_substring_match_is_case_insensitive()
    {
        var species = new List<Species>
        {
            new() { Id = 1, Name = "Fox" },
            new() { Id = 2, Name = "Badger" }
        };

        var id = SpeciesMatching.TryMatchSpeciesId("A wild FOX appeared", species);
        Assert.Equal(1, id);
    }

    [Fact]
    public void TryMatchSpeciesId_returns_null_for_unknown_label()
    {
        var species = new List<Species> { new() { Id = 1, Name = "Fox" } };
        Assert.Null(SpeciesMatching.TryMatchSpeciesId("Mystery", species));
    }

    [Fact]
    public void TryMatchSpeciesId_returns_null_for_whitespace_label()
    {
        var species = new List<Species> { new() { Id = 1, Name = "Fox" } };
        Assert.Null(SpeciesMatching.TryMatchSpeciesId("   ", species));
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

    [Fact]
    public void GetBestRecognitionCandidate_falls_back_to_image_level()
    {
        var r = new RecognitionResponse
        {
            Detections = [],
            ImageLevelCandidates = [new RecognitionCandidate { Label = "Dog", Confidence = 0.42 }]
        };

        var (label, conf) = SpeciesMatching.GetBestRecognitionCandidate(r);
        Assert.Equal("Dog", label);
        Assert.Equal(0.42, conf);
    }

    [Fact]
    public void GetBestRecognitionCandidate_returns_empty_when_response_null()
    {
        var (label, conf) = SpeciesMatching.GetBestRecognitionCandidate(null);
        Assert.Null(label);
        Assert.Equal(0, conf);
    }

    [Fact]
    public void GetBestRecognitionCandidate_picks_highest_confidence_in_detection()
    {
        var r = new RecognitionResponse
        {
            Detections =
            [
                new RecognitionDetection
                {
                    TopCandidates =
                    [
                        new RecognitionCandidate { Label = "Low", Confidence = 0.1 },
                        new RecognitionCandidate { Label = "High", Confidence = 0.88 }
                    ]
                }
            ]
        };

        var (label, conf) = SpeciesMatching.GetBestRecognitionCandidate(r);
        Assert.Equal("High", label);
        Assert.Equal(0.88, conf);
    }
}
