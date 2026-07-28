using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNightInsulinSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "escalation_window_minutes",
                table: "meal_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "is_night_insulin",
                table: "meal_schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "repeat_interval_minutes",
                table: "meal_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "escalation_window_minutes",
                table: "meal_schedules");

            migrationBuilder.DropColumn(
                name: "is_night_insulin",
                table: "meal_schedules");

            migrationBuilder.DropColumn(
                name: "repeat_interval_minutes",
                table: "meal_schedules");
        }
    }
}
