using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <inheritdoc />
[Migration("20260601000007AddUserRefreshTokens")]
public partial class AddUserRefreshTokens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            columns: table => new
            {
                Id = table.Column<long>(
                    type: "bigint",
                    nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                Token = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),

                UserId = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),

                IsRevoked = table.Column<bool>(
                    type: "boolean",
                    nullable: false,
                    defaultValue: false),

                ReplacedByToken = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true),

                RevokedReason = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true),

                CreatedByIp = table.Column<string>(
                    type: "character varying(45)",
                    maxLength: 45,
                    nullable: true),

                CreatedByUserAgent = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true),

                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValueSql: "NOW()"),

                ExpiresAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),

                RevokedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PKRefreshTokens", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IXRefreshTokensToken",
            table: "RefreshTokens",
            column: "Token",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IXRefreshTokensUserId",
            table: "RefreshTokens",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IXRefreshTokensUserIdExpiresAt",
            table: "RefreshTokens",
            columns: new[] { "UserId", "ExpiresAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RefreshTokens");
    }
}
