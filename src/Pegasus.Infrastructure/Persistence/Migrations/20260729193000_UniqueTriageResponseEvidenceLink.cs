using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729193000_UniqueTriageResponseEvidenceLink")]
public sealed class UniqueTriageResponseEvidenceLink : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM [TriageResponseEvidenceLinks]
                GROUP BY [TriageId]
                HAVING COUNT(*) > 1)
                THROW 51000, 'A Triage record has multiple current response-evidence links. Resolve the retained evidence conflict before applying UniqueTriageResponseEvidenceLink.', 1;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_TriageResponseEvidenceLinks_TriageId",
            table: "TriageResponseEvidenceLinks",
            column: "TriageId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TriageResponseEvidenceLinks_TriageId",
            table: "TriageResponseEvidenceLinks");
    }
}
