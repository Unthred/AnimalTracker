using System.Security.Cryptography;
using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimalTracker.Tests;

public sealed class PhotoImportServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public PhotoImportServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RunAsync_throws_when_no_files_selected()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = await CreateServiceAsync(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync([], fallbackSpeciesId: null, locationId: null));
    }

    [Fact]
    public async Task RunAsync_throws_when_active_region_has_no_species()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = await CreateServiceAsync(db, regionKey: "inat-place:1", regionName: "Region");
        var file = new TestBrowserFile("a.jpg", "image/jpeg", [1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync([file], fallbackSpeciesId: null, locationId: null));
    }

    [Fact]
    public async Task RunAsync_throws_when_fallback_species_not_in_active_region()
    {
        await using var db = await _fixture.CreateContextAsync();
        var speciesId = await AddSpeciesInRegionAsync(db, "Fox", "inat-place:1", "Region");
        var service = await CreateServiceAsync(db, regionKey: "inat-place:1", regionName: "Region");
        var file = new TestBrowserFile("a.jpg", "image/jpeg", [1, 2, 3, 4]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync([file], fallbackSpeciesId: speciesId + 999, locationId: null));
    }

    [Fact]
    public async Task RunAsync_skips_duplicates_and_completes_batch_without_creating_sightings()
    {
        await using var db = await _fixture.CreateContextAsync();
        var speciesId = await AddSpeciesInRegionAsync(db, "Fox", "inat-place:1", "Region");
        var locationId = await AddLocationAsync(db, SqliteServiceTestFixture.PrimaryUserId);
        var sourceBytes = new byte[] { 9, 9, 9, 9 };
        var hash = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
        var sightingId = await AddSightingAsync(db, SqliteServiceTestFixture.PrimaryUserId, speciesId, locationId);
        await AddPhotoAsync(db, sightingId, hash);

        var service = await CreateServiceAsync(db, regionKey: "inat-place:1", regionName: "Region");
        var file = new TestBrowserFile("duplicate.jpg", "image/jpeg", sourceBytes);

        var result = await service.RunAsync([file], fallbackSpeciesId: speciesId, locationId: locationId);

        Assert.Equal(0, result.CreatedSightings);
        Assert.Equal(0, result.PhotosAttached);
        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Equal(0, result.FailedItems);
    }

    private async Task<PhotoImportService> CreateServiceAsync(
        ApplicationDbContext db,
        string? regionKey = null,
        string? regionName = null)
    {
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        var env = _fixture.CreateWebHostEnvironment();
        var photoStorage = new PhotoStorageService(env, currentUser);
        var appSettings = new AppSettingsService(db, photoStorage);
        if (regionKey is not null || regionName is not null)
            await appSettings.UpdateActiveSpeciesRegionAsync(regionKey, regionName);

        var locationService = new LocationService(db, currentUser);
        var sightingService = new SightingService(db, currentUser, locationService, photoStorage);
        var speciesService = new SpeciesService(db, appSettings);

        return new PhotoImportService(
            db,
            currentUser,
            sightingService,
            photoStorage,
            new ExifMetadataService(),
            new NullRecognitionService(),
            speciesService,
            Options.Create(new RecognitionOptions()),
            Options.Create(new PhotoImportOptions()),
            NullLogger<PhotoImportService>.Instance);
    }

    private static async Task<int> AddSpeciesInRegionAsync(ApplicationDbContext db, string name, string regionKey, string regionName)
    {
        var species = new Species { Name = name };
        db.Species.Add(species);
        await db.SaveChangesAsync();
        db.SpeciesRegionCaches.Add(new SpeciesRegionCache
        {
            RegionKey = regionKey,
            RegionName = regionName,
            SpeciesId = species.Id,
            SyncedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return species.Id;
    }

    private static async Task<int> AddLocationAsync(ApplicationDbContext db, string ownerUserId)
    {
        var now = DateTime.UtcNow;
        var row = new Location
        {
            OwnerUserId = ownerUserId,
            Name = "Import Home",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Locations.Add(row);
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

    private static async Task AddPhotoAsync(ApplicationDbContext db, int sightingId, string sha256)
    {
        db.SightingPhotos.Add(new SightingPhoto
        {
            SightingId = sightingId,
            StoredPath = $"App_Data/photos/{Guid.NewGuid():N}.jpg",
            OriginalFileName = "existing.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 10,
            CreatedAtUtc = DateTime.UtcNow,
            ContentSha256Hex = sha256
        });
        await db.SaveChangesAsync();
    }

    private sealed class NullRecognitionService : IAnimalRecognitionService
    {
        public Task<RecognitionResponse?> RecognizeAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult<RecognitionResponse?>(null);
    }

    private sealed class TestBrowserFile(string name, string contentType, byte[] bytes) : IBrowserFile
    {
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public string Name { get; } = name;
        public long Size => bytes.Length;
        public string ContentType { get; } = contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
                throw new IOException("Max file size exceeded.");
            return new MemoryStream(bytes, writable: false);
        }
    }
}
