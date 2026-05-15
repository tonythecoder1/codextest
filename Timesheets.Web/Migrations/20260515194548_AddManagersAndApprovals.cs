using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timesheets.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddManagersAndApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimesheetEntries_EmployeeId",
                table: "TimesheetEntries");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "TimesheetEntries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "TimesheetEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByEmployeeId",
                table: "TimesheetEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsManager",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ResponsibleId",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectResponsibles",
                columns: table => new
                {
                    ManagedProjectsId = table.Column<int>(type: "integer", nullable: false),
                    ResponsibleEmployeesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectResponsibles", x => new { x.ManagedProjectsId, x.ResponsibleEmployeesId });
                    table.ForeignKey(
                        name: "FK_ProjectResponsibles_Employees_ResponsibleEmployeesId",
                        column: x => x.ResponsibleEmployeesId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectResponsibles_Projects_ManagedProjectsId",
                        column: x => x.ManagedProjectsId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_ApprovedByEmployeeId",
                table: "TimesheetEntries",
                column: "ApprovedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_EmployeeId_ApprovalStatus",
                table: "TimesheetEntries",
                columns: new[] { "EmployeeId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ResponsibleId",
                table: "Employees",
                column: "ResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResponsibles_ResponsibleEmployeesId",
                table: "ProjectResponsibles",
                column: "ResponsibleEmployeesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ResponsibleId",
                table: "Employees",
                column: "ResponsibleId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimesheetEntries_Employees_ApprovedByEmployeeId",
                table: "TimesheetEntries",
                column: "ApprovedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ResponsibleId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_TimesheetEntries_Employees_ApprovedByEmployeeId",
                table: "TimesheetEntries");

            migrationBuilder.DropTable(
                name: "ProjectResponsibles");

            migrationBuilder.DropIndex(
                name: "IX_TimesheetEntries_ApprovedByEmployeeId",
                table: "TimesheetEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimesheetEntries_EmployeeId_ApprovalStatus",
                table: "TimesheetEntries");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ResponsibleId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "TimesheetEntries");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "TimesheetEntries");

            migrationBuilder.DropColumn(
                name: "ApprovedByEmployeeId",
                table: "TimesheetEntries");

            migrationBuilder.DropColumn(
                name: "IsManager",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ResponsibleId",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_EmployeeId",
                table: "TimesheetEntries",
                column: "EmployeeId");
        }
    }
}
