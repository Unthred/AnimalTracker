using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AnimalTracker.Components;
using AnimalTracker.Components.Account;
using AnimalTracker.Data;
using AnimalTracker.Services;
using AnimalTracker.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

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

builder.Services.ConfigureApplicationCookie(options =>
{
    // Public internet: always require HTTPS at the edge/proxy.
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

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
        // Internet-facing defaults: lockout can be used by admins and also limits brute force.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 10;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    // IMPORTANT: AddRoles must come before AddEntityFrameworkStores so EF registers IRoleStore.
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Persist keys so auth cookies survive container restarts.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")))
    .SetApplicationName("AnimalTracker");

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
builder.Services.AddScoped<AdminUserService>();

var app = builder.Build();

// Ensure schema + seed data exist for dev/MVP.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await EnsureSqliteUserSettingsColumnsAsync(db);
    var adminUsers = scope.ServiceProvider.GetRequiredService<AdminUserService>();
    await adminUsers.EnsureAdminRoleAsync();
    await EnsureAdminUserAsync(scope.ServiceProvider);

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
// Reverse proxy (HAProxy) support: required for correct scheme/IP and Secure cookies.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // In homelab/reverse-proxy setups we typically want to trust the proxy chain.
    // Restrict KnownNetworks/KnownProxies if you have static proxy IPs.
    KnownIPNetworks = { },
    KnownProxies = { }
});
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

static async Task EnsureSqliteUserSettingsColumnsAsync(ApplicationDbContext db)
{
    // Defensive schema repair:
    // We had earlier migrations created/removed during development; a local DB can end up missing
    // new columns even if the migration history looks "up to date". These ALTERs are safe to run
    // on SQLite at startup: they either succeed or we ignore "duplicate column" errors.
    if (!db.Database.IsSqlite())
        return;

    async Task TryAddAsync(string sql)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch
        {
            // Ignore (e.g. column already exists).
        }
    }

    await TryAddAsync("ALTER TABLE UserSettings ADD COLUMN BackgroundImageRelativePath TEXT NULL");
    await TryAddAsync("ALTER TABLE UserSettings ADD COLUMN ThemeMode TEXT NOT NULL DEFAULT 'system'");
}

static async Task EnsureAdminUserAsync(IServiceProvider sp)
{
    var config = sp.GetRequiredService<IConfiguration>();
    var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

    var email = config["AnimalTracker:AdminEmail"] ?? config["ADMIN_EMAIL"];
    var password = config["AnimalTracker:AdminPassword"] ?? config["ADMIN_PASSWORD"];
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        return; // No admin configured.

    var existing = await userManager.FindByEmailAsync(email);
    if (existing is null)
    {
        var created = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(created, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        existing = created;
    }

    if (!await userManager.IsInRoleAsync(existing, AdminUserService.AdminRoleName))
    {
        var result = await userManager.AddToRoleAsync(existing, AdminUserService.AdminRoleName);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
