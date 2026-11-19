using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Parkhouse, code <c>PHOUSE</c>, and its repairer (operator, 18 November
/// 2026; R1191, R1192, R1194, R1195). The Principal is created, active, where
/// a database holds neither the code nor the name, so that its accepted mail
/// route has its Principal on a fresh database. The Repairer contact
/// "Parkhouse Bodyshop" is created where a database holds no Repairer of that
/// name, and linked to PHOUSE. Additive: seeded rows only. No row is changed,
/// so a PHOUSE record a database already holds stays as it is, inactive
/// included, until staff tick Active on the contact (R1192).
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261118180000_ParkhousePrincipalAndRepairer")]
public partial class ParkhousePrincipalAndRepairer : Migration
{
    internal const string OrganizationId = "00000000-0000-4000-8000-00000000D038";
    internal const string PrincipalId = "00000000-0000-4000-8000-00000000C038";
    internal const string LineageId = "00000000-0000-4000-8000-00000000E038";

    /// <summary>The Parkhouse Bodyshop Repairer where this migration creates it.</summary>
    internal const string RepairerId = "00000000-0000-4000-8000-00000000F01A";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(SeedSql);
        }
    }

    /// <summary>
    /// The Parkhouse record as production holds it on 18 November 2026, under
    /// the name the operator gave it that day: its address, telephone and
    /// e-mail, a physical inspection address (R1194), the standard default
    /// fee and the default report sending rules. The Repairer takes the name,
    /// address and postcode Parkhouse Assist's letter prints over its
    /// repairer block, and no e-mail or telephone. Held apart so a test can
    /// run it a second time.
    /// </summary>
    internal const string SeedSql =
        $$"""
        IF NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [Code] = N'PHOUSE')
            AND NOT EXISTS (SELECT 1 FROM [dbo].[Organizations] WHERE [Role] = N'principal' AND [NormalizedName] = N'PARKHOUSE')
        BEGIN
            INSERT INTO [dbo].[Organizations] ([Active], [Address], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'C/O Parkhouse Assist Ltd, Brulimar House, Jubilee Road, Middleton', N'office@parkhouseassist.com', '{{OrganizationId}}', N'Parkhouse', NULL, N'M24 2LX', N'principal', N'0161 524 0000', 0);
            INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-11-18T18:00:00+00:00', '{{LineageId}}');
            INSERT INTO [dbo].[Principals] ([AuditWorkflow], [Code], [DefaultFee], [Id], [InspectionMode], [InvoicePaymentTerms], [InvoiceVatNumber], [IsActive], [OrganizationId], [PredecessorId], [PrincipalInvoice], [ReferenceScheme], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'original_engineer_report', N'PHOUSE', 180.00, '{{PrincipalId}}', N'physical_address', NULL, NULL, 1, '{{OrganizationId}}', NULL, N'none', N'principal_sequence', N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '{{LineageId}}', NULL, 0);
        END

        DECLARE @phouse uniqueidentifier = (SELECT [Id] FROM [dbo].[Principals] WHERE [Code] = N'PHOUSE');
        IF @phouse IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM [dbo].[Organizations]
            WHERE [Role] = N'repairer' AND [NormalizedName] = N'PARKHOUSE BODYSHOP')
            INSERT INTO [dbo].[Organizations] ([Active], [Address], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, N'Unit 7, Park House Bridge Estate, 7 Langley Rd, Manchester, Pendlebury, Salford', NULL, '{{RepairerId}}', N'Parkhouse Bodyshop', NULL, N'M6 6JQ', N'repairer', NULL, 0);

        DECLARE @bodyshop uniqueidentifier = (
            SELECT [Id] FROM [dbo].[Organizations]
            WHERE [Role] = N'repairer' AND [NormalizedName] = N'PARKHOUSE BODYSHOP');

        IF @phouse IS NOT NULL AND @bodyshop IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM [dbo].[ContactPrincipalLinks]
            WHERE [PrincipalId] = @phouse AND [OrganizationId] = @bodyshop)
            INSERT INTO [dbo].[ContactPrincipalLinks] ([PrincipalId], [OrganizationId]) VALUES (@phouse, @bodyshop);
        """;

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            // The seeded Principal goes only while nothing refers to it; one
            // that has Cases stays, as any other Principal would. The
            // Repairer contact stays, as one staff added in Contacts would.
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [dbo].[Cases] WHERE [PrincipalId] = '{PrincipalId}')
                    AND NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [PredecessorId] = '{PrincipalId}' OR [SuccessorId] = '{PrincipalId}')
                BEGIN
                    DELETE FROM [dbo].[ContactPrincipalLinks] WHERE [PrincipalId] = '{PrincipalId}';
                    DELETE FROM [dbo].[Principals] WHERE [Id] = '{PrincipalId}';
                    DELETE FROM [dbo].[CaseSequences] WHERE [SequenceLineageId] = '{LineageId}';
                    DELETE FROM [dbo].[PrincipalSequenceLineages] WHERE [Id] = '{LineageId}' AND NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [SequenceLineageId] = '{LineageId}');
                    DELETE FROM [dbo].[Organizations] WHERE [Id] = '{OrganizationId}' AND NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [OrganizationId] = '{OrganizationId}');
                END
                """);
        }
    }
}
