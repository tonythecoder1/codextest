using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timesheets.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryTypesAndPdfSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "TimesheetEntries",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "EntryType",
                table: "TimesheetEntries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntryType",
                table: "TimesheetEntries");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "TimesheetEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
