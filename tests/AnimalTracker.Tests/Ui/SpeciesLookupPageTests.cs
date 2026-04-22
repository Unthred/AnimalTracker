using AnimalTracker.Components.Pages;
using AnimalTracker.Data;
using AnimalTracker.Services;
using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimalTracker.Tests.Ui;

public sealed class SpeciesLookupPageTests : BunitTestBase
{
    [Fact]
    public void Shows_prompt_when_no_active_region()
    {
        using var scope = CreateServiceScope(activeRegionKey: null, activeRegionName: null);
        RegisterServices(scope.ServiceProvider);

        var cut = RenderComponent<AnimalTracker.Components.Pages.Index>();
        cut.Markup.Contains("Select a species region in Settings first.", StringComparison.OrdinalIgnoreCase);
    }

    private IServiceScope CreateServiceScope(string? activeRegionKey, string? activeRegionName)
    {
        var services = new ServiceCollection();

        var root = Path.Combine(Path.GetTempPath(), $"animaltracker-bunit-species-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "app.db");

        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(root));
        services.AddSingleton<ICurrentUserAccessor>(new TestCurrentUserAccessor(DefaultUserId));
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlite($"Data Source={dbPath};Cache=Shared")
             .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IHttpClientFactory>(new DummyHttpClientFactory());

        services.AddScoped<PhotoStorageService>();
        services.AddScoped<AppSettingsService>();
        services.AddScoped<SpeciesLookupService>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        // Seed a settings row with no active region.
        if (!db.AppSettings.Any())
        {
            db.AppSettings.Add(new AnimalTracker.Data.Entities.AppSettings
            {
                DefaultThemeMode = "system",
                ActiveSpeciesRegionKey = activeRegionKey,
                ActiveSpeciesRegionName = activeRegionName,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        return scope;
    }

    private void RegisterServices(IServiceProvider sp)
    {
        Services.AddSingleton(sp.GetRequiredService<ApplicationDbContext>());
        Services.AddSingleton(sp.GetRequiredService<PhotoStorageService>());
        Services.AddSingleton(sp.GetRequiredService<AppSettingsService>());
        Services.AddSingleton(sp.GetRequiredService<SpeciesLookupService>());
    }

    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new HttpClientHandler()) { BaseAddress = new Uri("http://test/") };
    }
}

