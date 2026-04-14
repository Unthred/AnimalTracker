using System.Text.RegularExpressions;
using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed class UserSettingsService(ApplicationDbContext db, CurrentUserService currentUser)
{
    private static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public async Task<UserSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        var existing = await db.UserSettings.FirstOrDefaultAsync(x => x.OwnerUserId == userId, cancellationToken);
        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var created = new UserSettings
        {
            OwnerUserId = userId,
            AccentColorHex = "#0f172a",
            CompactMode = false,
            TimelinePageSize = 50,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.UserSettings.Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task<UserSettings> UpdateAsync(
        string accentColorHex,
        bool compactMode,
        int timelinePageSize,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);

        accentColorHex = (accentColorHex ?? "").Trim();
        if (!Hex.IsMatch(accentColorHex))
            throw new ArgumentException("Accent color must be a hex value like #22c55e.", nameof(accentColorHex));

        if (timelinePageSize is not (25 or 50 or 100))
            throw new ArgumentOutOfRangeException(nameof(timelinePageSize), "Timeline page size must be 25, 50, or 100.");

        settings.AccentColorHex = accentColorHex;
        settings.CompactMode = compactMode;
        settings.TimelinePageSize = timelinePageSize;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }
}

