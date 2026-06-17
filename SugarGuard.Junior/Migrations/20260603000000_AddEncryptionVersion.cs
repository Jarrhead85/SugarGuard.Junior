// Миграция для добавления версии шифрования (C-1 release 1.0.0).
// Каждая запись с PHI-полями получает колонку EncryptionVersion (1=LegacyCbc, 2=AesGcm).
// Существующие записи маркируются как LegacyCbc, новые пишутся как AesGcm.
// Backfill существующих данных до AesGcm выполняется фоновым MauiReEncryptJob
// в течение 30 дней после деплоя.
using Microsoft.EntityFrameworkCore.Migrations;

namespace SugarGuard.Junior.Migrations;

/// <summary>
/// Добавляет колонку <c>EncryptionVersion</c> в 8 таблиц с PHI-полями
/// (User, Child, Measurement, DiabetesSettings, AIRecommendation,
/// BackpackItem, BackpackHistory, SnackConsumptionLog) +
/// partial-индексы <c>WHERE EncryptionVersion = 1</c> для быстрого
/// поиска legacy-записей фоновым <c>MauiReEncryptJob</c>.
/// <para>
/// Существующие записи принудительно маркируются <see cref="byte"/> 1
/// (LegacyCbc), потому что до этой миграции все записи шифровались
/// через <c>SugarGuard.Junior/Security/CryptoService.cs</c> (AES-256-CBC).
/// </para>
/// <para>
/// Validates: Requirement C-1, Phase 1.3 of release 1.0.0.
/// </para>
/// </summary>
public partial class AddEncryptionVersion : Migration
{
    /// <summary>
    /// Значение EncryptionVersion для существующих записей (legacy AES-256-CBC).
    /// </summary>
    private const byte LegacyCbc = 1;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ========== USERS ==========
        migrationBuilder.AddColumn<byte>(
            name: "EncryptionVersion",
            table: "Users",
            type: "INTEGER",
            nullable: false,
            defaultValue: (byte)2); // AesGcm для новых записей

        migrationBuilder.Sql("UPDATE Users SET EncryptionVersion = 1;");
        migrationBuilder.CreateIndex(
            name: "IX_Users_LegacyCbc",
            table: "Users",
            column: "EncryptionVersion",
            filter: "\"EncryptionVersion\" = 1");

        // ========== CHILDREN ==========
        migrationBuilder.AddColumn<byte>(
            name: "EncryptionVersion",
            table: "Children",
            type: "INTEGER",
            nullable: false,
            defaultValue: (byte)2);

        migrationBuilder.Sql("UPDATE Children SET EncryptionVersion = 1;");
        migrationBuilder.CreateIndex(
            name: "IX_Children_LegacyCbc",
            table: "Children",
            column: "EncryptionVersion",
            filter: "\"EncryptionVersion\" = 1");

        // ========== MEASUREMENTS ==========
        migrationBuilder.AddColumn<byte>(
            name: "EncryptionVersion",
            table: "Measurements",
            type: "INTEGER",
            nullable: false,
            defaultValue: (byte)2);

        migrationBuilder.Sql("UPDATE Measurements SET EncryptionVersion = 1;");
        migrationBuilder.CreateIndex(
            name: "IX_Measurements_LegacyCbc",
            table: "Measurements",
            column: "EncryptionVersion",
            filter: "\"EncryptionVersion\" = 1");

