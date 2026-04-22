using AnimalTracker.Components.Pages.Sightings;
using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using AnimalTracker.Services;
using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnimalTracker.Tests.Ui;

public sealed class ImportPageTests : BunitTestBase
{
    [Fact]
    public void Renders_header_and_import_button()
    {
        using var scope = CreateServiceScope();
        RegisterRealServices(scope.ServiceProvider);

        var cut = RenderComponent<Import>();

        cut.Markup.Contains("Import photos", StringComparison.OrdinalIgnoreCase);
        cut.Find("button[type=\"submit\"]").TextContent.Contains("Import", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Shows_validation_message_when_submitting_with_no_files()
    {
        using var scope = CreateServiceScope();
        RegisterRealServices(scope.ServiceProvider);

        var cut = RenderComponent<Import>();

        // Trigger submit without selecting files.
        cut.Find("form").Submit();

        await cut.InvokeAsync(() => Task.CompletedTask);
        cut.Markup.Contains("Select at least one image.", StringComparison.Ordinal);
    }

    private static IServiceScope CreateServiceScope()
    {
        var services = new ServiceCollection();

        var root = Path.Combine(Path.GetTempPath(), $"animaltracker-bunit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "app.db");

        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(root));
        services.AddSingleton<ICurrentUserAccessor>(new TestCurrentUserAccessor(DefaultUserId));
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlite($"Data Source={dbPath};Cache=Shared")
             .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<PhotoStorageService>();
        services.AddScoped<AppSettingsService>();
        services.AddScoped<LocationService>();
        services.AddScoped<SpeciesService>();
        services.AddScoped<SightingService>();
        services.AddScoped<ExifMetadataService>();
        services.AddScoped<IAnimalRecognitionService, NullRecognitionService>();
        services.AddSingleton<IOptions<RecognitionOptions>>(Options.Create(new RecognitionOptions()));
        services.AddSingleton<IOptions<PhotoImportOptions>>(Options.Create(new PhotoImportOptions()));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<PhotoImportService>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        return scope;
    }

    private void RegisterRealServices(IServiceProvider sp)
    {
        Services.AddSingleton(sp.GetRequiredService<ApplicationDbContext>());
        Services.AddSingleton(sp.GetRequiredService<PhotoStorageService>());
        Services.AddSingleton(sp.GetRequiredService<AppSettingsService>());
        Services.AddSingleton(sp.GetRequiredService<LocationService>());
        Services.AddSingleton(sp.GetRequiredService<SpeciesService>());
        Services.AddSingleton(sp.GetRequiredService<PhotoImportService>());
    }

    private sealed class NullRecognitionService : IAnimalRecognitionService
    {
        public Task<RecognitionResponse?> RecognizeAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult<RecognitionResponse?>(null);
    }
}

