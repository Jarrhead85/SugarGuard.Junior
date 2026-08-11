using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenConcurrencyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConcurrencyVersion",
                table: "RefreshTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyVersion",
                table: "RefreshTokens");
        }
    }
}
