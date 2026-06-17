// Миграция для добавления зашифрованного поля Notes
using Microsoft.EntityFrameworkCore.Migrations;

namespace SugarGuard.Junior.Migrations;

/// <summary>
/// Добавляет зашифрованное поле Notes в таблицу Measurements
/// Requirements: 16.1, 16.2, 16.3
/// </summary>
public partial class AddEncryptedNotesField : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Добавляем зашифрованные заметки в measurements
        migrationBuilder.AddColumn<string>(
            name: "EncryptedNotes",
            table: "Measurements",
            type: "TEXT",
            maxLength: 2000,
            nullable: true);

        // Примечание: EncryptedGlucoseValue уже существует в схеме как обязательное поле
        // EncryptedChildState был добавлен в предыдущей миграции
        // Это завершает полное шифрование всех PHI полей в Measurements
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Откат миграции - удаляем добавленную колонку
        migrationBuilder.DropColumn(
            name: "EncryptedNotes",
            table: "Measurements");
    }
}