        // ========== DIABETES_SETTINGS ==========
        migrationBuilder.AddColumn<byte>(
            name: "EncryptionVersion",
            table: "DiabetesSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: (byte)2);

        migrationBuilder.Sql("UPDATE DiabetesSettings SET EncryptionVersion = 1;");
        migrationBuilder.CreateIndex(
            name: "IX_DiabetesSettings_LegacyCbc",
            table: "DiabetesSettings",
            column: "EncryptionVersion",
            filter: "\"EncryptionVersion\" = 1");

        // ========== AI_RECOMMENDATIONS ==========
        migrationBuilder.AddColumn<byte>(
            name: "EncryptionVersion",
            table: "AIRecommendations",
            type: "INTEGER",
            nullable: false,
            defaultValue: (byte)2);

        migrationBuilder.Sql("UPDATE AIRecommendations SET EncryptionVersion = 1;");
        migrationBuilder.CreateIndex(
            name: "IX_AIRecommendations_LegacyCbc",
            table: "AIRecommendations",
            column: "EncryptionVersion",
            filter: "\"EncryptionVersion\" = 1");

        // ========== BACKPACK_ITEMS ==========
        migrationBuilder.AddColumn<byte>(
            name: "EncryptionVersion",
            table: "BackpackItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: (byte)2);

        migrationBuilder.Sql("UPDATE BackpackItems SET EncryptionVersion = 1;");
        migrationBuilder.CreateIndex(
            name: "IX_BackpackItems_LegacyCbc",
            table: "BackpackItems",
            column: "EncryptionVersion",
            filter: "\"EncryptionVersion\" = 1");

        // ========== BACKPACK_HISTORY ==========
        migrationBuilder.AddColumn<byte>(
            name: "EncryptionVersion",
            table: "BackpackHistory",
            type: "INTEGER",
            nullable: false,
            defaultValue: (byte)2);

        migrationBuilder.Sql("UPDATE BackpackHistory SET EncryptionVersion = 1;");
        migrationBuilder.CreateIndex(
            name: "IX_BackpackHistory_LegacyCbc",
            table: "BackpackHistory",
            column: "EncryptionVersion",
            filter: "\"EncryptionVersion\" = 1");

        // ========== SNACK_CONSUMPTION_LOGS ==========
        migrationBuilder.AddColumn<byte>(
            name: "EncryptionVersion",
            table: "SnackConsumptionLogs",
            type: "INTEGER",
            nullable: false,
            defaultValue: (byte)2);

        migrationBuilder.Sql("UPDATE SnackConsumptionLogs SET EncryptionVersion = 1;");
        migrationBuilder.CreateIndex(
            name: "IX_SnackConsumptionLogs_LegacyCbc",
            table: "SnackConsumptionLogs",
            column: "EncryptionVersion",
            filter: "\"EncryptionVersion\" = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Откат в обратном порядке: сначала индексы, потом колонки.
        // Данные, зашифрованные AesGcm (version=2), после отката колонки
        // станут нерасшифровываемыми — откат только для emergency recovery.

        migrationBuilder.DropIndex(
            name: "IX_SnackConsumptionLogs_LegacyCbc",
            table: "SnackConsumptionLogs");
        migrationBuilder.DropColumn(
            name: "EncryptionVersion",
            table: "SnackConsumptionLogs");

        migrationBuilder.DropIndex(
            name: "IX_BackpackHistory_LegacyCbc",
            table: "BackpackHistory");
        migrationBuilder.DropColumn(
            name: "EncryptionVersion",
            table: "BackpackHistory");

        migrationBuilder.DropIndex(
            name: "IX_BackpackItems_LegacyCbc",
            table: "BackpackItems");
        migrationBuilder.DropColumn(
            name: "EncryptionVersion",
            table: "BackpackItems");

        migrationBuilder.DropIndex(
            name: "IX_AIRecommendations_LegacyCbc",
            table: "AIRecommendations");
        migrationBuilder.DropColumn(
            name: "EncryptionVersion",
            table: "AIRecommendations");

        migrationBuilder.DropIndex(
            name: "IX_DiabetesSettings_LegacyCbc",
            table: "DiabetesSettings");
        migrationBuilder.DropColumn(
            name: "EncryptionVersion",
            table: "DiabetesSettings");

        migrationBuilder.DropIndex(
            name: "IX_Measurements_LegacyCbc",
            table: "Measurements");
        migrationBuilder.DropColumn(
            name: "EncryptionVersion",
            table: "Measurements");

        migrationBuilder.DropIndex(
            name: "IX_Children_LegacyCbc",
            table: "Children");
        migrationBuilder.DropColumn(
            name: "EncryptionVersion",
            table: "Children");

        migrationBuilder.DropIndex(
            name: "IX_Users_LegacyCbc",
            table: "Users");
        migrationBuilder.DropColumn(
            name: "EncryptionVersion",
            table: "Users");
    }
}
