using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using AnimalTracker.Components;
using AnimalTracker.Components.Account;
using AnimalTracker.Data;
using AnimalTracker.Services;
using AnimalTracker.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

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
builder.Services.AddScoped<IClaimsTransformation, RoleClaimsTransformation>();

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
builder.Services.AddScoped<AppSettingsService>();

var ukCulture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = ukCulture;
CultureInfo.DefaultThreadCurrentUICulture = ukCulture;

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(ukCulture);
    options.SupportedCultures = [ukCulture];
    options.SupportedUICultures = [ukCulture];
});

var app = builder.Build();

// Ensure schema + seed data exist for dev/MVP.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await EnsureSqliteUserSettingsColumnsAsync(db);
    await EnsureSqliteAppSettingsTableAsync(db);
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
app.UseRequestLocalization();
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

// Local/dev resilience:
// when the DB is reset, an old auth cookie can reference a deleted user id.
// That breaks Account/Manage pages and can hide admin nav checks. Detect and
// clear stale cookies early so the user can re-authenticate cleanly.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
            context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
            return;
        }
    }

    await next();
});

// Keep the theme cookie aligned with effective preference on full-page requests.
// This ensures auth pages (excluded from interactive routing) render with the
// correct theme on first paint after login/logout/refresh.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method))
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isStaticAssetRequest =
            path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/photos", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/app/default-auth-image", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/user/background-image", StringComparison.OrdinalIgnoreCase);

        if (!isStaticAssetRequest)
        {
            var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();

            string? desiredThemeMode = null;
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is not null)
                {
                    desiredThemeMode = await db.UserSettings
                        .AsNoTracking()
                        .Where(x => x.OwnerUserId == user.Id)
                        .Select(x => x.ThemeMode)
                        .FirstOrDefaultAsync(context.RequestAborted);
                }
            }

            desiredThemeMode ??= await db.AppSettings
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => x.DefaultThemeMode)
                .FirstOrDefaultAsync(context.RequestAborted)
                ?? "system";

            desiredThemeMode = NormalizeThemeMode(desiredThemeMode);

            var currentThemeCookie = context.Request.Cookies["animaltracker_theme"];
            if (!string.Equals(currentThemeCookie, desiredThemeMode, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Cookies.Append("animaltracker_theme", desiredThemeMode, new CookieOptions
                {
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                    HttpOnly = false,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromDays(180),
                    Secure = context.Request.IsHttps
                });
            }
        }
    }

    await next();
});

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

app.MapGet("/app/default-auth-image", async (
    AppSettingsService settingsService,
    PhotoStorageService storage,
    CancellationToken cancellationToken) =>
{
    var settings = await settingsService.GetOrCreateAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(settings.DefaultAuthImageRelativePath))
        return Results.NotFound();

    var stream = storage.OpenRead(settings.DefaultAuthImageRelativePath);
    var contentType = PhotoStorageService.GetContentTypeForStoredPath(settings.DefaultAuthImageRelativePath);
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
    // on SQLite at startup, but we avoid attempting ALTER when the column already exists because
    // EF logs those failures loudly even if we catch the exception.
    if (!db.Database.IsSqlite())
        return;

    static async Task<HashSet<string>> GetColumnNamesAsync(ApplicationDbContext db)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('UserSettings');";

        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
            var name = reader["name"]?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                cols.Add(name);
        }

        return cols;
    }

    async Task TryAddAsync(string columnName, string sql)
    {
        var cols = await GetColumnNamesAsync(db);
        if (cols.Contains(columnName))
            return;

        await db.Database.ExecuteSqlRawAsync(sql);
    }

    await TryAddAsync("BackgroundImageRelativePath", "ALTER TABLE UserSettings ADD COLUMN BackgroundImageRelativePath TEXT NULL");
    await TryAddAsync("ThemeMode", "ALTER TABLE UserSettings ADD COLUMN ThemeMode TEXT NOT NULL DEFAULT 'system'");
    await TryAddAsync("SurfaceOpacityPercent", "ALTER TABLE UserSettings ADD COLUMN SurfaceOpacityPercent INTEGER NOT NULL DEFAULT 93");
    await TryAddAsync("DarkSurfaceOpacityPercent", "ALTER TABLE UserSettings ADD COLUMN DarkSurfaceOpacityPercent INTEGER NOT NULL DEFAULT 50");
}

static async Task EnsureSqliteAppSettingsTableAsync(ApplicationDbContext db)
{
    if (!db.Database.IsSqlite())
        return;

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS AppSettings (
            Id INTEGER NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY AUTOINCREMENT,
            DefaultThemeMode TEXT NOT NULL DEFAULT 'system',
            DefaultAuthImageRelativePath TEXT NULL,
            CreatedAtUtc TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
            UpdatedAtUtc TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP)
        );
        """);
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

static string NormalizeThemeMode(string? mode)
{
    var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
    return normalized is "light" or "dark" ? normalized : "system";
}
