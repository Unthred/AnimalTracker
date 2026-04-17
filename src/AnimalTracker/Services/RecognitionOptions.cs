namespace AnimalTracker.Services;

public sealed class RecognitionOptions
{
    public const string SectionName = "Recognition";

    public string BaseUrl { get; set; } = "";

    public string? ApiKey { get; set; }

    public double AutoAcceptThreshold { get; set; } = 0.85;

    public double ReviewThreshold { get; set; } = 0.55;

    public int TimeoutSeconds { get; set; } = 20;

    public int MaxRetries { get; set; } = 2;
}
