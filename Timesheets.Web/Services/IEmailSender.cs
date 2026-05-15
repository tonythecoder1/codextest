namespace Timesheets.Web.Services;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message);
}
