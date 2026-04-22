using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Tests;

public sealed class AnimalServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public AnimalServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SearchAsync_filters_to_current_user_and_species()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = new AnimalService(db, currentUser);

        var foxId = await AddSpeciesAsync(db, "Fox");
        var badgerId = await AddSpeciesAsync(db, "Badger");

        await AddAnimalAsync(db, SqliteServiceTestFixture.PrimaryUserId, foxId, "Rufus");
        await AddAnimalAsync(db, SqliteServiceTestFixture.PrimaryUserId, badgerId, "Stripe");
        await AddAnimalAsync(db, SqliteServiceTestFixture.SecondaryUserId, foxId, "Other User Fox");

        var rows = await service.SearchAsync("ru", foxId);
        Assert.Single(rows);
        Assert.Equal("Rufus", rows[0].DisplayName);
        Assert.Equal(SqliteServiceTestFixture.PrimaryUserId, rows[0].OwnerUserId);
    }

    [Fact]
    public async Task DeleteAsync_unlinks_sightings_before_deleting_animal()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var service = new AnimalService(db, currentUser);

        var speciesId = await AddSpeciesAsync(db, "Fox");
        var locationId = await AddLocationAsync(db, SqliteServiceTestFixture.PrimaryUserId);
        var animalId = await AddAnimalAsync(db, SqliteServiceTestFixture.PrimaryUserId, speciesId, "Tracker");
        var sightingId = await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, speciesId, locationId, animalId);

        await service.DeleteAsync(animalId);

        Assert.False(await db.Animals.AnyAsync(x => x.Id == animalId));
        var sighting = await db.Sightings.FindAsync(sightingId);
        Assert.NotNull(sighting);
        Assert.Null(sighting!.AnimalId);
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

    private static async Task<int> AddSightingAsync(ApplicationDbContext db, string ownerUserId, int speciesId, int locationId, int animalId)
    {
        var now = DateTime.UtcNow;
        var row = new Sighting
        {
            OwnerUserId = ownerUserId,
            SpeciesId = speciesId,
            LocationId = locationId,
            AnimalId = animalId,
            OccurredAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Sightings.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
