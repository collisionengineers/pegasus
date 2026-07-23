using CollisionSpike.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollisionSpike.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CollisionSpikeDbContext))]
[Migration("20260723171000_RemoveRetiredQdosCaseAllocation")]
public sealed class RemoveRetiredQdosCaseAllocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql("""
                INSERT INTO [AuditEvents]
                    ([Id], [IntakeReceiptId], [EventType], [Actor], [OccurredAtUtc], [DetailsJson])
                SELECT
                    NEWID(),
                    receipt.[Id],
                    N'RetiredLocalCaseAllocationPreserved',
                    N'schema-migration',
                    allocatedCase.[CreatedAtUtc],
                    (
                        SELECT
                            CAST(1 AS bit) AS [retiredLocalProof],
                            allocatedCase.[PrincipalCode] AS [principalCode],
                            allocatedCase.[CaseReference] AS [caseReference],
                            allocatedCase.[CreatedAtUtc] AS [createdAtUtc],
                            counter.[Year] AS [counterYear],
                            counter.[CurrentSequence] AS [counterCurrentSequence]
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                    )
                FROM [QdosIntakeReceipts] AS receipt
                INNER JOIN [Cases] AS allocatedCase ON allocatedCase.[Id] = receipt.[CaseId]
                LEFT JOIN [PrincipalYearCounters] AS counter
                    ON counter.[PrincipalCode] = allocatedCase.[PrincipalCode]
                    AND counter.[Year] = DATEPART(year, allocatedCase.[CreatedAtUtc]);
                """);
        }
        else if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            migrationBuilder.Sql("""
                INSERT INTO "AuditEvents"
                    ("Id", "IntakeReceiptId", "EventType", "Actor", "OccurredAtUtc", "DetailsJson")
                SELECT
                    lower(hex(randomblob(16))),
                    receipt."Id",
                    'RetiredLocalCaseAllocationPreserved',
                    'schema-migration',
                    allocatedCase."CreatedAtUtc",
                    json_object(
                        'retiredLocalProof', 1,
                        'principalCode', allocatedCase."PrincipalCode",
                        'caseReference', allocatedCase."CaseReference",
                        'createdAtUtc', allocatedCase."CreatedAtUtc",
                        'counterYear', counter."Year",
                        'counterCurrentSequence', counter."CurrentSequence")
                FROM "QdosIntakeReceipts" AS receipt
                INNER JOIN "Cases" AS allocatedCase ON allocatedCase."Id" = receipt."CaseId"
                LEFT JOIN "PrincipalYearCounters" AS counter
                    ON counter."PrincipalCode" = allocatedCase."PrincipalCode"
                    AND counter."Year" = CAST(strftime('%Y', allocatedCase."CreatedAtUtc") AS INTEGER);
                """);
        }
        else
        {
            throw new NotSupportedException($"The {ActiveProvider} database provider is not supported.");
        }

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditEvents_Cases_CaseId",
                table: "AuditEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_QdosIntakeReceipts_Cases_CaseId",
                table: "QdosIntakeReceipts");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_CaseId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_QdosIntakeReceipts_CaseId",
                table: "QdosIntakeReceipts");

            migrationBuilder.DropColumn(
                name: "CaseId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "CaseId",
                table: "QdosIntakeReceipts");

            migrationBuilder.DropTable(name: "Cases");
            migrationBuilder.DropTable(name: "PrincipalYearCounters");
        }
        else if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            migrationBuilder.Sql("""
                PRAGMA foreign_keys=OFF;
                BEGIN TRANSACTION;

                CREATE TABLE "__new_AuditEvents" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AuditEvents" PRIMARY KEY,
                    "IntakeReceiptId" TEXT NOT NULL,
                    "EventType" TEXT NOT NULL,
                    "Actor" TEXT NOT NULL,
                    "OccurredAtUtc" TEXT NOT NULL,
                    "DetailsJson" TEXT NOT NULL,
                    CONSTRAINT "FK_AuditEvents_QdosIntakeReceipts_IntakeReceiptId"
                        FOREIGN KEY ("IntakeReceiptId") REFERENCES "QdosIntakeReceipts" ("Id")
                        ON DELETE RESTRICT
                );
                INSERT INTO "__new_AuditEvents"
                    ("Id", "IntakeReceiptId", "EventType", "Actor", "OccurredAtUtc", "DetailsJson")
                SELECT "Id", "IntakeReceiptId", "EventType", "Actor", "OccurredAtUtc", "DetailsJson"
                FROM "AuditEvents";
                DROP TABLE "AuditEvents";
                ALTER TABLE "__new_AuditEvents" RENAME TO "AuditEvents";
                CREATE INDEX "IX_AuditEvents_IntakeReceiptId" ON "AuditEvents" ("IntakeReceiptId");

                CREATE TABLE "__new_QdosIntakeReceipts" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_QdosIntakeReceipts" PRIMARY KEY,
                    "SourceFileName" TEXT NOT NULL,
                    "MediaType" TEXT NOT NULL,
                    "SourceLength" INTEGER NOT NULL,
                    "SourceHash" TEXT NOT NULL,
                    "SourceChannel" TEXT NOT NULL,
                    "ExternalReceiptToken" TEXT NOT NULL,
                    "ReceivedAtUtc" TEXT NOT NULL,
                    "Decision" TEXT NOT NULL,
                    "DecisionReason" TEXT NOT NULL,
                    "EvidenceJson" TEXT NOT NULL,
                    "FieldsJson" TEXT NOT NULL,
                    "FailureCode" TEXT NULL,
                    "FailureReason" TEXT NULL,
                    "OcrCandidatesJson" TEXT NOT NULL
                );
                INSERT INTO "__new_QdosIntakeReceipts"
                    ("Id", "SourceFileName", "MediaType", "SourceLength", "SourceHash",
                     "SourceChannel", "ExternalReceiptToken", "ReceivedAtUtc", "Decision",
                     "DecisionReason", "EvidenceJson", "FieldsJson", "FailureCode",
                     "FailureReason", "OcrCandidatesJson")
                SELECT
                    "Id", "SourceFileName", "MediaType", "SourceLength", "SourceHash",
                    "SourceChannel", "ExternalReceiptToken", "ReceivedAtUtc", "Decision",
                    "DecisionReason", "EvidenceJson", "FieldsJson", "FailureCode",
                    "FailureReason", "OcrCandidatesJson"
                FROM "QdosIntakeReceipts";
                DROP TABLE "QdosIntakeReceipts";
                ALTER TABLE "__new_QdosIntakeReceipts" RENAME TO "QdosIntakeReceipts";
                CREATE INDEX "IX_QdosIntakeReceipts_SourceHash" ON "QdosIntakeReceipts" ("SourceHash");
                CREATE UNIQUE INDEX "IX_QdosIntakeReceipts_SourceChannel_ExternalReceiptToken"
                    ON "QdosIntakeReceipts" ("SourceChannel", "ExternalReceiptToken");

                DROP TABLE "Cases";
                DROP TABLE "PrincipalYearCounters";

                COMMIT;
                PRAGMA foreign_keys=ON;
                """,
                suppressTransaction: true);
        }
        else
        {
            throw new NotSupportedException($"The {ActiveProvider} database provider is not supported.");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "The retired Development allocation schema cannot be restored without inventing deleted ownership links. " +
            "Use the preserved audit evidence and a forward migration.");
    }
}
