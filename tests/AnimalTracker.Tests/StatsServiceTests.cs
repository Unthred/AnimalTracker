using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;

namespace AnimalTracker.Tests;

public sealed class StatsServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public StatsServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetSummaryAsync_computes_expected_totals_for_current_user()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = new StatsService(db, currentUser);

        var foxId = await AddSpeciesAsync(db, "Fox");
        var badgerId = await AddSpeciesAsync(db, "Badger");
        var locationId = await AddLocationAsync(db, SqliteServiceTestFixture.PrimaryUserId);
        var trackedAnimalId = await AddAnimalAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, "Tracked");
        var otherAnimalId = await AddAnimalAsync(db, SqliteServiceTestFixture.SecondaryUserId, foxId, "Other");
        var now = DateTime.UtcNow;

        var s1 = await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, locationId, now.AddHours(-4), 51.1, -1.2, SightingBehavior.Hunting, animalId: trackedAnimalId);
        var s2 = await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, locationId, now.AddHours(-3), null, null, null, animalId: null);
        _ = await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, badgerId, locationId, now.AddHours(-2), 51.2, -1.1, null, animalId: null);
        var otherLocationId = await AddLocationAsync(db, SqliteServiceTestFixture.SecondaryUserId);
        var other = await AddSightingAsync(db, SqliteServiceTestFixture.SecondaryUserId, foxId, otherLocationId, now.AddHours(-1), 50.0, -1.0, SightingBehavior.Hunting, animalId: otherAnimalId);

        await AddPhotoAsync(db, s1);
        await AddPhotoAsync(db, s2);
        await AddPhotoAsync(db, other);

        var summary = await service.GetSummaryAsync(fromUtc: now.AddDays(-1), toUtc: now.AddMinutes(1), speciesTopN: 5);

        Assert.Equal(3, summary.TotalSightings);
        Assert.Equal(2, summary.DistinctSpeciesCount);
        Assert.Equal(2, summary.TotalPhotos);
        Assert.Equal(2, summary.GeotaggedSightings);
        Assert.Equal(1, summary.LinkedAnimalSightings);
        Assert.Equal(1, summary.HuntingSightings);
        Assert.Equal(2, summary.SpeciesCounts.Count);
        Assert.Equal(24, summary.HourlyCounts.Count);
        Assert.Contains(summary.SpeciesCounts, x => x.SpeciesName == "Fox" && x.Count == 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task GetSummaryAsync_validates_species_top_n(int topN)
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = new StatsService(db, currentUser);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetSummaryAsync(null, null, speciesTopN: topN));
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
            Name = "Test Location",
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
        double? latitude,
        double? longitude,
        SightingBehavior? behavior,
        int? animalId)
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
            AnimalId = animalId,
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

    private static async Task AddPhotoAsync(ApplicationDbContext db, int sightingId)
    {
        db.SightingPhotos.Add(new SightingPhoto
        {
            SightingId = sightingId,
            StoredPath = $"photos/{Guid.NewGuid():N}.jpg",
            OriginalFileName = "x.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 123,
            CreatedAtUtc = DateTime.UtcNow,
            ContentSha256Hex = Guid.NewGuid().ToString("N").PadRight(64, 'a')[..64]
        });
        await db.SaveChangesAsync();
    }
}
