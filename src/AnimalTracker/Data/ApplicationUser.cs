using Microsoft.AspNetCore.Identity;

namespace AnimalTracker.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public DateTime? LastLoginAtUtc { get; set; }
    public string? LastLoginIpAddress { get; set; }
}

