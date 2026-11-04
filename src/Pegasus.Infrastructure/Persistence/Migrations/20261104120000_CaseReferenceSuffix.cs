using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Every existing Case/PO takes the temporary <c>-p</c> suffix, so no Pegasus
/// reference collides with an old-system case number; the switch that mints
/// it ships on (operator, 4 November 2026; R656). <c>QDOS26157</c> becomes
/// <c>QDOS26157-p</c> and its Audit reference <c>a.QDOS26157-p</c>, both in
/// one statement so <c>CK_Cases_AuditReference</c> holds. A notification's
/// shown reference and its reply link follow, because the reply composer
/// looks its Case up by that exact reference. History, frozen reports, sent
/// mail and intake records keep the reference they recorded. The Case's Box
/// folders are renamed by the Worker's one-off pass
/// (<c>CaseReferenceSuffixBoxRename</c>). Forward only: a reference minted
/// with the suffix after this cannot be told from one rewritten here.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261104120000_CaseReferenceSuffix")]
public partial class CaseReferenceSuffix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RewriteSql);

    /// <summary>The rewrite, held apart so a test can run it over Cases it seeded.</summary>
    internal const string RewriteSql =
            """
            UPDATE [n]
            SET [n].[Route] = REPLACE(
                    REPLACE([n].[Route],
                        N'correspondenceCaseReference=' + [c].[Reference] + N'&',
                        N'correspondenceCaseReference=' + [c].[Reference] + N'-p&'),
                    N'correspondenceCaseReference=a.' + [c].[Reference] + N'&',
                    N'correspondenceCaseReference=a.' + [c].[Reference] + N'-p&'),
                [n].[Reference] = CASE
                    WHEN [n].[Reference] = [c].[Reference] OR [n].[Reference] = [c].[AuditReference]
                        THEN [n].[Reference] + N'-p'
                    ELSE [n].[Reference]
                END
            FROM [StaffNotifications] AS [n]
            INNER JOIN [Cases] AS [c] ON [c].[Id] = [n].[CaseId]
            WHERE [c].[Reference] NOT LIKE N'%-p';

            UPDATE [Cases]
            SET [Reference] = [Reference] + N'-p',
                [AuditReference] = [AuditReference] + N'-p'
            WHERE [Reference] NOT LIKE N'%-p';
            """;

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "A Case/PO minted with the -p suffix cannot be told from one this migration rewrote.");
}
