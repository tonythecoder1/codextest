namespace Timesheets.Web.Models;

public static class TimesheetEntryTypeExtensions
{
    public static string ToDisplayName(this TimesheetEntryType entryType)
    {
        return entryType switch
        {
            TimesheetEntryType.Work => "Trabalho",
            TimesheetEntryType.Vacation => "Férias",
            TimesheetEntryType.Absence => "Ausência",
            _ => entryType.ToString()
        };
    }
}
