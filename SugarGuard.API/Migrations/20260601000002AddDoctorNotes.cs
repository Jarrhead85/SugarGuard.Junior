using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <inheritdoc />
[Migration("20260601000002AddDoctorNotes")]
public partial class AddDoctorNotes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "doctornotes",
            columns: table => new
            {
                doctornoteid = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                childid = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                measurementid = table.Column<Guid>(
                    type: "uuid",
                    nullable: true),

                doctoruserid = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                notetext = table.Column<string>(
                    type: "text",
                    nullable: false),

                notetype = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false,
                    defaultValue: "Observation"),

                urgency = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false,
                    defaultValue: "Normal"),

                isvisibletoparent = table.Column<bool>(
                    type: "boolean",
                    nullable: false,
                    defaultValue: true),

                isdeleted = table.Column<bool>(
                    type: "boolean",
                    nullable: false,
                    defaultValue: false),

                isimportant = table.Column<bool>(
                    type: "boolean",
                    nullable: false,
                    defaultValue: false),

                createdat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValueSql: "NOW()"),

                updatedat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PKdoctornotes", x => x.doctornoteid);

                table.ForeignKey(
                    name: "FKdoctornoteschildrenchildid",
                    column: x => x.childid,
                    principalTable: "children",
                    principalColumn: "childid",
                    onDelete: ReferentialAction.Cascade);

                table.ForeignKey(
                    name: "FKdoctornotesmeasurementsmeasurementid",
                    column: x => x.measurementid,
                    principalTable: "measurements",
                    principalColumn: "measurementid",
                    onDelete: ReferentialAction.SetNull);

                table.ForeignKey(
                    name: "FKdoctornotesusersdoctoruserid",
                    column: x => x.doctoruserid,
                    principalTable: "users",
                    principalColumn: "userid",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_doctornotes_child_createdat_active",
            table: "doctornotes",
            columns: new[] { "childid", "createdat" },
            filter: "isdeleted = false");

        migrationBuilder.CreateIndex(
            name: "idx_doctornotes_doctoruserid",
            table: "doctornotes",
            column: "doctoruserid");

        migrationBuilder.CreateIndex(
            name: "idx_doctornotes_measurementid",
            table: "doctornotes",
            column: "measurementid",
            filter: "measurementid IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "idx_doctornotes_child_urgency_visible",
            table: "doctornotes",
            columns: new[] { "childid", "urgency" },
            filter: "isdeleted = false AND isvisibletoparent = true");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "doctornotes");
    }
}
