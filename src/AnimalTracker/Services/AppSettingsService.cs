using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed class AppSettingsService(ApplicationDbContext db, PhotoStorageService photos)
{
    public async Task<AppSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var existing = await db.AppSettings
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            // Defensive cleanup: local/dev race conditions previously allowed
            // multiple rows. Keep the most recently updated row as canonical.
            var duplicateRows = await db.AppSettings
                .Where(x => x.Id != existing.Id)
                .ToListAsync(cancellationToken);
            if (duplicateRows.Count > 0)
            {
                db.AppSettings.RemoveRange(duplicateRows);
                await db.SaveChangesAsync(cancellationToken);
            }

            return existing;
        }

        var now = DateTime.UtcNow;
        var created = new AppSettings
        {
            DefaultThemeMode = "system",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.AppSettings.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            // Another request created settings concurrently; read canonical row.
            db.Entry(created).State = EntityState.Detached;
            var canonical = await db.AppSettings
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (canonical is null)
                throw;
            return canonical;
        }
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
