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
    bool UnknownOnly,
    bool WithCoordinatesOnly = false,
    double? MaxLocationAccuracyMeters = null);

public sealed record SightingMapPoint(
    int SightingId,
    DateTime OccurredAtUtc,
    string SpeciesName,
    string? AnimalDisplayName,
    string LocationName,
    double Latitude,
    double Longitude,
    double? LocationAccuracyMeters,
    SightingBehavior? Behavior,
    int? PrimaryPhotoId);

public sealed record AnimalTerritorySummary(
    int AnimalId,
    string AnimalLabel,
    string SpeciesName,
    int CoordinateSightings,
    double? CoreLatitude,
    double? CoreLongitude,
    double RadiusMeters,
    string BestActivityWindowLocal,
    int HuntingSightings);

public sealed record AnimalTerritoryInsights(
    int AnimalId,
    string AnimalLabel,
    string SpeciesName,
    int TotalSightings,
    int CoordinateSightings,
    double? CoreLatitude,
    double? CoreLongitude,
    double RadiusMeters,
    string BestActivityWindowLocal,
    int HuntingSightings,
    IReadOnlyList<SightingMapPoint> RecentPoints);

public sealed class SightingService(ApplicationDbContext db, ICurrentUserAccessor currentUser, LocationService locations, PhotoStorageService photos)
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
        q = ApplyFilters(q, filters);

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
        DateTime? observedUntilUtc = null,
        double? latitude = null,
        double? longitude = null,
        double? locationAccuracyMeters = null,
        SightingBehavior? behavior = null,
        SightingConfidence? speciesConfidence = null,
        SightingConfidence? individualConfidence = null,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        ValidateCoordinates(latitude, longitude);
        ValidateObservedWindow(occurredAtUtc, observedUntilUtc);

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
            ObservedUntilUtc = observedUntilUtc,
            Latitude = latitude,
            Longitude = longitude,
            LocationAccuracyMeters = NormalizeOptionalPositive(locationAccuracyMeters),
            Behavior = behavior,
            SpeciesConfidence = speciesConfidence,
            IndividualConfidence = individualConfidence,
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
        DateTime? observedUntilUtc = null,
        double? latitude = null,
        double? longitude = null,
        double? locationAccuracyMeters = null,
        SightingBehavior? behavior = null,
        SightingConfidence? speciesConfidence = null,
        SightingConfidence? individualConfidence = null,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        ValidateCoordinates(latitude, longitude);
        ValidateObservedWindow(occurredAtUtc, observedUntilUtc);

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
        entity.ObservedUntilUtc = observedUntilUtc;
        entity.Latitude = latitude;
        entity.Longitude = longitude;
        entity.LocationAccuracyMeters = NormalizeOptionalPositive(locationAccuracyMeters);
        entity.Behavior = behavior;
        entity.SpeciesConfidence = speciesConfidence;
        entity.IndividualConfidence = individualConfidence;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<List<SightingMapPoint>> GetMapPointsAsync(
        int take,
        SightingFilters filters,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 5000.");

        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var query = db.Sightings
            .AsNoTracking()
            .Include(x => x.Species)
            .Include(x => x.Location)
            .Include(x => x.Animal)
            .Where(x => x.OwnerUserId == userId && x.Latitude != null && x.Longitude != null);

        query = ApplyFilters(query, filters);

        return await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new SightingMapPoint(
                x.Id,
                x.OccurredAtUtc,
                x.Species.Name,
                x.Animal != null && !string.IsNullOrWhiteSpace(x.Animal.DisplayName) ? x.Animal.DisplayName : null,
                x.Location.Name,
                x.Latitude!.Value,
                x.Longitude!.Value,
                x.LocationAccuracyMeters,
                x.Behavior,
                x.Photos.OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AnimalTerritorySummary>> GetTerritorySummariesAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 12,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 200.");

        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var query = db.Sightings
            .AsNoTracking()
            .Include(x => x.Animal).ThenInclude(a => a!.Species)
            .Where(x => x.OwnerUserId == userId && x.AnimalId != null && x.Latitude != null && x.Longitude != null);

        if (fromUtc is not null)
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        if (toUtc is not null)
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);

        var sightings = await query.ToListAsync(cancellationToken);
        var grouped = sightings
            .GroupBy(x => x.AnimalId!.Value)
            .Select(g =>
            {
                var points = g.Where(x => x.Latitude != null && x.Longitude != null).ToList();
                var coreLat = points.Average(x => x.Latitude!.Value);
                var coreLng = points.Average(x => x.Longitude!.Value);
                var radiusMeters = points
                    .Select(x => HaversineMeters(coreLat, coreLng, x.Latitude!.Value, x.Longitude!.Value))
                    .DefaultIfEmpty(0)
                    .Average();

                var bestWindow = points
                    .GroupBy(x => (x.OccurredAtUtc.ToLocalTime().Hour / 3) * 3)
                    .OrderByDescending(x => x.Count())
                    .FirstOrDefault();

                var bestWindowLabel = bestWindow is null
                    ? "No activity window yet"
                    : $"{bestWindow.Key:D2}:00-{((bestWindow.Key + 3) % 24):D2}:00";

                var any = g.First();
                var animal = any.Animal!;
                var label = !string.IsNullOrWhiteSpace(animal.DisplayName)
                    ? animal.DisplayName!
                    : $"Animal #{animal.Id}";

                return new AnimalTerritorySummary(
                    AnimalId: g.Key,
                    AnimalLabel: label,
                    SpeciesName: animal.Species.Name,
                    CoordinateSightings: points.Count,
                    CoreLatitude: coreLat,
                    CoreLongitude: coreLng,
                    RadiusMeters: radiusMeters,
                    BestActivityWindowLocal: bestWindowLabel,
                    HuntingSightings: g.Count(x => x.Behavior == SightingBehavior.Hunting));
            })
            .OrderByDescending(x => x.CoordinateSightings)
            .ThenBy(x => x.AnimalLabel)
            .Take(take)
            .ToList();

        return grouped;
    }

    public async Task<AnimalTerritoryInsights?> GetTerritoryInsightsForAnimalAsync(
        int animalId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var query = db.Sightings
            .AsNoTracking()
            .Include(x => x.Animal).ThenInclude(a => a!.Species)
            .Include(x => x.Species)
            .Include(x => x.Location)
            .Include(x => x.Photos)
            .Where(x => x.OwnerUserId == userId && x.AnimalId == animalId);

        if (fromUtc is not null)
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        if (toUtc is not null)
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);

        var sightings = await query.OrderByDescending(x => x.OccurredAtUtc).ToListAsync(cancellationToken);
        if (sightings.Count == 0)
            return null;

        var any = sightings[0];
        var animal = any.Animal;
        var animalLabel = animal is not null && !string.IsNullOrWhiteSpace(animal.DisplayName)
            ? animal.DisplayName!
            : $"Animal #{animalId}";
        var speciesName = animal?.Species.Name ?? any.Species.Name;

        var points = sightings.Where(x => x.Latitude != null && x.Longitude != null).ToList();
        double? coreLat = points.Count == 0 ? null : points.Average(x => x.Latitude!.Value);
        double? coreLng = points.Count == 0 ? null : points.Average(x => x.Longitude!.Value);
        var radiusMeters = points.Count == 0 || coreLat is null || coreLng is null
            ? 0
            : points.Average(x => HaversineMeters(coreLat.Value, coreLng.Value, x.Latitude!.Value, x.Longitude!.Value));

        var bestWindow = points
            .GroupBy(x => (x.OccurredAtUtc.ToLocalTime().Hour / 3) * 3)
            .OrderByDescending(x => x.Count())
            .FirstOrDefault();
        var bestWindowLabel = bestWindow is null
            ? "No activity window yet"
            : $"{bestWindow.Key:D2}:00-{((bestWindow.Key + 3) % 24):D2}:00";

        var recentPoints = sightings
            .Where(x => x.Latitude != null && x.Longitude != null)
            .Take(100)
            .Select(x => new SightingMapPoint(
                x.Id,
                x.OccurredAtUtc,
                x.Species.Name,
                animalLabel,
                x.Location.Name,
                x.Latitude!.Value,
                x.Longitude!.Value,
                x.LocationAccuracyMeters,
                x.Behavior,
                x.Photos.Count == 0 ? null : (int?)x.Photos.OrderBy(p => p.Id).First().Id))
            .ToList();

        return new AnimalTerritoryInsights(
            AnimalId: animalId,
            AnimalLabel: animalLabel,
            SpeciesName: speciesName,
            TotalSightings: sightings.Count,
            CoordinateSightings: points.Count,
            CoreLatitude: coreLat,
            CoreLongitude: coreLng,
            RadiusMeters: radiusMeters,
            BestActivityWindowLocal: bestWindowLabel,
            HuntingSightings: sightings.Count(x => x.Behavior == SightingBehavior.Hunting),
            RecentPoints: recentPoints);
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

    private static IQueryable<Sighting> ApplyFilters(IQueryable<Sighting> query, SightingFilters filters)
    {
        if (filters.FromUtc is not null)
            query = query.Where(x => x.OccurredAtUtc >= filters.FromUtc);
        if (filters.ToUtc is not null)
            query = query.Where(x => x.OccurredAtUtc <= filters.ToUtc);
        if (filters.SpeciesId is not null)
            query = query.Where(x => x.SpeciesId == filters.SpeciesId);
        if (filters.LocationId is not null)
            query = query.Where(x => x.LocationId == filters.LocationId);
        if (filters.AnimalId is not null)
            query = query.Where(x => x.AnimalId == filters.AnimalId);
        if (filters.UnknownOnly)
            query = query.Where(x => x.AnimalId == null);
        if (filters.WithCoordinatesOnly)
            query = query.Where(x => x.Latitude != null && x.Longitude != null);
        if (filters.MaxLocationAccuracyMeters is not null)
            query = query.Where(x => x.LocationAccuracyMeters == null || x.LocationAccuracyMeters <= filters.MaxLocationAccuracyMeters.Value);

        return query;
    }

    private static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if ((latitude is null) != (longitude is null))
            throw new ArgumentException("Latitude and longitude must both be set or both be empty.");

        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
    }

    private static void ValidateObservedWindow(DateTime occurredAtUtc, DateTime? observedUntilUtc)
    {
        if (observedUntilUtc is not null && observedUntilUtc.Value < occurredAtUtc)
            throw new ArgumentException("Observed-until time cannot be before observed-at time.");
    }

    private static double? NormalizeOptionalPositive(double? value)
    {
        if (value is null)
            return null;

        if (value <= 0)
            return null;

        return value.Value;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusMeters = 6371000;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(DegreesToRadians(lat1)) *
                Math.Cos(DegreesToRadians(lat2)) *
                Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double value) => value * (Math.PI / 180);
}

