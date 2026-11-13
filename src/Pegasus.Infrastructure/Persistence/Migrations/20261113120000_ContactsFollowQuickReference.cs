using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Contact records follow the Report Sending Quick Reference of 29
/// October 2026 (operator, 13 November 2026; R1030 to R1036):
/// the placeholder Claim Source "Paul Mandy / instructed garages" is retired
/// and the introducers Oakwood's instructions name become Claim Sources linked
/// to OAK; SBL never copies SBL@connexus.co.uk; PCH's iRevolution rule reads
/// the Case's Repairer, with that Repairer contact created where a database
/// has none; the job sheet's "Sending Report:" passages leave the Notes on
/// every Case of the Principals the quick reference names; QCL's contract
/// repair line becomes the quick reference's QCL note; and FRZ is active.
/// Non-additive and forward only: it rewrites stored rules and notes and
/// retires a contact. Every step matches by role and name, by code or by the
/// exact stored text, so a record already changed by hand is left as it is.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261113120000_ContactsFollowQuickReference")]
public partial class ContactsFollowQuickReference : Migration
{
    /// <summary>The iRevolution Repair Network Repairer where this migration creates it.</summary>
    internal const string IRevolutionRepairerId = "00000000-0000-4000-8000-00000000f00d";

    /// <summary>QCL's note, as the quick reference words it.</summary>
    internal const string QclNote =
        "Jobs not involving Hackney: contract repair at 73%. Hackney Solutions jobs: Collision dates the report on the day the engineer completes it and sends it over. Hackney must tell us on every job, when they send the images, whether the vehicle is in hire and the inspection location. In hire: TL threshold 74%, noted as a contract repair. Not in hire: TL threshold 79%, noted as a contract repair.";

    private const string QclContractRepairLine = "Contract repair at 73%.";

    /// <summary>
    /// The introducers Oakwood's instructions name, spelled as they spell
    /// them, each with the id it takes where this migration creates it.
    /// </summary>
    internal static readonly (string Id, string Name)[] OakIntroducers =
    [
        ("00000000-0000-4000-8000-00000000f00e", "A To Z Scotland Limited"),
        ("00000000-0000-4000-8000-00000000f00f", "A76 Recovery Ltd"),
        ("00000000-0000-4000-8000-00000000f010", "Assured Accident Repairs Centre Ltd"),
        ("00000000-0000-4000-8000-00000000f011", "Ecs Accident Repair Ltd"),
        ("00000000-0000-4000-8000-00000000f012", "Glasgow Smart Repair"),
        ("00000000-0000-4000-8000-00000000f013", "J P Auto Repairs Clydebank Ltd"),
        ("00000000-0000-4000-8000-00000000f014", "Mr Claim"),
        ("00000000-0000-4000-8000-00000000f015", "O'malley Recovery"),
        ("00000000-0000-4000-8000-00000000f016", "Premier 1 Accident Repair Centre"),
        ("00000000-0000-4000-8000-00000000f017", "The Coachworks"),
        ("00000000-0000-4000-8000-00000000f018", "Thomas Ash & Sons Ltd"),
        ("00000000-0000-4000-8000-00000000f019", "Tradeston Performance")
    ];

