using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Garage authorisation and invoice Audit workflow and its first Principal
/// (operator, 17 November 2026; R1138, R1145, R1147, R1148, R1152). Principals
/// gain <c>AuditWorkflow</c>, <c>PrincipalInvoice</c>, <c>InvoiceVatNumber</c>
/// and <c>InvoicePaymentTerms</c>; Cases gain <c>AuditWorkflow</c>, which a
/// standalone Audit copies from its Principal when it is created. Every
/// Principal and Case there is keeps the original engineer's report and no
/// invoice of its own. United Legal Assistance Limited, code <c>ULA</c>, is
/// created where a database holds neither the code nor the name. Additive:
/// defaulted and nullable columns, their check constraints and three seeded
/// rows; no row changes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261117210000_AuditWorkflowAndUnitedLegalAssistance")]
public partial class AuditWorkflowAndUnitedLegalAssistance : Migration
{
    internal const string OrganizationId = "00000000-0000-4000-8000-00000000D037";
    internal const string PrincipalId = "00000000-0000-4000-8000-00000000C037";
    internal const string LineageId = "00000000-0000-4000-8000-00000000E037";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AuditWorkflow",
            table: "Principals",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "original_engineer_report");

        migrationBuilder.AddColumn<string>(
            name: "PrincipalInvoice",
            table: "Principals",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "none");

        migrationBuilder.AddColumn<string>(
            name: "InvoiceVatNumber",
            table: "Principals",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InvoicePaymentTerms",
            table: "Principals",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AuditWorkflow",
            table: "Cases",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "original_engineer_report");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Principals_AuditWorkflow",
            table: "Principals",
            sql: "[AuditWorkflow] IN ('original_engineer_report', 'garage_authorisation_and_invoice')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Principals_PrincipalInvoice",
            table: "Principals",
            sql: "[PrincipalInvoice] IN ('none', 'ula_invoice')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Cases_AuditWorkflow",
            table: "Cases",
            sql: "[AuditWorkflow] IN ('original_engineer_report', 'garage_authorisation_and_invoice')");

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(SeedSql);
        }
    }

    /// <summary>
    /// The United Legal Assistance record, as a Principal Contact saved in
    /// Contacts would be (R1145, R1152): its address, telephone and e-mail,
    /// always Image Based Assessment, the standard default fee and report
    /// names, the default report sending rules with both Offer ticks on
    /// (R1151), the Garage authorisation and
    /// invoice Audit workflow and the ULA Invoice, with no Invoice VAT number,
    /// payment terms or notes. A database that already holds the code or the
    /// name keeps what it has. Held apart so a test can run it a second time.
    /// </summary>
    internal const string SeedSql =
        $$"""
        IF NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [Code] = N'ULA')
            AND NOT EXISTS (SELECT 1 FROM [dbo].[Organizations] WHERE [Role] = N'principal' AND [NormalizedName] = N'UNITED LEGAL ASSISTANCE LIMITED')
        BEGIN
            INSERT INTO [dbo].[Organizations] ([Active], [Address], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'130c Lord Street' + NCHAR(13) + NCHAR(10) + N'Southport', N'claims@united-legal.co.uk', '{{OrganizationId}}', N'United Legal Assistance Limited', NULL, N'PR9 0AE', N'principal', N'0330 999 9000', 0);
            INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-11-17T21:00:00+00:00', '{{LineageId}}');
            INSERT INTO [dbo].[Principals] ([AuditWorkflow], [Code], [DefaultFee], [Id], [InspectionMode], [InvoicePaymentTerms], [InvoiceVatNumber], [IsActive], [OrganizationId], [PredecessorId], [PrincipalInvoice], [ReferenceScheme], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'garage_authorisation_and_invoice', N'ULA', 180.00, '{{PrincipalId}}', N'image_based_assessment', NULL, NULL, 1, '{{OrganizationId}}', NULL, N'ula_invoice', N'principal_sequence', N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false,"offerExternalEstimates":true,"offerRepairInvoices":true},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '{{LineageId}}', NULL, 0);
        END
        """;

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            // The seeded record goes only while nothing refers to it; a
            // Principal that has Cases stays, as any other Principal would.
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [dbo].[Cases] WHERE [PrincipalId] = '{PrincipalId}')
                    AND NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [PredecessorId] = '{PrincipalId}' OR [SuccessorId] = '{PrincipalId}')
                BEGIN
                    DELETE FROM [dbo].[Principals] WHERE [Id] = '{PrincipalId}';
                    DELETE FROM [dbo].[CaseSequences] WHERE [SequenceLineageId] = '{LineageId}';
                    DELETE FROM [dbo].[PrincipalSequenceLineages] WHERE [Id] = '{LineageId}' AND NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [SequenceLineageId] = '{LineageId}');
                    DELETE FROM [dbo].[Organizations] WHERE [Id] = '{OrganizationId}' AND NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [OrganizationId] = '{OrganizationId}');
                END
                """);
        }

        migrationBuilder.DropCheckConstraint(name: "CK_Cases_AuditWorkflow", table: "Cases");
        migrationBuilder.DropCheckConstraint(name: "CK_Principals_PrincipalInvoice", table: "Principals");
        migrationBuilder.DropCheckConstraint(name: "CK_Principals_AuditWorkflow", table: "Principals");
        migrationBuilder.DropColumn(name: "AuditWorkflow", table: "Cases");
        migrationBuilder.DropColumn(name: "InvoicePaymentTerms", table: "Principals");
        migrationBuilder.DropColumn(name: "InvoiceVatNumber", table: "Principals");
        migrationBuilder.DropColumn(name: "PrincipalInvoice", table: "Principals");
        migrationBuilder.DropColumn(name: "AuditWorkflow", table: "Principals");
    }
}
