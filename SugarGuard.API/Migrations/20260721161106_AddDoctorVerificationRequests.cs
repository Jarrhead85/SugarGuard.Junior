using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorVerificationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doctor_verification_requests",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    encrypted_license_number = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    organization_name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_verification_requests", x => x.request_id);
                    table.ForeignKey(
                        name: "FK_doctor_verification_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "doctor_verification_documents",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stored_file_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_verification_documents", x => x.document_id);
                    table.ForeignKey(
                        name: "FK_doctor_verification_documents_doctor_verification_requests_~",
                        column: x => x.request_id,
                        principalTable: "doctor_verification_requests",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_verification_documents_request_id",
                table: "doctor_verification_documents",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_doctor_verification_requests_status_submitted",
                table: "doctor_verification_requests",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_verification_requests_user_id",
                table: "doctor_verification_requests",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_verification_documents");

            migrationBuilder.DropTable(
                name: "doctor_verification_requests");

        }
    }
}
