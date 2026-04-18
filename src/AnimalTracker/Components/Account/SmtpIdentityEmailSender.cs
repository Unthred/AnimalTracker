using AnimalTracker.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AnimalTracker.Components.Account;

internal sealed class SmtpIdentityEmailSender(
    ILogger<SmtpIdentityEmailSender> logger)
{
    public async Task SendEmailAsync(SmtpEmailOptions options, string toEmail, string subject, string htmlBody)
    {
        if (!options.IsConfigured)
            throw new InvalidOperationException("SMTP email delivery is not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        logger.LogInformation("Sending auth email '{Subject}' to {Email} via SMTP host {Host}:{Port}.",
            subject, toEmail, options.Host, options.Port);

        using var client = new SmtpClient
        {
            Timeout = 30000
        };

        var socketOptions = GetSecureSocketOptions(options);

        await client.ConnectAsync(options.Host, options.Port, socketOptions);

        if (!string.IsNullOrWhiteSpace(options.UserName))
            await client.AuthenticateAsync(options.UserName, options.Password ?? "");

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    private static SecureSocketOptions GetSecureSocketOptions(SmtpEmailOptions options) =>
        options.EnableSsl
            ? options.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls
            : SecureSocketOptions.None;
}
