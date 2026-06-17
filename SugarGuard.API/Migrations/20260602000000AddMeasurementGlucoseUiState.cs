using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class _20260602000000AddMeasurementGlucoseUiState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    ALTER TABLE measurements
                        ADD COLUMN glucose_ui_state VARCHAR(16)
                        GENERATED ALWAYS AS (
                            CASE
                                WHEN glucose_value <= 3.1 OR glucose_value > 15.0 THEN 'Critical'
                                WHEN glucose_value >= 4.0 AND glucose_value <= 10.0 THEN 'Normal'
                                ELSE 'Attention'
                            END
                        ) STORED;

                    CREATE INDEX idx_measurements_child_uistate_time
                        ON measurements (child_id, glucose_ui_state, measurement_time DESC);
                ");
            }
            else
            {
                migrationBuilder.Sql(@"
                    ALTER TABLE measurements
                        ADD COLUMN glucose_ui_state TEXT
                        GENERATED ALWAYS AS (
                            CASE
                                WHEN glucose_value <= 3.1 OR glucose_value > 15.0 THEN 'Critical'
                                WHEN glucose_value >= 4.0 AND glucose_value <= 10.0 THEN 'Normal'
                                ELSE 'Attention'
                            END
                        ) STORED;

                    CREATE INDEX idx_measurements_child_uistate_time
                        ON measurements (child_id, glucose_ui_state, measurement_time DESC);
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    DROP INDEX IF EXISTS idx_measurements_child_uistate_time;
                    ALTER TABLE measurements DROP COLUMN IF EXISTS glucose_ui_state;
                ");
            }
            else
            {
                migrationBuilder.Sql(@"
                    DROP INDEX IF EXISTS idx_measurements_child_uistate_time;
                    ALTER TABLE measurements DROP COLUMN glucose_ui_state;
                ");
            }
        }
    }
}
