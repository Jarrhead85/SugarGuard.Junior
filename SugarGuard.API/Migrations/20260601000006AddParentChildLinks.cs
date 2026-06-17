using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <inheritdoc />
[Migration("20260601000006AddParentChildLinks")]
public partial class AddParentChildLinks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "linkedbyuserid",
            table: "parentchildlinks",
            type: "uuid",
            nullable: true,
            defaultValue: null);

        migrationBuilder.AddForeignKey(
            name: "FKparentchildlinksuserslinkdbyuserid",
            table: "parentchildlinks",
            column: "linkedbyuserid",
            principalTable: "users",
            principalColumn: "userid",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddColumn<string>(
            name: "notes",
            table: "parentchildlinks",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true,
            defaultValue: null);

        migrationBuilder.CreateIndex(
            name: "idx_parentchildlinks_createdat",
            table: "parentchildlinks",
            column: "createdat");

        migrationBuilder.CreateIndex(
            name: "idx_parentchildlinks_linkedby",
            table: "parentchildlinks",
            column: "linkedbyuserid");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_parentchildlinks_linkedby",
            table: "parentchildlinks");

        migrationBuilder.DropIndex(
            name: "idx_parentchildlinks_createdat",
            table: "parentchildlinks");

        migrationBuilder.DropForeignKey(
            name: "FKparentchildlinksuserslinkdbyuserid",
            table: "parentchildlinks");

        migrationBuilder.DropColumn(
            name: "notes",
            table: "parentchildlinks");

        migrationBuilder.DropColumn(
            name: "linkedbyuserid",
            table: "parentchildlinks");
    }
}
