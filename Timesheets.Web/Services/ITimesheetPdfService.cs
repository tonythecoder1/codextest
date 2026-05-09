namespace Timesheets.Web.Services;

public interface ITimesheetPdfService
{
    byte[] GenerateMonthlyPdf(MonthlyTimesheetPdfModel model);
    byte[] GenerateReportPdf(TimesheetReportPdfModel model);
}
