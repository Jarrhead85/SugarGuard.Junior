using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <summary>
/// Миграция для создания таблицы bot_user_contexts
/// Хранит контекст пользователя Telegram-бота
/// </summary>
public partial class AddBotUserContextsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bot_user_contexts",
            columns: table => new
            {
                context_id = table.Column<Guid>(type: "uuid", nullable: false),
                telegram_user_id = table.Column<long>(type: "bigint", nullable: false),
                current_child_id = table.Column<Guid>(type: "uuid", nullable: true),
                last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bot_user_contexts", x => x.context_id);
                table.ForeignKey(
                    name: "FK_bot_user_contexts_children_current_child_id",
                    column: x => x.current_child_id,
                    principalTable: "children",
                    principalColumn: "child_id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "idx_bot_context_telegram",
            table: "bot_user_contexts",
            column: "telegram_user_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "bot_user_contexts");
    }
}
