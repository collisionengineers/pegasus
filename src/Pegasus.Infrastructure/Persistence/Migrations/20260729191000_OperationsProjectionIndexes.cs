using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729191000_OperationsProjectionIndexes")]
public sealed class OperationsProjectionIndexes : Migration
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        RequireSqlServer();

        migrationBuilder.DropIndex(
            name: "IX_ApprovedInboxPollStates_DueAtUtc",
            table: "ApprovedInboxPollStates");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedInboxPoisonMessages_QuarantinedAtUtc",
            table: "ApprovedInboxPoisonMessages");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedSentPollStates_DueAtUtc",
            table: "ApprovedSentPollStates");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedSentPollOutcomes_MailboxId_RecordedAtUtc",
            table: "ApprovedSentPollOutcomes");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedSentPollOutcomes_OutcomeKind_RecordedAtUtc",
            table: "ApprovedSentPollOutcomes");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedInboxPollStates_DueAtUtc_MailboxId",
            table: "ApprovedInboxPollStates",
            columns: new[] { "DueAtUtc", "MailboxId" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedInboxPoisonMessages_QuarantinedAtUtc_Id",
            table: "ApprovedInboxPoisonMessages",
            columns: new[] { "QuarantinedAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollStates_DueAtUtc_MailboxId",
            table: "ApprovedSentPollStates",
            columns: new[] { "DueAtUtc", "MailboxId" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_MailboxId_RecordedAtUtc_Id",
            table: "ApprovedSentPollOutcomes",
            columns: new[] { "MailboxId", "RecordedAtUtc", "Id" },
            descending: new[] { false, true, false });
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_OutcomeKind_RecordedAtUtc_Id",
            table: "ApprovedSentPollOutcomes",
            columns: new[] { "OutcomeKind", "RecordedAtUtc", "Id" },
            descending: new[] { false, true, false });
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_RecordedAtUtc_Id",
            table: "ApprovedSentPollOutcomes",
            columns: new[] { "RecordedAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_IntakeReceipts_SourceChannel_ProcessedAtUtc_Id",
            table: "IntakeReceipts",
            columns: new[] { "SourceChannel", "ProcessedAtUtc", "Id" },
            descending: new[] { false, true, false });
        migrationBuilder.CreateIndex(
            name: "IX_SentEmailEvidence_SentAtUtc_Id",
            table: "SentEmailEvidence",
            columns: new[] { "SentAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_EmailResponseEvidence_DiscoveredAtUtc_Id",
            table: "EmailResponseEvidence",
            columns: new[] { "DiscoveredAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_CaseReportSentEvidence_DiscoveredAtUtc_Id",
            table: "CaseReportSentEvidence",
            columns: new[] { "DiscoveredAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_BoxFileRequests_CreatedAtUtc_Id",
            table: "BoxFileRequests",
            columns: new[] { "CreatedAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_BoxFileRequests_DeactivatedAtUtc_Id",
            table: "BoxFileRequests",
            columns: new[] { "DeactivatedAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_RequestUploadLinks_CreatedAtUtc_Id",
            table: "RequestUploadLinks",
            columns: new[] { "CreatedAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_RequestUploadLinks_RevokedAtUtc_Id",
            table: "RequestUploadLinks",
            columns: new[] { "RevokedAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_RequestUploadReceipts_RequestId_ReceivedAtUtc",
            table: "RequestUploadReceipts",
            columns: new[] { "RequestId", "ReceivedAtUtc" },
            descending: new[] { false, true });
        migrationBuilder.CreateIndex(
            name: "IX_ExternalWorkItems_DueAtUtc_Id",
            table: "ExternalWorkItems",
            columns: new[] { "DueAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_ExternalWorkItems_LeaseExpiresAtUtc_Id",
            table: "ExternalWorkItems",
            columns: new[] { "LeaseExpiresAtUtc", "Id" },
            descending: new[] { true, false });
        migrationBuilder.CreateIndex(
            name: "IX_ExternalWorkItems_CompletedAtUtc_Id",
            table: "ExternalWorkItems",
            columns: new[] { "CompletedAtUtc", "Id" },
            descending: new[] { true, false });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RequireSqlServer();

        migrationBuilder.DropIndex(
            name: "IX_ApprovedInboxPollStates_DueAtUtc_MailboxId",
            table: "ApprovedInboxPollStates");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedInboxPoisonMessages_QuarantinedAtUtc_Id",
            table: "ApprovedInboxPoisonMessages");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedSentPollStates_DueAtUtc_MailboxId",
            table: "ApprovedSentPollStates");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedSentPollOutcomes_MailboxId_RecordedAtUtc_Id",
            table: "ApprovedSentPollOutcomes");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedSentPollOutcomes_OutcomeKind_RecordedAtUtc_Id",
            table: "ApprovedSentPollOutcomes");
        migrationBuilder.DropIndex(
            name: "IX_ApprovedSentPollOutcomes_RecordedAtUtc_Id",
            table: "ApprovedSentPollOutcomes");
        migrationBuilder.DropIndex(
            name: "IX_IntakeReceipts_SourceChannel_ProcessedAtUtc_Id",
            table: "IntakeReceipts");
        migrationBuilder.DropIndex(
            name: "IX_SentEmailEvidence_SentAtUtc_Id",
            table: "SentEmailEvidence");
        migrationBuilder.DropIndex(
            name: "IX_EmailResponseEvidence_DiscoveredAtUtc_Id",
            table: "EmailResponseEvidence");
        migrationBuilder.DropIndex(
            name: "IX_CaseReportSentEvidence_DiscoveredAtUtc_Id",
            table: "CaseReportSentEvidence");
        migrationBuilder.DropIndex(
            name: "IX_BoxFileRequests_CreatedAtUtc_Id",
            table: "BoxFileRequests");
        migrationBuilder.DropIndex(
            name: "IX_BoxFileRequests_DeactivatedAtUtc_Id",
            table: "BoxFileRequests");
        migrationBuilder.DropIndex(
            name: "IX_RequestUploadLinks_CreatedAtUtc_Id",
            table: "RequestUploadLinks");
        migrationBuilder.DropIndex(
            name: "IX_RequestUploadLinks_RevokedAtUtc_Id",
            table: "RequestUploadLinks");
        migrationBuilder.DropIndex(
            name: "IX_RequestUploadReceipts_RequestId_ReceivedAtUtc",
            table: "RequestUploadReceipts");
        migrationBuilder.DropIndex(
            name: "IX_ExternalWorkItems_DueAtUtc_Id",
            table: "ExternalWorkItems");
        migrationBuilder.DropIndex(
            name: "IX_ExternalWorkItems_LeaseExpiresAtUtc_Id",
            table: "ExternalWorkItems");
        migrationBuilder.DropIndex(
            name: "IX_ExternalWorkItems_CompletedAtUtc_Id",
            table: "ExternalWorkItems");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedInboxPollStates_DueAtUtc",
            table: "ApprovedInboxPollStates",
            column: "DueAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedInboxPoisonMessages_QuarantinedAtUtc",
            table: "ApprovedInboxPoisonMessages",
            column: "QuarantinedAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollStates_DueAtUtc",
            table: "ApprovedSentPollStates",
            column: "DueAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_MailboxId_RecordedAtUtc",
            table: "ApprovedSentPollOutcomes",
            columns: new[] { "MailboxId", "RecordedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_OutcomeKind_RecordedAtUtc",
            table: "ApprovedSentPollOutcomes",
            columns: new[] { "OutcomeKind", "RecordedAtUtc" });
    }

    private void RequireSqlServer()
    {
        if (!string.Equals(ActiveProvider, SqlServerProvider, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }
    }
}
