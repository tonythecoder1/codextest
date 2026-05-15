using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Timesheets.Web.Services;

public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task<EmailSendResult> SendAsync(EmailMessage message)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            logger.LogWarning("Email not sent because SMTP is not configured. Subject: {Subject}; To: {ToEmail}", message.Subject, message.ToEmail);
            return EmailSendResult.Failure("SMTP não configurado.");
        }

        try
        {
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false
            };

            mailMessage.To.Add(new MailAddress(message.ToEmail, message.ToName));

            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
            {
                client.Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword);
            }

            await client.SendMailAsync(mailMessage);
            return EmailSendResult.Success();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send email. Subject: {Subject}; To: {ToEmail}", message.Subject, message.ToEmail);
            return EmailSendResult.Failure(exception.Message);
        }
    }
}
