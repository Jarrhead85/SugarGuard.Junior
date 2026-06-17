using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <inheritdoc />
[Migration("20260601000003AddEmailVerificationFields")]
public partial class AddEmailVerificationFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "isemailverified",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "emailverifiedat",
            table: "users",
            type: "timestamp with time zone",
            nullable: true,
            defaultValue: null);

        migrationBuilder.AddColumn<bool>(
            name: "isactive",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "deactivatedat",
            table: "users",
            type: "timestamp with time zone",
            nullable: true,
            defaultValue: null);

        migrationBuilder.CreateIndex(
            name: "idx_users_isactive_deactivated",
            table: "users",
            column: "isactive",
            filter: "isactive = false");

        migrationBuilder.CreateTable(
            name: "verificationcodes",
            columns: table => new
            {
                verificationcodeid = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                userid = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                codehash = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),

                codetype = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),

                deliverychannel = table.Column<string>(
                    type: "character varying(8)",
                    maxLength: 8,
                    nullable: true),

                maskedrecipient = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true),

                requestip = table.Column<string>(
                    type: "character varying(45)",
                    maxLength: 45,
                    nullable: true),

                createdat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValueSql: "NOW()"),

                expiresat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),

                usedat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PKverificationcodes", x => x.verificationcodeid);

                table.ForeignKey(
                    name: "FKverificationcodesusers",
                    column: x => x.userid,
                    principalTable: "users",
                    principalColumn: "userid",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_verificationcodes_userid_hash_active",
            table: "verificationcodes",
            columns: new[] { "userid", "codehash" },
            filter: "usedat IS NULL");

        migrationBuilder.CreateIndex(
            name: "idx_verificationcodes_expiresat",
            table: "verificationcodes",
            column: "expiresat");

        migrationBuilder.CreateIndex(
            name: "idx_verificationcodes_userid_type_active",
            table: "verificationcodes",
            columns: new[] { "userid", "codetype" },
            filter: "usedat IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "verificationcodes");

        migrationBuilder.DropIndex(
            name: "idx_users_isactive_deactivated",
            table: "users");

        migrationBuilder.DropColumn(
            name: "deactivatedat",
            table: "users");

        migrationBuilder.DropColumn(
            name: "isactive",
            table: "users");

        migrationBuilder.DropColumn(
            name: "emailverifiedat",
            table: "users");

        migrationBuilder.DropColumn(
            name: "isemailverified",
            table: "users");
    }
}
