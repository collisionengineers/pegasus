using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The unroadworthy reason bank is one list for every Case, kept from
/// Administration (operator, 11 November 2026; R952 to R954). Wordings saved
/// under a Principal join the one list: a wording held under more than one
/// Principal keeps its oldest row, and the Principal column goes. The seven
/// standard wordings, until now fixed in code, become its first rows. The Web
/// role may now reword and remove a wording. Non-additive and forward only:
/// the Principal a wording was saved under and the merged duplicate rows are
/// not kept.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261111140000_UnroadworthyReasonsOneList")]
public partial class UnroadworthyReasonsOneList : Migration
{
    private const string Table = "UnroadworthyReasons";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            WITH [Ranked] AS (
                SELECT ROW_NUMBER() OVER (PARTITION BY [Text] ORDER BY [CreatedAtUtc], [Id]) AS [Position]
                FROM [UnroadworthyReasons])
            DELETE FROM [Ranked] WHERE [Position] > 1;
            """);

        migrationBuilder.DropIndex(
            name: "IX_UnroadworthyReasons_PrincipalCode_Text",
            table: Table);

        migrationBuilder.DropColumn(
            name: "PrincipalCode",
            table: Table);

        migrationBuilder.CreateIndex(
            name: "IX_UnroadworthyReasons_Text",
            table: Table,
            column: "Text",
            unique: true);

        // The standard wordings as they stood in code (R756), dated ahead of
        // every saved wording so they keep listing first, in their order.
        migrationBuilder.Sql(
            """
            INSERT INTO [UnroadworthyReasons] ([Id], [Text], [CreatedBy], [CreatedAtUtc])
            SELECT [Standard].[Id], [Standard].[Text], N'system:standard-wordings', [Standard].[CreatedAtUtc]
            FROM (VALUES
                (CAST('00000000-0000-4000-8000-0000000b0001' AS uniqueidentifier), N'as the steering and suspension geometry has been compromised', CAST('1970-01-01T00:00:01+00:00' AS datetimeoffset)),
                (CAST('00000000-0000-4000-8000-0000000b0002' AS uniqueidentifier), N'as the rear lamp assemblies are inoperative', CAST('1970-01-01T00:00:02+00:00' AS datetimeoffset)),
                (CAST('00000000-0000-4000-8000-0000000b0003' AS uniqueidentifier), N'as the headlamp assemblies are inoperative', CAST('1970-01-01T00:00:03+00:00' AS datetimeoffset)),
                (CAST('00000000-0000-4000-8000-0000000b0004' AS uniqueidentifier), N'as the vehicle presents sharp edges likely to cause injury', CAST('1970-01-01T00:00:04+00:00' AS datetimeoffset)),
                (CAST('00000000-0000-4000-8000-0000000b0005' AS uniqueidentifier), N'as the supplementary restraint systems have deployed', CAST('1970-01-01T00:00:05+00:00' AS datetimeoffset)),
                (CAST('00000000-0000-4000-8000-0000000b0006' AS uniqueidentifier), N'as structural distortion is evident to the body shell', CAST('1970-01-01T00:00:06+00:00' AS datetimeoffset)),
                (CAST('00000000-0000-4000-8000-0000000b0007' AS uniqueidentifier), N'as there is a loss of essential fluids', CAST('1970-01-01T00:00:07+00:00' AS datetimeoffset))
            ) AS [Standard] ([Id], [Text], [CreatedAtUtc])
            WHERE NOT EXISTS (
                SELECT 1 FROM [UnroadworthyReasons] AS [Kept] WHERE [Kept].[Text] = [Standard].[Text]);
            """);

        // The Web role rewords and removes a wording (RuntimePermissions.csv);
        // the grant replaces the Baseline's denial of DELETE.
        migrationBuilder.Sql(
            """
            GRANT UPDATE, DELETE ON OBJECT::[dbo].[UnroadworthyReasons] TO [pegasus_web_runtime_role];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "The unroadworthy reason bank is one list; the Principal each wording was saved under is not kept.");
}
