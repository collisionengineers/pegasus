using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729192000_TriageReplaySnapshots")]
public sealed class TriageReplaySnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [Triage])
                THROW 51000, 'Existing Triage records lack authoritative post-operation snapshots. Migrate that retained history explicitly before applying TriageReplaySnapshots.', 1;
            """);

        migrationBuilder.AddColumn<Guid>(
            name: "ConcurrencyToken",
            table: "Triage",
            type: "uniqueidentifier",
            nullable: false);

        migrationBuilder.AddColumn<Guid>(
            name: "AfterAssigneeId",
            table: "TriageHistory",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "AfterLinkedCaseId",
            table: "TriageHistory",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AfterState",
            table: "TriageHistory",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AfterState",
            table: "TriageHistory");

        migrationBuilder.DropColumn(
            name: "AfterLinkedCaseId",
            table: "TriageHistory");

        migrationBuilder.DropColumn(
            name: "AfterAssigneeId",
            table: "TriageHistory");

        migrationBuilder.DropColumn(
            name: "ConcurrencyToken",
            table: "Triage");
    }
}
