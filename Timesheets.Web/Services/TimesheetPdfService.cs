using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Timesheets.Web.Services;

public class TimesheetPdfService : ITimesheetPdfService
{
    public byte[] GenerateMonthlyPdf(MonthlyTimesheetPdfModel model)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(text => text.FontSize(10));

                    page.Header().Column(column =>
                    {
                        column.Item().Text(model.CompanyName).FontSize(24).SemiBold().FontColor(Colors.Teal.Darken2);
                        column.Item().Text(model.Slogan).FontSize(9).FontColor(Colors.Grey.Darken1);
                        column.Item().PaddingTop(10).Text($"Timesheet mensal · {model.MonthLabel}").FontSize(16).SemiBold();
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Element(card => BuildInfoCard(card, "Colaborador", model.EmployeeName));
                            row.RelativeItem().Element(card => BuildInfoCard(card, "Email", model.EmployeeEmail));
                            row.RelativeItem().Element(card => BuildInfoCard(card, "Total", $"{model.TotalHours:0.##}h"));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(80);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(55);
                                columns.RelativeColumn(2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Data");
                                header.Cell().Element(HeaderCell).Text("Tipo");
                                header.Cell().Element(HeaderCell).Text("Projeto");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Horas");
                                header.Cell().Element(HeaderCell).Text("Descrição");
                            });

                            foreach (var entry in model.Entries.OrderBy(entry => entry.WorkDate))
                            {
                                table.Cell().Element(BodyCell).Text(entry.WorkDate.ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).Text(entry.TypeLabel);
                                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.ProjectName) ? "-" : entry.ProjectName);
                                table.Cell().Element(BodyCell).AlignRight().Text($"{entry.Hours:0.##}h");
                                table.Cell().Element(BodyCell).Text(entry.Description);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Página ");
                        text.CurrentPageNumber();
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] GenerateReportPdf(TimesheetReportPdfModel model)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(text => text.FontSize(10));

                    page.Header().Column(column =>
                    {
                        column.Item().Text(model.CompanyName).FontSize(24).SemiBold().FontColor(Colors.Teal.Darken2);
                        column.Item().Text(model.Slogan).FontSize(9).FontColor(Colors.Grey.Darken1);
                        column.Item().PaddingTop(10).Text(model.Title).FontSize(16).SemiBold();
                        if (!string.IsNullOrWhiteSpace(model.ScopeLabel))
                        {
                            column.Item().Text(model.ScopeLabel).FontSize(10).FontColor(Colors.Grey.Darken2);
                        }
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Element(card => BuildInfoCard(card, "Registos", model.TotalEntries.ToString()));
                            row.RelativeItem().Element(card => BuildInfoCard(card, "Total de horas", $"{model.TotalHours:0.##}h"));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(60);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(75);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(55);
                                columns.RelativeColumn(1.8f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Data");
                                header.Cell().Element(HeaderCell).Text("Colaborador");
                                header.Cell().Element(HeaderCell).Text("Tipo");
                                header.Cell().Element(HeaderCell).Text("Projeto");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Horas");
                                header.Cell().Element(HeaderCell).Text("Descrição");
                            });

                            foreach (var entry in model.Entries.OrderByDescending(entry => entry.WorkDate))
                            {
                                table.Cell().Element(BodyCell).Text(entry.WorkDate.ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).Text(entry.EmployeeName);
                                table.Cell().Element(BodyCell).Text(entry.TypeLabel);
                                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.ProjectName) ? "-" : entry.ProjectName);
                                table.Cell().Element(BodyCell).AlignRight().Text($"{entry.Hours:0.##}h");
                                table.Cell().Element(BodyCell).Text(entry.Description);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Página ");
                        text.CurrentPageNumber();
                    });
                });
            })
            .GeneratePdf();
    }

    private static void BuildInfoCard(IContainer container, string label, string value)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken1);
                column.Item().PaddingTop(3).Text(value).SemiBold();
            });
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(6)
            .PaddingHorizontal(8);
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(6)
            .PaddingHorizontal(8);
    }
}
