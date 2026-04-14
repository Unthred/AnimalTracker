using AnimalTracker.Data;
using AnimalTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed record SpeciesUsageRow(int Id, string Name, int AnimalCount, int SightingCount);

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

    public async Task<List<SpeciesUsageRow>> ListWithUsageAsync(CancellationToken cancellationToken = default)
    {
        var species = await db.Species.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken);
        var list = new List<SpeciesUsageRow>(species.Count);
        foreach (var s in species)
        {
            var ac = await db.Animals.AsNoTracking().CountAsync(a => a.SpeciesId == s.Id, cancellationToken);
            var sc = await db.Sightings.AsNoTracking().CountAsync(si => si.SpeciesId == s.Id, cancellationToken);
            list.Add(new SpeciesUsageRow(s.Id, s.Name, ac, sc));
        }

        return list;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (await db.Animals.AnyAsync(a => a.SpeciesId == id, cancellationToken)
            || await db.Sightings.AnyAsync(s => s.SpeciesId == id, cancellationToken))
        {
            throw new InvalidOperationException("This species is still used by animals or sightings and cannot be deleted.");
        }

        var row = await db.Species.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Species not found.");

        db.Species.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
    }
}

