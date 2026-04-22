using AnimalTracker.Data;
using AnimalTracker.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimalTracker.Tests;

public sealed class SpeciesCatalogSyncServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public SpeciesCatalogSyncServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SyncActiveRegionAsync_throws_for_invalid_region_key_format()
    {
        await using var db = await _fixture.CreateContextAsync();
        var env = _fixture.CreateWebHostEnvironment();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var appSettings = new AppSettingsService(db, new PhotoStorageService(env, currentUser));
        await appSettings.UpdateActiveSpeciesRegionAsync("bad-key", "Invalid");

        var service = new SpeciesCatalogSyncService(
            db,
            appSettings,
            new EmptyHttpClientFactory(),
            NullLogger<SpeciesCatalogSyncService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncActiveRegionAsync(forceRefresh: true));
    }

    private sealed class EmptyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
