using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarGuard.API.Migrations
{
    /// <inheritdoc />
    public partial class _20260603190000AddSyncLogResolutionSourceAndAuditBrinIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "resolution_source",
                table: "sync_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    CREATE INDEX IF NOT EXISTS ""IX_SyncLogs_ChildId_CreatedAt""
                        ON sync_logs (child_id, created_at DESC);
                ");
            }
            else
            {
                migrationBuilder.Sql(@"
                    CREATE INDEX IF NOT EXISTS ""IX_SyncLogs_ChildId_CreatedAt""
                        ON sync_logs (child_id, created_at DESC);
                ");
            }

            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_CreatedAt_Brin""
                        ON audit_logs
                        USING BRIN (created_at)
                        WITH (pages_per_range = 32);
                ");
            }
            else
            {
                migrationBuilder.Sql(@"
                    CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_CreatedAt_BTree""
                        ON audit_logs (created_at DESC);
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    DROP INDEX IF EXISTS ""IX_AuditLogs_CreatedAt_Brin"";
                ");
            }
            else
            {
                migrationBuilder.Sql(@"
                    DROP INDEX IF EXISTS ""IX_AuditLogs_CreatedAt_BTree"";
                ");
            }

            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_SyncLogs_ChildId_CreatedAt"";
            ");

            migrationBuilder.DropColumn(
                name: "resolution_source",
                table: "sync_logs");
        }
    }
}
