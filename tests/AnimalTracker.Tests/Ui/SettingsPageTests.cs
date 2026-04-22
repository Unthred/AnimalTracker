using AnimalTracker.Components.Pages.Settings;
using AnimalTracker.Data;
using AnimalTracker.Services;
using AnimalTracker.State;
using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimalTracker.Tests.Ui;

public sealed class SettingsPageTests : BunitTestBase
{
    [Fact]
    public async Task Renders_user_preferences_card_and_background_section()
    {
        await using var scope = await CreateServiceScopeAsync();
        RegisterServices(scope.ServiceProvider);

        var cut = RenderComponent<User>();

        cut.Markup.Contains("User preferences", StringComparison.OrdinalIgnoreCase);
        cut.Markup.Contains("Background image", StringComparison.OrdinalIgnoreCase);
        cut.Find("#accent");
        cut.Find("#surfaceOpacity");
    }

    private async Task<AsyncServiceScope> CreateServiceScopeAsync()
    {
        var services = new ServiceCollection();

        var root = Path.Combine(Path.GetTempPath(), $"animaltracker-bunit-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "app.db");

        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(root));
        services.AddSingleton<ICurrentUserAccessor>(new TestCurrentUserAccessor(DefaultUserId));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlite($"Data Source={dbPath};Cache=Shared")
             .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddHttpClient(nameof(SpeciesCatalogSyncService), c => c.BaseAddress = new Uri("http://test/"));
        services.AddScoped<PhotoStorageService>();
        services.AddScoped<AppSettingsService>();
        services.AddScoped<LocationService>();
        services.AddScoped<SpeciesCatalogSyncService>();
        services.AddScoped<UserSettingsService>();

        services.AddSingleton(new UserPreferencesState());
        services.AddSingleton(new UiProgressState());

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await EnsureUserSettingsColumnsAsync(db);

        // Seed user settings so the page has data to render.
        if (!await db.UserSettings.AnyAsync())
        {
            db.UserSettings.Add(new AnimalTracker.Data.Entities.UserSettings
            {
                OwnerUserId = DefaultUserId,
                AccentColorHex = "#0f172a",
                CompactMode = false,
                TimelinePageSize = 50,
                ThemeMode = "system",
                SurfaceOpacityPercent = 93,
                DarkSurfaceOpacityPercent = 50,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        if (!await db.AppSettings.AnyAsync())
        {
            db.AppSettings.Add(new AnimalTracker.Data.Entities.AppSettings
            {
                DefaultThemeMode = "system",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return scope;
    }

    private static async Task EnsureUserSettingsColumnsAsync(ApplicationDbContext db)
    {
        if (!db.Database.IsSqlite())
            return;

        var cmds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BackgroundImageRelativePath"] = "ALTER TABLE UserSettings ADD COLUMN BackgroundImageRelativePath TEXT NULL",
            ["ThemeMode"] = "ALTER TABLE UserSettings ADD COLUMN ThemeMode TEXT NOT NULL DEFAULT 'system'",
            ["SurfaceOpacityPercent"] = "ALTER TABLE UserSettings ADD COLUMN SurfaceOpacityPercent INTEGER NOT NULL DEFAULT 93",
            ["DarkSurfaceOpacityPercent"] = "ALTER TABLE UserSettings ADD COLUMN DarkSurfaceOpacityPercent INTEGER NOT NULL DEFAULT 50",
        };

        foreach (var (col, sql) in cmds)
        {
            if (!await HasColumnAsync(db, "UserSettings", col))
                await db.Database.ExecuteSqlRawAsync(sql);
        }
    }

    private static async Task<bool> HasColumnAsync(ApplicationDbContext db, string tableName, string columnName)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{tableName}');";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader["name"]?.ToString();
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void RegisterServices(IServiceProvider sp)
    {
        Services.AddSingleton(sp.GetRequiredService<ApplicationDbContext>());
        Services.AddSingleton(sp.GetRequiredService<PhotoStorageService>());
        Services.AddSingleton(sp.GetRequiredService<AppSettingsService>());
        Services.AddSingleton(sp.GetRequiredService<LocationService>());
        Services.AddSingleton(sp.GetRequiredService<SpeciesCatalogSyncService>());
        Services.AddSingleton(sp.GetRequiredService<UserSettingsService>());
        Services.AddSingleton(sp.GetRequiredService<UserPreferencesState>());
        Services.AddSingleton(sp.GetRequiredService<UiProgressState>());
    }
}

