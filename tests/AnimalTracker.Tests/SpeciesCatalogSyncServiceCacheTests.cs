using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimalTracker.Tests;

public sealed class SpeciesCatalogSyncServiceCacheTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public SpeciesCatalogSyncServiceCacheTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SyncRegionAsync_returns_cache_hit_without_http_call()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (appSettings, http) = CreateDeps(db, handler: new CountingHttpMessageHandler());
        var svc = new SpeciesCatalogSyncService(db, appSettings, http, NullLogger<SpeciesCatalogSyncService>.Instance);

        var speciesId = await AddSpeciesAsync(db, "Cached");
        db.SpeciesRegionCaches.Add(new SpeciesRegionCache
        {
            RegionKey = "inat-place:1",
            RegionName = "Region",
            SpeciesId = speciesId,
            SyncedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await svc.SyncRegionAsync("inat-place:1", "Region", forceRefresh: false);

        Assert.True(result.UsedCache);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, ((CountingHttpMessageHandler)http.Handler).CallCount);
    }

    [Fact]
    public async Task SyncRegionAsync_force_refresh_rebuilds_cache_using_fallback_when_http_fails()
    {
        await using var db = await _fixture.CreateContextAsync();

        var handler = new CountingHttpMessageHandler { AlwaysThrow = true };
        var (appSettings, http) = CreateDeps(db, handler);
        var svc = new SpeciesCatalogSyncService(db, appSettings, http, NullLogger<SpeciesCatalogSyncService>.Instance);

        var oldSpeciesId = await AddSpeciesAsync(db, "Old");
        db.SpeciesRegionCaches.Add(new SpeciesRegionCache
        {
            RegionKey = "inat-place:2",
            RegionName = "Region",
            SpeciesId = oldSpeciesId,
            SyncedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var before = await db.SpeciesRegionCaches.CountAsync(x => x.RegionKey == "inat-place:2");
        Assert.Equal(1, before);

        var result = await svc.SyncRegionAsync("inat-place:2", "Region", forceRefresh: true);

        Assert.False(result.UsedCache);
        Assert.True(result.UpdatedCount > 0);
        var after = await db.SpeciesRegionCaches.CountAsync(x => x.RegionKey == "inat-place:2");
        Assert.True(after > 0);
        Assert.True(handler.CallCount > 0);
    }

    [Fact]
    public async Task SyncRegionAsync_is_idempotent_with_fallback_catalog()
    {
        await using var db = await _fixture.CreateContextAsync();

        var handler = new CountingHttpMessageHandler { AlwaysThrow = true };
        var (appSettings, http) = CreateDeps(db, handler);
        var svc = new SpeciesCatalogSyncService(db, appSettings, http, NullLogger<SpeciesCatalogSyncService>.Instance);

        var first = await svc.SyncRegionAsync("inat-place:3", "Region", forceRefresh: true);
        var speciesCountAfterFirst = await db.Species.CountAsync();
        var cacheCountAfterFirst = await db.SpeciesRegionCaches.CountAsync(x => x.RegionKey == "inat-place:3");

        var second = await svc.SyncRegionAsync("inat-place:3", "Region", forceRefresh: true);
        var speciesCountAfterSecond = await db.Species.CountAsync();
        var cacheCountAfterSecond = await db.SpeciesRegionCaches.CountAsync(x => x.RegionKey == "inat-place:3");

        Assert.False(first.UsedCache);
        Assert.False(second.UsedCache);
        Assert.Equal(speciesCountAfterFirst, speciesCountAfterSecond);
        Assert.Equal(cacheCountAfterFirst, cacheCountAfterSecond);
    }

    private (AppSettingsService AppSettings, TestHttpClientFactory Http) CreateDeps(ApplicationDbContext db, CountingHttpMessageHandler handler)
    {
        var env = _fixture.CreateWebHostEnvironment();
        var user = _fixture.CreatePrimaryUserAccessor();
        var photos = new PhotoStorageService(env, user);
        var appSettings = new AppSettingsService(db, photos);
        var http = new TestHttpClientFactory(handler);
        return (appSettings, http);
    }

    private static async Task<int> AddSpeciesAsync(ApplicationDbContext db, string name)
    {
        var s = new Species { Name = name };
        db.Species.Add(s);
        await db.SaveChangesAsync();
        return s.Id;
    }

    private sealed class TestHttpClientFactory(CountingHttpMessageHandler handler) : IHttpClientFactory
    {
        public CountingHttpMessageHandler Handler => handler;
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("http://test/") };
    }

    private sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public bool AlwaysThrow { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (AlwaysThrow)
                throw new HttpRequestException("boom");

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"results":[]}""")
            });
        }
    }
}

