using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed record SpeciesRegionOption(string RegionKey, string RegionName, int PlaceId);
public sealed record SpeciesSyncResult(string RegionKey, string RegionName, int UpdatedCount, bool UsedCache);

public sealed class SpeciesCatalogSyncService(
    ApplicationDbContext db,
    AppSettingsService appSettings,
    IHttpClientFactory httpClientFactory,
    ILogger<SpeciesCatalogSyncService> logger)
{
    private const string SourceName = "iNaturalist";
    private const string RegionKeyPrefix = "inat-place:";
    private const int SpeciesCountsPageSize = 200;
    private const int SpeciesCountsMaxPages = 25;
    private static readonly HashSet<string> AllowedAnimalIconicTaxa = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mammalia",
        "Aves",
        "Reptilia",
        "Amphibia",
        "Actinopterygii",
        "Insecta",
        "Arachnida",
        "Mollusca",
        "Animalia"
    };

    public async Task<List<SpeciesRegionOption>> SearchRegionsAsync(string query, CancellationToken cancellationToken = default)
    {
        query = (query ?? "").Trim();
        if (query.Length < 2)
            return [];

        try
        {
            var client = httpClientFactory.CreateClient(nameof(SpeciesCatalogSyncService));
            var response = await client.GetFromJsonAsync<INaturalistPlacesResponse>(
                $"v1/places/autocomplete?q={Uri.EscapeDataString(query)}&per_page=12",
                cancellationToken);

            return response?.Results?
                .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.DisplayName))
                .Select(x => new SpeciesRegionOption($"{RegionKeyPrefix}{x.Id}", x.DisplayName!, x.Id))
                .DistinctBy(x => x.RegionKey)
                .ToList()
                ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Region lookup failed for query '{Query}'.", query);
            return [];
        }
    }

    public async Task<SpeciesSyncResult?> SyncActiveRegionAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var settings = await appSettings.GetOrCreateAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ActiveSpeciesRegionKey) || string.IsNullOrWhiteSpace(settings.ActiveSpeciesRegionName))
            return null;

        return await SyncRegionAsync(
            settings.ActiveSpeciesRegionKey,
            settings.ActiveSpeciesRegionName,
            forceRefresh,
            cancellationToken);
    }

    public async Task<SpeciesSyncResult> SyncRegionAsync(
        string regionKey,
        string regionName,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryParsePlaceId(regionKey, out var placeId))
            throw new InvalidOperationException("Invalid species region key.");

        if (!forceRefresh)
        {
            var hasCache = await db.SpeciesRegionCaches
                .AsNoTracking()
                .AnyAsync(x => x.RegionKey == regionKey, cancellationToken);
            if (hasCache)
                return new SpeciesSyncResult(regionKey, regionName, UpdatedCount: 0, UsedCache: true);
        }

        var imported = await TryLoadRegionSpeciesAsync(placeId, cancellationToken);
        if (imported.Count == 0)
            imported = GetFallbackCatalog();

        var existingBySourceId = await db.Species
            .Where(x => x.CatalogSource == SourceName && x.CatalogSourceId != null)
            .ToDictionaryAsync(x => x.CatalogSourceId!, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingByName = await db.Species
            .ToDictionaryAsync(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        if (forceRefresh)
        {
            await db.SpeciesRegionCaches
                .Where(x => x.RegionKey == regionKey)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var cacheSet = await db.SpeciesRegionCaches
            .Where(x => x.RegionKey == regionKey)
            .Select(x => x.SpeciesId)
            .ToHashSetAsync(cancellationToken);

        var changedCount = 0;
        foreach (var item in imported)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                continue;

            Species? entity = null;
            if (!string.IsNullOrWhiteSpace(item.SourceId))
                existingBySourceId.TryGetValue(item.SourceId, out entity);
            if (entity is null)
                existingByName.TryGetValue(item.Name, out entity);

            if (entity is null)
            {
                entity = new Species
                {
                    Name = item.Name.Trim(),
                    ScientificName = item.ScientificName?.Trim(),
                    ImageUrl = item.ImageUrl?.Trim(),
                    ImageLicense = item.ImageLicense?.Trim(),
                    ImageAttribution = item.ImageAttribution?.Trim(),
                    CatalogSource = SourceName,
                    CatalogSourceId = item.SourceId?.Trim(),
                    Description = null
                };
                db.Species.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
                existingByName[entity.Name] = entity;
                if (!string.IsNullOrWhiteSpace(entity.CatalogSourceId))
                    existingBySourceId[entity.CatalogSourceId] = entity;
                changedCount++;
            }
            else
            {
                var wasChanged = false;
                wasChanged |= ApplyIfDifferent(entity.ScientificName, item.ScientificName?.Trim(), v => entity.ScientificName = v);
                wasChanged |= ApplyIfDifferent(entity.ImageUrl, item.ImageUrl?.Trim(), v => entity.ImageUrl = v);
                wasChanged |= ApplyIfDifferent(entity.ImageLicense, item.ImageLicense?.Trim(), v => entity.ImageLicense = v);
                wasChanged |= ApplyIfDifferent(entity.ImageAttribution, item.ImageAttribution?.Trim(), v => entity.ImageAttribution = v);
                wasChanged |= ApplyIfDifferent(entity.CatalogSource, SourceName, v => entity.CatalogSource = v);
                wasChanged |= ApplyIfDifferent(entity.CatalogSourceId, item.SourceId?.Trim(), v => entity.CatalogSourceId = v);
                if (wasChanged)
                    changedCount++;
            }

            if (!cacheSet.Contains(entity.Id))
            {
                db.SpeciesRegionCaches.Add(new SpeciesRegionCache
                {
                    RegionKey = regionKey,
                    RegionName = regionName,
                    SpeciesId = entity.Id,
                    SyncedAtUtc = DateTime.UtcNow
                });
                cacheSet.Add(entity.Id);
                changedCount++;
            }
        }

        if (changedCount > 0)
            await db.SaveChangesAsync(cancellationToken);

        return new SpeciesSyncResult(regionKey, regionName, UpdatedCount: changedCount, UsedCache: false);
    }

    private async Task<List<ImportedSpecies>> TryLoadRegionSpeciesAsync(int placeId, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(SpeciesCatalogSyncService));
            var imported = new List<ImportedSpecies>(SpeciesCountsPageSize * 2);
            for (var page = 1; page <= SpeciesCountsMaxPages; page++)
            {
                var response = await client.GetFromJsonAsync<INaturalistSpeciesCountsResponse>(
                    $"v1/observations/species_counts?place_id={placeId}&verifiable=true&per_page={SpeciesCountsPageSize}&page={page}",
                    cancellationToken);

                var results = response?.Results ?? [];
                if (results.Count == 0)
                    break;

                imported.AddRange(results
                    .Select(x => x.Taxon)
                    .Where(t => t is not null)
                    .Where(t => !string.IsNullOrWhiteSpace(t!.IconicTaxonName)
                        && AllowedAnimalIconicTaxa.Contains(t.IconicTaxonName!))
                    .Select(x => new ImportedSpecies(
                        Name: string.IsNullOrWhiteSpace(x!.PreferredCommonName) ? x.Name : x.PreferredCommonName,
                        ScientificName: x.Name,
                        SourceId: x.Id.ToString(),
                        ImageUrl: x.DefaultPhoto?.MediumUrl,
                        ImageLicense: x.DefaultPhoto?.LicenseCode,
                        ImageAttribution: x.DefaultPhoto?.Attribution))
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name)));

                if (results.Count < SpeciesCountsPageSize)
                    break;
            }

            return imported
                .DistinctBy(x => x.SourceId ?? x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Species catalog sync failed for place id {PlaceId}. Using fallback list.", placeId);
            return [];
        }
    }

    private static List<ImportedSpecies> GetFallbackCatalog() =>
    [
        new("Fox", "Vulpes vulpes", "fallback-red-fox", null, null, null),
        new("Badger", "Meles meles", "fallback-badger", null, null, null),
        new("Hedgehog", "Erinaceus europaeus", "fallback-hedgehog", null, null, null),
        new("Rabbit", "Oryctolagus cuniculus", "fallback-rabbit", null, null, null),
        new("Squirrel", "Sciurus carolinensis", "fallback-squirrel", null, null, null),
        new("Robin", "Erithacus rubecula", "fallback-robin", null, null, null),
        new("Blackbird", "Turdus merula", "fallback-blackbird", null, null, null)
    ];

    private static bool TryParsePlaceId(string regionKey, out int placeId)
    {
        placeId = 0;
        if (!regionKey.StartsWith(RegionKeyPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(regionKey[RegionKeyPrefix.Length..], out placeId) && placeId > 0;
    }

    private static bool ApplyIfDifferent(string? current, string? next, Action<string?> apply)
    {
        if (string.Equals(current, next, StringComparison.Ordinal))
            return false;
        apply(next);
        return true;
    }

    private sealed record ImportedSpecies(
        string Name,
        string? ScientificName,
        string? SourceId,
        string? ImageUrl,
        string? ImageLicense,
        string? ImageAttribution);

    private sealed class INaturalistPlacesResponse
    {
        [JsonPropertyName("results")]
        public List<INaturalistPlace>? Results { get; set; }
    }

    private sealed class INaturalistPlace
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }

    private sealed class INaturalistSpeciesCountsResponse
    {
        [JsonPropertyName("results")]
        public List<INaturalistSpeciesCount>? Results { get; set; }
    }

    private sealed class INaturalistSpeciesCount
    {
        [JsonPropertyName("taxon")]
        public INaturalistTaxon? Taxon { get; set; }
    }

    private sealed class INaturalistTaxon
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("preferred_common_name")]
        public string? PreferredCommonName { get; set; }

        [JsonPropertyName("iconic_taxon_name")]
        public string? IconicTaxonName { get; set; }

        [JsonPropertyName("default_photo")]
        public INaturalistPhoto? DefaultPhoto { get; set; }
    }

    private sealed class INaturalistPhoto
    {
        [JsonPropertyName("medium_url")]
        public string? MediumUrl { get; set; }

        [JsonPropertyName("license_code")]
        public string? LicenseCode { get; set; }

        [JsonPropertyName("attribution")]
        public string? Attribution { get; set; }
    }
}
