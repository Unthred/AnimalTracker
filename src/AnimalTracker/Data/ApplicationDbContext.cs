using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AnimalTracker.Data.Entities;

namespace AnimalTracker.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Species> Species => Set<Species>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<Sighting> Sightings => Set<Sighting>();
    public DbSet<SightingPhoto> SightingPhotos => Set<SightingPhoto>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

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

            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<SightingPhoto>(e =>
        {
            e.HasIndex(x => x.SightingId);
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Entity<UserSettings>(e =>
        {
            e.HasIndex(x => x.OwnerUserId).IsUnique();
            e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
