using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGigaChatPromptTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "precached_prompt_tokens",
                table: "ai_conversation_messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt_version",
                table: "ai_conversation_messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "precached_prompt_tokens",
                table: "ai_conversation_messages");

            migrationBuilder.DropColumn(
                name: "prompt_version",
                table: "ai_conversation_messages");
        }
    }
}
