using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AnimalTracker.Services;

public sealed record StoredPhoto(
    string StoredRelativePath,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hex);

public sealed class PhotoStorageService(IWebHostEnvironment env, CurrentUserService currentUser)
{
    // Upload limits are about protecting server resources; we still optimize images after upload.
    public const long DefaultMaxUploadBytes = 25 * 1024 * 1024; // 25 MB (sighting photos)
    private const long BytesPerMb = 1024 * 1024;
    private const int DefaultSightingMaxDimensionPx = 2048;
    private const int DefaultBackgroundMaxDimensionPx = 2560;
    private const int DefaultJpegQuality = 82;

    public async Task<StoredPhoto> SaveSightingPhotoAsync(
        IBrowserFile file,
        long maxBytes = DefaultMaxUploadBytes,
        CancellationToken cancellationToken = default)
    {
        if (file.Size <= 0)
            throw new ArgumentException("File is empty.", nameof(file));
        if (file.Size > maxBytes)
            throw new ArgumentException($"File too large (max {(maxBytes / BytesPerMb)} MB).", nameof(file));

        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var photosRoot = Path.Combine(env.ContentRootPath, "App_Data", "photos");

        var now = DateTimeOffset.UtcNow;
        var dir = Path.Combine(photosRoot, userId, now.Year.ToString("0000"), now.Month.ToString("00"));
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}.jpg";
        var absPath = Path.Combine(dir, fileName);

        var originalName = Path.GetFileName(file.Name);
        var stored = await SaveOptimizedJpegAsync(
            file,
            absPath,
            maxAllowedBytes: maxBytes,
            maxDimensionPx: DefaultSightingMaxDimensionPx,
            jpegQuality: DefaultJpegQuality,
            cancellationToken: cancellationToken);

        var relative = Path.GetRelativePath(env.ContentRootPath, absPath).Replace('\\', '/');
        return new StoredPhoto(
            StoredRelativePath: relative,
            OriginalFileName: originalName,
            ContentType: "image/jpeg",
            SizeBytes: stored.SizeBytes,
            Sha256Hex: stored.Sha256Hex);
    }

    public FileStream OpenRead(string storedRelativePath)
    {
        var abs = Path.GetFullPath(Path.Combine(env.ContentRootPath, storedRelativePath));
        var root = Path.GetFullPath(env.ContentRootPath);
        if (!abs.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid stored photo path.");

        return new FileStream(abs, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>
    /// Deletes a file under the content root if it exists. Returns false if path is invalid or missing.
    /// </summary>
    public bool TryDeleteStoredFile(string? storedRelativePath)
    {
        if (string.IsNullOrWhiteSpace(storedRelativePath))
            return false;

        var abs = Path.GetFullPath(Path.Combine(env.ContentRootPath, storedRelativePath));
        var root = Path.GetFullPath(env.ContentRootPath);
        if (!abs.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            if (File.Exists(abs))
            {
                File.Delete(abs);
                return true;
            }
        }
        catch
        {
            /* ignore */
        }

        return false;
    }

    public const long DefaultMaxBackgroundBytes = 15 * 1024 * 1024; // 15 MB

    /// <summary>
    /// Saves a user background image under App_Data/backgrounds/{userId}/ (JPEG/PNG/GIF/WebP only).
    /// </summary>
    public async Task<StoredPhoto> SaveBackgroundImageAsync(
        IBrowserFile file,
        long maxBytes = DefaultMaxBackgroundBytes,
        CancellationToken cancellationToken = default)
    {
        if (GuessExtension(file.ContentType) is null)
            throw new ArgumentException("Background must be a JPEG, PNG, GIF, or WebP image.", nameof(file));

        if (file.Size <= 0)
            throw new ArgumentException("File is empty.", nameof(file));
        if (file.Size > maxBytes)
            throw new ArgumentException($"Background image is too large. Max {(maxBytes / BytesPerMb)} MB.", nameof(file));

        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var dir = Path.Combine(env.ContentRootPath, "App_Data", "backgrounds", userId);
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}.jpg";
        var absPath = Path.Combine(dir, fileName);

        var originalName = Path.GetFileName(file.Name);
        var stored = await SaveOptimizedJpegAsync(
            file,
            absPath,
            maxAllowedBytes: maxBytes,
            maxDimensionPx: DefaultBackgroundMaxDimensionPx,
            jpegQuality: DefaultJpegQuality,
            cancellationToken: cancellationToken);

        var relative = Path.GetRelativePath(env.ContentRootPath, absPath).Replace('\\', '/');
        return new StoredPhoto(
            StoredRelativePath: relative,
            OriginalFileName: originalName,
            ContentType: "image/jpeg",
            SizeBytes: stored.SizeBytes,
            Sha256Hex: stored.Sha256Hex);
    }

    private static string? GuessExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => null
        };

    public static string GetContentTypeForStoredPath(string relativePath)
    {
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }

    private sealed record StoredFileResult(long SizeBytes, string Sha256Hex);

    private static async Task<StoredFileResult> SaveOptimizedJpegAsync(
        IBrowserFile file,
        string absPath,
        long maxAllowedBytes,
        int maxDimensionPx,
        int jpegQuality,
        CancellationToken cancellationToken)
    {
        // Enforce we only attempt to decode known image types (avoids surprising CPU/memory).
        if (GuessExtension(file.ContentType) is null)
            throw new ArgumentException("Unsupported image type. Use JPEG, PNG, GIF, or WebP.", nameof(file));

        await using var input = file.OpenReadStream(maxAllowedSize: maxAllowedBytes, cancellationToken: cancellationToken);

        Image image;
        try
        {
            image = await Image.LoadAsync(input, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Could not read image: {ex.Message}", nameof(file));
        }

        using var _ = image; // ensure dispose even if encode fails

        // If animated (GIF/WebP), keep only the first frame for now.
        while (image.Frames.Count > 1)
            image.Frames.RemoveFrame(1);

        // Strip metadata (privacy + smaller files).
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        var maxSide = Math.Max(image.Width, image.Height);
        if (maxSide > maxDimensionPx)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxDimensionPx, maxDimensionPx),
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);

        await using var output = new FileStream(absPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024, useAsync: true);
        using var sha = SHA256.Create();
        await using var crypto = new CryptoStream(output, sha, CryptoStreamMode.Write, leaveOpen: true);

        var encoder = new JpegEncoder
        {
            Quality = Math.Clamp(jpegQuality, 40, 95),
        };

        await image.SaveAsJpegAsync(crypto, encoder, cancellationToken);
        await crypto.FlushAsync(cancellationToken);
        crypto.FlushFinalBlock();

        var size = output.Length;
        var hashHex = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        return new StoredFileResult(SizeBytes: size, Sha256Hex: hashHex);
    }
}

