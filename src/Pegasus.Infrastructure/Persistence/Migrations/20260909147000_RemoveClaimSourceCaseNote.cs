using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260909147000_RemoveClaimSourceCaseNote")]
public partial class RemoveClaimSourceCaseNote : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM dbo.CaseDataFields
                WHERE FieldName = N'claim_source_case_note'
                  AND ValueKind = N'confirmed'
                  AND DATALENGTH(Value) > 4000)
                THROW 51011, N'Claim Source Case Note exceeds the 2000-character Case Note limit. Correct it before applying this migration.', 1;

            INSERT INTO dbo.CaseWorkflowEvents
                (Id, CaseId, EventType, OperationKey, RequestHash, ActorKind, ActorSubjectId,
                 ActorRolesJson, Reason, OccurredAtUtc, BeforeVersion, AfterVersion)
            SELECT NEWID(),
                   field.CaseId,
                   N'operator_note',
                   N'legacy-claim-source-note:' + CONVERT(nvarchar(36), field.CaseId),
                   CONVERT(nchar(64), CONVERT(varchar(64), HASHBYTES('SHA2_256',
                       N'legacy-claim-source-note:' + CONVERT(nvarchar(36), field.CaseId)), 2)),
                   N'Staff',
                   field.ConfirmedByActor,
                   N'[]',
                   field.Value,
                   field.ConfirmedAtUtc,
                   workflow.Version,
                   workflow.Version
            FROM dbo.CaseDataFields AS field
            INNER JOIN dbo.CaseWorkflows AS workflow ON workflow.CaseId = field.CaseId
            WHERE field.FieldName = N'claim_source_case_note'
              AND field.ValueKind = N'confirmed'
              AND NULLIF(LTRIM(RTRIM(field.Value)), N'') IS NOT NULL;

            DELETE FROM dbo.CaseDataFields WHERE FieldName = N'claim_source_case_note';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "The obsolete Claim Source Case Note field is removed in this pre-release migration.");
}
