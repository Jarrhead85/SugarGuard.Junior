using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "children",
                columns: table => new
                {
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    last_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    height = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    diabetes_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    diagnosis_date = table.Column<DateOnly>(type: "date", nullable: true),
                    insulin_scheme = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    current_insulins = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_children", x => x.child_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    telegram_id = table.Column<long>(type: "bigint", nullable: true),
                    encrypted_first_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    encrypted_last_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    encrypted_email = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    password_salt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "backpack_history",
                columns: table => new
                {
                    history_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snack_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    bread_units = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backpack_history", x => x.history_id);
                    table.ForeignKey(
                        name: "FK_backpack_history_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backpack_items",
                columns: table => new
                {
                    backpack_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snack_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    bread_units = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    added_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backpack_items", x => x.backpack_item_id);
                    table.ForeignKey(
                        name: "FK_backpack_items_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "connection_codes",
                columns: table => new
                {
                    code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_codes", x => x.code_id);
                    table.ForeignKey(
                        name: "FK_connection_codes_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diabetes_settings",
                columns: table => new
                {
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_range_min = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    target_range_max = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    insulin_sensitivity = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    carb_insulin_ratio = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diabetes_settings", x => x.child_id);
                    table.ForeignKey(
                        name: "FK_diabetes_settings_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "measurement_schedules",
                columns: table => new
                {
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurement_schedules", x => x.schedule_id);
                    table.ForeignKey(
                        name: "FK_measurement_schedules_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parent_child_links",
                columns: table => new
                {
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_child_links", x => x.link_id);
                    table.ForeignKey(
                        name: "FK_parent_child_links_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parent_child_links_users_parent_user_id",
                        column: x => x.parent_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_recommendations",
                columns: table => new
                {
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measurement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    glucose_value_at_request = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    recommendation_text = table.Column<string>(type: "text", nullable: false),
                    urgency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_used = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_from_cache = table.Column<bool>(type: "boolean", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_recommendations", x => x.recommendation_id);
                    table.ForeignKey(
                        name: "FK_ai_recommendations_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "measurements",
                columns: table => new
                {
                    measurement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    glucose_value = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    measurement_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    child_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    data_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurements", x => x.measurement_id);
                    table.ForeignKey(
                        name: "FK_measurements_ai_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "ai_recommendations",
                        principalColumn: "recommendation_id");
                    table.ForeignKey(
                        name: "FK_measurements_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snack_consumption_logs",
                columns: table => new
                {
                    log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snack_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    bread_units = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snack_consumption_logs", x => x.log_id);
                    table.ForeignKey(
                        name: "FK_snack_consumption_logs_ai_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "ai_recommendations",
                        principalColumn: "recommendation_id");
                    table.ForeignKey(
                        name: "FK_snack_consumption_logs_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_child",
                table: "ai_recommendations",
                columns: new[] { "child_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_measurement_id",
                table: "ai_recommendations",
                column: "measurement_id");

            migrationBuilder.CreateIndex(
                name: "IX_backpack_history_child_id",
                table: "backpack_history",
                column: "child_id");

            migrationBuilder.CreateIndex(
                name: "idx_backpack_child",
                table: "backpack_items",
                column: "child_id");

            migrationBuilder.CreateIndex(
                name: "IX_connection_codes_child_id",
                table: "connection_codes",
                column: "child_id");

            migrationBuilder.CreateIndex(
                name: "idx_schedules_child",
                table: "measurement_schedules",
                columns: new[] { "child_id", "scheduled_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_measurements_child_time",
                table: "measurements",
                columns: new[] { "child_id", "measurement_time" });

            migrationBuilder.CreateIndex(
                name: "IX_measurements_recommendation_id",
                table: "measurements",
                column: "recommendation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parent_child_links_child_id",
                table: "parent_child_links",
                column: "child_id");

            migrationBuilder.CreateIndex(
                name: "IX_parent_child_links_parent_user_id_child_id",
                table: "parent_child_links",
                columns: new[] { "parent_user_id", "child_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snack_consumption_logs_child_id",
                table: "snack_consumption_logs",
                column: "child_id");

            migrationBuilder.CreateIndex(
                name: "IX_snack_consumption_logs_recommendation_id",
                table: "snack_consumption_logs",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_telegram_id",
                table: "users",
                column: "telegram_id",
                unique: true,
                filter: "telegram_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_recommendations_measurements_measurement_id",
                table: "ai_recommendations",
                column: "measurement_id",
                principalTable: "measurements",
                principalColumn: "measurement_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_recommendations_children_child_id",
                table: "ai_recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_measurements_children_child_id",
                table: "measurements");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_recommendations_measurements_measurement_id",
                table: "ai_recommendations");

            migrationBuilder.DropTable(
                name: "backpack_history");

            migrationBuilder.DropTable(
                name: "backpack_items");

            migrationBuilder.DropTable(
                name: "connection_codes");

            migrationBuilder.DropTable(
                name: "diabetes_settings");

            migrationBuilder.DropTable(
                name: "measurement_schedules");

            migrationBuilder.DropTable(
                name: "parent_child_links");

            migrationBuilder.DropTable(
                name: "snack_consumption_logs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "children");

            migrationBuilder.DropTable(
                name: "measurements");

            migrationBuilder.DropTable(
                name: "ai_recommendations");
        }
    }
}
