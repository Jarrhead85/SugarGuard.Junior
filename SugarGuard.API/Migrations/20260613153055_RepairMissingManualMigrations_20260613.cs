using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingManualMigrations_20260613 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pgcrypto;

                ALTER TABLE users ADD COLUMN IF NOT EXISTS email_for_login character varying(256);
                ALTER TABLE users ADD COLUMN IF NOT EXISTS isemailverified boolean NOT NULL DEFAULT false;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS emailverifiedat timestamp with time zone;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS isactive boolean NOT NULL DEFAULT true;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS deactivatedat timestamp with time zone;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS onboardingcompleted boolean NOT NULL DEFAULT true;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS onboardingcompletedat timestamp with time zone;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS onboardingcurrentstep integer NOT NULL DEFAULT 0;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS onboardingstartedat timestamp with time zone;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS onboardingskippedat timestamp with time zone;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email_for_login
                    ON users (email_for_login)
                    WHERE email_for_login IS NOT NULL;
                CREATE INDEX IF NOT EXISTS idx_users_onboarding_incomplete
                    ON users (onboardingcompleted)
                    WHERE onboardingcompleted = false;
                CREATE INDEX IF NOT EXISTS idx_users_isactive_deactivated
                    ON users (isactive)
                    WHERE isactive = false;

                ALTER TABLE children ADD COLUMN IF NOT EXISTS setupcompleted boolean NOT NULL DEFAULT true;
                ALTER TABLE children ADD COLUMN IF NOT EXISTS setupcompletedat timestamp with time zone;
                ALTER TABLE children ADD COLUMN IF NOT EXISTS photo_url character varying(500);
                CREATE INDEX IF NOT EXISTS idx_children_setup_incomplete
                    ON children (setupcompleted)
                    WHERE setupcompleted = false;

                CREATE TABLE IF NOT EXISTS parent_child_links (
                    link_id uuid PRIMARY KEY,
                    parent_user_id uuid NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    child_id uuid NOT NULL REFERENCES children(child_id) ON DELETE CASCADE,
                    created_at timestamp with time zone NOT NULL,
                    linkedbyuserid uuid,
                    notes character varying(1000)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_parent_child_links_parent_user_id_child_id"
                    ON parent_child_links (parent_user_id, child_id);
                CREATE INDEX IF NOT EXISTS "IX_parent_child_links_child_id"
                    ON parent_child_links (child_id);

                ALTER TABLE doctor_child_links ADD COLUMN IF NOT EXISTS isactive boolean NOT NULL DEFAULT true;
                ALTER TABLE doctor_child_links ADD COLUMN IF NOT EXISTS deactivatedat timestamp with time zone;
                ALTER TABLE doctor_child_links ADD COLUMN IF NOT EXISTS linkedbyuserid uuid;
                ALTER TABLE doctor_child_links ADD COLUMN IF NOT EXISTS notes character varying(1000);

                CREATE TABLE IF NOT EXISTS doctornotes (
                    noteid uuid PRIMARY KEY,
                    doctoruserid uuid NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    childid uuid NOT NULL REFERENCES children(child_id) ON DELETE CASCADE,
                    measurementid uuid REFERENCES measurements(measurement_id) ON DELETE SET NULL,
                    notetext character varying(4000) NOT NULL,
                    isimportant boolean NOT NULL DEFAULT false,
                    createdat timestamp with time zone NOT NULL DEFAULT NOW(),
                    updatedat timestamp with time zone
                );
                CREATE INDEX IF NOT EXISTS "IXdoctornoteschildidcreatedat"
                    ON doctornotes (childid, createdat);
                CREATE INDEX IF NOT EXISTS "IXdoctornotesdoctoruseridchildid"
                    ON doctornotes (doctoruserid, childid);
                CREATE INDEX IF NOT EXISTS "IXdoctornotesmeasurementid"
                    ON doctornotes (measurementid)
                    WHERE measurementid IS NOT NULL;

                CREATE TABLE IF NOT EXISTS invitationcodes (
                    invitecodeid uuid PRIMARY KEY,
                    childid uuid NOT NULL REFERENCES children(child_id) ON DELETE CASCADE,
                    code character varying(8) NOT NULL,
                    targetrole character varying(32) NOT NULL,
                    status character varying(16) NOT NULL DEFAULT 'Pending',
                    expiresat timestamp with time zone NOT NULL,
                    claimedbydyuserid uuid REFERENCES users(user_id),
                    createdat timestamp with time zone NOT NULL DEFAULT NOW()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_invitecodes_code
                    ON invitationcodes (code);
                CREATE INDEX IF NOT EXISTS ix_invitecodes_child_expires
                    ON invitationcodes (childid, expiresat);

                CREATE TABLE IF NOT EXISTS "RefreshTokens" (
                    "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    "Token" character varying(64) NOT NULL,
                    "UserId" character varying(128) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "ExpiresAt" timestamp with time zone NOT NULL,
                    "IsRevoked" boolean NOT NULL DEFAULT false,
                    "RevokedAt" timestamp with time zone,
                    "RevokedReason" character varying(64),
                    "ReplacedByToken" character varying(64),
                    "CreatedByIp" character varying(64),
                    "CreatedByUserAgent" character varying(256)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_RefreshTokens_Token"
                    ON "RefreshTokens" ("Token");
                CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_UserId"
                    ON "RefreshTokens" ("UserId");
                CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_UserId_ExpiresAt"
                    ON "RefreshTokens" ("UserId", "ExpiresAt");

                CREATE TABLE IF NOT EXISTS onboardingevents (
                    onboardingeventid uuid PRIMARY KEY,
                    userid uuid NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    stepnumber integer NOT NULL,
                    stepname character varying(64) NOT NULL,
                    eventtype character varying(16) NOT NULL,
                    userrole character varying(32) NOT NULL,
                    durationonsecond integer,
                    metadata jsonb,
                    requestip character varying(45),
                    createdat timestamp with time zone NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idxonboardingeventssteptype
                    ON onboardingevents (stepnumber, eventtype)
                    WHERE eventtype IN ('started', 'completed');
                CREATE INDEX IF NOT EXISTS idxonboardingeventsuseridstep
                    ON onboardingevents (userid, stepnumber);
                CREATE INDEX IF NOT EXISTS idxonboardingeventsroletype
                    ON onboardingevents (userrole, eventtype);
                CREATE INDEX IF NOT EXISTS idxonboardingeventscreatedat
                    ON onboardingevents (createdat);

                ALTER TABLE sync_logs ADD COLUMN IF NOT EXISTS resolution_source character varying(32);

                ALTER TABLE measurements
                    ADD COLUMN IF NOT EXISTS glucose_ui_state character varying(16)
                    GENERATED ALWAYS AS (
                        CASE
                            WHEN glucose_value <= 3.1 OR glucose_value > 15.0 THEN 'Critical'
                            WHEN glucose_value >= 4.0 AND glucose_value <= 10.0 THEN 'Normal'
                            ELSE 'Attention'
                        END
                    ) STORED;
                CREATE INDEX IF NOT EXISTS idx_measurements_child_uistate_time
                    ON measurements (child_id, glucose_ui_state, measurement_time);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
