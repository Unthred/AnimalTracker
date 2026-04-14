using AnimalTracker.Data.Entities;

namespace AnimalTracker.State;

public sealed class UserPreferencesState
{
    public event Action? Changed;

    public string AccentColorHex { get; private set; } = "#0f172a";
    public bool CompactMode { get; private set; }
    public int TimelinePageSize { get; private set; } = 50;

    /// <summary>Relative path under content root, when set.</summary>
    public string? BackgroundImageRelativePath { get; private set; }

    /// <summary>Bumps when preferences change so background URL cache-busts.</summary>
    public int UiStamp { get; private set; }

    public void Apply(UserSettings settings)
    {
        AccentColorHex = settings.AccentColorHex;
        CompactMode = settings.CompactMode;
        TimelinePageSize = settings.TimelinePageSize;
        BackgroundImageRelativePath = settings.BackgroundImageRelativePath;
        UiStamp++;
        Changed?.Invoke();
    }
}

