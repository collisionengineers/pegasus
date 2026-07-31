using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729183000_SentEvidencePolling")]
public sealed class SentEvidencePolling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [CaseReportSentEvidence])
                THROW 51000, 'Existing report Sent evidence lacks immutable Internet-message, source-occurrence, source-copy, and MIME provenance. Migrate that evidence explicitly before applying SentEvidencePolling.', 1;
            """);

        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [EmailResponseEvidence])
                THROW 51000, 'Existing response evidence lacks exact approved-mailbox Sent provenance. Migrate that evidence explicitly before applying SentEvidencePolling.', 1;
            """);

        migrationBuilder.DropIndex(
            name: "IX_CaseReportSentEvidence_MailboxIdentity_ImmutableItemIdentity",
            table: "CaseReportSentEvidence");

        migrationBuilder.AlterColumn<string>(
            name: "MailboxIdentity",
            table: "CaseReportSentEvidence",
            type: "nvarchar(320)",
            maxLength: 320,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportSentEvidence_MailboxIdentity_ImmutableItemIdentity",
            table: "CaseReportSentEvidence",
            columns: new[] { "MailboxIdentity", "ImmutableItemIdentity" },
            unique: true);

        migrationBuilder.AddColumn<string>(
            name: "InternetMessageIdentity",
            table: "CaseReportSentEvidence",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "MimeSha256",
            table: "CaseReportSentEvidence",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "SourceOccurrenceIdentity",
            table: "CaseReportSentEvidence",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "SourceSha256",
            table: "CaseReportSentEvidence",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: false);

        migrationBuilder.CreateTable(
            name: "ApprovedSentPollStates",
            columns: table => new
            {
                MailboxId = table.Column<string>(maxLength: 100, nullable: false),
                MailboxAddress = table.Column<string>(maxLength: 320, nullable: false),
                SentFolderIdentity = table.Column<string>(maxLength: 200, nullable: false),
                Cursor = table.Column<string>(nullable: true),
                DueAtUtc = table.Column<DateTimeOffset>(nullable: false),
                LeaseToken = table.Column<string>(maxLength: 64, nullable: true),
                LeaseExpiresAtUtc = table.Column<DateTimeOffset>(nullable: true),
                LastCompletedAtUtc = table.Column<DateTimeOffset>(nullable: true),
                LastFailureCode = table.Column<string>(maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovedSentPollStates", item => item.MailboxId);
            });

        migrationBuilder.CreateTable(
            name: "ApprovedSentPollOutcomes",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                MailboxId = table.Column<string>(maxLength: 100, nullable: false),
                MailboxAddress = table.Column<string>(maxLength: 320, nullable: false),
                SourceOccurrenceIdentity = table.Column<string>(maxLength: 200, nullable: false),
                SourceSha256 = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                OriginalSourceSha256 = table.Column<string>(fixedLength: true, maxLength: 64, nullable: true),
                ObservedSourceSha256 = table.Column<string>(fixedLength: true, maxLength: 64, nullable: true),
                EvidenceMarker = table.Column<string>(maxLength: 40, nullable: true),
                CurrentLocationIdentity = table.Column<string>(maxLength: 500, nullable: true),
                ObservationKind = table.Column<string>(maxLength: 40, nullable: false),
                SentFolderIdentity = table.Column<string>(maxLength: 200, nullable: true),
                ImmutableItemIdentity = table.Column<string>(maxLength: 500, nullable: true),
                InternetMessageIdentity = table.Column<string>(maxLength: 500, nullable: true),
                ConversationIdentity = table.Column<string>(maxLength: 500, nullable: true),
                ReplyChainIdentity = table.Column<string>(maxLength: 500, nullable: true),
                InReplyToIdentitiesJson = table.Column<string>(nullable: true),
                AuthoritativeCaseIdentitiesJson = table.Column<string>(nullable: true),
                SentAtUtc = table.Column<DateTimeOffset>(nullable: true),
                MimeSha256 = table.Column<string>(fixedLength: true, maxLength: 64, nullable: true),
                OutcomeKind = table.Column<string>(maxLength: 80, nullable: false),
                RelatedEvidenceId = table.Column<Guid>(nullable: true),
                FailureCode = table.Column<string>(maxLength: 100, nullable: true),
                RecordedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                CursorAfterItem = table.Column<string>(nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovedSentPollOutcomes", item => item.Id);
                table.ForeignKey(
                    name: "FK_ApprovedSentPollOutcomes_ApprovedSentPollStates_MailboxId",
                    column: item => item.MailboxId,
                    principalTable: "ApprovedSentPollStates",
                    principalColumn: "MailboxId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.RenameColumn(
            name: "ReceivedAtUtc",
            table: "EmailResponseEvidence",
            newName: "DiscoveredAtUtc");

        migrationBuilder.AlterColumn<string>(
            name: "MessageIdentity",
            table: "EmailResponseEvidence",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);

        migrationBuilder.AddColumn<Guid>(
            name: "PollOutcomeId",
            table: "EmailResponseEvidence",
            type: "uniqueidentifier",
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "MailboxId",
            table: "EmailResponseEvidence",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "MailboxAddress",
            table: "EmailResponseEvidence",
            type: "nvarchar(320)",
            maxLength: 320,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "SentFolderIdentity",
            table: "EmailResponseEvidence",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "ImmutableItemIdentity",
            table: "EmailResponseEvidence",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "ConversationIdentity",
            table: "EmailResponseEvidence",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "ReplyChainIdentity",
            table: "EmailResponseEvidence",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "InReplyToIdentitiesJson",
            table: "EmailResponseEvidence",
            type: "nvarchar(max)",
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "SourceOccurrenceIdentity",
            table: "EmailResponseEvidence",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false);
        migrationBuilder.AddColumn<string>(
            name: "SourceSha256",
            table: "EmailResponseEvidence",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: false);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SentAtUtc",
            table: "EmailResponseEvidence",
            type: "datetimeoffset",
            nullable: false);

        migrationBuilder.CreateIndex(
            name: "IX_EmailResponseEvidence_PollOutcomeId",
            table: "EmailResponseEvidence",
            column: "PollOutcomeId",
            unique: true);
        migrationBuilder.AddForeignKey(
            name: "FK_EmailResponseEvidence_ApprovedSentPollOutcomes_PollOutcomeId",
            table: "EmailResponseEvidence",
            column: "PollOutcomeId",
            principalTable: "ApprovedSentPollOutcomes",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_MailboxId_RecordedAtUtc",
            table: "ApprovedSentPollOutcomes",
            columns: new[] { "MailboxId", "RecordedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_OperationKey",
            table: "ApprovedSentPollOutcomes",
            column: "OperationKey",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_OutcomeKind_RecordedAtUtc",
            table: "ApprovedSentPollOutcomes",
            columns: new[] { "OutcomeKind", "RecordedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollOutcomes_RelatedEvidenceId",
            table: "ApprovedSentPollOutcomes",
            column: "RelatedEvidenceId");
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollStates_DueAtUtc",
            table: "ApprovedSentPollStates",
            column: "DueAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_ApprovedSentPollStates_MailboxAddress",
            table: "ApprovedSentPollStates",
            column: "MailboxAddress",
            unique: true);

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, UPDATE ON OBJECT::[dbo].[ApprovedSentPollStates] TO [pegasus_web_runtime_role];
                GRANT SELECT ON OBJECT::[dbo].[ApprovedSentPollOutcomes] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT ON OBJECT::[dbo].[ApprovedMailboxes] TO [pegasus_worker_runtime_role];
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ApprovedSentPollStates] TO [pegasus_worker_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[ApprovedSentPollOutcomes] TO [pegasus_worker_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[ActionHistory] TO [pegasus_worker_runtime_role];
                GRANT SELECT, UPDATE ON OBJECT::[dbo].[CaseWorkflows] TO [pegasus_worker_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseWorkflowEvents] TO [pegasus_worker_runtime_role];
                GRANT SELECT ON OBJECT::[dbo].[CaseDueWork] TO [pegasus_worker_runtime_role];
                GRANT SELECT ON OBJECT::[dbo].[CaseReportApprovals] TO [pegasus_worker_runtime_role];
                GRANT UPDATE ON OBJECT::[dbo].[CaseReportSentEvidence] TO [pegasus_worker_runtime_role];
                GRANT UPDATE ON OBJECT::[dbo].[SentEmailEvidence] TO [pegasus_worker_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[TriageResponseEvidenceLinks] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                REVOKE SELECT, UPDATE ON OBJECT::[dbo].[ApprovedSentPollStates] FROM [pegasus_web_runtime_role];
                REVOKE SELECT ON OBJECT::[dbo].[ApprovedSentPollOutcomes] FROM [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                REVOKE SELECT ON OBJECT::[dbo].[ApprovedMailboxes] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ApprovedSentPollStates] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT, INSERT ON OBJECT::[dbo].[ApprovedSentPollOutcomes] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT, INSERT ON OBJECT::[dbo].[ActionHistory] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT, UPDATE ON OBJECT::[dbo].[CaseWorkflows] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT, INSERT ON OBJECT::[dbo].[CaseWorkflowEvents] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT ON OBJECT::[dbo].[CaseDueWork] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT ON OBJECT::[dbo].[CaseReportApprovals] FROM [pegasus_worker_runtime_role];
                REVOKE UPDATE ON OBJECT::[dbo].[CaseReportSentEvidence] FROM [pegasus_worker_runtime_role];
                REVOKE UPDATE ON OBJECT::[dbo].[SentEmailEvidence] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT, INSERT ON OBJECT::[dbo].[TriageResponseEvidenceLinks] FROM [pegasus_worker_runtime_role];
            END;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_EmailResponseEvidence_ApprovedSentPollOutcomes_PollOutcomeId",
            table: "EmailResponseEvidence");
        migrationBuilder.DropIndex(
            name: "IX_EmailResponseEvidence_PollOutcomeId",
            table: "EmailResponseEvidence");

        migrationBuilder.DropColumn(name: "ConversationIdentity", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "ImmutableItemIdentity", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "InReplyToIdentitiesJson", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "MailboxAddress", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "MailboxId", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "PollOutcomeId", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "ReplyChainIdentity", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "SentAtUtc", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "SentFolderIdentity", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "SourceOccurrenceIdentity", table: "EmailResponseEvidence");
        migrationBuilder.DropColumn(name: "SourceSha256", table: "EmailResponseEvidence");

        migrationBuilder.AlterColumn<string>(
            name: "MessageIdentity",
            table: "EmailResponseEvidence",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500);
        migrationBuilder.RenameColumn(
            name: "DiscoveredAtUtc",
            table: "EmailResponseEvidence",
            newName: "ReceivedAtUtc");

        migrationBuilder.DropTable(name: "ApprovedSentPollOutcomes");
        migrationBuilder.DropTable(name: "ApprovedSentPollStates");

        migrationBuilder.DropColumn(name: "InternetMessageIdentity", table: "CaseReportSentEvidence");
        migrationBuilder.DropColumn(name: "MimeSha256", table: "CaseReportSentEvidence");
        migrationBuilder.DropColumn(name: "SourceOccurrenceIdentity", table: "CaseReportSentEvidence");
        migrationBuilder.DropColumn(name: "SourceSha256", table: "CaseReportSentEvidence");

        migrationBuilder.DropIndex(
            name: "IX_CaseReportSentEvidence_MailboxIdentity_ImmutableItemIdentity",
            table: "CaseReportSentEvidence");

        migrationBuilder.AlterColumn<string>(
            name: "MailboxIdentity",
            table: "CaseReportSentEvidence",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(320)",
            oldMaxLength: 320);

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportSentEvidence_MailboxIdentity_ImmutableItemIdentity",
            table: "CaseReportSentEvidence",
            columns: new[] { "MailboxIdentity", "ImmutableItemIdentity" },
            unique: true);
    }
}
