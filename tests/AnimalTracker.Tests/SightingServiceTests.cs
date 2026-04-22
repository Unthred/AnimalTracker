using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Tests;

public sealed class SightingServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public SightingServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateAsync_requires_both_latitude_and_longitude()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = CreateSightingService(db, currentUser);
        var speciesId = await AddSpeciesAsync(db, "Fox");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                occurredAtUtc: DateTime.UtcNow,
                speciesId: speciesId,
                locationId: null,
                animalId: null,
                notes: null,
                latitude: 51.5,
                longitude: null));
    }

    [Fact]
    public async Task UpdateAsync_rejects_location_not_owned_by_current_user()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = CreateSightingService(db, currentUser);
        var now = DateTime.UtcNow;

        var speciesId = await AddSpeciesAsync(db, "Fox");
        var ownLocationId = await AddLocationAsync(db, SqliteServiceTestFixture.PrimaryUserId, "Home");
        var otherLocationId = await AddLocationAsync(db, SqliteServiceTestFixture.SecondaryUserId, "Elsewhere");
        var sightingId = await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, speciesId, ownLocationId, now);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(
                id: sightingId,
                occurredAtUtc: now,
                speciesId: speciesId,
                locationId: otherLocationId,
                animalId: null,
                notes: "updated"));
    }

    [Fact]
    public async Task GetRecentAsync_applies_user_scope_and_filters()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = CreateSightingService(db, currentUser);
        var now = DateTime.UtcNow;

        var foxId = await AddSpeciesAsync(db, "Fox");
        var badgerId = await AddSpeciesAsync(db, "Badger");
        var locationId = await AddLocationAsync(db, SqliteServiceTestFixture.PrimaryUserId, "Garden");
        var trackedAnimalId = await AddAnimalAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, "Tracked Fox");

        await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, locationId, now.AddHours(-1), animalId: null);
        await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, locationId, now.AddHours(-2), animalId: trackedAnimalId);
        await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, badgerId, locationId, now.AddHours(-3), animalId: null);
        await AddSightingAsync(db, SqliteServiceTestFixture.SecondaryUserId, foxId, locationId, now.AddMinutes(-10), animalId: null);

        var filters = new SightingFilters(
            FromUtc: now.AddDays(-1),
            ToUtc: now,
            SpeciesId: foxId,
            LocationId: locationId,
            AnimalId: null,
            UnknownOnly: true);

        var rows = await service.GetRecentAsync(take: 20, filters);
        Assert.Single(rows);
        Assert.All(rows, x => Assert.Equal(SqliteServiceTestFixture.PrimaryUserId, x.OwnerUserId));
        Assert.Equal(foxId, rows[0].SpeciesId);
        Assert.Null(rows[0].AnimalId);
    }

    [Fact]
    public async Task GetRecentAsync_validates_take_bounds()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = CreateSightingService(db, currentUser);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetRecentAsync(0, new SightingFilters(null, null, null, null, null, false)));
    }

    private SightingService CreateSightingService(ApplicationDbContext db, ICurrentUserAccessor currentUser)
    {
        var env = _fixture.CreateWebHostEnvironment();
        var locationService = new LocationService(db, currentUser);
        var photoStorage = new PhotoStorageService(env, currentUser);
        return new SightingService(db, currentUser, locationService, photoStorage);
    }

    private static async Task<int> AddSpeciesAsync(ApplicationDbContext db, string name)
    {
        var row = new Species { Name = name };
        db.Species.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<int> AddLocationAsync(ApplicationDbContext db, string ownerUserId, string name)
    {
        var now = DateTime.UtcNow;
        var row = new Location
        {
            OwnerUserId = ownerUserId,
            Name = name,
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
        int? animalId = null)
    {
        var now = DateTime.UtcNow;
        var row = new Sighting
        {
            OwnerUserId = ownerUserId,
            SpeciesId = speciesId,
            LocationId = locationId,
            AnimalId = animalId,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Sightings.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<int> AddAnimalAsync(ApplicationDbContext db, string ownerUserId, int speciesId, string displayName)
    {
        var now = DateTime.UtcNow;
        var row = new Animal
        {
            OwnerUserId = ownerUserId,
            SpeciesId = speciesId,
            DisplayName = displayName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Animals.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
