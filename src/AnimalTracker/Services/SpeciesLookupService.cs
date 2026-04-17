using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using AnimalTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed record SpeciesLookupItem(
    int Id,
    string Name,
    string? ScientificName,
    string? ImageUrl,
    string? Description,
    string? CatalogSource,
    string? CatalogSourceId);

public sealed record SpeciesLookupDetails(
    int Id,
    string Name,
    string? ScientificName,
    string? ImageUrl,
    string? Description,
    string? ActiveTimesSummary,
    string? NestingPlacesSummary,
    string? BehaviourTraitsSummary,
    string? HabitatSummary,
    string? HabitsSummary,
    string? TaxonGroup,
    string? ConservationStatus,
    int? ObservationsCount,
    string? WikipediaUrl,
    string? SourceLabel,
    string? SourceUrl,
    string? ImageLicense,
    string? ImageAttribution);

public sealed class SpeciesLookupService(
    ApplicationDbContext db,
    AppSettingsService appSettings,
    IHttpClientFactory httpClientFactory,
    ILogger<SpeciesLookupService> logger)
{
    private const string INaturalistSourceName = "iNaturalist";
    private static readonly string[] HabitatKeywords =
    [
        "habitat", "habitats", "forest", "woodland", "grassland", "wetland", "marsh", "moor",
        "coast", "coastal", "river", "lake", "urban", "countryside", "mountain", "scrub", "heath"
    ];
    private static readonly string[] HabitsKeywords =
    [
        "nocturnal", "diurnal", "feeds", "feeding", "diet", "forage", "foraging", "hunt", "hunting",
        "social", "solitary", "migrate", "migration", "breed", "breeding", "nest", "behavior", "behaviour"
    ];
    private static readonly string[] ActiveTimesKeywords =
    [
        "nocturnal", "diurnal", "crepuscular", "cathemeral", "active at night", "active during the day",
        "dusk", "dawn", "night", "daytime"
    ];
    private static readonly string[] NestingKeywords =
    [
        "nest", "nests", "nesting", "burrow", "den", "tree hollow", "cavity", "reedbed", "ground nest",
        "hedgerow", "scrub", "cliff", "bank"
    ];
    private static readonly string[] BehaviourTraitsKeywords =
    [
        "solitary", "social", "territorial", "aggressive", "shy", "elusive", "migratory", "sedentary",
        "foraging", "hunting", "ambush", "opportunistic", "pair bond", "monogamous", "gregarious"
    ];

    public async Task<List<SpeciesLookupItem>> SearchActiveRegionAsync(
        string? query,
        int take = 30,
        CancellationToken cancellationToken = default)
    {
        var activeRegionKey = await GetActiveRegionKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(activeRegionKey))
            return [];

        var trimmed = (query ?? string.Empty).Trim();
        take = Math.Clamp(take, 1, 100);

        var regionSpecies = db.SpeciesRegionCaches
            .AsNoTracking()
            .Where(x => x.RegionKey == activeRegionKey)
            .Select(x => x.Species);

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            regionSpecies = regionSpecies.Where(x =>
                EF.Functions.Like(x.Name, $"%{trimmed}%")
                || (x.ScientificName != null && EF.Functions.Like(x.ScientificName, $"%{trimmed}%")));
        }

        return await regionSpecies
            .OrderBy(x => x.Name)
            .Select(x => new SpeciesLookupItem(
                x.Id,
                x.Name,
                x.ScientificName,
                x.ImageUrl,
                x.Description,
                x.CatalogSource,
                x.CatalogSourceId))
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<SpeciesLookupDetails?> GetDetailsAsync(int speciesId, CancellationToken cancellationToken = default)
    {
        var activeRegionKey = await GetActiveRegionKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(activeRegionKey))
            return null;

        var species = await db.SpeciesRegionCaches
            .AsNoTracking()
            .Where(x => x.RegionKey == activeRegionKey && x.SpeciesId == speciesId)
            .Select(x => x.Species)
            .FirstOrDefaultAsync(cancellationToken);
        if (species is null)
            return null;

        var sourceUrl = BuildSourceUrl(species.CatalogSource, species.CatalogSourceId);
        var description = CleanSummaryText(species.Description);
        var imageUrl = species.ImageUrl;
        var scientificName = species.ScientificName;
        string? taxonGroup = null;
        string? conservationStatus = null;
        int? observationsCount = null;
        string? wikipediaUrl = null;

        if (string.Equals(species.CatalogSource, INaturalistSourceName, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(species.CatalogSourceId, out var taxonId)
            && (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(scientificName)))
        {
            var enrichment = await TryLoadINaturalistTaxonAsync(taxonId, cancellationToken);
            if (enrichment is not null)
            {
                description = string.IsNullOrWhiteSpace(description) ? enrichment.Description : description;
                imageUrl = string.IsNullOrWhiteSpace(imageUrl) ? enrichment.ImageUrl : imageUrl;
                scientificName = string.IsNullOrWhiteSpace(scientificName) ? enrichment.ScientificName : scientificName;
                sourceUrl ??= enrichment.SourceUrl;
                taxonGroup = enrichment.TaxonGroup;
                conservationStatus = enrichment.ConservationStatus;
                observationsCount = enrichment.ObservationsCount;
                wikipediaUrl = enrichment.WikipediaUrl;
            }
        }

        var habitatSummary = ExtractThematicSnippet(description, HabitatKeywords);
        var habitsSummary = ExtractThematicSnippet(description, HabitsKeywords);
        var activeTimesSummary = ExtractThematicSnippet(description, ActiveTimesKeywords);
        var nestingPlacesSummary = ExtractThematicSnippet(description, NestingKeywords);
        var behaviourTraitsSummary = ExtractThematicSnippet(description, BehaviourTraitsKeywords);

        return new SpeciesLookupDetails(
            species.Id,
            species.Name,
            scientificName,
            imageUrl,
            description,
            activeTimesSummary,
            nestingPlacesSummary,
            behaviourTraitsSummary,
            habitatSummary,
            habitsSummary,
            taxonGroup,
            conservationStatus,
            observationsCount,
            wikipediaUrl,
            species.CatalogSource,
            sourceUrl,
            species.ImageLicense,
            species.ImageAttribution);
    }

    private async Task<string?> GetActiveRegionKeyAsync(CancellationToken cancellationToken)
    {
        var settings = await appSettings.GetOrCreateAsync(cancellationToken);
        return settings.ActiveSpeciesRegionKey;
    }

    private async Task<INaturalistTaxonDetails?> TryLoadINaturalistTaxonAsync(int taxonId, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(SpeciesCatalogSyncService));
            var response = await client.GetFromJsonAsync<INaturalistTaxaResponse>(
                $"v1/taxa/{taxonId}",
                cancellationToken);

            var taxon = response?.Results?.FirstOrDefault();
            if (taxon is null)
                return null;

            return new INaturalistTaxonDetails(
                ScientificName: taxon.Name,
                ImageUrl: taxon.DefaultPhoto?.MediumUrl,
                Description: CleanSummaryText(taxon.WikipediaSummary),
                TaxonGroup: taxon.IconicTaxonName,
                ConservationStatus: taxon.ConservationStatus?.StatusName,
                ObservationsCount: taxon.ObservationsCount,
                WikipediaUrl: taxon.WikipediaUrl,
                SourceUrl: $"https://www.inaturalist.org/taxa/{taxon.Id}");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Species detail lookup failed for taxon id {TaxonId}.", taxonId);
            return null;
        }
    }

    private static string? BuildSourceUrl(string? catalogSource, string? sourceId)
    {
        if (!string.Equals(catalogSource, INaturalistSourceName, StringComparison.OrdinalIgnoreCase))
            return null;
        return int.TryParse(sourceId, out var taxonId) && taxonId > 0
            ? $"https://www.inaturalist.org/taxa/{taxonId}"
            : null;
    }

    private static string? CleanSummaryText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var withoutTags = Regex.Replace(text, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var normalizedWhitespace = Regex.Replace(decoded, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(normalizedWhitespace) ? null : normalizedWhitespace;
    }

    private sealed record INaturalistTaxonDetails(
        string? ScientificName,
        string? ImageUrl,
        string? Description,
        string? TaxonGroup,
        string? ConservationStatus,
        int? ObservationsCount,
        string? WikipediaUrl,
        string? SourceUrl);

    private static string? ExtractThematicSnippet(string? description, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var sentences = Regex.Split(description.Trim(), @"(?<=[.!?])\s+")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (sentences.Count == 0)
            return null;

        var matches = sentences
            .Where(sentence => keywords.Any(keyword => sentence.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .Take(2)
            .ToList();
        if (matches.Count == 0)
            return null;

        return string.Join(" ", matches);
    }

    private sealed class INaturalistTaxaResponse
    {
        [JsonPropertyName("results")]
        public List<INaturalistTaxon>? Results { get; set; }
    }

    private sealed class INaturalistTaxon
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("wikipedia_summary")]
        public string? WikipediaSummary { get; set; }

        [JsonPropertyName("iconic_taxon_name")]
        public string? IconicTaxonName { get; set; }

        [JsonPropertyName("observations_count")]
        public int? ObservationsCount { get; set; }

        [JsonPropertyName("wikipedia_url")]
        public string? WikipediaUrl { get; set; }

        [JsonPropertyName("conservation_status")]
        public INaturalistConservationStatus? ConservationStatus { get; set; }

        [JsonPropertyName("default_photo")]
        public INaturalistPhoto? DefaultPhoto { get; set; }
    }

    private sealed class INaturalistConservationStatus
    {
        [JsonPropertyName("status_name")]
        public string? StatusName { get; set; }
    }

    private sealed class INaturalistPhoto
    {
        [JsonPropertyName("medium_url")]
        public string? MediumUrl { get; set; }
    }
}
