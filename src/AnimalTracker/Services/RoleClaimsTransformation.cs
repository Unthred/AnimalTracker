using System.Security.Claims;
using AnimalTracker.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace AnimalTracker.Services;

/// <summary>
/// Keeps role claims aligned with the database even when an existing auth cookie is stale.
/// This avoids local-dev lockouts after role changes (e.g. promoting an existing user to Admin).
/// </summary>
public sealed class RoleClaimsTransformation(UserManager<ApplicationUser> userManager) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        var identity = principal.Identities.FirstOrDefault(i => i.IsAuthenticated);
        if (identity is null)
            return principal;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return principal;

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return principal;

        var isAdminInDb = await userManager.IsInRoleAsync(user, AdminUserService.AdminRoleName);
        var hasAdminClaim = principal.Claims.Any(c =>
            c.Type == ClaimTypes.Role &&
            string.Equals(c.Value, AdminUserService.AdminRoleName, StringComparison.Ordinal));

        if (isAdminInDb && !hasAdminClaim)
            identity.AddClaim(new Claim(ClaimTypes.Role, AdminUserService.AdminRoleName));

        if (!isAdminInDb && hasAdminClaim)
        {
            foreach (var claim in identity.Claims
                         .Where(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, AdminUserService.AdminRoleName, StringComparison.Ordinal))
                         .ToList())
            {
                identity.RemoveClaim(claim);
            }
        }

        return principal;
    }
}
