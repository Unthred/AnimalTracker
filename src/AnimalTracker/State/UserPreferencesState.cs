using AnimalTracker.Data.Entities;

namespace AnimalTracker.State;

public sealed class UserPreferencesState
{
    public event Action? Changed;

    public string AccentColorHex { get; private set; } = "#0f172a";
    public bool CompactMode { get; private set; }
    public int TimelinePageSize { get; private set; } = 50;

    public void Apply(UserSettings settings)
    {
        AccentColorHex = settings.AccentColorHex;
        CompactMode = settings.CompactMode;
        TimelinePageSize = settings.TimelinePageSize;
        Changed?.Invoke();
    }
}

