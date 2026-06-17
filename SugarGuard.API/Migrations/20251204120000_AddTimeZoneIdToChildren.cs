using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

public partial class AddTimeZoneIdToChildren : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "time_zone_id",
            table: "children",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "UTC");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "time_zone_id",
            table: "children");
    }
}
