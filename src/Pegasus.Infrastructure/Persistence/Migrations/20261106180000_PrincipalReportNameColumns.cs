using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// What a Principal's reports are called moves from its report sending rules
/// to its report settings (operator, 6 November 2026; R740): four columns, an
/// Inspection report's and an Audit report's name for a first send and for a
/// re-send. A Principal whose rules set an attachment name has its First and
/// Re-send copied into both, with <c>{ref}</c>, <c>{reg}</c> and
/// <c>{outcome}</c> rewritten as <c>{CASE REF}</c>, <c>{REG}</c> and
/// <c>{OUTCOME}</c>. Additive: nullable columns and the rows' copy into them;
/// the stored rules are not rewritten, and the old attachment name in them is
/// never read again.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261106180000_PrincipalReportNameColumns")]
public partial class PrincipalReportNameColumns : Migration
{
    private static readonly string[] Columns =
    [
        "InspectionReportNameFirst",
        "InspectionReportNameResend",
        "AuditReportNameFirst",
        "AuditReportNameResend",
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.AddColumn<string>(
                name: column,
                table: "Principals",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(CopySql);
        }
    }

    /// <summary>The copy, held apart so a test can run it over Principals it seeded.</summary>
    internal const string CopySql =
        """
        IF EXISTS (
            SELECT 1 FROM [dbo].[Principals]
            WHERE LEN(JSON_VALUE([ReportSendingRulesJson], '$.attachmentName.first')) > 100
                OR LEN(JSON_VALUE([ReportSendingRulesJson], '$.attachmentName.resend')) > 100)
            THROW 50000, N'A Principal''s attachment name is too long to copy; reconcile it before migrating.', 1;
        UPDATE [p]
        SET [InspectionReportNameFirst] = [n].[First],
            [InspectionReportNameResend] = [n].[Resend],
            [AuditReportNameFirst] = [n].[First],
            [AuditReportNameResend] = [n].[Resend]
        FROM [dbo].[Principals] AS [p]
        CROSS APPLY (SELECT
            REPLACE(REPLACE(REPLACE(JSON_VALUE([p].[ReportSendingRulesJson], '$.attachmentName.first'),
                N'{ref}', N'{CASE REF}'), N'{reg}', N'{REG}'), N'{outcome}', N'{OUTCOME}') AS [First],
            REPLACE(REPLACE(REPLACE(JSON_VALUE([p].[ReportSendingRulesJson], '$.attachmentName.resend'),
                N'{ref}', N'{CASE REF}'), N'{reg}', N'{REG}'), N'{outcome}', N'{OUTCOME}') AS [Resend]) AS [n]
        WHERE [n].[First] IS NOT NULL OR [n].[Resend] IS NOT NULL;
        """;

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.DropColumn(name: column, table: "Principals");
        }
    }
}
