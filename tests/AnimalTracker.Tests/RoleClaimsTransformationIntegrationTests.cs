using System.Security.Claims;
using AnimalTracker.Data;
using AnimalTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AnimalTracker.Tests;

public sealed class RoleClaimsTransformationIntegrationTests : IClassFixture<AnimalTrackerWebAppFactory>
{
    private readonly AnimalTrackerWebAppFactory _factory;

    public RoleClaimsTransformationIntegrationTests(AnimalTrackerWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task TransformAsync_adds_admin_role_claim_when_user_is_admin_in_db()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await EnsureUserAsync(userManager, "claims-admin@example.com", "test-user-claims-admin");
        if (!await userManager.IsInRoleAsync(user, AdminUserService.AdminRoleName))
            await userManager.AddToRoleAsync(user, AdminUserService.AdminRoleName);

        var transformation = new RoleClaimsTransformation(userManager);
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id)],
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var updated = await transformation.TransformAsync(principal);

        Assert.Contains(updated.Claims, c =>
            c.Type == ClaimTypes.Role
            && string.Equals(c.Value, AdminUserService.AdminRoleName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransformAsync_removes_admin_role_claim_when_user_not_admin_in_db()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await EnsureUserAsync(userManager, "claims-nonadmin@example.com", "test-user-claims-nonadmin");
        if (await userManager.IsInRoleAsync(user, AdminUserService.AdminRoleName))
            await userManager.RemoveFromRoleAsync(user, AdminUserService.AdminRoleName);

        var transformation = new RoleClaimsTransformation(userManager);
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Role, AdminUserService.AdminRoleName)
            ],
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var updated = await transformation.TransformAsync(principal);

        Assert.DoesNotContain(updated.Claims, c =>
            c.Type == ClaimTypes.Role
            && string.Equals(c.Value, AdminUserService.AdminRoleName, StringComparison.Ordinal));
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string id)
    {
        var existing = await userManager.FindByIdAsync(id);
        if (existing is not null)
            return existing;

        var user = new ApplicationUser
        {
            Id = id,
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, "Temp1234!temp");
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return user;
    }
}