    /// <summary>
    /// Each "Sending Report:" passage the job sheet left in a quick-reference
    /// Principal's notes, to the end of its block, exactly as stored.
    /// </summary>
    internal static readonly (string Code, string Passage)[] SendingPassages =
    [
        ("ACSP", "Sending Report: Return to file handler copy in admin@accidentspecialist.co.uk"),
        ("ALISON", "Sending Report: Return to file handler copy in marya.sheikh@alisonlaw.co.uk & Bradford@alison-law.co.uk"),
        ("ALS", "Sending Report: Send back to handler and claims@autologistic.co.uk with fee separate\r\nCopy in Paul Mandy"),
        ("AX", "Sending Report: Fee note not separate - Copy in engineersreports@ax.com and p.mandy@oakwoodscotland.co.uk (except if request is Easdons and mentions Guardian or mentions James Claims)\r\nALSO send pages 1-4 back to garage - authority will come from AX"),
        ("BC", "Sending Report: Fee note needs to be separate if complete injury claims job copy in completeinjuryclaims@hotmail.com"),
        ("BLACK", "Sending Report: Fee note separate return to file handler"),
        ("FW", "Sending Report: Fee note does not need to be separate"),
        ("KBS", "Sending Report: Fee note separate - Copy in info@knightsbridgesolicitors.co.uk"),
        ("KBS", "Sending Report: Fee note separate"),
        ("KMR", "Sending Report: Fee note separate return to file handler"),
        ("KMR", "Sending Report: Send back with fee note separate"),
        ("MOTORX", "Sending Report: Fee note does not need to be separate"),
        ("MP", "Sending Report: Report without images on 1 document\r\nImages as 1 document\r\nParts list as 3rd document\r\nCopy in mdakri@montrealprestige.co.uk"),
        ("OAK", "Sending Report: Fee note does not need to be separate - copy in p.mandy@oakwoodscotland.co.uk"),
        ("QCL", "Sending Report: Fee note does not need to be separate but copy in credithire@hackneysolutions.co.uk (for Hackney jobs only direct jobs just go back to email instruction came from)"),
        ("QCL", "Sending Report: Fee note does not need to be separate"),
        ("RJS", "Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk"),
        ("RJS", "Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk and admin@accidentspecialist.co.uk"),
        ("RJS", "Sending Report: Fee note separate - copy in engreport@robertjameslaw.co.uk and also - info@claimspecialists.co.uk and accounts@carhirespec"),
        ("SS", "Sending Report: Email back to solicitors and copy in mw@savasandsavage.co.uk"),
        ("SWAN", "Sending Report: Fee note separate return to file handler"),
        ("TEN", "Sending Report: Fee note separate return to file handler"),
        ("WLS", "Sending Report: Fee note separate send back to handler if it is a complete injury claims job copy in completeinjuryclaims@hotmail.com")
    ];

