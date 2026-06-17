using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class _20260601000008AddPushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""
                    ALTER TABLE export_jobs DROP COLUMN IF EXISTS "CsvContent";
                    ALTER TABLE export_jobs DROP COLUMN IF EXISTS csvcontent;

                    DO $$
                    BEGIN
                        IF to_regclass('public.doctornotes') IS NOT NULL THEN
                            ALTER TABLE doctornotes DROP COLUMN IF EXISTS "IsFlag";
                            ALTER TABLE doctornotes DROP COLUMN IF EXISTS isflag;
                            ALTER TABLE doctornotes ADD COLUMN IF NOT EXISTS isimportant boolean NOT NULL DEFAULT false;
                            ALTER TABLE doctornotes ALTER COLUMN isimportant SET DEFAULT false;
                            ALTER TABLE doctornotes ALTER COLUMN isimportant SET NOT NULL;
                        END IF;
                    END $$;
                    """);
            }
            else
            {
                migrationBuilder.DropColumn(
                    name: "CsvContent",
                    table: "export_jobs");

                migrationBuilder.DropColumn(
                    name: "IsFlag",
                    table: "doctornotes");

                migrationBuilder.AlterColumn<bool>(
                    name: "isimportant",
                    table: "doctornotes",
                    type: "boolean",
                    nullable: false,
                    defaultValue: false,
                    oldClrType: typeof(bool),
                    oldType: "boolean");
            }

            migrationBuilder.CreateTable(
                name: "pushsubscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    P256Dh = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Auth = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pushsubscriptions", x => x.SubscriptionId);
                    table.ForeignKey(
                        name: "FK_pushsubscriptions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pushsubscriptions_Endpoint",
                table: "pushsubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pushsubscriptions_UserId",
                table: "pushsubscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pushsubscriptions");

            migrationBuilder.AddColumn<byte[]>(
                name: "CsvContent",
                table: "export_jobs",
                type: "bytea",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "isimportant",
                table: "doctornotes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlag",
                table: "doctornotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
