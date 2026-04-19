using System.IO;
using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AnimalTracker.Services;

public sealed record PhotoImportBatchResult(
    int BatchId,
    int? FirstCreatedSightingId,
    int CreatedSightings,
    int PhotosAttached,
    int SkippedDuplicates,
    int FailedItems,
    int NeedsReviewItems,
    IReadOnlyList<string> Errors);

public sealed record PhotoImportProgress(
    string Stage,
    string StatusText,
    string? CurrentFileName,
    int Current,
    int Total,
    int Percent);

public sealed class PhotoImportService(
    ApplicationDbContext db,
    CurrentUserService currentUser,
    SightingService sightings,
    PhotoStorageService storage,
    ExifMetadataService exif,
    IAnimalRecognitionService recognition,
    SpeciesService speciesService,
    IOptions<RecognitionOptions> recognitionOptions,
    IOptions<PhotoImportOptions> importOptions,
    ILogger<PhotoImportService> logger)
{
    private const long MaxPhotoBytes = PhotoStorageService.DefaultMaxUploadBytes;

    public async Task<PhotoImportBatchResult> RunAsync(
        IReadOnlyList<IBrowserFile> files,
        int? fallbackSpeciesId,
        int? locationId,
        Func<PhotoImportProgress, Task>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
            throw new ArgumentException("No files selected.", nameof(files));

        await ReportProgressAsync(
            progress,
            stage: "Scanning photos",
            statusText: $"Scanning photo 1 of {files.Count}",
            currentFileName: files[0].Name,
            current: 0,
            total: files.Count);

        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var speciesList = await speciesService.GetAllAsync(cancellationToken);
        if (speciesList.Count == 0)
            throw new InvalidOperationException("No species available for your active region. Configure Settings first.");

        if (fallbackSpeciesId is not null &&
            !speciesList.Any(s => s.Id == fallbackSpeciesId.Value))
            throw new ArgumentException("Fallback species is not in the active species list.");

        var recOpts = recognitionOptions.Value;
        var impOpts = importOptions.Value;
        var timeWindow = Math.Clamp(impOpts.DedupeTimeWindowSeconds, 10, 3600);
        var distanceM = Math.Clamp(impOpts.DedupeDistanceMeters, 5, 5000);

        var batch = new PhotoImportBatch
        {
            OwnerUserId = userId,
            Status = PhotoImportBatchStatus.Running,
            CreatedAtUtc = DateTime.UtcNow,
            StartedAtUtc = DateTime.UtcNow,
            TotalItems = files.Count
        };
        db.PhotoImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        var workItems = new List<ImportWorkItem>();
        var errors = new List<string>();
        var skippedDup = 0;
        var failed = 0;
        var needsReview = 0;
        var fileIndex = 0;

        foreach (var file in files)
        {
            fileIndex++;

            await ReportProgressAsync(
                progress,
                stage: "Scanning photos",
                statusText: $"Scanning photo {fileIndex} of {files.Count}",
                currentFileName: file.Name,
                current: fileIndex - 1,
                total: files.Count);

            try
            {
                await using var raw = new MemoryStream();
                await using (var s = file.OpenReadStream(MaxPhotoBytes))
                {
                    await s.CopyToAsync(raw, cancellationToken);
                }

                var bytes = raw.ToArray();
                var sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

                var duplicate = await db.SightingPhotos
                    .AsNoTracking()
                    .AnyAsync(
                        p => p.ContentSha256Hex == sourceHash && p.Sighting.OwnerUserId == userId,
                        cancellationToken);

                if (duplicate)
                {
                    skippedDup++;
                    db.PhotoImportItems.Add(new PhotoImportItem
                    {
                        BatchId = batch.Id,
                        OriginalFileName = file.Name,
                        ContentSha256Hex = sourceHash,
                        Status = PhotoImportItemStatus.SkippedDuplicate
                    });
                    continue;
                }

                await using var exifStream = new MemoryStream(bytes, writable: false);
                var exifResult = await exif.TryExtractAsync(exifStream, cancellationToken);

                RecognitionResponse? recResponse = null;
                try
                {
                    await using var recStream = new MemoryStream(bytes, writable: false);
                    recResponse = await recognition.RecognizeAsync(recStream, file.Name, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Recognition failed for {File}", file.Name);
                }

                var (label, conf) = SpeciesMatching.GetBestRecognitionCandidate(recResponse);
                var matchedSpeciesId = SpeciesMatching.TryMatchSpeciesId(label, speciesList);

                var auto = recOpts.AutoAcceptThreshold;
                var review = recOpts.ReviewThreshold;

                int resolvedSpeciesId;
                var itemNeedsReview = false;
                if (matchedSpeciesId is not null && conf >= auto)
                {
                    resolvedSpeciesId = matchedSpeciesId.Value;
                }
                else if (matchedSpeciesId is not null && conf >= review)
                {
                    resolvedSpeciesId = matchedSpeciesId.Value;
                    itemNeedsReview = true;
                    needsReview++;
                }
                else if (fallbackSpeciesId is not null)
                {
                    resolvedSpeciesId = fallbackSpeciesId.Value;
                    itemNeedsReview = true;
                    needsReview++;
                }
                else
                {
                    failed++;
                    db.PhotoImportItems.Add(new PhotoImportItem
                    {
                        BatchId = batch.Id,
                        OriginalFileName = file.Name,
                        ContentSha256Hex = sourceHash,
                        Status = PhotoImportItemStatus.Failed,
                        RecognizedLabel = label,
                        CandidateConfidence = conf,
                        ErrorMessage = "Could not resolve species. Set a fallback species or configure recognition."
                    });
                    errors.Add($"{file.Name}: unresolved species (label={label}, confidence={conf:0.00}).");
                    continue;
                }

                var occurred = exifResult.CaptureUtc ?? DateTime.UtcNow;
                double? lat = exifResult.Latitude;
                double? lng = exifResult.Longitude;

                workItems.Add(new ImportWorkItem
                {
                    OriginalFileName = file.Name,
                    SourceBytes = bytes,
                    SourceSha256Hex = sourceHash,
                    SpeciesId = resolvedSpeciesId,
                    OccurredAtUtc = occurred,
                    ExifCaptureUtc = exifResult.CaptureUtc,
                    Latitude = lat,
                    Longitude = lng,
                    RecognitionConfidence = string.IsNullOrWhiteSpace(label) ? null : conf,
                    RecognizedLabel = label,
                    NeedsReview = itemNeedsReview
                });
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{file.Name}: {ex.Message}");
                logger.LogWarning(ex, "Import failed for {File}", file.Name);
            }
        }

        if (workItems.Count == 0)
        {
            await ReportProgressAsync(
                progress,
                stage: "Finishing import",
                statusText: skippedDup == files.Count
                    ? $"All {files.Count} selected photos were already imported."
                    : "No photos needed to be saved.",
                currentFileName: null,
                current: files.Count,
                total: files.Count);
        }

        var clusters = PhotoBurstClustering.Cluster(workItems, timeWindow, distanceM);
        var createdSightings = 0;
        var photosAttached = 0;
        int? firstSightingId = null;

        if (workItems.Count > 0)
        {
            await ReportProgressAsync(
                progress,
                stage: "Saving imported photos",
                statusText: $"Saving photo 1 of {workItems.Count}",
                currentFileName: workItems[0].OriginalFileName,
                current: 0,
                total: workItems.Count);
        }

        var savedPhotoIndex = 0;
        foreach (var cluster in clusters)
        {
            if (cluster.Count == 0)
                continue;

            var first = cluster.OrderBy(x => x.OccurredAtUtc).First();
            var last = cluster.OrderByDescending(x => x.OccurredAtUtc).First();
            var occurredAt = first.OccurredAtUtc;
            var lat = cluster.Select(x => x.Latitude).FirstOrDefault(x => x is not null);
            var lng = cluster.Select(x => x.Longitude).FirstOrDefault(x => x is not null);
            var speciesConf = cluster.Any(x => x.NeedsReview) ? SightingConfidence.Medium : SightingConfidence.High;

            var sighting = await sightings.CreateAsync(
                occurredAtUtc: occurredAt,
                speciesId: first.SpeciesId,
                locationId: locationId,
                animalId: null,
                notes: cluster.Count > 1 ? $"Imported burst ({cluster.Count} photos)." : "Imported from photos.",
                observedUntilUtc: cluster.Count > 1 ? last.OccurredAtUtc : null,
                latitude: lat,
                longitude: lng,
                locationAccuracyMeters: null,
                behavior: null,
                speciesConfidence: speciesConf,
                individualConfidence: null,
                cancellationToken: cancellationToken);

            createdSightings++;
            firstSightingId ??= sighting.Id;

            foreach (var item in cluster)
            {
                savedPhotoIndex++;

                await ReportProgressAsync(
                    progress,
                    stage: "Saving imported photos",
                    statusText: $"Saving photo {savedPhotoIndex} of {workItems.Count}",
                    currentFileName: item.OriginalFileName,
                    current: savedPhotoIndex - 1,
                    total: workItems.Count);

                await using var photoStream = new MemoryStream(item.SourceBytes, writable: false);
                var stored = await storage.SaveSightingPhotoFromStreamAsync(
                    photoStream,
                    item.OriginalFileName,
                    "image/jpeg",
                    MaxPhotoBytes,
                    cancellationToken);

                db.SightingPhotos.Add(new SightingPhoto
                {
                    SightingId = sighting.Id,
                    StoredPath = stored.StoredRelativePath,
                    OriginalFileName = stored.OriginalFileName,
                    ContentType = stored.ContentType,
                    SizeBytes = stored.SizeBytes,
                    CreatedAtUtc = DateTime.UtcNow,
                    ContentSha256Hex = stored.Sha256Hex,
                    OriginalCaptureUtc = item.ExifCaptureUtc ?? item.OccurredAtUtc,
                    OriginalLatitude = item.Latitude,
                    OriginalLongitude = item.Longitude
                });
                photosAttached++;

                db.PhotoImportItems.Add(new PhotoImportItem
                {
                    BatchId = batch.Id,
                    OriginalFileName = item.OriginalFileName,
                    ContentSha256Hex = item.SourceSha256Hex,
                    Status = item.NeedsReview ? PhotoImportItemStatus.NeedsReview : PhotoImportItemStatus.Created,
                    SpeciesId = item.SpeciesId,
                    CandidateConfidence = item.RecognitionConfidence,
                    RecognizedLabel = item.RecognizedLabel,
                    SightingId = sighting.Id
                });
            }
        }

        batch.Status = PhotoImportBatchStatus.Completed;
        batch.CompletedAtUtc = DateTime.UtcNow;
        batch.ProcessedItems = files.Count;
        batch.CreatedSightings = createdSightings;
        batch.SkippedDuplicates = skippedDup;
        batch.NeedsReviewCount = needsReview;
        await db.SaveChangesAsync(cancellationToken);

        await ReportProgressAsync(
            progress,
            stage: "Import complete",
            statusText: $"Processed {files.Count} photo(s).",
            currentFileName: null,
            current: Math.Max(workItems.Count, files.Count),
            total: Math.Max(workItems.Count, files.Count));

        return new PhotoImportBatchResult(
            batch.Id,
            firstSightingId,
            createdSightings,
            photosAttached,
            skippedDup,
            failed,
            needsReview,
            errors);
    }

    private static Task ReportProgressAsync(
        Func<PhotoImportProgress, Task>? progress,
        string stage,
        string statusText,
        string? currentFileName,
        int current,
        int total)
    {
        if (progress is null)
            return Task.CompletedTask;

        var safeTotal = Math.Max(total, 1);
        var safeCurrent = Math.Clamp(current, 0, safeTotal);
        var update = new PhotoImportProgress(
            stage,
            statusText,
            currentFileName,
            safeCurrent,
            safeTotal,
            (int)Math.Round((double)safeCurrent / safeTotal * 100, MidpointRounding.AwayFromZero));

        return progress(update);
    }
}
