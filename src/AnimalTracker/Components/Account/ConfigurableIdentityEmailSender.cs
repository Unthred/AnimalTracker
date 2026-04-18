using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using AnimalTracker.Data;
using AnimalTracker.Services;

namespace AnimalTracker.Components.Account;

internal sealed class ConfigurableIdentityEmailSender(
    EmailSettingsService emailSettings,
    SmtpIdentityEmailSender smtpSender,
    IdentityNoOpEmailSender fallbackSender) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendEmailAsync(
            email,
            "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password using the following code: {resetCode}");

    private async Task SendEmailAsync(string email, string subject, string htmlBody)
    {
        var options = await emailSettings.GetEffectiveOptionsAsync();
        if (options is null)
        {
            await fallbackSender.SendEmailAsync(email, subject, htmlBody);
            return;
        }

        await smtpSender.SendEmailAsync(options, email, subject, htmlBody);
    }
}
