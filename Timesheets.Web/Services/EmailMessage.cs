namespace Timesheets.Web.Services;

public class EmailMessage
{
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class EmailSendResult
{
    public bool Sent { get; set; }
    public string? Error { get; set; }

    public static EmailSendResult Success()
    {
        return new EmailSendResult { Sent = true };
    }

    public static EmailSendResult Failure(string error)
    {
        return new EmailSendResult { Sent = false, Error = error };
    }
}
