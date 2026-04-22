using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Tests;

public sealed class TerritoryInsightsServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public TerritoryInsightsServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetDashboardAsync_hotspots_are_ordered_and_filtered()
    {
        await using var db = await _fixture.CreateContextAsync();
        var user = _fixture.CreatePrimaryUserAccessor();
        var env = _fixture.CreateWebHostEnvironment();
        var photos = new PhotoStorageService(env, user);
        var locations = new LocationService(db, user);
        var sightings = new SightingService(db, user, locations, photos);
        var svc = new TerritoryInsightsService(db, user, sightings);

        var foxId = await AddSpeciesAsync(db, "Fox");
        var badgerId = await AddSpeciesAsync(db, "Badger");
        var locId = await AddLocationAsync(db, SqliteServiceTestFixture.PrimaryUserId);

        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(1);

        // Fox: 2 coordinate sightings in range, 1 hunting
        await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, locId, from.AddHours(1), 51.0, -1.0, SightingBehavior.Hunting);
        await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, locId, from.AddHours(2), 51.2, -1.2, null);

        // Badger: 1 coordinate sighting in range
        await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, badgerId, locId, from.AddHours(3), 51.1, -1.1, null);

        // Out of range: should not count
        await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, locId, to.AddHours(1), 55.0, -2.0, SightingBehavior.Hunting);

        var dashboard = await svc.GetDashboardAsync(fromUtc: from, toUtc: to);
        Assert.NotNull(dashboard);
        Assert.NotEmpty(dashboard.SpeciesHotspots);

        var first = dashboard.SpeciesHotspots[0];
        Assert.Equal("Fox", first.SpeciesName);
        Assert.Equal(2, first.CoordinateSightings);
        Assert.Equal(1, first.HuntingSightings);
        Assert.InRange(first.CoreLatitude!.Value, 51.09, 51.11);
        Assert.InRange(first.CoreLongitude!.Value, -1.11, -1.09);
    }

    private static async Task<int> AddSpeciesAsync(ApplicationDbContext db, string name)
    {
        var row = new Species { Name = name };
        db.Species.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<int> AddLocationAsync(ApplicationDbContext db, string ownerUserId)
    {
        var now = DateTime.UtcNow;
        var row = new Location
        {
            OwnerUserId = ownerUserId,
            Name = "Home",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Locations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<int> AddSightingAsync(
        ApplicationDbContext db,
        string ownerUserId,
        int speciesId,
        int locationId,
        DateTime occurredAtUtc,
        double latitude,
        double longitude,
        SightingBehavior? behavior)
    {
        var now = DateTime.UtcNow;
        var row = new Sighting
        {
            OwnerUserId = ownerUserId,
            SpeciesId = speciesId,
            LocationId = locationId,
            OccurredAtUtc = occurredAtUtc,
            Latitude = latitude,
            Longitude = longitude,
            Behavior = behavior,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Sightings.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}

