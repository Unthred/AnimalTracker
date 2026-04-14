using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace AnimalTracker.Services;

public sealed class CurrentUserService(AuthenticationStateProvider authStateProvider, IHttpContextAccessor httpContextAccessor)
{
    public async Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Minimal APIs, <img src="...">, and other HTTP endpoints have no Blazor AuthenticationStateProvider scope.
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
        {
            var id = httpUser.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(id))
                return id;
        }

        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("User must be authenticated.");

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim.");
    }
}
