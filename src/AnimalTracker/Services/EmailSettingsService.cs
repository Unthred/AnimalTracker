using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AnimalTracker.Services;

public sealed record EmailSettingsStatus(
    string Source,
    bool IsEnabled,
    bool IsConfigured,
    bool UsesStoredSettings,
    string Message);

public sealed class EmailSettingsService(
    ApplicationDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<SmtpEmailOptions> fallbackOptions)
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("AnimalTracker.EmailSettings.v1");

    public async Task<SmtpEmailOptions> GetStoredOptionsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);

        return new SmtpEmailOptions
        {
            Enabled = settings.EmailEnabled,
            Host = settings.EmailHost ?? "",
            Port = settings.EmailPort ?? 587,
            UserName = Unprotect(settings.EmailUserNameProtected),
            Password = Unprotect(settings.EmailPasswordProtected),
            FromEmail = settings.EmailFromEmail ?? "",
            FromName = string.IsNullOrWhiteSpace(settings.EmailFromName) ? "AnimalTracker" : settings.EmailFromName!,
            EnableSsl = settings.EmailEnableSsl
        };
    }

    public async Task<bool> HasStoredPasswordAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(settings.EmailPasswordProtected);
    }

    public async Task SaveStoredOptionsAsync(
        SmtpEmailOptions options,
        bool keepExistingPassword,
        bool clearStoredPassword,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);

        settings.EmailEnabled = options.Enabled;
        settings.EmailHost = Normalize(options.Host);
        settings.EmailPort = options.Port;
        settings.EmailFromEmail = Normalize(options.FromEmail);
        settings.EmailFromName = Normalize(options.FromName) ?? "AnimalTracker";
        settings.EmailEnableSsl = options.EnableSsl;
        settings.EmailUserNameProtected = ProtectOrNull(options.UserName);

        if (clearStoredPassword)
        {
            settings.EmailPasswordProtected = null;
        }
        else if (!keepExistingPassword || !string.IsNullOrWhiteSpace(options.Password))
        {
            settings.EmailPasswordProtected = ProtectOrNull(options.Password);
        }

        settings.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SmtpEmailOptions?> GetEffectiveOptionsAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredOptionsAsync(cancellationToken);
        if (stored.IsConfigured)
            return stored;

        var fallback = fallbackOptions.Value;
        return fallback.IsConfigured
            ? new SmtpEmailOptions
            {
                Enabled = fallback.Enabled,
                Host = fallback.Host,
                Port = fallback.Port,
                UserName = fallback.UserName,
                Password = fallback.Password,
                FromEmail = fallback.FromEmail,
                FromName = fallback.FromName,
                EnableSsl = fallback.EnableSsl
            }
            : null;
    }

    public async Task<EmailSettingsStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredOptionsAsync(cancellationToken);
        if (stored.IsConfigured)
        {
            return new EmailSettingsStatus(
                Source: "Admin settings",
                IsEnabled: true,
                IsConfigured: true,
                UsesStoredSettings: true,
                Message: $"Email delivery is active via the admin SMTP settings for host {stored.Host}:{stored.Port}.");
        }

        var fallback = fallbackOptions.Value;

        if (stored.Enabled && fallback.IsConfigured)
        {
            return new EmailSettingsStatus(
                Source: "Environment",
                IsEnabled: true,
                IsConfigured: true,
                UsesStoredSettings: false,
                Message: $"Admin SMTP settings are enabled but incomplete, so environment SMTP settings for host {fallback.Host}:{fallback.Port} are currently being used.");
        }

        if (stored.Enabled)
        {
            return new EmailSettingsStatus(
                Source: "Admin settings",
                IsEnabled: true,
                IsConfigured: false,
                UsesStoredSettings: true,
                Message: "Admin email delivery is enabled but incomplete. Complete the SMTP settings or disable it.");
        }

        if (fallback.IsConfigured)
        {
            return new EmailSettingsStatus(
                Source: "Environment",
                IsEnabled: true,
                IsConfigured: true,
                UsesStoredSettings: false,
                Message: $"Email delivery is using environment SMTP settings for host {fallback.Host}:{fallback.Port}.");
        }

        return new EmailSettingsStatus(
            Source: "Fallback",
            IsEnabled: false,
            IsConfigured: false,
            UsesStoredSettings: false,
            Message: "Email delivery is not configured. Auth pages will show generated links instead.");
    }

    private async Task<AppSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var existing = await db.AppSettings
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var created = new AppSettings
        {
            DefaultThemeMode = "system",
            EmailEnableSsl = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.AppSettings.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            db.Entry(created).State = EntityState.Detached;
            var canonical = await db.AppSettings
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (canonical is null)
                throw;

            return canonical;
        }
    }

    private string? ProtectOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : protector.Protect(value.Trim());

    private string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
            return null;

        try
        {
            return protector.Unprotect(protectedValue);
        }
        catch
        {
            return null;
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
