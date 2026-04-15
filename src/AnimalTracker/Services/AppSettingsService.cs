using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed class AppSettingsService(ApplicationDbContext db, PhotoStorageService photos)
{
    public async Task<AppSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var existing = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var created = new AppSettings
        {
            DefaultThemeMode = "system",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.AppSettings.Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task<AppSettings> UpdateThemeModeAsync(string defaultThemeMode, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeThemeMode(defaultThemeMode);
        var settings = await GetOrCreateAsync(cancellationToken);
        settings.DefaultThemeMode = normalized;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<AppSettings> SetDefaultAuthImageAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        var stored = await photos.SaveAuthPageImageAsync(file, cancellationToken: cancellationToken);
        var settings = await GetOrCreateAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.DefaultAuthImageRelativePath))
            photos.TryDeleteStoredFile(settings.DefaultAuthImageRelativePath);

        settings.DefaultAuthImageRelativePath = stored.StoredRelativePath;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<AppSettings> ClearDefaultAuthImageAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        photos.TryDeleteStoredFile(settings.DefaultAuthImageRelativePath);
        settings.DefaultAuthImageRelativePath = null;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static string NormalizeThemeMode(string mode)
    {
        mode = (mode ?? "").Trim().ToLowerInvariant();
        return mode is "system" or "light" or "dark"
            ? mode
            : throw new ArgumentOutOfRangeException(nameof(mode), "Theme mode must be system, light, or dark.");
    }
}
