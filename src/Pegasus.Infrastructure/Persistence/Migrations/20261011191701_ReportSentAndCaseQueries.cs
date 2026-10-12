using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Report sent replaces Completed, and a query is a record on the Case
    /// (operator, 11 October 2026). The PostReport, PostReportComplete and
    /// Query states collapse into ReportSent, the Completed closure outcome
    /// goes, and every Case that was in Query keeps one open Mail query per
    /// post-report e-mail currently linked to it. A reply's "Also resolve
    /// query" tick is kept on the send. Destructive and forward-only.
    /// </summary>
    public partial class ReportSentAndCaseQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
                throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

            migrationBuilder.AddColumn<bool>(
                name: "ResolvesCaseQueries",
                table: "StaffMailSendOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasOpenQuery",
                table: "CaseWorkflows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CaseQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RaisedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RaisedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RaisedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RaisedOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClosedAs = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ClosedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedByMailOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseQueries", x => x.Id);
                    table.CheckConstraint("CK_CaseQueries_Channel", "[Channel] IS NULL OR [Channel] IN ('Email', 'Phone', 'WhatsApp', 'Other')");
                    table.CheckConstraint("CK_CaseQueries_Closure", "([ClosedAs] IS NULL AND [ClosedAtUtc] IS NULL AND [ClosedByKind] IS NULL AND [ClosedBySubjectId] IS NULL AND [ResolvedByMailOperationId] IS NULL) OR ([ClosedAs] IN ('Resolved', 'Withdrawn') AND [ClosedAtUtc] IS NOT NULL AND [ClosedByKind] IS NOT NULL AND [ClosedBySubjectId] IS NOT NULL)");
                    table.CheckConstraint("CK_CaseQueries_Source", "[Source] IN ('Mail', 'Staff')");
                    table.CheckConstraint("CK_CaseQueries_SourceShape", "([Source] = 'Mail' AND [IntakeReceiptId] IS NOT NULL AND [Channel] IS NULL AND [Details] IS NULL) OR ([Source] = 'Staff' AND [IntakeReceiptId] IS NULL AND [Channel] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CaseQueries_CaseWorkflows_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseWorkflows",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseQueries_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseWorkflows_HasOpenQuery",
                table: "CaseWorkflows",
                column: "HasOpenQuery",
                filter: "[HasOpenQuery] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CaseQueries_CaseId_IntakeReceiptId",
                table: "CaseQueries",
                columns: new[] { "CaseId", "IntakeReceiptId" },
                unique: true,
                filter: "[ClosedAtUtc] IS NULL AND [IntakeReceiptId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseQueries_CaseId_RaisedAtUtc",
                table: "CaseQueries",
                columns: new[] { "CaseId", "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseQueries_IntakeReceiptId",
                table: "CaseQueries",
                column: "IntakeReceiptId");

            // Every Case in Query (or held from it) keeps one open Mail query per
            // post-report mailbox receipt currently linked to it: an active
            // staff association, or the accepted origin link of a receipt no
            // staff association ever named (the rule CurrentIntakeAssociations
            // reads). Raised when the Case entered Query, by the system.
            migrationBuilder.Sql(
                """
                ;WITH linked AS (
                    SELECT association.IntakeReceiptId, association.CaseId
                    FROM dbo.IntakeManualAssociations AS association
                    WHERE association.IsActive = 1
                    UNION
                    SELECT link.IntakeReceiptId, link.CaseId
                    FROM dbo.CaseIntakeLinks AS link
                    WHERE NOT EXISTS (
                        SELECT 1 FROM dbo.IntakeManualAssociations AS named
                        WHERE named.IntakeReceiptId = link.IntakeReceiptId)
                )
                INSERT INTO dbo.CaseQueries
                    (Id, CaseId, Source, IntakeReceiptId, RaisedAtUtc, RaisedByKind, RaisedBySubjectId, RaisedOperationKey)
                SELECT
                    NEWID(),
                    workflow.CaseId,
                    N'Mail',
                    receipt.Id,
                    COALESCE(workflow.StateEnteredAtUtc, receipt.ReceivedAtUtc),
                    N'SystemWorker',
                    N'migration:ReportSentAndCaseQueries',
                    N'migration:ReportSentAndCaseQueries:' + CONVERT(nvarchar(36), receipt.Id)
                FROM dbo.CaseWorkflows AS workflow
                JOIN linked ON linked.CaseId = workflow.CaseId
                JOIN dbo.IntakeReceipts AS receipt
                    ON receipt.Id = linked.IntakeReceiptId
                    AND receipt.SourceChannel = N'mailbox'
                JOIN dbo.IntakeMailClassificationDecisions AS decision
                    ON decision.IntakeReceiptId = receipt.Id
                WHERE (workflow.[State] = N'Query'
                        OR (workflow.[State] = N'Held' AND workflow.PreHoldState = N'Query'))
                    AND decision.Outcome = N'classified'
                    AND decision.Direction = N'received'
                    AND decision.Family = N'post-report-emails';

                UPDATE dbo.CaseWorkflows
                SET HasOpenQuery = 1
                WHERE EXISTS (
                    SELECT 1 FROM dbo.CaseQueries AS query
                    WHERE query.CaseId = CaseWorkflows.CaseId AND query.ClosedAtUtc IS NULL);

                UPDATE dbo.CaseWorkflows
                SET [State] = N'ReportSent', ClosureOutcome = NULL
                WHERE [State] IN (N'PostReport', N'PostReportComplete', N'Query');

                UPDATE dbo.CaseWorkflows
                SET PreHoldState = N'ReportSent'
                WHERE PreHoldState IN (N'PostReport', N'PostReportComplete', N'Query');

                UPDATE dbo.CaseWorkflows
                SET ClosureOutcome = NULL
                WHERE ClosureOutcome = N'PostReportComplete';
                """);

            // Web logs, tags and resolves queries; the Worker raises one when a
            // post-report e-mail links automatically and resolves them when a
            // flagged reply reaches Sent. A query is closed, never deleted.
            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseQueries] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseQueries] TO [pegasus_web_runtime_role];
                END;
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseQueries] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseQueries] TO [pegasus_worker_runtime_role];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "ReportSentAndCaseQueries is forward-only: the Completed and Query states and the "
                + "Completed outcome are gone. Restore from an approved backup instead.");
    }
}
