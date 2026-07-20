using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <summary>
/// Убирает использование текстовой заметки как признака технической связи детского устройства.
/// </summary>
public partial class AddParentChildLinkType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "link_type",
            table: "parent_child_links",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Переносим данные из устаревшего текстового маркера до того,
        // как код авторизации начнёт читать только структурированное поле.
        migrationBuilder.Sql(
            "UPDATE parent_child_links " +
            "SET link_type = 1 " +
            "WHERE notes = 'Self-link for child mobile account';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "link_type",
            table: "parent_child_links");
    }
}
