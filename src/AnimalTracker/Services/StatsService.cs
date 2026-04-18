using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed record SpeciesCountRow(string SpeciesName, int Count);

public sealed record DailySightingCount(DateTime DayUtc, int Count);

public sealed record BehaviorCountRow(SightingBehavior Behavior, int Count);

public sealed record HourlySightingCount(int HourLocal, int Count);

public sealed record SightingStatsSummary(
    int TotalSightings,
    int DistinctSpeciesCount,
    int TotalPhotos,
    int GeotaggedSightings,
    int LinkedAnimalSightings,
    int HuntingSightings,
    IReadOnlyList<SpeciesCountRow> SpeciesCounts,
    IReadOnlyList<DailySightingCount> DailyCounts,
    IReadOnlyList<BehaviorCountRow> BehaviorCounts,
    IReadOnlyList<HourlySightingCount> HourlyCounts);

public sealed class StatsService(ApplicationDbContext db, CurrentUserService currentUser)
{
    public async Task<SightingStatsSummary> GetSummaryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int speciesTopN = 20,
        CancellationToken cancellationToken = default)
    {
        if (speciesTopN is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(speciesTopN), "Top N must be between 1 and 200.");

        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var filters = new SightingFilters(fromUtc, toUtc, null, null, null, UnknownOnly: false);

        var baseQuery = db.Sightings.AsNoTracking().Where(x => x.OwnerUserId == userId);
        baseQuery = ApplyFilters(baseQuery, filters);

        var totalSightings = await baseQuery.CountAsync(cancellationToken);
        var distinctSpecies = await baseQuery.Select(x => x.SpeciesId).Distinct().CountAsync(cancellationToken);

        var totalPhotos = await db.SightingPhotos
            .AsNoTracking()
            .Join(baseQuery, p => p.SightingId, s => s.Id, (p, _) => p)
            .CountAsync(cancellationToken);

        var geotagged = await baseQuery.CountAsync(x => x.Latitude != null && x.Longitude != null, cancellationToken);
        var linkedAnimals = await baseQuery.CountAsync(x => x.AnimalId != null, cancellationToken);
        var hunting = await baseQuery.CountAsync(x => x.Behavior == SightingBehavior.Hunting, cancellationToken);

        // Group by SpeciesId only (SQLite cannot translate GroupBy with navigation Species.Name).
        var speciesCountsRaw = await baseQuery
            .GroupBy(x => x.SpeciesId)
            .Select(g => new { SpeciesId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.SpeciesId)
            .Take(speciesTopN)
            .ToListAsync(cancellationToken);

        var speciesIdList = speciesCountsRaw.Select(x => x.SpeciesId).Distinct().ToList();
        var speciesNameLookup = await db.Species.AsNoTracking()
            .Where(s => speciesIdList.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var speciesCounts = speciesCountsRaw
            .Select(x => new SpeciesCountRow(speciesNameLookup[x.SpeciesId], x.Count))
            .ToList();

        // Daily buckets: avoid GroupBy on constructed DateTime / .Date (SQLite translation varies).
        var occurredAtList = await baseQuery.Select(x => x.OccurredAtUtc).ToListAsync(cancellationToken);
        var dailyCounts = occurredAtList
            .GroupBy(t => t.Date)
            .Select(g => new DailySightingCount(g.Key, g.Count()))
            .OrderBy(x => x.DayUtc)
            .ToList();

        var hourlyCounts = occurredAtList
            .GroupBy(t => t.ToLocalTime().Hour)
            .ToDictionary(g => g.Key, g => g.Count());
        var hourlyRows = Enumerable.Range(0, 24)
            .Select(hour => new HourlySightingCount(hour, hourlyCounts.GetValueOrDefault(hour, 0)))
            .ToList();

        // Behavior: SQLite/EF cannot translate GroupBy on nullable enum .Value (correlated Count fails).
        var behaviorValues = await baseQuery
            .Where(x => x.Behavior != null)
            .Select(x => x.Behavior!.Value)
            .ToListAsync(cancellationToken);
        var behaviorRows = behaviorValues
            .GroupBy(b => b)
            .Select(g => new BehaviorCountRow(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Behavior)
            .ToList();

        return new SightingStatsSummary(
            TotalSightings: totalSightings,
            DistinctSpeciesCount: distinctSpecies,
            TotalPhotos: totalPhotos,
            GeotaggedSightings: geotagged,
            LinkedAnimalSightings: linkedAnimals,
            HuntingSightings: hunting,
            SpeciesCounts: speciesCounts,
            DailyCounts: dailyCounts,
            BehaviorCounts: behaviorRows,
            HourlyCounts: hourlyRows);
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
}
