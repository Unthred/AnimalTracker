using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;

namespace AnimalTracker.Tests;

public sealed class SpeciesServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public SpeciesServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetAllAsync_returns_empty_when_no_active_region()
    {
        await using var db = await _fixture.CreateContextAsync();
        var appSettings = CreateAppSettingsService(db);
        var service = new SpeciesService(db, appSettings);

        var rows = await service.GetAllAsync();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetAllAsync_returns_species_for_active_region_sorted()
    {
        await using var db = await _fixture.CreateContextAsync();
        var appSettings = CreateAppSettingsService(db);
        var service = new SpeciesService(db, appSettings);

        var fox = await AddSpeciesAsync(db, "Fox");
        var badger = await AddSpeciesAsync(db, "Badger");
        db.SpeciesRegionCaches.AddRange(
            new SpeciesRegionCache { RegionKey = "inat-place:1", RegionName = "Region", SpeciesId = fox, SyncedAtUtc = DateTime.UtcNow },
            new SpeciesRegionCache { RegionKey = "inat-place:1", RegionName = "Region", SpeciesId = badger, SyncedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await appSettings.UpdateActiveSpeciesRegionAsync("inat-place:1", "Region");
        var rows = await service.GetAllAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Badger", rows[0].Name);
        Assert.Equal("Fox", rows[1].Name);
    }

    private AppSettingsService CreateAppSettingsService(ApplicationDbContext db)
    {
        var env = _fixture.CreateWebHostEnvironment();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var photos = new PhotoStorageService(env, currentUser);
        return new AppSettingsService(db, photos);
    }

    private static async Task<int> AddSpeciesAsync(ApplicationDbContext db, string name)
    {
        var row = new Species { Name = name };
        db.Species.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
