// Миграция для добавления недостающих полей в таблицу sync_conflict_history
using Microsoft.EntityFrameworkCore.Migrations;

namespace SugarGuard.Junior.Migrations;

/// <summary>
/// Добавляет недостающие поля в таблицу sync_conflict_history
/// Requirements: 14.5
/// </summary>
public partial class AddSyncConflictHistoryFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Добавляем поле для причины разрешения конфликта
        migrationBuilder.AddColumn<string>(
            name: "ResolutionReason",
            table: "SyncConflictHistory",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        // Добавляем поле для указания кто разрешил конфликт
        migrationBuilder.AddColumn<string>(
            name: "ResolvedBy",
            table: "SyncConflictHistory",
            type: "TEXT",
            maxLength: 100,
            nullable: false,
            defaultValue: "SyncConflictResolver");

        // Создаём составной индекс по entity_id и entity_type для быстрого поиска
        migrationBuilder.CreateIndex(
            name: "IX_SyncConflictHistory_EntityIdType",
            table: "SyncConflictHistory",
            columns: new[] { "EntityId", "EntityType" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Откат миграции - удаляем добавленные поля и индекс
        migrationBuilder.DropIndex(
            name: "IX_SyncConflictHistory_EntityIdType",
            table: "SyncConflictHistory");

        migrationBuilder.DropColumn(
            name: "ResolutionReason",
            table: "SyncConflictHistory");

        migrationBuilder.DropColumn(
            name: "ResolvedBy",
            table: "SyncConflictHistory");
    }
}
