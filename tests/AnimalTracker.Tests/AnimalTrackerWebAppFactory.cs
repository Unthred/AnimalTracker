using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AnimalTracker.Tests;

/// <summary>
/// Hosts the real app with an isolated SQLite file so integration tests do not touch dev data.
/// </summary>
public sealed class AnimalTrackerWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"animaltracker-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Ensure this wins over appsettings / defaults so tests never touch the developer Data/app.db.
        builder.UseSetting("AnimalTracker:SqlitePath", _dbPath);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AnimalTracker:SqlitePath"] = _dbPath
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
            /* ignore */
        }
    }
}
