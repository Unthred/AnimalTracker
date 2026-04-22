using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;

namespace AnimalTracker.Tests;

public sealed class UserSettingsServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public UserSettingsServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetOrCreateAsync_uses_app_default_theme_mode()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var (appSettings, userSettings) = CreateServices(db, currentUser);

        await appSettings.UpdateThemeModeAsync("dark");
        var settings = await userSettings.GetOrCreateAsync();

        Assert.Equal("dark", settings.ThemeMode);
        Assert.Equal(SqliteServiceTestFixture.PrimaryUserId, settings.OwnerUserId);
    }

    [Fact]
    public async Task UpdateAsync_validates_accent_color()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var (_, userSettings) = CreateServices(db, currentUser);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            userSettings.UpdateAsync(
                accentColorHex: "blue",
                compactMode: false,
                timelinePageSize: 50,
                themeMode: "system",
                surfaceOpacityPercent: 93,
                darkSurfaceOpacityPercent: 50));
    }

    [Fact]
    public async Task UpdateAsync_persists_valid_settings()
    {
        await using var db = await _fixture.CreateContextAsync();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var (_, userSettings) = CreateServices(db, currentUser);

        var updated = await userSettings.UpdateAsync(
            accentColorHex: "#22c55e",
            compactMode: true,
            timelinePageSize: 100,
            themeMode: "light",
            surfaceOpacityPercent: 90,
            darkSurfaceOpacityPercent: 55);

        Assert.Equal("#22c55e", updated.AccentColorHex);
        Assert.True(updated.CompactMode);
        Assert.Equal(100, updated.TimelinePageSize);
        Assert.Equal("light", updated.ThemeMode);
        Assert.Equal(90, updated.SurfaceOpacityPercent);
        Assert.Equal(55, updated.DarkSurfaceOpacityPercent);
    }

    private (AppSettingsService AppSettings, UserSettingsService UserSettings) CreateServices(
        ApplicationDbContext db,
        TestCurrentUserAccessor currentUser)
    {
        var env = _fixture.CreateWebHostEnvironment();
        var photos = new PhotoStorageService(env, currentUser);
        var appSettings = new AppSettingsService(db, photos);
        var userSettings = new UserSettingsService(db, currentUser, photos, appSettings);
        return (appSettings, userSettings);
    }
}
