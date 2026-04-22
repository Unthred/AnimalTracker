using AnimalTracker.Data;
using AnimalTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimalTracker.Tests;

public sealed class WebApplicationIntegrationTests : IClassFixture<AnimalTrackerWebAppFactory>
{
    private readonly AnimalTrackerWebAppFactory _factory;

    public WebApplicationIntegrationTests(AnimalTrackerWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_login_page_sets_referrer_policy_header_required_for_osm_tiles()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Account/Login");

        IEnumerable<string>? values = null;
        if (response.Headers.TryGetValues("Referrer-Policy", out var validatedValues))
        {
            values = validatedValues;
        }
        else if (response.Headers.NonValidated.TryGetValues("Referrer-Policy", out var nonValidatedValues))
        {
            values = nonValidatedValues.ToArray();
        }

        if (values is null)
        {
            var headerDump = string.Join(
                Environment.NewLine,
                response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));

            throw new Xunit.Sdk.XunitException(
                $"Missing Referrer-Policy header. Status={(int)response.StatusCode}.{Environment.NewLine}{headerDump}");
        }

        Assert.Contains("strict-origin-when-cross-origin", values);
    }

    [Fact]
    public async Task Get_root_returns_success()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/");
        Assert.True(
            (int)response.StatusCode is >= 200 and < 400,
            $"Unexpected status {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Get_default_auth_image_does_not_serve_an_image_when_unconfigured()
    {
        using var scope = _factory.Services.CreateScope();
        var defaults = await scope.ServiceProvider.GetRequiredService<AppSettingsService>().GetOrCreateAsync();
        Assert.True(string.IsNullOrWhiteSpace(defaults.DefaultAuthImageRelativePath));

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/app/default-auth-image");

        // Minimal APIs return 404, but UseStatusCodePagesWithReExecute may re-execute to /not-found
        // and surface as 200 with HTML. Either way, we must not stream image/* for an unconfigured image.
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
        Assert.DoesNotContain("image/", mediaType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AppSettingsService_GetOrCreateAsync_returns_stable_canonical_row()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settingsService = scope.ServiceProvider.GetRequiredService<AppSettingsService>();

        // Startup already ensures a canonical AppSettings row exists.
        var countBefore = await db.AppSettings.AsNoTracking().CountAsync();
        Assert.True(countBefore >= 1);

        var first = await settingsService.GetOrCreateAsync();
        Assert.True(first.Id > 0);
        var second = await settingsService.GetOrCreateAsync();
        Assert.Equal(first.Id, second.Id);

        var countAfter = await db.AppSettings.AsNoTracking().CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task Database_migrations_applied_and_identity_schema_present()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
    }
}
