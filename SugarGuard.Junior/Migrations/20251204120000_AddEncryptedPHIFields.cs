// Миграция для добавления зашифрованных PHI полей
using Microsoft.EntityFrameworkCore.Migrations;

namespace SugarGuard.Junior.Migrations;

/// <summary>
/// Добавляет зашифрованные поля для PHI данных
/// Requirements: 5.1, 5.2, 5.3
/// </summary>
public partial class AddEncryptedPHIFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Добавляем зашифрованное состояние ребёнка в measurements
        migrationBuilder.AddColumn<string>(
            name: "EncryptedChildState",
            table: "Measurements",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        // Добавляем зашифрованные хлебные единицы в backpack_items
        migrationBuilder.AddColumn<string>(
            name: "EncryptedBreadUnits",
            table: "BackpackItems",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        // Добавляем зашифрованные хлебные единицы в backpack_history
        migrationBuilder.AddColumn<string>(
            name: "EncryptedBreadUnits",
            table: "BackpackHistory",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        // Добавляем зашифрованные хлебные единицы в snack_consumption_logs
        migrationBuilder.AddColumn<string>(
            name: "EncryptedBreadUnits",
            table: "SnackConsumptionLogs",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        // Добавляем зашифрованные настройки диабета
        migrationBuilder.AddColumn<string>(
            name: "EncryptedTargetRangeMin",
            table: "DiabetesSettings",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EncryptedTargetRangeMax",
            table: "DiabetesSettings",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EncryptedInsulinSensitivity",
            table: "DiabetesSettings",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EncryptedCarbInsulinRatio",
            table: "DiabetesSettings",
            type: "TEXT",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Откат миграции - удаляем добавленные колонки
        migrationBuilder.DropColumn(
            name: "EncryptedChildState",
            table: "Measurements");

        migrationBuilder.DropColumn(
            name: "EncryptedBreadUnits",
            table: "BackpackItems");

        migrationBuilder.DropColumn(
            name: "EncryptedBreadUnits",
            table: "BackpackHistory");

        migrationBuilder.DropColumn(
            name: "EncryptedBreadUnits",
            table: "SnackConsumptionLogs");

        migrationBuilder.DropColumn(
            name: "EncryptedTargetRangeMin",
            table: "DiabetesSettings");

        migrationBuilder.DropColumn(
            name: "EncryptedTargetRangeMax",
            table: "DiabetesSettings");

        migrationBuilder.DropColumn(
            name: "EncryptedInsulinSensitivity",
            table: "DiabetesSettings");

        migrationBuilder.DropColumn(
            name: "EncryptedCarbInsulinRatio",
            table: "DiabetesSettings");
    }
}
