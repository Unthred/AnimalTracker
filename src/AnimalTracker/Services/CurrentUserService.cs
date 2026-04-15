using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace AnimalTracker.Services;

public sealed class CurrentUserService(AuthenticationStateProvider authStateProvider, IHttpContextAccessor httpContextAccessor)
{
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
