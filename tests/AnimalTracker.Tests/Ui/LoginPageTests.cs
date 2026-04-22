using AnimalTracker.Components.Account;
using AnimalTracker.Components.Account.Pages;
using AnimalTracker.Data;
using AnimalTracker.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace AnimalTracker.Tests.Ui;

public sealed class LoginPageTests : BunitTestBase
{
    [Fact]
    public async Task Renders_background_image_div_when_default_auth_image_configured()
    {
        await using var scope = await CreateServiceScopeAsync(defaultAuthImage: "App_Data/auth-page/bg.jpg");
        RegisterServices(scope.ServiceProvider);

        var ctx = CreateHttpContext();
        ctx.Request.Method = "POST"; // avoid external signout path

        var cut = RenderComponent<Login>(ps => ps.AddCascadingValue(ctx));

        // When configured, we render a bg div with style containing /app/default-auth-image.
        Assert.Contains("/app/default-auth-image", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<AsyncServiceScope> CreateServiceScopeAsync(string? defaultAuthImage)
    {
        var services = new ServiceCollection();

        var root = Path.Combine(Path.GetTempPath(), $"animaltracker-bunit-login-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "app.db");

        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(root));
        services.AddSingleton<ICurrentUserAccessor>(new TestCurrentUserAccessor(DefaultUserId));
        services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(DefaultUserId));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddHttpContextAccessor();
        services.AddAuthentication().AddCookie(IdentityConstants.ApplicationScheme);
        services.AddAuthorization();

        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlite($"Data Source={dbPath};Cache=Shared")
             .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddIdentityCore<ApplicationUser>(o => o.SignIn.RequireConfirmedAccount = false)
            .AddRoles<IdentityRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<PhotoStorageService>();
        services.AddScoped<AppSettingsService>();
        services.AddScoped<LoginAuditService>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.AppSettings.AnyAsync())
        {
            db.AppSettings.Add(new AnimalTracker.Data.Entities.AppSettings
            {
                DefaultThemeMode = "system",
                DefaultAuthImageRelativePath = defaultAuthImage,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return scope;
    }

    private void RegisterServices(IServiceProvider sp)
    {
        Services.AddSingleton(sp.GetRequiredService<ApplicationDbContext>());
        Services.AddSingleton(sp.GetRequiredService<AppSettingsService>());
        Services.AddSingleton(sp.GetRequiredService<LoginAuditService>());
        Services.AddSingleton(sp.GetRequiredService<UserManager<ApplicationUser>>());
        Services.AddSingleton(sp.GetRequiredService<SignInManager<ApplicationUser>>());
        Services.AddSingleton<IdentityRedirectManager>(sp2 =>
            new IdentityRedirectManager(sp2.GetRequiredService<NavigationManager>()));
    }
}

