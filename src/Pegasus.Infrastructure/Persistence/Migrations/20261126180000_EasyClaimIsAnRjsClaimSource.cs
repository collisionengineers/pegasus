using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Easy Claim, a credit-hire firm that sends RJS motorcycle jobs, is a Claim
/// Source linked to RJS, and RJS's reports do not copy it (operator, 26
/// November 2026; R1302). The Claim Source "Easy Claim" is created, active,
/// where a database holds no Claim Source of that name, and linked to RJS.
/// Additive: seeded rows only. No row is changed, so an Easy Claim record a
/// database already holds stays as it is, and is only linked to RJS.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261126180000_EasyClaimIsAnRjsClaimSource")]
public partial class EasyClaimIsAnRjsClaimSource : Migration
{
    /// <summary>The Easy Claim Claim Source where this migration creates it.</summary>
    internal const string ClaimSourceId = "00000000-0000-4000-8000-00000000F01B";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(SeedSql);
        }
    }

    /// <summary>
    /// The Claim Source takes the name the operator gave it and nothing else:
    /// no address, e-mail, telephone or notes. Held apart so a test can run
    /// it a second time.
    /// </summary>
    internal const string SeedSql =
        $$"""
        DECLARE @rjs uniqueidentifier = (SELECT [Id] FROM [dbo].[Principals] WHERE [Code] = N'RJS');
        IF @rjs IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM [dbo].[Organizations]
            WHERE [Role] = N'claim_source' AND [NormalizedName] = N'EASY CLAIM')
            INSERT INTO [dbo].[Organizations] ([Active], [Address], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, '{{ClaimSourceId}}', N'Easy Claim', NULL, NULL, N'claim_source', NULL, 0);

        DECLARE @easyClaim uniqueidentifier = (
            SELECT TOP (1) [Id] FROM [dbo].[Organizations]
            WHERE [Role] = N'claim_source' AND [NormalizedName] = N'EASY CLAIM'
            ORDER BY [Id]);

        IF @rjs IS NOT NULL AND @easyClaim IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM [dbo].[ContactPrincipalLinks]
            WHERE [PrincipalId] = @rjs AND [OrganizationId] = @easyClaim)
            INSERT INTO [dbo].[ContactPrincipalLinks] ([PrincipalId], [OrganizationId]) VALUES (@rjs, @easyClaim);
        """;

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            // The seeded Claim Source and its RJS link go only while nothing
            // refers to it: no Case names it, no sending rule reads it, no
            // photo capture or standing link holds it and no other Principal
            // is linked to it. One that is used stays, as any other contact
            // would.
            migrationBuilder.Sql($"""
                DECLARE @rjs uniqueidentifier = (SELECT [Id] FROM [dbo].[Principals] WHERE [Code] = N'RJS');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[CaseDataFields] WHERE UPPER([Value]) = N'{ClaimSourceId}')
                    AND NOT EXISTS (SELECT 1 FROM [dbo].[Principals] WHERE UPPER([ReportSendingRulesJson]) LIKE N'%{ClaimSourceId}%')
                    AND NOT EXISTS (SELECT 1 FROM [dbo].[PhotoCaptures] WHERE [OrganizationId] = '{ClaimSourceId}')
                    AND NOT EXISTS (SELECT 1 FROM [dbo].[StandingPhotoLinks] WHERE [OrganizationId] = '{ClaimSourceId}')
                    AND NOT EXISTS (
                        SELECT 1 FROM [dbo].[ContactPrincipalLinks]
                        WHERE [OrganizationId] = '{ClaimSourceId}' AND ([PrincipalId] <> @rjs OR @rjs IS NULL))
                BEGIN
                    DELETE FROM [dbo].[ContactPrincipalLinks] WHERE [OrganizationId] = '{ClaimSourceId}';
                    DELETE FROM [dbo].[Organizations] WHERE [Id] = '{ClaimSourceId}';
                END
                """);
        }
    }
}
