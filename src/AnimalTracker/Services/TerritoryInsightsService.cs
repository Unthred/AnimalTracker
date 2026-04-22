using AnimalTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed record SpeciesHotspotSummary(
    string SpeciesName,
    int CoordinateSightings,
    int HuntingSightings,
    double? CoreLatitude,
    double? CoreLongitude);

public sealed record TerritoryDashboardData(
    IReadOnlyList<AnimalTerritorySummary> Territories,
    IReadOnlyList<SpeciesHotspotSummary> SpeciesHotspots);

public sealed class TerritoryInsightsService(
    ApplicationDbContext db,
    ICurrentUserAccessor currentUser,
    SightingService sightings)
{
    public Task<AnimalTerritoryInsights?> GetAnimalInsightsAsync(
        int animalId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default) =>
        sightings.GetTerritoryInsightsForAnimalAsync(animalId, fromUtc, toUtc, cancellationToken);

    public async Task<TerritoryDashboardData> GetDashboardAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var territories = await sightings.GetTerritorySummariesAsync(fromUtc, toUtc, take: 12, cancellationToken);
        var hotspots = await GetSpeciesHotspotsAsync(fromUtc, toUtc, cancellationToken);
        return new TerritoryDashboardData(territories, hotspots);
    }

    private async Task<List<SpeciesHotspotSummary>> GetSpeciesHotspotsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);

        var query = db.Sightings
            .AsNoTracking()
            .Include(x => x.Species)
            .Where(x => x.OwnerUserId == userId && x.Latitude != null && x.Longitude != null);

        if (fromUtc is not null)
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        if (toUtc is not null)
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);

        var sightingsBySpecies = await query.ToListAsync(cancellationToken);
        return sightingsBySpecies
            .GroupBy(x => x.Species.Name)
            .Select(g =>
            {
                var coreLat = g.Average(x => x.Latitude!.Value);
                var coreLng = g.Average(x => x.Longitude!.Value);
                return new SpeciesHotspotSummary(
                    SpeciesName: g.Key,
                    CoordinateSightings: g.Count(),
                    HuntingSightings: g.Count(x => x.Behavior == Data.Entities.SightingBehavior.Hunting),
                    CoreLatitude: coreLat,
                    CoreLongitude: coreLng);
            })
            .OrderByDescending(x => x.CoordinateSightings)
            .ThenBy(x => x.SpeciesName)
            .Take(10)
            .ToList();
    }
}
