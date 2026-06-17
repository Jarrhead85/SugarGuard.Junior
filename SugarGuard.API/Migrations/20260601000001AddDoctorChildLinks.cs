using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <inheritdoc />
[Migration("20260601000001AddDoctorChildLinks")]
public partial class AddDoctorChildLinks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "linkedbyuserid",
            table: "doctorchildlinks",
            type: "uuid",
            nullable: true,
            defaultValue: null);

        migrationBuilder.AddForeignKey(
            name: "FKdoctorchildlinksuserslinkdbyuserid",
            table: "doctorchildlinks",
            column: "linkedbyuserid",
            principalTable: "users",
            principalColumn: "userid",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddColumn<string>(
            name: "notes",
            table: "doctorchildlinks",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true,
            defaultValue: null);

        migrationBuilder.AddColumn<bool>(
            name: "isactive",
            table: "doctorchildlinks",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "deactivatedat",
            table: "doctorchildlinks",
            type: "timestamp with time zone",
            nullable: true,
            defaultValue: null);

        migrationBuilder.CreateIndex(
            name: "idx_doctorchildlinks_createdat",
            table: "doctorchildlinks",
            column: "createdat");

        migrationBuilder.CreateIndex(
            name: "idx_doctorchildlinks_linkedby",
            table: "doctorchildlinks",
            column: "linkedbyuserid");

        migrationBuilder.CreateIndex(
            name: "idx_doctorchildlinks_isactive",
            table: "doctorchildlinks",
            column: "isactive",
            filter: "isactive = true");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_doctorchildlinks_isactive",
            table: "doctorchildlinks");

        migrationBuilder.DropIndex(
            name: "idx_doctorchildlinks_linkedby",
            table: "doctorchildlinks");

        migrationBuilder.DropIndex(
            name: "idx_doctorchildlinks_createdat",
            table: "doctorchildlinks");

        migrationBuilder.DropForeignKey(
            name: "FKdoctorchildlinksuserslinkdbyuserid",
            table: "doctorchildlinks");

        migrationBuilder.DropColumn(
            name: "deactivatedat",
            table: "doctorchildlinks");

        migrationBuilder.DropColumn(
            name: "isactive",
            table: "doctorchildlinks");

        migrationBuilder.DropColumn(
            name: "notes",
            table: "doctorchildlinks");

        migrationBuilder.DropColumn(
            name: "linkedbyuserid",
            table: "doctorchildlinks");
    }
}
