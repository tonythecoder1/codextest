namespace Timesheets.Web.Models;

public static class TimesheetApprovalStatusExtensions
{
    public static string ToDisplayName(this TimesheetApprovalStatus status)
    {
        return status switch
        {
            TimesheetApprovalStatus.Pending => "Pendente",
            TimesheetApprovalStatus.Approved => "Aprovado",
            _ => status.ToString()
        };
    }
}
