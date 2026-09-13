using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Settlement's Proposed column (Phase 5b, approved 13 September): the latest
/// AI proposal on each decision field and how staff resolved it. The current
/// field row keeps only the value in force, and a staff confirmation of the
/// same value overwrites its Automation provenance, so Accepted and Corrected
/// cannot be derived from it. One row per Case and field; a later Automation
/// proposal replaces it. Nothing is backfilled: a proposal already in force
/// before this migration is still read from its unconfirmed field row until
/// the next write.
///
/// Both runtime roles write field values (Web saves, Worker-hosted Automation
/// work), so both read, insert and update proposals; neither deletes one.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913200000_CaseFieldProposals")]
public partial class CaseFieldProposals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.CreateTable(
            name: "CaseFieldProposals",
            columns: table => new
            {
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FieldPath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ProposedValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                ProposedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ProposedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Resolution = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                ResolvedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseFieldProposals", x => new { x.CaseId, x.FieldPath });
                table.CheckConstraint("CK_CaseFieldProposals_Resolution", "[Resolution] IS NULL OR [Resolution] IN ('Accepted', 'Corrected')");
                table.ForeignKey(
                    name: "FK_CaseFieldProposals_Cases_CaseId",
                    column: x => x.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseFieldProposals] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[CaseFieldProposals] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseFieldProposals] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[CaseFieldProposals] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CaseFieldProposals");
    }
}
