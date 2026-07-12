using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAiLongTermMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_conversations",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    summary_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    provider_conversation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversations", x => x.conversation_id);
                    table.ForeignKey(
                        name: "FK_ai_conversations_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_context_snapshots",
                columns: table => new
                {
                    context_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measurement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    format_version = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    context_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_context_snapshots", x => x.context_snapshot_id);
                    table.ForeignKey(
                        name: "FK_ai_context_snapshots_ai_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "ai_conversations",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_context_snapshots_children_child_id",
                        column: x => x.child_id,
                        principalTable: "children",
                        principalColumn: "child_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_context_snapshots_measurements_measurement_id",
                        column: x => x.measurement_id,
                        principalTable: "measurements",
                        principalColumn: "measurement_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_conversation_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    model = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: true),
                    output_tokens = table.Column<int>(type: "integer", nullable: true),
                    safety_result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    measurement_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversation_messages", x => x.message_id);
                    table.ForeignKey(
                        name: "FK_ai_conversation_messages_ai_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "ai_conversations",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_conversation_messages_ai_recommendations_recommendation_~",
                        column: x => x.recommendation_id,
                        principalTable: "ai_recommendations",
                        principalColumn: "recommendation_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_conversation_messages_measurements_measurement_id",
                        column: x => x.measurement_id,
                        principalTable: "measurements",
                        principalColumn: "measurement_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_conversation_messages_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_context_snapshots_child_created",
                table: "ai_context_snapshots",
                columns: new[] { "child_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_context_snapshots_conversation_created",
                table: "ai_context_snapshots",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_context_snapshots_measurement_id",
                table: "ai_context_snapshots",
                column: "measurement_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_messages_author_user_id",
                table: "ai_conversation_messages",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversation_messages_measurement_id",
                table: "ai_conversation_messages",
                column: "measurement_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_messages_conversation_created",
                table: "ai_conversation_messages",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_messages_recommendation",
                table: "ai_conversation_messages",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_conversations_child_status_last",
                table: "ai_conversations",
                columns: new[] { "child_id", "status", "last_message_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_context_snapshots");

            migrationBuilder.DropTable(
                name: "ai_conversation_messages");

            migrationBuilder.DropTable(
                name: "ai_conversations");
        }
    }
}
