using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A general correspondence send leaves a Notes line on its Case once the
/// Sent-evidence poll observes the Sent item (FRD-16 Notes, FRD-21). It
/// records itself in the Case history at the Case's current version, as an
/// operator note does, so it is exempt from the per-Case, per-version
/// uniqueness index.
///
/// The same poll now retains every Sent item under the Sent Items scope
/// (FRD-20). A retained message's mailbox was keyed to the Inbox poll state,
/// which only a mailbox with inbound intake has; a Sent-only mailbox could
/// never retain. The approved mailbox itself is the message's owner, so the
/// key moves to <c>ApprovedMailboxes</c>. The Sent copy of a message the
/// mailbox also received is a second mailbox item with the same Message-ID, so
/// that identity becomes unique per folder. Nothing is dropped but the keys.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261005090000_CorrespondenceSentEvent")]
public partial class CorrespondenceSentEvent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_RetainedMailboxMessages_ApprovedInboxPollStates_MailboxId",
            table: "RetainedMailboxMessages");

        migrationBuilder.AddForeignKey(
            name: "FK_RetainedMailboxMessages_ApprovedMailboxes_MailboxId",
            table: "RetainedMailboxMessages",
            column: "MailboxId",
            principalTable: "ApprovedMailboxes",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropIndex(
            name: "IX_RetainedMailboxMessages_MailboxId_CanonicalInternetMessageIdentity",
            table: "RetainedMailboxMessages");

        migrationBuilder.CreateIndex(
            name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_CanonicalInternetMessageIdentity",
            table: "RetainedMailboxMessages",
            columns: new[] { "MailboxId", "FolderScope", "CanonicalInternetMessageIdentity" },
            unique: true,
            filter: "[CanonicalInternetMessageIdentity] IS NOT NULL");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents",
            columns: new[] { "CaseId", "AfterVersion" },
            unique: true,
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' AND [EventType] <> 'case_estimate_document_previewed' AND [EventType] <> 'edit_lease_taken_over' AND [EventType] <> 'market_research_attached' AND [EventType] <> 'correspondence_sent'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_RetainedMailboxMessages_ApprovedMailboxes_MailboxId",
            table: "RetainedMailboxMessages");

        migrationBuilder.AddForeignKey(
            name: "FK_RetainedMailboxMessages_ApprovedInboxPollStates_MailboxId",
            table: "RetainedMailboxMessages",
            column: "MailboxId",
            principalTable: "ApprovedInboxPollStates",
            principalColumn: "ApprovedMailboxId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropIndex(
            name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_CanonicalInternetMessageIdentity",
            table: "RetainedMailboxMessages");

        migrationBuilder.CreateIndex(
            name: "IX_RetainedMailboxMessages_MailboxId_CanonicalInternetMessageIdentity",
            table: "RetainedMailboxMessages",
            columns: new[] { "MailboxId", "CanonicalInternetMessageIdentity" },
            unique: true,
            filter: "[CanonicalInternetMessageIdentity] IS NOT NULL");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents",
            columns: new[] { "CaseId", "AfterVersion" },
            unique: true,
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' AND [EventType] <> 'case_estimate_document_previewed' AND [EventType] <> 'edit_lease_taken_over' AND [EventType] <> 'market_research_attached'");
    }
}
