using AnimalTracker.Components.Pages.Admin;
using AnimalTracker.Data;
using AnimalTracker.Services;
using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace AnimalTracker.Tests.Ui;

public sealed class AdminUsersPageTests : BunitTestBase
{
    [Fact]
    public async Task Renders_users_list_and_disables_delete_for_admin_user()
    {
        await using var scope = await CreateServiceScopeAsync(makeAdmin: true);
        RegisterServices(scope.ServiceProvider);

        var cut = RenderComponent<Users>();

        cut.Markup.Contains("All users", StringComparison.OrdinalIgnoreCase);
        cut.Markup.Contains("Admin", StringComparison.OrdinalIgnoreCase);

        var deleteButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Delete", StringComparison.OrdinalIgnoreCase));

        Assert.True(deleteButton.HasAttribute("disabled"));
    }

    private async Task<AsyncServiceScope> CreateServiceScopeAsync(bool makeAdmin)
    {
        var services = new ServiceCollection();

        var root = Path.Combine(Path.GetTempPath(), $"animaltracker-bunit-admin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "app.db");

        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(root));
        services.AddSingleton<ICurrentUserAccessor>(new TestCurrentUserAccessor(DefaultUserId));
        services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(DefaultUserId));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlite($"Data Source={dbPath};Cache=Shared")
             .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<PhotoStorageService>();
        services.AddScoped<AppSettingsService>();
        services.AddScoped<AdminUserService>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Seed user and optional admin role.
        var user = await userManager.FindByIdAsync(DefaultUserId);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = DefaultUserId,
                UserName = "admin@example.com",
                Email = "admin@example.com",
                EmailConfirmed = true
            };
            var created = await userManager.CreateAsync(user, "Temp1234!temp");
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        if (makeAdmin)
        {
            if (!await roleManager.RoleExistsAsync(AdminUserService.AdminRoleName))
            {
                var r = await roleManager.CreateAsync(new IdentityRole(AdminUserService.AdminRoleName));
                if (!r.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", r.Errors.Select(e => e.Description)));
            }

            if (!await userManager.IsInRoleAsync(user, AdminUserService.AdminRoleName))
            {
                var r = await userManager.AddToRoleAsync(user, AdminUserService.AdminRoleName);
                if (!r.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", r.Errors.Select(e => e.Description)));
            }
        }

        // Ensure at least one AppSettings row exists for the page.
        if (!await db.AppSettings.AnyAsync())
        {
            db.AppSettings.Add(new AnimalTracker.Data.Entities.AppSettings
            {
                DefaultThemeMode = "system",
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
        Services.AddSingleton(sp.GetRequiredService<PhotoStorageService>());
        Services.AddSingleton(sp.GetRequiredService<AppSettingsService>());
        Services.AddSingleton(sp.GetRequiredService<AdminUserService>());
    }
}

