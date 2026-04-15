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

    /// <summary>"system", "light", or "dark"</summary>
    public string ThemeMode { get; private set; } = "system";
    public int SurfaceOpacityPercent { get; private set; } = 93;
    public int DarkSurfaceOpacityPercent { get; private set; } = 50;

    /// <summary>Bumps when preferences change so background URL cache-busts.</summary>
    public int UiStamp { get; private set; }

    private Guid? temporaryBackgroundToken;
    public string? TemporaryBackgroundUrl { get; private set; }

    public void Apply(UserSettings settings)
    {
        AccentColorHex = settings.AccentColorHex;
        CompactMode = settings.CompactMode;
        TimelinePageSize = settings.TimelinePageSize;
        BackgroundImageRelativePath = settings.BackgroundImageRelativePath;
        ThemeMode = string.IsNullOrWhiteSpace(settings.ThemeMode) ? "system" : settings.ThemeMode;
        SurfaceOpacityPercent = settings.SurfaceOpacityPercent is < 35 or > 100 ? 93 : settings.SurfaceOpacityPercent;
        DarkSurfaceOpacityPercent = settings.DarkSurfaceOpacityPercent is < 35 or > 100 ? 50 : settings.DarkSurfaceOpacityPercent;
        UiStamp++;
        Changed?.Invoke();
    }

    /// <summary>
    /// Applies theme mode immediately in-memory (used for optimistic UI sync before persistence completes).
    /// </summary>
    public void SetThemeMode(string? themeMode)
    {
        var normalized = (themeMode ?? "").Trim().ToLowerInvariant();
        ThemeMode = normalized is "light" or "dark" ? normalized : "system";
        Changed?.Invoke();
    }

    /// <summary>
    /// Temporarily overrides the app background (e.g. per-page hero wallpaper). Dispose to revert.
    /// </summary>
    public IDisposable UseTemporaryBackground(string url)
    {
        // Token ensures "last writer wins" and that a stale Dispose() can't clear a newer override.
        var token = Guid.NewGuid();
        temporaryBackgroundToken = token;
        TemporaryBackgroundUrl = url;
        Changed?.Invoke();
        return new TemporaryBackgroundScope(this, token);
    }

    private void ClearTemporaryBackground(Guid token)
    {
        if (temporaryBackgroundToken != token)
            return;
        temporaryBackgroundToken = null;
        TemporaryBackgroundUrl = null;
        Changed?.Invoke();
    }

    private sealed class TemporaryBackgroundScope(UserPreferencesState prefs, Guid token) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            prefs.ClearTemporaryBackground(token);
        }
    }
}

