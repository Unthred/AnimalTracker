namespace AnimalTracker.Services;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromEmail { get; set; } = "";

    public string FromName { get; set; } = "AnimalTracker";

    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(FromEmail);
}
