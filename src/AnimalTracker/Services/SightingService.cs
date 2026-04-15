using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed record SightingFilters(
    DateTime? FromUtc,
    DateTime? ToUtc,
    int? SpeciesId,
    int? LocationId,
    int? AnimalId,
    bool UnknownOnly);

public sealed class SightingService(ApplicationDbContext db, CurrentUserService currentUser, LocationService locations, PhotoStorageService photos)
{
    public async Task<List<Sighting>> GetRecentAsync(int take, SightingFilters filters, CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 200.");

        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        IQueryable<Sighting> q = db.Sightings
            .AsNoTracking()
            .Include(x => x.Species)
            .Include(x => x.Location)
            .Include(x => x.Animal)
            .Where(x => x.OwnerUserId == userId);

        if (filters.FromUtc is not null)
            q = q.Where(x => x.OccurredAtUtc >= filters.FromUtc);
        if (filters.ToUtc is not null)
            q = q.Where(x => x.OccurredAtUtc <= filters.ToUtc);
        if (filters.SpeciesId is not null)
            q = q.Where(x => x.SpeciesId == filters.SpeciesId);
        if (filters.LocationId is not null)
            q = q.Where(x => x.LocationId == filters.LocationId);
        if (filters.AnimalId is not null)
            q = q.Where(x => x.AnimalId == filters.AnimalId);
        if (filters.UnknownOnly)
            q = q.Where(x => x.AnimalId == null);

        return await q
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Sighting?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        return await db.Sightings
            .AsNoTracking()
            .Include(x => x.Species)
            .Include(x => x.Location)
            .Include(x => x.Animal).ThenInclude(a => a!.Species)
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, cancellationToken);
    }

    public async Task<Sighting> CreateAsync(
        DateTime occurredAtUtc,
        int speciesId,
        int? locationId,
        int? animalId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var resolvedLocationId = locationId ?? (await locations.GetOrCreateDefaultAsync(cancellationToken)).Id;

        var entity = new Sighting
        {
            OwnerUserId = userId,
            OccurredAtUtc = occurredAtUtc,
            SpeciesId = speciesId,
            LocationId = resolvedLocationId,
            AnimalId = animalId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Sightings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task LinkAnimalAsync(int sightingId, int? animalId, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        var entity = await db.Sightings.FirstOrDefaultAsync(
            x => x.Id == sightingId && x.OwnerUserId == userId,
            cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Sighting not found.");

        entity.AnimalId = animalId;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Sighting> UpdateAsync(
        int id,
        DateTime occurredAtUtc,
        int speciesId,
        int? locationId,
        int? animalId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        var entity = await db.Sightings.FirstOrDefaultAsync(
            x => x.Id == id && x.OwnerUserId == userId,
            cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Sighting not found.");

        // Validate / resolve location
        var resolvedLocationId = locationId ?? (await locations.GetOrCreateDefaultAsync(cancellationToken)).Id;
        var locationOk = await db.Locations.AsNoTracking()
            .AnyAsync(l => l.Id == resolvedLocationId && l.OwnerUserId == userId, cancellationToken);
        if (!locationOk)
            throw new InvalidOperationException("Location not found.");

        // Validate animal ownership if provided
        if (animalId is not null)
        {
            var animalOk = await db.Animals.AsNoTracking()
                .AnyAsync(a => a.Id == animalId && a.OwnerUserId == userId, cancellationToken);
            if (!animalOk)
                throw new InvalidOperationException("Animal not found.");
        }

        entity.OccurredAtUtc = occurredAtUtc;
        entity.SpeciesId = speciesId;
        entity.LocationId = resolvedLocationId;
        entity.AnimalId = animalId;
        entity.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<int?> GetLatestPhotoIdForAnimalAsync(int animalId, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        return await db.SightingPhotos
            .AsNoTracking()
            .Where(p => p.Sighting.OwnerUserId == userId && p.Sighting.AnimalId == animalId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var entity = await db.Sightings
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Sighting not found.");

        // Photos are stored on disk; deleting the DB row alone would orphan files.
        foreach (var p in entity.Photos)
            photos.TryDeleteStoredFile(p.StoredPath);

        db.Sightings.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePhotoAsync(int sightingPhotoId, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        var photo = await db.SightingPhotos
            .Include(p => p.Sighting)
            .FirstOrDefaultAsync(p => p.Id == sightingPhotoId, cancellationToken);

        if (photo is null || photo.Sighting.OwnerUserId != userId)
            throw new InvalidOperationException("Photo not found.");

        // Delete disk file first; even if it fails, still delete DB row to allow user to proceed.
        photos.TryDeleteStoredFile(photo.StoredPath);

        db.SightingPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
    }
}

