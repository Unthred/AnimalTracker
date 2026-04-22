using AnimalTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AnimalTracker.Tests;

public sealed class SqliteServiceTestFixture : IAsyncLifetime
{
    public const string PrimaryUserId = "test-user-1";
    public const string SecondaryUserId = "test-user-2";

    private string _rootDir = string.Empty;
    private readonly List<string> _dbPaths = [];

    public Task InitializeAsync()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"animaltracker-service-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        return Task.CompletedTask;
    }

    public async Task<ApplicationDbContext> CreateContextAsync()
    {
        var dbPath = Path.Combine(_rootDir, $"{Guid.NewGuid():N}.db");
        _dbPaths.Add(dbPath);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={dbPath};Cache=Shared")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        await EnsureUserSettingsColumnsAsync(db);
        await EnsureAppSettingsColumnsAsync(db);
        await SeedUsersAsync(db);
        return db;
    }

    public TestCurrentUserAccessor CreatePrimaryUserAccessor() => new(PrimaryUserId);

    public TestCurrentUserAccessor CreateSecondaryUserAccessor() => new(SecondaryUserId);

    public TestWebHostEnvironment CreateWebHostEnvironment()
    {
        var contentRoot = Path.Combine(_rootDir, "content");
        Directory.CreateDirectory(contentRoot);
        return new TestWebHostEnvironment(contentRoot);
    }

    public async Task DisposeAsync()
    {
        await Task.Yield();
        foreach (var path in _dbPaths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                /* ignore */
            }
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_rootDir) && Directory.Exists(_rootDir))
                Directory.Delete(_rootDir, recursive: true);
        }
        catch
        {
            /* ignore */
        }
    }

    private static async Task SeedUsersAsync(ApplicationDbContext db)
    {
        if (await db.Users.AnyAsync(u => u.Id == PrimaryUserId || u.Id == SecondaryUserId))
            return;

        db.Users.AddRange(
            new ApplicationUser
            {
                Id = PrimaryUserId,
                UserName = "primary@example.com",
                NormalizedUserName = "PRIMARY@EXAMPLE.COM",
                Email = "primary@example.com",
                NormalizedEmail = "PRIMARY@EXAMPLE.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            },
            new ApplicationUser
            {
                Id = SecondaryUserId,
                UserName = "secondary@example.com",
                NormalizedUserName = "SECONDARY@EXAMPLE.COM",
                Email = "secondary@example.com",
                NormalizedEmail = "SECONDARY@EXAMPLE.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });

        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserSettingsColumnsAsync(ApplicationDbContext db)
    {
        var commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BackgroundImageRelativePath"] = "ALTER TABLE UserSettings ADD COLUMN BackgroundImageRelativePath TEXT NULL",
            ["ThemeMode"] = "ALTER TABLE UserSettings ADD COLUMN ThemeMode TEXT NOT NULL DEFAULT 'system'",
            ["SurfaceOpacityPercent"] = "ALTER TABLE UserSettings ADD COLUMN SurfaceOpacityPercent INTEGER NOT NULL DEFAULT 93",
            ["DarkSurfaceOpacityPercent"] = "ALTER TABLE UserSettings ADD COLUMN DarkSurfaceOpacityPercent INTEGER NOT NULL DEFAULT 50",
        };

        foreach (var (column, sql) in commands)
        {
            if (!await HasColumnAsync(db, "UserSettings", column))
                await db.Database.ExecuteSqlRawAsync(sql);
        }
    }

    private static async Task EnsureAppSettingsColumnsAsync(ApplicationDbContext db)
    {
        var commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ActiveSpeciesRegionKey"] = "ALTER TABLE AppSettings ADD COLUMN ActiveSpeciesRegionKey TEXT NULL",
            ["ActiveSpeciesRegionName"] = "ALTER TABLE AppSettings ADD COLUMN ActiveSpeciesRegionName TEXT NULL",
            ["EmailEnabled"] = "ALTER TABLE AppSettings ADD COLUMN EmailEnabled INTEGER NOT NULL DEFAULT 0",
            ["EmailHost"] = "ALTER TABLE AppSettings ADD COLUMN EmailHost TEXT NULL",
            ["EmailPort"] = "ALTER TABLE AppSettings ADD COLUMN EmailPort INTEGER NULL DEFAULT 587",
            ["EmailUserNameProtected"] = "ALTER TABLE AppSettings ADD COLUMN EmailUserNameProtected TEXT NULL",
            ["EmailPasswordProtected"] = "ALTER TABLE AppSettings ADD COLUMN EmailPasswordProtected TEXT NULL",
            ["EmailFromEmail"] = "ALTER TABLE AppSettings ADD COLUMN EmailFromEmail TEXT NULL",
            ["EmailFromName"] = "ALTER TABLE AppSettings ADD COLUMN EmailFromName TEXT NULL DEFAULT 'AnimalTracker'",
            ["EmailEnableSsl"] = "ALTER TABLE AppSettings ADD COLUMN EmailEnableSsl INTEGER NOT NULL DEFAULT 1",
        };

        foreach (var (column, sql) in commands)
        {
            if (!await HasColumnAsync(db, "AppSettings", column))
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
}
