using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Report sending rules ask staff nothing: every condition reads a fact the
/// Case holds (operator, 12 November 2026; R1023 to R1026). The two condition
/// kinds that had no Case fact, "Bodyshop mentions" and "Images from", are
/// retired, and the two rules that used them are restated as the operator
/// chose: AX's is "Repairer is Easdons Bodyshop and Claim Source is Guardian
/// Accident Claims or James Claims", YML's is "Claim Source is Swinton". The
/// Repairer contact is created where a database has none of that name, as the
/// Baseline created the Claim Sources the rules name. Non-additive and
/// forward only: it rewrites the stored rules of those two Principals, and it
/// refuses to run while any other rule still names a retired kind, so no rule
/// is dropped or guessed at.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261112180000_SendingRulesReadCaseFacts")]
public partial class SendingRulesReadCaseFacts : Migration
{
    /// <summary>The Easdons Bodyshop Repairer where this migration creates it.</summary>
    internal const string EasdonsBodyshopId = "00000000-0000-4000-8000-00000000f00c";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(RestateSql);
        }
    }

    /// <summary>The restatement, held apart so a test can run it over Principals it seeded.</summary>
    internal const string RestateSql =
        $$"""
        IF (SELECT COUNT(*) FROM [dbo].[Organizations]
            WHERE [Role] = N'claim_source'
                AND [Id] IN (
                    '00000000-0000-4000-8000-00000000f007',
                    '00000000-0000-4000-8000-00000000f009',
                    '00000000-0000-4000-8000-00000000f00a')) <> 3
            AND EXISTS (
                SELECT 1 FROM [dbo].[Principals]
                WHERE [ReportSendingRulesJson] LIKE N'%"kind":"BodyshopMentions"%'
                    OR [ReportSendingRulesJson] LIKE N'%"kind":"ImagesFrom"%')
            THROW 50000, N'The Guardian Accident Claims, James Claims or Swinton Claim Source is missing; reconcile the contacts before migrating.', 1;

        IF NOT EXISTS (
            SELECT 1 FROM [dbo].[Organizations]
            WHERE [Role] = N'repairer' AND [NormalizedName] = N'EASDONS BODYSHOP')
            INSERT INTO [dbo].[Organizations] ([Active], [Address], [ContactPerson], [Email], [Id], [Name], [NotesOnEveryCase], [Postcode], [Role], [Telephone], [Version]) VALUES (1, NULL, NULL, NULL, '{{EasdonsBodyshopId}}', N'Easdons Bodyshop', NULL, NULL, N'repairer', NULL, 0);

        DECLARE @easdonsBodyshop nvarchar(36) = (
            SELECT LOWER(CONVERT(nvarchar(36), [Id])) FROM [dbo].[Organizations]
            WHERE [Role] = N'repairer' AND [NormalizedName] = N'EASDONS BODYSHOP');

        UPDATE [dbo].[Principals]
        SET [ReportSendingRulesJson] = REPLACE(
            [ReportSendingRulesJson],
            N'"if":[{"kind":"BodyshopMentions","values":["Easdons"]},{"kind":"BodyshopMentions","values":["Guardian","James Claims"]}]',
            N'"if":[{"kind":"Repairer","values":["' + @easdonsBodyshop + N'"]},{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f007","00000000-0000-4000-8000-00000000f009"]}]')
        WHERE [Code] = N'AX';

        UPDATE [dbo].[Principals]
        SET [ReportSendingRulesJson] = REPLACE(
            [ReportSendingRulesJson],
            N'"if":[{"kind":"ImagesFrom","values":["Swinton"]}]',
            N'"if":[{"kind":"ClaimSource","values":["00000000-0000-4000-8000-00000000f00a"]}]')
        WHERE [Code] = N'YML';

        IF EXISTS (
            SELECT 1 FROM [dbo].[Principals]
            WHERE [ReportSendingRulesJson] LIKE N'%"kind":"BodyshopMentions"%'
                OR [ReportSendingRulesJson] LIKE N'%"kind":"ImagesFrom"%')
            THROW 50000, N'A Principal''s report sending rules still name Bodyshop mentions or Images from; restate that rule on the Principal contact before migrating.', 1;
        """;

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Report sending rules read the Case's facts; the retired condition kinds are not restored.");
}
