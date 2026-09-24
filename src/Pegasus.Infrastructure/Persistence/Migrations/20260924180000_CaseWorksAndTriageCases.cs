using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// One Case with per-work data, and Triage as a Case (Stage 2, 24 September
/// 2026). Every Case gains its primary work in <c>CaseWorks</c> (the work's id
/// is the Case id); the ten per-work tables key on <c>WorkId</c> instead of
/// <c>CaseId</c>; a report generation records its work. The linked Audit Case
/// (<c>Cases.AuditOfCaseId</c>) and the recorded engineer finding are removed,
/// so the sequence index no longer needs its filter. A Triage row keys on its
/// own Case, carries no reference, sequence or Principal of its own, and may
/// have no origin receipt; <c>TriageSequences</c> is dropped.
/// Forward only, and it refuses to run over data it cannot carry: a linked
/// Audit Case, an Audit reference, a Triage row or Triage-type Case, an
/// engineer finding, or an applied valuation without its Case. The approved
/// pegasus-wipe-intake-data reset clears them first.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260924180000_CaseWorksAndTriageCases")]
public partial class CaseWorksAndTriageCases : Migration
{
    // The per-work tables whose CaseId pointed at Cases (AppliedValuationSnapshots
    // had no foreign key): the old key, the indexes named after CaseId, and the
    // key to CaseWorks that replaces it. CaseDataFields follows its snapshot.
    private static readonly (string Table, string OldForeignKey, (string Name, string NewName)[] Indexes, string NewForeignKey)[] WorkTables =
    [
        ("CaseDataSnapshots", "FK_CaseDataSnapshots_Cases_CaseId", [], "FK_CaseDataSnapshots_CaseWorks_WorkId"),
        ("CaseAssessmentFields", "FK_CaseAssessmentFields_Cases_CaseId", [], "FK_CaseAssessmentFields_CaseWorks_WorkId"),
        ("CaseFieldProposals", "FK_CaseFieldProposals_Cases_CaseId", [], "FK_CaseFieldProposals_CaseWorks_WorkId"),
        ("CaseRepairSpecifications", "FK_CaseRepairSpecifications_Cases_CaseId",
            [
                ("IX_CaseRepairSpecifications_CaseId", "IX_CaseRepairSpecifications_WorkId"),
                ("IX_CaseRepairSpecifications_CaseId_CreationOperationKey", "IX_CaseRepairSpecifications_WorkId_CreationOperationKey"),
                ("IX_CaseRepairSpecifications_CaseId_Version", "IX_CaseRepairSpecifications_WorkId_Version")
            ],
            "FK_CaseRepairSpecifications_CaseWorks_WorkId"),
        ("CaseEstimateLines", "FK_CaseEstimateLines_Cases_CaseId",
            [("IX_CaseEstimateLines_CaseId", "IX_CaseEstimateLines_WorkId")],
            "FK_CaseEstimateLines_CaseWorks_WorkId"),
        ("CaseRepairSpecificationSnapshots", "FK_CaseRepairSpecificationSnapshots_Cases_CaseId",
            [("IX_CaseRepairSpecificationSnapshots_CaseId", "IX_CaseRepairSpecificationSnapshots_WorkId")],
            "FK_CaseRepairSpecificationSnapshots_CaseWorks_WorkId"),
        ("CaseValuations", "FK_CaseValuations_Cases_CaseId",
            [("IX_CaseValuations_CaseId_Date_Time", "IX_CaseValuations_WorkId_Date_Time")],
            "FK_CaseValuations_CaseWorks_WorkId"),
        ("AppliedValuationSnapshots", null,
            [
                ("IX_AppliedValuationSnapshots_CaseId_SnapshotHash", "IX_AppliedValuationSnapshots_WorkId_SnapshotHash"),
                ("IX_AppliedValuationSnapshots_CaseId_AcceptedAtUtc", "IX_AppliedValuationSnapshots_WorkId_AcceptedAtUtc")
            ],
            "FK_AppliedValuationSnapshots_CaseWorks_WorkId"),
        ("CaseReportWordings", "FK_CaseReportWordings_Cases_CaseId",
            [("IX_CaseReportWordings_CaseId_BlockKey", "IX_CaseReportWordings_WorkId_BlockKey")],
            "FK_CaseReportWordings_CaseWorks_WorkId")
    ];

