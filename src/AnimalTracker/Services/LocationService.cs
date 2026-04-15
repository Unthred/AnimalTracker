using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed class LocationService(ApplicationDbContext db, CurrentUserService currentUser)
{
    public async Task<List<Location>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        return await db.Locations
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Location> GetOrCreateDefaultAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        var existing = await db.Locations
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.OwnerUserId == userId, cancellationToken);

        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var home = new Location
        {
            OwnerUserId = userId,
            Name = "Home",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Locations.Add(home);
        await db.SaveChangesAsync(cancellationToken);
        return home;
    }

    public async Task<Location> CreateAsync(string name, string? notes, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 200)
            throw new ArgumentException("Location name is required (max 200 chars).", nameof(name));

        var now = DateTime.UtcNow;
        var entity = new Location
        {
            OwnerUserId = userId,
            Name = name,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Locations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var owned = await db.Locations
            .Where(x => x.OwnerUserId == userId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (owned.Count <= 1)
            // Enforces an invariant used throughout the app: every user always has at least one location.
            throw new InvalidOperationException("You need at least one location.");

        var remove = owned.FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException("Location not found.");

        var replacement = owned.First(x => x.Id != id);

        // Preserve historical sightings by re-homing them instead of deleting.
        await db.Sightings
            .Where(s => s.OwnerUserId == userId && s.LocationId == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LocationId, replacement.Id).SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow),
                cancellationToken);

        db.Locations.Remove(remove);
        await db.SaveChangesAsync(cancellationToken);
    }
}

