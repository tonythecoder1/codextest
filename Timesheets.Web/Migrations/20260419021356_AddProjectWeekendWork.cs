using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timesheets.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectWeekendWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowWeekendWork",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowWeekendWork",
                table: "Projects");
        }
    }
}
