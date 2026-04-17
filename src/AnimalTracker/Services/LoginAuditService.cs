using AnimalTracker.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace AnimalTracker.Services;

public sealed class LoginAuditService(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task RecordSuccessfulLoginAsync(ApplicationUser user)
    {
        var remoteIp = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        user.LastLoginAtUtc = DateTime.UtcNow;
        user.LastLoginIpAddress = string.IsNullOrWhiteSpace(remoteIp) ? null : remoteIp;
        await userManager.UpdateAsync(user);
    }
}
