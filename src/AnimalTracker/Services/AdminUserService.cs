using System.Security.Cryptography;
using AnimalTracker.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AnimalTracker.Services;

public sealed record AdminUserRow(
    string Id,
    string Email,
    bool EmailConfirmed,
    bool IsAdmin,
    bool IsLockedOut);

public sealed class AdminUserService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    public const string AdminRoleName = "Admin";

    public async Task EnsureAdminRoleAsync(CancellationToken cancellationToken = default)
    {
        if (await roleManager.RoleExistsAsync(AdminRoleName))
            return;

        var result = await roleManager.CreateAsync(new IdentityRole(AdminRoleName));
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<List<AdminUserRow>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.Email ?? u.UserName)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.EmailConfirmed,
                u.LockoutEnd
            })
            .ToListAsync(cancellationToken);

        var list = new List<AdminUserRow>(users.Count);
        foreach (var u in users)
        {
            // UserManager.IsInRoleAsync requires a tracked user instance; fetch minimal user by id.
            var user = await userManager.FindByIdAsync(u.Id);
            var isAdmin = user is not null && await userManager.IsInRoleAsync(user, AdminRoleName);
            list.Add(new AdminUserRow(
                Id: u.Id,
                Email: u.Email ?? "(no email)",
                EmailConfirmed: u.EmailConfirmed,
                IsAdmin: isAdmin,
                IsLockedOut: u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow));
        }

        return list;
    }

    public async Task LockUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found.");
        await userManager.SetLockoutEnabledAsync(user, true);
        var result = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task UnlockUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found.");
        var result = await userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found.");
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<string> ResetPasswordAsync(string userId, string? newPassword = null, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found.");
        newPassword ??= GeneratePassword();
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        return newPassword;
    }

    private static string GeneratePassword()
    {
        // 20 chars base64-ish: strong enough and easy to paste.
        Span<byte> bytes = stackalloc byte[15];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "A").Replace("/", "b");
    }
}

