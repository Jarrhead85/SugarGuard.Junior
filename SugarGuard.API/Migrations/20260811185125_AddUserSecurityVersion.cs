using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSecurityVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "security_version",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_users_emailforlogin;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email_for_login
                    ON users (email_for_login)
                    WHERE email_for_login IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "security_version",
                table: "users");
        }
    }
}
