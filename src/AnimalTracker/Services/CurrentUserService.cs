using System.Security.Claims;
using AnimalTracker.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace AnimalTracker.Services;

public sealed class CurrentUserService(
    AuthenticationStateProvider authStateProvider,
    IHttpContextAccessor httpContextAccessor,
    UserManager<ApplicationUser> userManager) : ICurrentUserAccessor
{
    private static readonly string[] UserIdClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
    ];

    public async Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // NOTE:
        // - Blazor component code runs in a circuit and can use AuthenticationStateProvider.
        // - Minimal APIs and plain HTTP requests (e.g. <img src="...">) do NOT have that circuit scope.
        //   If we call AuthenticationStateProvider there, it throws.
        // Prefer HttpContext when available, and fall back to the circuit when not.
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
        {
            var resolved = await ResolveUserIdFromPrincipalAsync(httpUser);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;

            var id = FindFirstClaimValue(httpUser, UserIdClaimTypes);
            if (!string.IsNullOrEmpty(id))
                return id;
        }

        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("User must be authenticated.");

        var resolvedFromAuthState = await ResolveUserIdFromPrincipalAsync(user);
        if (!string.IsNullOrWhiteSpace(resolvedFromAuthState))
            return resolvedFromAuthState;

        return FindFirstClaimValue(user, UserIdClaimTypes)
            ?? throw new InvalidOperationException("Authenticated user has no resolvable user id claim.");
    }

    private static string? FindFirstClaimValue(ClaimsPrincipal principal, IEnumerable<string> claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private async Task<string?> ResolveUserIdFromPrincipalAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is not null)
            return user.Id;

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(email))
        {
            user = await userManager.FindByEmailAsync(email);
            if (user is not null)
                return user.Id;
        }

        var name = principal.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            user = await userManager.FindByNameAsync(name);
            if (user is not null)
                return user.Id;
        }

        return null;
    }
}
