using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "child_achievements",
                columns: table => new
                {
                    child_achievement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    achievement_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    unlocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_achievements", x => x.child_achievement_id);
                    table.ForeignKey(
                        name: "FK_child_achievements_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_schedules",
                columns: table => new
                {
                    meal_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    scheduled_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    planned_bread_units = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    days_of_week_mask = table.Column<int>(type: "integer", nullable: false),
                    reminder_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    reminder_minutes_before = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_schedules", x => x.meal_schedule_id);
                    table.ForeignKey(
                        name: "FK_meal_schedules_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_entries",
                columns: table => new
                {
                    nutrition_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    meal_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    meal_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    bread_units = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    insulin_units = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    glucose_before = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_entries", x => x.nutrition_entry_id);
                    table.ForeignKey(
                        name: "FK_nutrition_entries_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_child_achievements_child_code",
                table: "child_achievements",
                columns: new[] { "child_id", "achievement_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_meal_schedules_child_time_title",
                table: "meal_schedules",
                columns: new[] { "child_id", "scheduled_time", "title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_entries_child_recorded",
                table: "nutrition_entries",
                columns: new[] { "child_id", "recorded_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "child_achievements");

            migrationBuilder.DropTable(
                name: "meal_schedules");

            migrationBuilder.DropTable(
                name: "nutrition_entries");
        }
    }
}
