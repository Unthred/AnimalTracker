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
}