    /// <summary>The restatement, held apart so a test can run it over records it seeded.</summary>
    internal static readonly string RestateSql = BuildRestateSql();

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(RestateSql);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "The Contact records follow the quick reference; the earlier records are not restored.");

    private static string BuildRestateSql()
    {
        var sql = new StringBuilder();
        sql.AppendLine(
            CultureInfo.InvariantCulture,
            $$"""
            UPDATE [dbo].[Organizations]
            SET [Active] = 0
            WHERE [Role] = N'claim_source' AND [NormalizedName] = N'PAUL MANDY / INSTRUCTED GARAGES';

            DECLARE @oak uniqueidentifier = (SELECT [Id] FROM [dbo].[Principals] WHERE [Code] = N'OAK');
            DECLARE @introducers TABLE ([Id] uniqueidentifier NOT NULL, [Name] nvarchar(300) NOT NULL);
            INSERT INTO @introducers ([Id], [Name]) VALUES
            {{string.Join("," + Environment.NewLine, OakIntroducers.Select(item => $"    ('{item.Id}', {Literal(item.Name)})"))}};

            INSERT INTO [dbo].[Organizations] ([Active], [Address], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version])
            SELECT 1, NULL, NULL, [i].[Id], [i].[Name], NULL, NULL, N'claim_source', NULL, 0
            FROM @introducers AS [i]
            WHERE @oak IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[Organizations] AS [o]
                    WHERE [o].[Role] = N'claim_source' AND [o].[NormalizedName] = UPPER([i].[Name]));

            INSERT INTO [dbo].[ContactPrincipalLinks] ([PrincipalId], [OrganizationId])
            SELECT @oak, [o].[Id]
            FROM @introducers AS [i]
            INNER JOIN [dbo].[Organizations] AS [o]
                ON [o].[Role] = N'claim_source' AND [o].[NormalizedName] = UPPER([i].[Name])
            WHERE @oak IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[ContactPrincipalLinks] AS [l]
                    WHERE [l].[PrincipalId] = @oak AND [l].[OrganizationId] = [o].[Id]);

            UPDATE [dbo].[Principals]
            SET [ReportSendingRulesJson] = REPLACE(
                [ReportSendingRulesJson],
                N'"cc":["SBL@connexus.co.uk"],"neverCc":[]',
                N'"cc":[],"neverCc":["SBL@connexus.co.uk"]')
            WHERE [Code] = N'SBL';

            DECLARE @pch uniqueidentifier = (SELECT [Id] FROM [dbo].[Principals] WHERE [Code] = N'PCH');
            IF @pch IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM [dbo].[Organizations]
                WHERE [Role] = N'repairer' AND [NormalizedName] = N'IREVOLUTION REPAIR NETWORK')
                INSERT INTO [dbo].[Organizations] ([Active], [Address], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, '{{IRevolutionRepairerId}}', N'iRevolution Repair Network', NULL, NULL, N'repairer', NULL, 0);

            DECLARE @iRevolution uniqueidentifier = (
                SELECT [Id] FROM [dbo].[Organizations]
                WHERE [Role] = N'repairer' AND [NormalizedName] = N'IREVOLUTION REPAIR NETWORK');

            IF @pch IS NOT NULL AND @iRevolution IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM [dbo].[ContactPrincipalLinks]
                WHERE [PrincipalId] = @pch AND [OrganizationId] = @iRevolution)
                INSERT INTO [dbo].[ContactPrincipalLinks] ([PrincipalId], [OrganizationId]) VALUES (@pch, @iRevolution);

            UPDATE [dbo].[Principals]
            SET [ReportSendingRulesJson] = REPLACE(
                [ReportSendingRulesJson],
                N'"if":[{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f00b"]},{"kind":"Outcome","values":["repairable"]}]',
                N'"if":[{"kind":"Repairer","values":["' + LOWER(CONVERT(nvarchar(36), @iRevolution)) + N'"]},{"kind":"Outcome","values":["repairable"]}]')
            WHERE [Code] = N'PCH' AND @iRevolution IS NOT NULL;

            DECLARE @lineBreak nvarchar(2) = NCHAR(13) + NCHAR(10);
            DECLARE @blockEnd nvarchar(4) = @lineBreak + @lineBreak;
            """);

        // A passage is taken out only where it runs to the end of its block,
        // so one that has been reworded, or that another passage begins with,
        // is left whole. The appended block end stands in for the end of the
        // notes and comes off again.
        foreach (var (code, passage) in SendingPassages)
        {
            sql.AppendLine(
                CultureInfo.InvariantCulture,
                $"""

                UPDATE [o]
                SET [NotesOnEveryCase] = LEFT([r].[Notes], DATALENGTH([r].[Notes]) / 2 - 4)
                FROM [dbo].[Organizations] AS [o]
                INNER JOIN [dbo].[Principals] AS [p] ON [p].[OrganizationId] = [o].[Id]
                CROSS APPLY (
                    SELECT REPLACE(CONVERT(nvarchar(max), [o].[NotesOnEveryCase]) + @blockEnd, @lineBreak + {Literal(passage)} + @blockEnd, @blockEnd) AS [Notes]) AS [r]
                WHERE [p].[Code] = {Literal(code)} AND [o].[NotesOnEveryCase] IS NOT NULL;
                """);
        }

        sql.AppendLine(
            CultureInfo.InvariantCulture,
            $"""

            UPDATE [o]
            SET [NotesOnEveryCase] = STUFF(CONVERT(nvarchar(max), [o].[NotesOnEveryCase]), 1, {QclContractRepairLine.Length}, {Literal(QclNote)})
            FROM [dbo].[Organizations] AS [o]
            INNER JOIN [dbo].[Principals] AS [p] ON [p].[OrganizationId] = [o].[Id]
            WHERE [p].[Code] = N'QCL'
                AND ([o].[NotesOnEveryCase] = {Literal(QclContractRepairLine)}
                    OR LEFT([o].[NotesOnEveryCase], {QclContractRepairLine.Length + 2}) = {Literal(QclContractRepairLine)} + @lineBreak);

            UPDATE [o]
            SET [Active] = 1
            FROM [dbo].[Organizations] AS [o]
            INNER JOIN [dbo].[Principals] AS [p] ON [p].[OrganizationId] = [o].[Id]
            WHERE [p].[Code] = N'FRZ';

            UPDATE [dbo].[Principals] SET [IsActive] = 1 WHERE [Code] = N'FRZ';
            """);
        return sql.ToString();
    }

    /// <summary>A Unicode string literal, with its line breaks written as characters.</summary>
    private static string Literal(string text) =>
        "N'" + text
            .Replace("'", "''", StringComparison.Ordinal)
            .Replace("\r\n", "' + NCHAR(13) + NCHAR(10) + N'", StringComparison.Ordinal) + "'";
}
