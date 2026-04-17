using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AnimalTracker.Data.Entities;

namespace AnimalTracker.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Species> Species => Set<Species>();
    public DbSet<SpeciesRegionCache> SpeciesRegionCaches => Set<SpeciesRegionCache>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<Sighting> Sightings => Set<Sighting>();
    public DbSet<SightingPhoto> SightingPhotos => Set<SightingPhoto>();
    public DbSet<PhotoImportBatch> PhotoImportBatches => Set<PhotoImportBatch>();
    public DbSet<PhotoImportItem> PhotoImportItems => Set<PhotoImportItem>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Location>(e =>
        {
            e.HasIndex(x => new { x.OwnerUserId, x.Name }).IsUnique();
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<Species>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<SpeciesRegionCache>(e =>
        {
            e.HasIndex(x => new { x.RegionKey, x.SpeciesId }).IsUnique();
            e.HasIndex(x => x.RegionKey);
            e.Property(x => x.SyncedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<Animal>(e =>
        {
            e.HasIndex(x => new { x.OwnerUserId, x.SpeciesId, x.DisplayName });
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<Sighting>(e =>
        {
            e.HasIndex(x => new { x.OwnerUserId, x.OccurredAtUtc });
            e.HasIndex(x => new { x.OwnerUserId, x.SpeciesId, x.OccurredAtUtc });
            e.HasIndex(x => new { x.OwnerUserId, x.AnimalId, x.OccurredAtUtc });
            e.HasIndex(x => new { x.OwnerUserId, x.Latitude, x.Longitude });

            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.LocationAccuracyMeters).HasPrecision(8, 2);

            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<SightingPhoto>(e =>
        {
            e.HasIndex(x => x.SightingId);
            e.HasIndex(x => x.ContentSha256Hex);
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<PhotoImportBatch>(e =>
        {
            e.HasIndex(x => new { x.OwnerUserId, x.CreatedAtUtc });
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<PhotoImportItem>(e =>
        {
            e.HasIndex(x => x.BatchId);
            e.HasIndex(x => x.ContentSha256Hex);
            e.HasIndex(x => x.SightingId);
        });

        builder.Entity<UserSettings>(e =>
        {
            e.HasIndex(x => x.OwnerUserId).IsUnique();
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<AppSettings>(e =>
        {
            e.Property(x => x.DefaultThemeMode).HasDefaultValue("system");
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
