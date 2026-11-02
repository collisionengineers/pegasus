using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// One-off Cases for private customers (operator, 31 October and 1 November 2026; R574 to R581).
/// Principals gain <c>ReferenceScheme</c>: <c>principal_sequence</c> for every Principal there is,
/// <c>registration</c> for the standing Private customer record this migration creates, code
/// <c>PRIVATE</c>, whose Cases are referenced by the vehicle registration. Additive: one defaulted
/// column, its check constraint and three seeded rows; no row changes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261101120000_PrivateCustomerPrincipal")]
public partial class PrivateCustomerPrincipal : Migration
{
    private const string OrganizationId = "00000000-0000-4000-8000-00000000D036";
    private const string PrincipalId = "00000000-0000-4000-8000-00000000C036";
    private const string LineageId = "00000000-0000-4000-8000-00000000E036";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ReferenceScheme",
            table: "Principals",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "principal_sequence");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Principals_ReferenceScheme",
            table: "Principals",
            sql: "[ReferenceScheme] IN ('principal_sequence', 'registration')");

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            // The standing Private customer record, as a Principal Contact
            // saved in Contacts would be: active, the standard default fee and
            // the default report sending rules. A database that already holds
            // the code or the name is reconciled by hand, not overwritten.
            migrationBuilder.Sql($$"""
                IF EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE [Code] = N'PRIVATE')
                    OR EXISTS (SELECT 1 FROM [dbo].[Organizations] WHERE [Role] = N'principal' AND [NormalizedName] = N'PRIVATE CUSTOMER')
                    THROW 50000, N'The standing Private customer Principal already exists; reconcile it before migrating.', 1;
                INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '{{OrganizationId}}', N'Private customer', NULL, NULL, N'principal', NULL, 0);
                INSERT INTO [dbo].[PrincipalSequenceLineages] ([CreatedAtUtc], [Id]) VALUES ('2026-11-01T12:00:00+00:00', '{{LineageId}}');
                INSERT INTO [dbo].[Principals] ([Code], [DefaultFee], [DefaultInspectionAddress], [DefaultInspectionLocationLabel], [DefaultInspectionPostcode], [DefaultInspectionSourceKind], [DefaultInspectionSourceRecordId], [DefaultInspectionSourceVersion], [Id], [InspectionMode], [IsActive], [OrganizationId], [PredecessorId], [ReferenceScheme], [ReportSendingRulesJson], [SalvageMatrixJson], [SequenceLineageId], [SuccessorId], [Version]) VALUES (N'PRIVATE', 180.00, NULL, NULL, NULL, NULL, NULL, NULL, '{{PrincipalId}}', N'physical_address', 1, '{{OrganizationId}}', NULL, N'registration', N'{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}', NULL, '{{LineageId}}', NULL, 0);
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            // The seeded record goes only while nothing refers to it; a
            // Private customer that has Cases stays as an ordinary Principal.
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

        migrationBuilder.DropCheckConstraint(
            name: "CK_Principals_ReferenceScheme",
            table: "Principals");

        migrationBuilder.DropColumn(
            name: "ReferenceScheme",
            table: "Principals");
    }
}
