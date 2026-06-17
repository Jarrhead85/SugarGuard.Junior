using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations;

/// <inheritdoc />
[Migration("20260601000005AddOnboardingFields")]
public partial class AddOnboardingFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "onboardingcompleted",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "onboardingcompletedat",
            table: "users",
            type: "timestamp with time zone",
            nullable: true,
            defaultValue: null);

        migrationBuilder.AddColumn<int>(
            name: "onboardingcurrentstep",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "onboardingstartedat",
            table: "users",
            type: "timestamp with time zone",
            nullable: true,
            defaultValue: null);

        migrationBuilder.AddColumn<DateTime>(
            name: "onboardingskippedat",
            table: "users",
            type: "timestamp with time zone",
            nullable: true,
            defaultValue: null);

        migrationBuilder.CreateIndex(
            name: "idx_users_onboarding_incomplete",
            table: "users",
            column: "onboardingcompleted",
            filter: "onboardingcompleted = false");

        migrationBuilder.AddColumn<bool>(
            name: "setupcompleted",
            table: "children",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "setupcompletedat",
            table: "children",
            type: "timestamp with time zone",
            nullable: true,
            defaultValue: null);

        migrationBuilder.CreateIndex(
            name: "idx_children_setup_incomplete",
            table: "children",
            column: "setupcompleted",
            filter: "setupcompleted = false");

        migrationBuilder.CreateTable(
            name: "onboardingevents",
            columns: table => new
            {
                onboardingeventid = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                userid = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),

                stepnumber = table.Column<int>(
                    type: "integer",
                    nullable: false),

                stepname = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),

                eventtype = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false),

                userrole = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),

                durationonsecond = table.Column<int>(
                    type: "integer",
                    nullable: true),

                metadata = table.Column<string>(
                    type: "jsonb",
                    nullable: true),

                requestip = table.Column<string>(
                    type: "character varying(45)",
                    maxLength: 45,
                    nullable: true),

                createdat = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValueSql: "NOW()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("PKonboardingevents", x => x.onboardingeventid);

                table.ForeignKey(
                    name: "FKonboardingeventsusers",
                    column: x => x.userid,
                    principalTable: "users",
                    principalColumn: "userid",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_onboardingevents_step_type",
            table: "onboardingevents",
            columns: new[] { "stepnumber", "eventtype" },
            filter: "eventtype IN ('started', 'completed')");

        migrationBuilder.CreateIndex(
            name: "idx_onboardingevents_userid_step",
            table: "onboardingevents",
            columns: new[] { "userid", "stepnumber" });

        migrationBuilder.CreateIndex(
            name: "idx_onboardingevents_role_type",
            table: "onboardingevents",
            columns: new[] { "userrole", "eventtype" });

        migrationBuilder.CreateIndex(
            name: "idx_onboardingevents_createdat",
            table: "onboardingevents",
            column: "createdat");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "onboardingevents");

        migrationBuilder.DropIndex(
            name: "idx_children_setup_incomplete",
            table: "children");

        migrationBuilder.DropColumn(
            name: "setupcompletedat",
            table: "children");

        migrationBuilder.DropColumn(
            name: "setupcompleted",
            table: "children");

        migrationBuilder.DropIndex(
            name: "idx_users_onboarding_incomplete",
            table: "users");

        migrationBuilder.DropColumn(
            name: "onboardingskippedat",
            table: "users");

        migrationBuilder.DropColumn(
            name: "onboardingstartedat",
            table: "users");

        migrationBuilder.DropColumn(
            name: "onboardingcurrentstep",
            table: "users");

        migrationBuilder.DropColumn(
            name: "onboardingcompletedat",
            table: "users");

        migrationBuilder.DropColumn(
            name: "onboardingcompleted",
            table: "users");
    }
}
