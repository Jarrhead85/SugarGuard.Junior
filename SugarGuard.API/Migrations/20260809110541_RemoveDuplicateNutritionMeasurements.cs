using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicateNutritionMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The nutrition migration mirrors "glucose before meal" values into
            // measurements. If a manual measurement already exists for precisely
            // the same child, timestamp and glucose value, the manual record is
            // authoritative and the mirror must not create a second chart point.
            migrationBuilder.Sql(
                """
                DELETE FROM measurements AS nutrition_measurement
                WHERE nutrition_measurement.nutrition_entry_id IS NOT NULL
                  AND EXISTS (
                      SELECT 1
                      FROM measurements AS manual_measurement
                      WHERE manual_measurement.nutrition_entry_id IS NULL
                        AND manual_measurement.child_id = nutrition_measurement.child_id
                        AND manual_measurement.measurement_time = nutrition_measurement.measurement_time
                        AND manual_measurement.glucose_value = nutrition_measurement.glucose_value
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
