using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AnimalTracker.Components;
using AnimalTracker.Components.Account;
using AnimalTracker.Data;
using AnimalTracker.Services;
using AnimalTracker.State;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Use an absolute SQLite path so running from different working directories
// doesn't silently create multiple databases (which breaks login persistence).
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "Data", "app.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
var connectionString = $"Data Source={dbPath};Cache=Shared";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<SpeciesService>();
builder.Services.AddScoped<AnimalService>();
builder.Services.AddScoped<SightingService>();
builder.Services.AddScoped<PhotoStorageService>();
builder.Services.AddScoped<TimelineFilterState>();
builder.Services.AddScoped<UserSettingsService>();
builder.Services.AddScoped<UserPreferencesState>();

var app = builder.Build();

// Ensure schema + seed data exist for dev/MVP.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.Species.AsNoTracking().AnyAsync())
    {
        db.Species.AddRange(
            new AnimalTracker.Data.Entities.Species { Name = "Squirrel" },
            new AnimalTracker.Data.Entities.Species { Name = "Fox" },
            new AnimalTracker.Data.Entities.Species { Name = "Bird" },
            new AnimalTracker.Data.Entities.Species { Name = "Cat" },
            new AnimalTracker.Data.Entities.Species { Name = "Rabbit" }
        );
        await db.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/user/background-image", [Authorize] async (
    ApplicationDbContext db,
    CurrentUserService currentUser,
    PhotoStorageService storage,
    CancellationToken cancellationToken) =>
{
    var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
    var path = await db.UserSettings.AsNoTracking()
        .Where(x => x.OwnerUserId == userId)
        .Select(x => x.BackgroundImageRelativePath)
        .FirstOrDefaultAsync(cancellationToken);

    if (string.IsNullOrWhiteSpace(path))
        return Results.NotFound();

    var stream = storage.OpenRead(path);
    var contentType = PhotoStorageService.GetContentTypeForStoredPath(path);
    return Results.File(stream, contentType, enableRangeProcessing: true);
});

app.MapGet("/photos/{id:int}", [Authorize] async (
    int id,
    ApplicationDbContext db,
    CurrentUserService currentUser,
    PhotoStorageService storage,
    CancellationToken cancellationToken) =>
{
    var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
    var photo = await db.SightingPhotos
        .AsNoTracking()
        .Where(x => x.Id == id)
        .Include(x => x.Sighting)
        .Select(x => new
        {
            x.Id,
            x.StoredPath,
            x.ContentType,
            x.OriginalFileName,
            x.Sighting.OwnerUserId
        })
        .FirstOrDefaultAsync(cancellationToken);

    if (photo is null || photo.OwnerUserId != userId)
        return Results.NotFound();

    var stream = storage.OpenRead(photo.StoredPath);
    return Results.File(stream, contentType: photo.ContentType, fileDownloadName: photo.OriginalFileName);
});

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
