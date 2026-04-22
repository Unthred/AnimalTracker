namespace AnimalTracker.Services;

public interface ICurrentUserAccessor
{
    Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken = default);
}
