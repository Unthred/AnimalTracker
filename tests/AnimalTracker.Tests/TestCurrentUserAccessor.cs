using AnimalTracker.Services;

namespace AnimalTracker.Tests;

public sealed class TestCurrentUserAccessor(string userId) : ICurrentUserAccessor
{
    public string UserId { get; set; } = userId;

    public Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(UserId);
    }
}
