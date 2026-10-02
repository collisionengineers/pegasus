using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Dismiss on the Work Centre (FRD-15, 2 October 2026): one row per dismissed
/// record (a Case, Unidentified item or AI job) holding when it was last
/// dismissed and by whom. Its rows that began at or before then are hidden.
///
/// Web alone writes the table: it inserts a first dismissal and moves a later
/// one on, and is denied DELETE, as there is no undo. The Worker has no part in
/// it and is denied DELETE too.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261002105641_WorkCentreDismissals")]
public partial class WorkCentreDismissals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.CreateTable(
            name: "WorkCentreDismissals",
            columns: table => new
            {
                RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DismissedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DismissedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkCentreDismissals", x => x.RecordId);
            });

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[WorkCentreDismissals] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[WorkCentreDismissals] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                DENY DELETE ON OBJECT::[dbo].[WorkCentreDismissals] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WorkCentreDismissals");
    }
}
