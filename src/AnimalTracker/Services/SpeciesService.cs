using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed class SpeciesService(ApplicationDbContext db)
{
    public Task<List<Species>> GetAllAsync(CancellationToken cancellationToken = default) =>
        db.Species
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<Species> CreateAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 200)
            throw new ArgumentException("Species name is required (max 200 chars).", nameof(name));

        var entity = new Species
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        };

        db.Species.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }
}

