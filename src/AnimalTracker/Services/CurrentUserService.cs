using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace AnimalTracker.Services;

public sealed class CurrentUserService(AuthenticationStateProvider authStateProvider)
{
    public async Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken = default)
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var user = authState.User;
        if (user.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("User must be authenticated.");

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim.");
    }
}

