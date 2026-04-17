using System.Text.Json.Serialization;

namespace AnimalTracker.Services;

public sealed class RecognitionBoundingBox
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}

public sealed class RecognitionCandidate
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public sealed class RecognitionDetection
{
    [JsonPropertyName("bbox")]
    public RecognitionBoundingBox? Bbox { get; set; }

    [JsonPropertyName("topCandidates")]
    public List<RecognitionCandidate> TopCandidates { get; set; } = [];
}

public sealed class RecognitionResponse
{
    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; set; }

    [JsonPropertyName("processingMs")]
    public int ProcessingMs { get; set; }

    [JsonPropertyName("detections")]
    public List<RecognitionDetection> Detections { get; set; } = [];

    [JsonPropertyName("imageLevelCandidates")]
    public List<RecognitionCandidate> ImageLevelCandidates { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}
