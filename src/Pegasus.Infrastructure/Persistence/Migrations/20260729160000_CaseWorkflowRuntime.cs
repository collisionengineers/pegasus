using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729160000_CaseWorkflowRuntime")]
public partial class CaseWorkflowRuntime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // no-runtime-grant: CaseReportApprovals - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: CaseReportSentEvidence - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: CaseWorkflows - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: CaseDueWork - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: CaseWorkflowEvents - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: CaseManualChases - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        migrationBuilder.CreateTable(
            name: "CaseReportApprovals",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                ArtifactIdentity = table.Column<string>(maxLength: 200, nullable: false),
                ArtifactSha256 = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                ApprovedByKind = table.Column<string>(maxLength: 40, nullable: false),
                ApprovedBySubjectId = table.Column<string>(maxLength: 200, nullable: false),
                ApprovedByRolesJson = table.Column<string>(maxLength: 500, nullable: false),
                ApprovedAtUtc = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseReportApprovals", x => x.Id);
                table.ForeignKey("FK_CaseReportApprovals_Cases_CaseId", x => x.CaseId, "Cases", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CaseReportSentEvidence",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                MailboxIdentity = table.Column<string>(maxLength: 200, nullable: false),
                SentFolderIdentity = table.Column<string>(maxLength: 200, nullable: false),
                ImmutableItemIdentity = table.Column<string>(maxLength: 500, nullable: false),
                ConversationIdentity = table.Column<string>(maxLength: 500, nullable: false),
                ReplyChainIdentity = table.Column<string>(maxLength: 500, nullable: false),
                SentAtUtc = table.Column<DateTimeOffset>(nullable: false),
                LinkedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                LinkedByKind = table.Column<string>(maxLength: 40, nullable: false),
                LinkedBySubjectId = table.Column<string>(maxLength: 200, nullable: false),
                LinkedByRolesJson = table.Column<string>(maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseReportSentEvidence", x => x.Id);
                table.ForeignKey("FK_CaseReportSentEvidence_Cases_CaseId", x => x.CaseId, "Cases", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CaseWorkflows",
            columns: table => new
            {
                CaseId = table.Column<Guid>(nullable: false),
                State = table.Column<string>(maxLength: 40, nullable: false),
                AssignedEngineerId = table.Column<Guid>(nullable: true),
                ReportApprovalId = table.Column<Guid>(nullable: true),
                ReportSentEvidenceId = table.Column<Guid>(nullable: true),
                ClosureOutcome = table.Column<string>(maxLength: 40, nullable: true),
                ReplacementCaseId = table.Column<Guid>(nullable: true),
                Version = table.Column<long>(nullable: false),
                EditLeaseTokenHash = table.Column<string>(fixedLength: true, maxLength: 64, nullable: true),
                EditLeaseHolder = table.Column<string>(maxLength: 200, nullable: true),
                EditLeaseOperationKey = table.Column<string>(maxLength: 100, nullable: true),
                EditLeaseExpiresAtUtc = table.Column<DateTimeOffset>(nullable: true),
                ConcurrencyToken = table.Column<Guid>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseWorkflows", x => x.CaseId);
                table.CheckConstraint("CK_CaseWorkflows_Version", "[Version] >= 0");
                table.ForeignKey("FK_CaseWorkflows_Cases_CaseId", x => x.CaseId, "Cases", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CaseWorkflows_Cases_ReplacementCaseId", x => x.ReplacementCaseId, "Cases", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CaseWorkflows_CaseReportApprovals_ReportApprovalId", x => x.ReportApprovalId, "CaseReportApprovals", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CaseWorkflows_CaseReportSentEvidence_ReportSentEvidenceId", x => x.ReportSentEvidenceId, "CaseReportSentEvidence", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CaseDueWork",
            columns: table => new
            {
                CaseId = table.Column<Guid>(nullable: false),
                MissingMaterialReason = table.Column<string>(maxLength: 500, nullable: false),
                DueBy = table.Column<DateOnly>(nullable: true),
                State = table.Column<string>(maxLength: 40, nullable: false),
                NextChaseAtUtc = table.Column<DateTimeOffset>(nullable: true),
                HeldAtUtc = table.Column<DateTimeOffset>(nullable: true),
                RemainingChaseIntervalTicks = table.Column<long>(nullable: true),
                MostRecentChannel = table.Column<string>(maxLength: 100, nullable: true),
                MostRecentOutcome = table.Column<string>(maxLength: 500, nullable: true),
                MostRecentNote = table.Column<string>(maxLength: 1000, nullable: true),
                Version = table.Column<long>(nullable: false),
                ConcurrencyToken = table.Column<Guid>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseDueWork", x => x.CaseId);
                table.CheckConstraint("CK_CaseDueWork_Version", "[Version] >= 0");
                table.ForeignKey("FK_CaseDueWork_CaseWorkflows_CaseId", x => x.CaseId, "CaseWorkflows", "CaseId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken)
            SELECT Id,
                   CASE InitialState WHEN 'not_ready' THEN 'NotReady' WHEN 'review' THEN 'Review' END,
                   0,
                   NEWID()
            FROM Cases
            WHERE InitialState IN ('not_ready', 'review');

            INSERT INTO CaseDueWork
                (CaseId, MissingMaterialReason, State, NextChaseAtUtc, Version, ConcurrencyToken)
            SELECT Id,
                   'Accepted intake is incomplete',
                   'Scheduled',
                   CURRENT_TIMESTAMP,
                   0,
                   NEWID()
            FROM Cases
            WHERE InitialState = 'not_ready';
            """);

        migrationBuilder.CreateTable(
            name: "CaseWorkflowEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                EventType = table.Column<string>(maxLength: 100, nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                RequestHash = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                ActorKind = table.Column<string>(maxLength: 40, nullable: false),
                ActorSubjectId = table.Column<string>(maxLength: 200, nullable: false),
                ActorRolesJson = table.Column<string>(maxLength: 500, nullable: false),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(nullable: false),
                BeforeVersion = table.Column<long>(nullable: false),
                AfterVersion = table.Column<long>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseWorkflowEvents", x => x.Id);
                table.ForeignKey("FK_CaseWorkflowEvents_CaseWorkflows_CaseId", x => x.CaseId, "CaseWorkflows", "CaseId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CaseManualChases",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                RequestHash = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                ActorKind = table.Column<string>(maxLength: 40, nullable: false),
                ActorSubjectId = table.Column<string>(maxLength: 200, nullable: false),
                ActorRolesJson = table.Column<string>(maxLength: 500, nullable: false),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                Channel = table.Column<string>(maxLength: 100, nullable: false),
                TargetPartyOrAddress = table.Column<string>(maxLength: 500, nullable: false),
                AttemptedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                Outcome = table.Column<string>(maxLength: 500, nullable: false),
                Note = table.Column<string>(maxLength: 1000, nullable: true),
                ResultingVersion = table.Column<long>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseManualChases", x => x.Id);
                table.ForeignKey("FK_CaseManualChases_CaseDueWork_CaseId", x => x.CaseId, "CaseDueWork", "CaseId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_CaseReportApprovals_CaseId_ArtifactIdentity_ArtifactSha256", "CaseReportApprovals", ["CaseId", "ArtifactIdentity", "ArtifactSha256"], unique: true);
        migrationBuilder.CreateIndex("IX_CaseReportSentEvidence_CaseId_ImmutableItemIdentity", "CaseReportSentEvidence", ["CaseId", "ImmutableItemIdentity"], unique: true);
        migrationBuilder.CreateIndex("IX_CaseWorkflows_ReplacementCaseId", "CaseWorkflows", "ReplacementCaseId");
        migrationBuilder.CreateIndex("IX_CaseWorkflows_ReportApprovalId", "CaseWorkflows", "ReportApprovalId", unique: true, filter: "[ReportApprovalId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_CaseWorkflows_ReportSentEvidenceId", "CaseWorkflows", "ReportSentEvidenceId", unique: true, filter: "[ReportSentEvidenceId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_CaseDueWork_State_NextChaseAtUtc", "CaseDueWork", ["State", "NextChaseAtUtc"]);
        migrationBuilder.CreateIndex("IX_CaseWorkflowEvents_CaseId_OperationKey", "CaseWorkflowEvents", ["CaseId", "OperationKey"], unique: true);
        migrationBuilder.CreateIndex("IX_CaseWorkflowEvents_CaseId_AfterVersion", "CaseWorkflowEvents", ["CaseId", "AfterVersion"], unique: true);
        migrationBuilder.CreateIndex("IX_CaseManualChases_CaseId_OperationKey", "CaseManualChases", ["CaseId", "OperationKey"], unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("CaseManualChases");
        migrationBuilder.DropTable("CaseWorkflowEvents");
        migrationBuilder.DropTable("CaseDueWork");
        migrationBuilder.DropTable("CaseWorkflows");
        migrationBuilder.DropTable("CaseReportApprovals");
        migrationBuilder.DropTable("CaseReportSentEvidence");
    }
}
