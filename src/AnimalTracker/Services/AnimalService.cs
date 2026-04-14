using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed class AnimalService(ApplicationDbContext db, CurrentUserService currentUser)
{
    public async Task<List<Animal>> SearchAsync(string? query, int? speciesId, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

        IQueryable<Animal> q = db.Animals.AsNoTracking().Include(x => x.Species)
            .Where(x => x.OwnerUserId == userId);

        if (speciesId is not null)
            q = q.Where(x => x.SpeciesId == speciesId);

        if (query is not null)
            q = q.Where(x =>
                (x.DisplayName != null && EF.Functions.Like(x.DisplayName, $"%{query}%")) ||
                (x.IdentifyingFeatures != null && EF.Functions.Like(x.IdentifyingFeatures, $"%{query}%")));

        return await q
            .OrderBy(x => x.Species.Name)
            .ThenBy(x => x.DisplayName ?? "")
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Animal?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        return await db.Animals
            .AsNoTracking()
            .Include(x => x.Species)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, cancellationToken);
    }

    public async Task<Animal> CreateAsync(
        int speciesId,
        string? displayName,
        string? identifyingFeatures,
        string? notes,
        AnimalStatus status = AnimalStatus.Active,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var entity = new Animal
        {
            OwnerUserId = userId,
            SpeciesId = speciesId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            IdentifyingFeatures = string.IsNullOrWhiteSpace(identifyingFeatures) ? null : identifyingFeatures.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = status,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Animals.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var entity = await db.Animals.FirstOrDefaultAsync(
            x => x.Id == id && x.OwnerUserId == userId,
            cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Animal not found.");

        await db.Sightings
            .Where(s => s.OwnerUserId == userId && s.AnimalId == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.AnimalId, (int?)null).SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow),
                cancellationToken);

        db.Animals.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

