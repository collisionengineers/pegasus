using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// An Inspection + Audit Case need not have an Audit: sending its Inspection
/// report with no Audit moves it to Report sent, and Create audit stays live
/// (operator, 5 November 2026, replacing 13 October 2026). Every such Case the
/// old rule left With Engineer holding its Inspection report's Sent evidence
/// enters Report sent when that evidence was linked. Its link's
/// <c>report_evidence_auto_linked</c> or Mark report sent event already
/// records the send, so no history row is added. Forward only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261105120000_InspectionReportSendEndsTheWork")]
public partial class InspectionReportSendEndsTheWork : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            UPDATE [w]
            SET [State] = N'ReportSent',
                [StateEnteredAtUtc] = [e].[LinkedAtUtc]
            FROM [CaseWorkflows] AS [w]
            INNER JOIN [Cases] AS [c] ON [c].[Id] = [w].[CaseId]
            INNER JOIN [CaseReportSentEvidence] AS [e] ON [e].[Id] = [w].[ReportSentEvidenceId]
            WHERE [c].[Type] = N'inspection_and_audit'
              AND [c].[AuditReference] IS NULL
              AND [w].[State] = N'ReportPreparation'
              AND [e].[LinkedAtUtc] IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM [CaseWorks] AS [k]
                  WHERE [k].[CaseId] = [c].[Id] AND [k].[Kind] = N'audit');
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "The Inspection report's send ends an Inspection + Audit Case's work; the With Engineer window is not restored.");
}