    // The Triage children renamed TriageId -> TriageCaseId: the old key, the
    // indexes named after TriageId, and the key that replaces it.
    private static readonly (string Table, string OldForeignKey, (string Name, string NewName)[] Indexes, string NewForeignKey)[] TriageChildTables =
    [
        ("TriageFindings", "FK_TriageFindings_Triage_TriageId",
            [("IX_TriageFindings_TriageId_RecordedAtUtc", "IX_TriageFindings_TriageCaseId_RecordedAtUtc")],
            "FK_TriageFindings_Triage_TriageCaseId"),
        ("TriageHistory", "FK_TriageHistory_Triage_TriageId",
            [("IX_TriageHistory_TriageId_OccurredAtUtc", "IX_TriageHistory_TriageCaseId_OccurredAtUtc")],
            "FK_TriageHistory_Triage_TriageCaseId"),
        // Its unique index was created filtered on [TriageId], which sp_rename
        // cannot rename through, so it is dropped and recreated around the rename.
        ("TriageResponseEvidenceLinks", "FK_TriageResponseEvidenceLinks_Triage_TriageId",
            [],
            "FK_TriageResponseEvidenceLinks_Triage_TriageCaseId"),
        ("SentEmailEvidence", "FK_SentEmailEvidence_Triage_TriageId",
            [
                ("IX_SentEmailEvidence_TriageId", "IX_SentEmailEvidence_TriageCaseId"),
                ("IX_SentEmailEvidence_ChaseDueAtUtc_TriageId", "IX_SentEmailEvidence_ChaseDueAtUtc_TriageCaseId")
            ],
            "FK_SentEmailEvidence_Triage_TriageCaseId")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Fail closed over anything this schema cannot carry.
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [Cases] WHERE [AuditOfCaseId] IS NOT NULL)
                THROW 51000, 'A linked Audit Case exists. Run the approved pegasus-wipe-intake-data reset before applying CaseWorksAndTriageCases.', 1;
            IF EXISTS (SELECT 1 FROM [Cases] WHERE [AuditReference] IS NOT NULL)
                THROW 51000, 'An Audit reference exists. Run the approved pegasus-wipe-intake-data reset before applying CaseWorksAndTriageCases.', 1;
            IF EXISTS (SELECT 1 FROM [Cases] WHERE [Type] = N'triage')
                THROW 51000, 'A Triage-type Case exists. Run the approved pegasus-wipe-intake-data reset before applying CaseWorksAndTriageCases.', 1;
            IF EXISTS (SELECT 1 FROM [Triage])
                THROW 51000, 'A Triage record exists. Run the approved pegasus-wipe-intake-data reset before applying CaseWorksAndTriageCases.', 1;
            IF EXISTS (SELECT 1 FROM [CaseEngineerFindings])
                THROW 51000, 'An engineer finding exists. Run the approved pegasus-wipe-intake-data reset before applying CaseWorksAndTriageCases.', 1;
            IF EXISTS (
                SELECT 1
                FROM [AppliedValuationSnapshots] AS [snapshot]
                WHERE NOT EXISTS (SELECT 1 FROM [Cases] AS [case] WHERE [case].[Id] = [snapshot].[CaseId]))
                THROW 51000, 'An applied valuation without its Case exists. Run the approved pegasus-wipe-intake-data reset before applying CaseWorksAndTriageCases.', 1;
            """);

        // 2. Every Case's primary work shares the Case id.
        migrationBuilder.CreateTable(
            name: "CaseWorks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReportApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReportSentEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseWorks", x => x.Id);
                table.CheckConstraint("CK_CaseWorks_Kind", "[Kind] IN (N'primary', N'audit')");
                table.CheckConstraint("CK_CaseWorks_PrimaryId", "[Kind] <> N'primary' OR [Id] = [CaseId]");
                table.ForeignKey(
                    name: "FK_CaseWorks_CaseReportApprovals_ReportApprovalId",
                    column: x => x.ReportApprovalId,
                    principalTable: "CaseReportApprovals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CaseWorks_CaseReportSentEvidence_ReportSentEvidenceId",
                    column: x => x.ReportSentEvidenceId,
                    principalTable: "CaseReportSentEvidence",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CaseWorks_Cases_CaseId",
                    column: x => x.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        // Unique indexes on NOT NULL columns are written as SQL: this migration
        // has no target model, so EF would filter them on IS NOT NULL, and a
        // filter naming a column blocks a later rename of it.
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX [IX_CaseWorks_CaseId_Kind] ON [CaseWorks] ([CaseId], [Kind]);");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorks_ReportApprovalId",
            table: "CaseWorks",
            column: "ReportApprovalId",
            unique: true,
            filter: "[ReportApprovalId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorks_ReportSentEvidenceId",
            table: "CaseWorks",
            column: "ReportSentEvidenceId",
            unique: true,
            filter: "[ReportSentEvidenceId] IS NOT NULL");

        migrationBuilder.Sql(
            """
            INSERT INTO [CaseWorks] ([Id], [CaseId], [Kind], [CreatedAtUtc])
            SELECT [Id], [Id], N'primary', [CreatedAtUtc] FROM [Cases];
            """);

        // 3. The per-work tables key on the work. The typed-data fields hang
        // off their snapshot's key, so that key goes first and returns last.
        migrationBuilder.DropForeignKey(
            name: "FK_CaseDataFields_CaseDataSnapshots_CaseId",
            table: "CaseDataFields");

        foreach (var (table, oldForeignKey, indexes, newForeignKey) in WorkTables)
        {
            if (oldForeignKey is not null)
            {
                migrationBuilder.DropForeignKey(name: oldForeignKey, table: table);
            }

            migrationBuilder.RenameColumn(name: "CaseId", table: table, newName: "WorkId");
            foreach (var (name, newName) in indexes)
            {
                migrationBuilder.RenameIndex(name: name, table: table, newName: newName);
            }

            migrationBuilder.AddForeignKey(
                name: newForeignKey,
                table: table,
                column: "WorkId",
                principalTable: "CaseWorks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        migrationBuilder.RenameColumn(name: "CaseId", table: "CaseDataFields", newName: "WorkId");
        migrationBuilder.AddForeignKey(
            name: "FK_CaseDataFields_CaseDataSnapshots_WorkId",
            table: "CaseDataFields",
            column: "WorkId",
            principalTable: "CaseDataSnapshots",
            principalColumn: "WorkId",
            onDelete: ReferentialAction.Restrict);

        // 4. A report generation records its work; every existing one is the
        // primary work's. Uniqueness among live snapshots is per work.
        migrationBuilder.AddColumn<Guid>(
            name: "WorkId",
            table: "CaseReportGenerations",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql("UPDATE [CaseReportGenerations] SET [WorkId] = [CaseId];");

        migrationBuilder.AlterColumn<Guid>(
            name: "WorkId",
            table: "CaseReportGenerations",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.DropIndex(
            name: "IX_CaseReportGenerations_CaseId_SnapshotHash",
            table: "CaseReportGenerations");

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportGenerations_CaseId",
            table: "CaseReportGenerations",
            column: "CaseId");

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportGenerations_WorkId_SnapshotHash",
            table: "CaseReportGenerations",
            columns: new[] { "WorkId", "SnapshotHash" },
            unique: true,
            filter: "[State] <> N'Stale'");

        migrationBuilder.AddForeignKey(
            name: "FK_CaseReportGenerations_CaseWorks_WorkId",
            table: "CaseReportGenerations",
            column: "WorkId",
            principalTable: "CaseWorks",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        // 5. Cases: no linked Audit Case. The sequence index's filter reads
        // AuditOfCaseId, so it goes before the column and returns unfiltered.
        migrationBuilder.DropIndex(
            name: "IX_Cases_SequenceLineageId_Year_Sequence",
            table: "Cases");

        migrationBuilder.DropForeignKey(
            name: "FK_Cases_Cases_AuditOfCaseId",
            table: "Cases");

        migrationBuilder.DropIndex(
            name: "IX_Cases_AuditOfCaseId",
            table: "Cases");

        migrationBuilder.DropColumn(
            name: "AuditOfCaseId",
            table: "Cases");

        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX [IX_Cases_SequenceLineageId_Year_Sequence] ON [Cases] ([SequenceLineageId], [Year], [Sequence]);");

        migrationBuilder.AlterColumn<string>(
            name: "InitialState",
            table: "Cases",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(40)",
            oldMaxLength: 40);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Cases_InitialState",
            table: "Cases",
            sql: "([Type] = N'triage' AND [InitialState] IS NULL) OR ([Type] <> N'triage' AND [InitialState] IS NOT NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Cases_AuditReference",
            table: "Cases",
            sql: "[AuditReference] IS NULL OR ([Type] = N'inspection_and_audit' AND [AuditReference] = N'a.' + [Reference])");

        // 6. A Case document is filed in the Case folder or the Audit (a.) folder.
        migrationBuilder.AddColumn<string>(
            name: "CustodyFolder",
            table: "CaseDocuments",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "case");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseDocuments_CustodyFolder",
            table: "CaseDocuments",
            sql: "[CustodyFolder] IN (N'case', N'audit')");

        // 7. Triage is a Case (every Triage table is empty after step 1).
        foreach (var (table, oldForeignKey, _, _) in TriageChildTables)
        {
            migrationBuilder.DropForeignKey(name: oldForeignKey, table: table);
        }

        migrationBuilder.DropCheckConstraint(
            name: "CK_Triage_Sequence",
            table: "Triage");

        migrationBuilder.DropIndex(
            name: "IX_Triage_Sequence",
            table: "Triage");

        migrationBuilder.DropIndex(
            name: "IX_Triage_Reference",
            table: "Triage");

        migrationBuilder.DropForeignKey(
            name: "FK_Triage_Principals_PrincipalId",
            table: "Triage");

        migrationBuilder.DropIndex(
            name: "IX_Triage_PrincipalId",
            table: "Triage");

        migrationBuilder.DropColumn(
            name: "Sequence",
            table: "Triage");

        migrationBuilder.DropColumn(
            name: "Reference",
            table: "Triage");

        migrationBuilder.DropColumn(
            name: "PrincipalId",
            table: "Triage");

        // The Triage row keys on its Case; PK_Triage keeps its name.
        migrationBuilder.RenameColumn(
            name: "Id",
            table: "Triage",
            newName: "CaseId");

        migrationBuilder.AddForeignKey(
            name: "FK_Triage_Cases_CaseId",
            table: "Triage",
            column: "CaseId",
            principalTable: "Cases",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropForeignKey(
            name: "FK_Triage_Cases_LinkedCaseId",
            table: "Triage");

        migrationBuilder.RenameColumn(
            name: "LinkedCaseId",
            table: "Triage",
            newName: "LinkedInstructionCaseId");

        migrationBuilder.RenameIndex(
            name: "IX_Triage_LinkedCaseId",
            table: "Triage",
            newName: "IX_Triage_LinkedInstructionCaseId");

        migrationBuilder.AddForeignKey(
            name: "FK_Triage_Cases_LinkedInstructionCaseId",
            table: "Triage",
            column: "LinkedInstructionCaseId",
            principalTable: "Cases",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        // A manual Triage has no origin receipt: the five origin columns are
        // all present or all absent.
        migrationBuilder.DropForeignKey(
            name: "FK_Triage_IntakeReceipts_OriginReceiptId",
            table: "Triage");

        migrationBuilder.DropIndex(
            name: "IX_Triage_OriginReceiptId",
            table: "Triage");

        migrationBuilder.DropIndex(
            name: "IX_Triage_SourceChannel_ExternalReceiptToken",
            table: "Triage");

        migrationBuilder.AlterColumn<Guid>(
            name: "OriginReceiptId",
            table: "Triage",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AlterColumn<string>(
            name: "SourceChannel",
            table: "Triage",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(40)",
            oldMaxLength: 40);

        migrationBuilder.AlterColumn<string>(
            name: "ExternalReceiptToken",
            table: "Triage",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);

        migrationBuilder.AlterColumn<string>(
            name: "SourceHash",
            table: "Triage",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nchar(64)",
            oldFixedLength: true,
            oldMaxLength: 64);

        migrationBuilder.AlterColumn<Guid>(
            name: "EvaluationRevisionId",
            table: "Triage",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.CreateIndex(
            name: "IX_Triage_OriginReceiptId",
            table: "Triage",
            column: "OriginReceiptId",
            unique: true,
            filter: "[OriginReceiptId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Triage_SourceChannel_ExternalReceiptToken",
            table: "Triage",
            columns: new[] { "SourceChannel", "ExternalReceiptToken" },
            unique: true,
            filter: "[SourceChannel] IS NOT NULL AND [ExternalReceiptToken] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_Triage_IntakeReceipts_OriginReceiptId",
            table: "Triage",
            column: "OriginReceiptId",
            principalTable: "IntakeReceipts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Triage_Origin",
            table: "Triage",
            sql: "([OriginReceiptId] IS NULL AND [SourceChannel] IS NULL AND [ExternalReceiptToken] IS NULL AND [SourceHash] IS NULL AND [EvaluationRevisionId] IS NULL) OR ([OriginReceiptId] IS NOT NULL AND [SourceChannel] IS NOT NULL AND [ExternalReceiptToken] IS NOT NULL AND [SourceHash] IS NOT NULL AND [EvaluationRevisionId] IS NOT NULL)");

        migrationBuilder.RenameColumn(
            name: "AfterLinkedCaseId",
            table: "TriageHistory",
            newName: "AfterLinkedInstructionCaseId");

        migrationBuilder.DropIndex(
            name: "IX_TriageResponseEvidenceLinks_TriageId",
            table: "TriageResponseEvidenceLinks");

        foreach (var (table, _, indexes, newForeignKey) in TriageChildTables)
        {
            migrationBuilder.RenameColumn(name: "TriageId", table: table, newName: "TriageCaseId");
            foreach (var (name, newName) in indexes)
            {
                migrationBuilder.RenameIndex(name: name, table: table, newName: newName);
            }

            migrationBuilder.AddForeignKey(
                name: newForeignKey,
                table: table,
                column: "TriageCaseId",
                principalTable: "Triage",
                principalColumn: "CaseId",
                onDelete: ReferentialAction.Restrict);
        }

        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX [IX_TriageResponseEvidenceLinks_TriageCaseId] ON [TriageResponseEvidenceLinks] ([TriageCaseId]);");

        // 8. The engineer finding and the Triage sequence go (its seed row with it).
        migrationBuilder.DropTable(
            name: "CaseEngineerFindings");

        migrationBuilder.DropTable(
            name: "TriageSequences");

        // 9. Web creates Cases (and Create audit's work, moving the Inspection
        // report's evidence onto the primary row); the Worker creates Cases at
        // intake; neither deletes a work. Web also creates Triage Cases
        // (Unidentified "Open the Triage", manual Create case).
        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseWorks] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[CaseWorks] TO [pegasus_web_runtime_role];
                GRANT INSERT ON OBJECT::[dbo].[Triage] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseWorks] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[CaseWorks] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "CaseWorksAndTriageCases is forward-only: it drops CaseEngineerFindings, TriageSequences and Triage identity columns. Restore from an approved backup instead.");
}
