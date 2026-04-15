using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Forms;

namespace AnimalTracker.Services;

public sealed record StoredPhoto(
    string StoredRelativePath,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hex);

public sealed class PhotoStorageService(IWebHostEnvironment env, CurrentUserService currentUser)
{
    public const long DefaultMaxUploadBytes = 10 * 1024 * 1024; // 10 MB
    private const long BytesPerMb = 1024 * 1024;

    public async Task<StoredPhoto> SaveAsync(
        IBrowserFile file,
        long maxBytes = DefaultMaxUploadBytes,
        CancellationToken cancellationToken = default)
    {
        if (file.Size <= 0)
            throw new ArgumentException("File is empty.", nameof(file));
        if (file.Size > maxBytes)
            throw new ArgumentException($"File too large (max {maxBytes} bytes).", nameof(file));

        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var photosRoot = Path.Combine(env.ContentRootPath, "App_Data", "photos");

        var now = DateTimeOffset.UtcNow;
        var dir = Path.Combine(photosRoot, userId, now.Year.ToString("0000"), now.Month.ToString("00"));
        Directory.CreateDirectory(dir);

        var safeExt = GuessExtension(file.ContentType) ?? Path.GetExtension(file.Name);
        if (safeExt.Length > 10) safeExt = "";
        if (safeExt.Length > 0 && !safeExt.StartsWith('.')) safeExt = "." + safeExt;

        var fileName = $"{Guid.NewGuid():N}{safeExt}";
        var absPath = Path.Combine(dir, fileName);

        await using var input = file.OpenReadStream(maxAllowedSize: maxBytes, cancellationToken: cancellationToken);
        await using var output = new FileStream(absPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024, useAsync: true);

        using var sha = SHA256.Create();
        var buffer = new byte[64 * 1024];
        int read;
        long total = 0;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sha.TransformBlock(buffer, 0, read, null, 0);
            total += read;
        }
        sha.TransformFinalBlock([], 0, 0);

        var relative = Path.GetRelativePath(env.ContentRootPath, absPath).Replace('\\', '/');
        return new StoredPhoto(
            StoredRelativePath: relative,
            OriginalFileName: Path.GetFileName(file.Name),
            ContentType: file.ContentType,
            SizeBytes: total,
            Sha256Hex: Convert.ToHexString(sha.Hash!).ToLowerInvariant());
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

    public const long DefaultMaxBackgroundBytes = 5 * 1024 * 1024;

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

        var safeExt = GuessExtension(file.ContentType) ?? ".jpg";
        var fileName = $"{Guid.NewGuid():N}{safeExt}";
        var absPath = Path.Combine(dir, fileName);

        await using var input = file.OpenReadStream(maxAllowedSize: maxBytes, cancellationToken: cancellationToken);
        await using var output = new FileStream(absPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024, useAsync: true);

        using var sha = SHA256.Create();
        var buffer = new byte[64 * 1024];
        int read;
        long total = 0;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sha.TransformBlock(buffer, 0, read, null, 0);
            total += read;
        }
        sha.TransformFinalBlock([], 0, 0);

        var relative = Path.GetRelativePath(env.ContentRootPath, absPath).Replace('\\', '/');
        return new StoredPhoto(
            StoredRelativePath: relative,
            OriginalFileName: Path.GetFileName(file.Name),
            ContentType: file.ContentType,
            SizeBytes: total,
            Sha256Hex: Convert.ToHexString(sha.Hash!).ToLowerInvariant());
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
}

