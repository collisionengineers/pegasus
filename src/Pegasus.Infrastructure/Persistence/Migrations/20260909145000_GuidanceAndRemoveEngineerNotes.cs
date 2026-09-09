using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260909145000_GuidanceAndRemoveEngineerNotes")]
public partial class GuidanceAndRemoveEngineerNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM dbo.EngineerNotes WHERE DATALENGTH(Note) > 4000)
                THROW 51010, N'Engineer note exceeds the 2000-character Case Note limit. Correct it before applying this migration.', 1;
            """);

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents");

        migrationBuilder.AlterColumn<string>(
            name: "Reason",
            table: "CaseWorkflowEvents",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500);

        migrationBuilder.Sql(
            """
            INSERT INTO dbo.CaseWorkflowEvents
                (Id, CaseId, EventType, OperationKey, RequestHash, ActorKind, ActorSubjectId,
                 ActorRolesJson, Reason, OccurredAtUtc, BeforeVersion, AfterVersion)
            SELECT note.Id,
                   note.CaseId,
                   N'operator_note',
                   N'legacy-engineer-note:' + CONVERT(nvarchar(36), note.Id),
                   CONVERT(nchar(64), CONVERT(varchar(64), HASHBYTES('SHA2_256',
                       N'legacy-engineer-note:' + CONVERT(nvarchar(36), note.Id)), 2)),
                   note.RecordedByKind,
                   note.RecordedBySubjectId,
                   note.RecordedByRolesJson,
                   note.Note,
                   note.RecordedAtUtc,
                   workflow.Version,
                   workflow.Version
            FROM dbo.EngineerNotes AS note
            INNER JOIN dbo.CaseWorkflows AS workflow ON workflow.CaseId = note.CaseId;
            """);

        migrationBuilder.DropTable(name: "EngineerNotes");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents",
            columns: new[] { "CaseId", "AfterVersion" },
            unique: true,
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied'");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Engineer Notes are preserved as Case timeline entries and cannot be downgraded safely.");
}
