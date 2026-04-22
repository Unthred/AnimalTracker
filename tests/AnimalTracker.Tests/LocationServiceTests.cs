using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Tests;

public sealed class LocationServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public LocationServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetOrCreateDefaultAsync_creates_once_per_user()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = new LocationService(db, currentUser);

        var first = await service.GetOrCreateDefaultAsync();
        var second = await service.GetOrCreateDefaultAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Home", first.Name);
    }

    [Fact]
    public async Task DeleteAsync_throws_when_only_one_location_exists()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = new LocationService(db, currentUser);
        var only = await service.GetOrCreateDefaultAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(only.Id));
    }

    [Fact]
    public async Task DeleteAsync_rehomes_sightings_to_another_location()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = new LocationService(db, currentUser);

        var speciesId = await AddSpeciesAsync(db, "Fox");
        var first = await service.CreateAsync("One", null);
        var second = await service.CreateAsync("Two", null);
        var sightingId = await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, speciesId, first.Id);
        db.ChangeTracker.Clear();

        await service.DeleteAsync(first.Id);

        var sighting = await db.Sightings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sightingId);
        Assert.NotNull(sighting);
        Assert.Equal(second.Id, sighting!.LocationId);
    }

    private static async Task<int> AddSpeciesAsync(ApplicationDbContext db, string name)
    {
        var row = new Species { Name = name };
        db.Species.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<int> AddSightingAsync(ApplicationDbContext db, string ownerUserId, int speciesId, int locationId)
    {
        var now = DateTime.UtcNow;
        var row = new Sighting
        {
            OwnerUserId = ownerUserId,
            SpeciesId = speciesId,
            LocationId = locationId,
            OccurredAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Sightings.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
