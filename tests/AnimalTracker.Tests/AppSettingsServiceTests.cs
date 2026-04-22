using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Tests;

public sealed class AppSettingsServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public AppSettingsServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetOrCreateAsync_removes_duplicate_rows_and_returns_latest()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = CreateService(db);

        var now = DateTime.UtcNow;
        db.AppSettings.AddRange(
            new AppSettings { DefaultThemeMode = "light", CreatedAtUtc = now.AddMinutes(-10), UpdatedAtUtc = now.AddMinutes(-10) },
            new AppSettings { DefaultThemeMode = "dark", CreatedAtUtc = now, UpdatedAtUtc = now });
        await db.SaveChangesAsync();

        var settings = await service.GetOrCreateAsync();
        var count = await db.AppSettings.CountAsync();

        Assert.Equal("dark", settings.DefaultThemeMode);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UpdateThemeModeAsync_normalizes_values()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = CreateService(db);

        var updated = await service.UpdateThemeModeAsync(" DARK ");
        Assert.Equal("dark", updated.DefaultThemeMode);
    }

    [Fact]
    public async Task UpdateActiveSpeciesRegionAsync_trims_and_persists()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = CreateService(db);

        var updated = await service.UpdateActiveSpeciesRegionAsync(" inat-place:42 ", " UK ");
        Assert.Equal("inat-place:42", updated.ActiveSpeciesRegionKey);
        Assert.Equal("UK", updated.ActiveSpeciesRegionName);
    }

    private AppSettingsService CreateService(ApplicationDbContext db)
    {
        var env = _fixture.CreateWebHostEnvironment();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var photos = new PhotoStorageService(env, currentUser);
        return new AppSettingsService(db, photos);
    }
}
