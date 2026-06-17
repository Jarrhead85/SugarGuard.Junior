using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <inheritdoc />
[Migration("20260601000004AddInviteCodes")]
public partial class AddInviteCodes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "invitationcodes",
            columns: table => new
            {
                invitationcodeid = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                initiatoruserid = table.Column<Guid>(
                    type: "uuid",
                    nullable: true),

                claimedbyuserid = table.Column<Guid>(
                    type: "uuid",
                    nullable: true),

                code = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),

                targetrole = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),

                status = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false,
                    defaultValue: "Pending"),

                recipientemail = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true),

                createdat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValueSql: "NOW()"),

                expiresat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),

                claimedat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true),

                notes = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PKinvitationcodes", x => x.invitationcodeid);

                table.ForeignKey(
                    name: "FKinvitationcodeusersinitiatouserid",
                    column: x => x.initiatoruserid,
                    principalTable: "users",
                    principalColumn: "userid",
                    onDelete: ReferentialAction.SetNull);

                table.ForeignKey(
                    name: "FKinvitationcodesusersclaimedbyuserid",
                    column: x => x.claimedbyuserid,
                    principalTable: "users",
                    principalColumn: "userid",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_invitationcodes_code",
            table: "invitationcodes",
            column: "code",
            unique: true,
            filter: "status = 'Pending'");

        migrationBuilder.CreateIndex(
            name: "idx_invitationcodes_expiresat",
            table: "invitationcodes",
            column: "expiresat");

        migrationBuilder.CreateIndex(
            name: "idx_invitationcodes_initiator",
            table: "invitationcodes",
            column: "initiatoruserid");

        migrationBuilder.CreateIndex(
            name: "idx_invitationcodes_claimedby",
            table: "invitationcodes",
            column: "claimedbyuserid",
            filter: "claimedbyuserid IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "idx_invitationcodes_status_expiresat",
            table: "invitationcodes",
            columns: new[] { "status", "expiresat" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "invitationcodes");
    }
}
