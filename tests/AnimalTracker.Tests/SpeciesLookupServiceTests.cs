using System.Net;
using System.Text;
using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimalTracker.Tests;

public sealed class SpeciesLookupServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public SpeciesLookupServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SearchActiveRegionAsync_trims_query_clamps_take_and_matches_scientific_name()
    {
        await using var db = await _fixture.CreateContextAsync();
        var appSettings = await CreateAppSettingsAsync(db, "inat-place:1", "Region");
        var http = new TestHttpClientFactory(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var svc = new SpeciesLookupService(db, appSettings, http, NullLogger<SpeciesLookupService>.Instance);

        var s1 = await AddSpeciesAsync(db, "Red Fox", scientificName: "Vulpes vulpes");
        var s2 = await AddSpeciesAsync(db, "Badger", scientificName: "Meles meles");
        await AddRegionCacheAsync(db, "inat-place:1", "Region", s1, s2);

        var rows = await svc.SearchActiveRegionAsync("  vulpes  ", take: 999);

        Assert.Single(rows);
        Assert.Equal("Red Fox", rows[0].Name);
    }

    [Fact]
    public async Task GetDetailsAsync_returns_null_when_species_not_in_active_region()
    {
        await using var db = await _fixture.CreateContextAsync();
        var appSettings = await CreateAppSettingsAsync(db, "inat-place:1", "Region");
        var http = new TestHttpClientFactory(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var svc = new SpeciesLookupService(db, appSettings, http, NullLogger<SpeciesLookupService>.Instance);

        var speciesId = await AddSpeciesAsync(db, "Fox", scientificName: null);
        // no cache row => not in active region

        var details = await svc.GetDetailsAsync(speciesId);
        Assert.Null(details);
    }

    [Fact]
    public async Task GetDetailsAsync_enriches_missing_fields_from_inaturalist()
    {
        await using var db = await _fixture.CreateContextAsync();
        var appSettings = await CreateAppSettingsAsync(db, "inat-place:1", "Region");

        var handler = new TestHttpClientFactory(req =>
        {
            if (req.RequestUri?.ToString().Contains("/v1/taxa/123", StringComparison.OrdinalIgnoreCase) == true)
            {
                var json = """
                           {
                             "results":[
                               {
                                 "id":123,
                                 "name":"Vulpes vulpes",
                                 "wikipedia_summary":"Foxes are nocturnal animals that live in woodland habitats.",
                                 "iconic_taxon_name":"Mammalia",
                                 "observations_count":42,
                                 "wikipedia_url":"https://en.wikipedia.org/wiki/Red_fox",
                                 "default_photo":{"medium_url":"https://example.com/fox.jpg"}
                               }
                             ]
                           }
                           """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var svc = new SpeciesLookupService(db, appSettings, handler, NullLogger<SpeciesLookupService>.Instance);

        var id = await AddSpeciesAsync(
            db,
            "Red Fox",
            scientificName: null,
            imageUrl: null,
            description: null,
            source: "iNaturalist",
            sourceId: "123");
        await AddRegionCacheAsync(db, "inat-place:1", "Region", id);

        var details = await svc.GetDetailsAsync(id);

        Assert.NotNull(details);
        Assert.Equal("Red Fox", details!.Name);
        Assert.Equal("Vulpes vulpes", details.ScientificName);
        Assert.Equal("https://example.com/fox.jpg", details.ImageUrl);
        Assert.Equal("Mammalia", details.TaxonGroup);
        Assert.Equal(42, details.ObservationsCount);
        Assert.NotNull(details.HabitatSummary);
    }

    [Fact]
    public async Task GetDetailsAsync_does_not_throw_when_enrichment_fails()
    {
        await using var db = await _fixture.CreateContextAsync();
        var appSettings = await CreateAppSettingsAsync(db, "inat-place:1", "Region");

        var handler = new TestHttpClientFactory(_ => throw new HttpRequestException("boom"));
        var svc = new SpeciesLookupService(db, appSettings, handler, NullLogger<SpeciesLookupService>.Instance);

        var id = await AddSpeciesAsync(
            db,
            "Red Fox",
            scientificName: null,
            imageUrl: null,
            description: null,
            source: "iNaturalist",
            sourceId: "123");
        await AddRegionCacheAsync(db, "inat-place:1", "Region", id);

        var details = await svc.GetDetailsAsync(id);

        Assert.NotNull(details);
        Assert.Equal("Red Fox", details!.Name);
        Assert.Null(details.ScientificName);
        Assert.Null(details.ImageUrl);
    }

    private async Task<AppSettingsService> CreateAppSettingsAsync(ApplicationDbContext db, string regionKey, string regionName)
    {
        var env = _fixture.CreateWebHostEnvironment();
        var user = _fixture.CreatePrimaryUserAccessor();
        var photos = new PhotoStorageService(env, user);
        var appSettings = new AppSettingsService(db, photos);
        await appSettings.UpdateActiveSpeciesRegionAsync(regionKey, regionName);
        return appSettings;
    }

    private static async Task<int> AddSpeciesAsync(
        ApplicationDbContext db,
        string name,
        string? scientificName,
        string? imageUrl = null,
        string? description = null,
        string? source = null,
        string? sourceId = null)
    {
        var row = new Species
        {
            Name = name,
            ScientificName = scientificName,
            ImageUrl = imageUrl,
            Description = description,
            CatalogSource = source,
            CatalogSourceId = sourceId
        };
        db.Species.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task AddRegionCacheAsync(ApplicationDbContext db, string regionKey, string regionName, params int[] speciesIds)
    {
        foreach (var id in speciesIds)
        {
            db.SpeciesRegionCaches.Add(new SpeciesRegionCache
            {
                RegionKey = regionKey,
                RegionName = regionName,
                SpeciesId = id,
                SyncedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private sealed class TestHttpClientFactory(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new Handler(responder)) { BaseAddress = new Uri("https://api.inaturalist.org/") };

        private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                responder(request);
        }
    }
}

