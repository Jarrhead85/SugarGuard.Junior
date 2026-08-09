using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class LinkNutritionGlucoseMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "nutrition_entry_id",
                table: "measurements",
                type: "uuid",
                nullable: true);

            // "glucose_before" historically lived only in the nutrition diary, so these
            // values were invisible to the measurements chart and period analytics.
            migrationBuilder.Sql(
                """
                INSERT INTO measurements
                    (measurement_id, child_id, glucose_value, measurement_time, data_source, nutrition_entry_id, created_at)
                SELECT
                    gen_random_uuid(),
                    entry.child_id,
                    entry.glucose_before,
                    entry.recorded_at,
                    'nutrition',
                    entry.nutrition_entry_id,
                    COALESCE(entry.updated_at, entry.created_at, NOW())
                FROM nutrition_entries AS entry
                WHERE entry.glucose_before IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_measurements_nutrition_entry",
                table: "measurements",
                column: "nutrition_entry_id",
                unique: true,
                filter: "nutrition_entry_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_measurements_nutrition_entries_nutrition_entry_id",
                table: "measurements",
                column: "nutrition_entry_id",
                principalTable: "nutrition_entries",
                principalColumn: "nutrition_entry_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_measurements_nutrition_entries_nutrition_entry_id",
                table: "measurements");

            migrationBuilder.DropIndex(
                name: "ux_measurements_nutrition_entry",
                table: "measurements");

            migrationBuilder.DropColumn(
                name: "nutrition_entry_id",
                table: "measurements");
        }
    }
}
