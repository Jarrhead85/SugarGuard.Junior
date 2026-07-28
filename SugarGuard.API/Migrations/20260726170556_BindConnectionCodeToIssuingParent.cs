using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class BindConnectionCodeToIssuingParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "issued_by_parent_user_id",
                table: "connection_codes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_connection_codes_issued_by_parent_user_id",
                table: "connection_codes",
                column: "issued_by_parent_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_connection_codes_users_issued_by_parent_user_id",
                table: "connection_codes",
                column: "issued_by_parent_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_connection_codes_users_issued_by_parent_user_id",
                table: "connection_codes");

            migrationBuilder.DropIndex(
                name: "IX_connection_codes_issued_by_parent_user_id",
                table: "connection_codes");

            migrationBuilder.DropColumn(
                name: "issued_by_parent_user_id",
                table: "connection_codes");
        }
    }
}
