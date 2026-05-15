namespace Timesheets.Web.Services;

public class EmailOptions
{
    public string FromAddress { get; set; } = "no-reply@timeflow.local";
    public string FromName { get; set; } = "TimeFlow";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
}
