using AnimalTracker.Data;
using AnimalTracker.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AnimalTracker.Tests;

public sealed class EmailSettingsServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public EmailSettingsServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveStoredOptionsAsync_persists_and_protects_credentials()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = CreateService(db, fallback: new SmtpEmailOptions { Enabled = false });

        await service.SaveStoredOptionsAsync(
            new SmtpEmailOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                Port = 587,
                UserName = "user",
                Password = "pass",
                FromEmail = "from@example.com",
                FromName = "AT",
                EnableSsl = true
            },
            keepExistingPassword: false,
            clearStoredPassword: false);

        var row = await db.AppSettings.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
        Assert.True(row.EmailEnabled);
        Assert.Equal("smtp.example.com", row.EmailHost);
        Assert.NotNull(row.EmailUserNameProtected);
        Assert.NotNull(row.EmailPasswordProtected);
        Assert.DoesNotContain("user", row.EmailUserNameProtected!, StringComparison.Ordinal);
        Assert.DoesNotContain("pass", row.EmailPasswordProtected!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveStoredOptionsAsync_keep_existing_password_does_not_overwrite_when_blank()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = CreateService(db, fallback: new SmtpEmailOptions { Enabled = false });

        await service.SaveStoredOptionsAsync(
            new SmtpEmailOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                FromEmail = "from@example.com",
                Password = "initial"
            },
            keepExistingPassword: false,
            clearStoredPassword: false);

        var before = await db.AppSettings.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
        Assert.NotNull(before.EmailPasswordProtected);

        await service.SaveStoredOptionsAsync(
            new SmtpEmailOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                FromEmail = "from@example.com",
                Password = "" // blank should not overwrite
            },
            keepExistingPassword: true,
            clearStoredPassword: false);

        var after = await db.AppSettings.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(before.EmailPasswordProtected, after.EmailPasswordProtected);
    }

    [Fact]
    public async Task SaveStoredOptionsAsync_clear_password_removes_stored_password()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = CreateService(db, fallback: new SmtpEmailOptions { Enabled = false });

        await service.SaveStoredOptionsAsync(
            new SmtpEmailOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                FromEmail = "from@example.com",
                Password = "initial"
            },
            keepExistingPassword: false,
            clearStoredPassword: false);

        await service.SaveStoredOptionsAsync(
            new SmtpEmailOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                FromEmail = "from@example.com",
                Password = "" // ignored due to clearStoredPassword
            },
            keepExistingPassword: false,
            clearStoredPassword: true);

        var row = await db.AppSettings.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
        Assert.Null(row.EmailPasswordProtected);
    }

    [Fact]
    public async Task GetStatusAsync_prefers_stored_when_configured()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = CreateService(db, fallback: new SmtpEmailOptions { Enabled = true, Host = "fallback", FromEmail = "f@x.com" });

        await service.SaveStoredOptionsAsync(
            new SmtpEmailOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                FromEmail = "from@example.com",
                Password = "x"
            },
            keepExistingPassword: false,
            clearStoredPassword: false);

        var status = await service.GetStatusAsync();
        Assert.True(status.IsEnabled);
        Assert.True(status.IsConfigured);
        Assert.True(status.UsesStoredSettings);
    }

    [Fact]
    public async Task GetStatusAsync_uses_fallback_when_stored_enabled_but_incomplete_and_fallback_configured()
    {
        await using var db = await _fixture.CreateContextAsync();
        var service = CreateService(db, fallback: new SmtpEmailOptions { Enabled = true, Host = "fallback", FromEmail = "f@x.com" });

        await service.SaveStoredOptionsAsync(
            new SmtpEmailOptions
            {
                Enabled = true,
                Host = "", // incomplete
                FromEmail = "",
                Password = ""
            },
            keepExistingPassword: false,
            clearStoredPassword: true);

        var status = await service.GetStatusAsync();
        Assert.True(status.IsEnabled);
        Assert.True(status.IsConfigured);
        Assert.False(status.UsesStoredSettings);
    }

    private static EmailSettingsService CreateService(ApplicationDbContext db, SmtpEmailOptions fallback)
    {
        var dp = DataProtectionProvider.Create("AnimalTracker.Tests");
        return new EmailSettingsService(db, dp, Options.Create(fallback));
    }
}

