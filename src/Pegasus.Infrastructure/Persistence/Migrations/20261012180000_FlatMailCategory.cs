using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A received message has one flat category (operator, 12 October 2026). The
/// stored family and subtype become one category code; an Ambiguous, Other or
/// Sent decision becomes Unclassified, and so does each correction-history
/// snapshot, because the history reader rebuilds every snapshot through the
/// current categories. An amendment request is a query, so the Case list's
/// Amendment requests column leaves the saved presets (a preset with an
/// unknown column refuses every download). The Ambiguous ownership or
/// destination Unidentified reason is No usable identification. The Outlook
/// folder moves and the approved-mailbox folder bindings that fed them are
/// dropped. Destructive and forward-only.
/// </summary>
public partial class FlatMailCategory : Migration
{
    // The one family/subtype to category mapping, applied to the decision
    // columns and to the history snapshots alike. Anything it does not name
    // (Sent, Other, a family/subtype outside the mapping) is Unclassified.
    private static string CategoryFor(string family, string subtype) =>
        $"""
        CASE
            WHEN {family} = N'General' THEN N'general'
            WHEN {family} = N'billing' THEN N'billing'
            WHEN {family} = N'new-instruction-received' THEN N'new-instruction'
            WHEN {family} = N'non-client-related' THEN N'not-client-related'
            WHEN {family} = N'internal-cc' THEN N'internal-cc'
            WHEN {family} = N'in-progress-cases' AND {subtype} = N'cancellation' THEN N'cancellation'
            WHEN {family} = N'in-progress-cases' THEN N'case-update'
            WHEN {family} = N'post-report-emails' THEN N'query'
            WHEN {family} = N'pre-instruction-emails' AND {subtype} = N'triage-request' THEN N'triage-request'
            WHEN {family} = N'pre-instruction-emails' AND {subtype} = N'images-received' THEN N'images-received'
            WHEN {family} = N'pre-instruction-emails' AND {subtype} = N'pre-formal-instruction-request' THEN N'pre-instruction'
        END
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.AddColumn<string>(
            name: "Category",
            table: "IntakeMailClassificationDecisions",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.Sql(
            $"""
            UPDATE [dbo].[IntakeMailClassificationDecisions]
            SET [Category] = {CategoryFor("[Family]", "[Subtype]")}
            WHERE [Outcome] = N'classified'
                AND [Direction] = N'received'
                AND [OtherName] IS NULL;

            UPDATE [dbo].[IntakeMailClassificationDecisions]
            SET [Outcome] = N'unclassified', [CaseType] = NULL
            WHERE [Category] IS NULL;
            """);

        // The snapshots are camel-case JSON with numeric enums: the old outcome
        // was Classified 0, Ambiguous 1, Unclassified 2 and the direction
        // Received 0, Sent 1; the new outcome is Classified 0, Unclassified 1.
        foreach (var column in new[] { "BeforeJson", "AfterJson" })
        {
            var category = CategoryFor(
                $"JSON_VALUE([history].[{column}], '$.family')",
                $"JSON_VALUE([history].[{column}], '$.subtype')");
            migrationBuilder.Sql(
                $"""
                UPDATE [history]
                SET [{column}] = CASE
                    WHEN [mapped].[Category] IS NULL THEN JSON_MODIFY([rewritten].[Json], '$.caseType', NULL)
                    ELSE [rewritten].[Json]
                END
                FROM [dbo].[IntakeMailClassificationHistory] AS [history]
                CROSS APPLY (
                    SELECT CASE
                        WHEN JSON_VALUE([history].[{column}], '$.outcome') = N'0'
                            AND JSON_VALUE([history].[{column}], '$.direction') = N'0'
                            AND JSON_VALUE([history].[{column}], '$.otherName') IS NULL
                        THEN {category}
                    END AS [Category]
                ) AS [mapped]
                CROSS APPLY (
                    SELECT JSON_MODIFY(JSON_MODIFY(
                        JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
                            [history].[{column}],
                            '$.direction', NULL),
                            '$.family', NULL),
                            '$.subtype', NULL),
                            '$.otherName', NULL),
                            '$.otherReasoning', NULL),
                            '$.ambiguousCandidates', NULL),
                        '$.outcome', CASE WHEN [mapped].[Category] IS NULL THEN 1 ELSE 0 END),
                        '$.category', [mapped].[Category]) AS [Json]
                ) AS [rewritten]
                WHERE ISJSON([history].[{column}]) = 1;
                """);
        }

        migrationBuilder.Sql(
            """
            UPDATE [preset]
            SET [ColumnKeysJson] = COALESCE((
                SELECT N'[' + STRING_AGG(CAST(N'"' + STRING_ESCAPE([keys].[value], 'json') + N'"' AS nvarchar(max)), N',')
                    WITHIN GROUP (ORDER BY CAST([keys].[key] AS int)) + N']'
                FROM OPENJSON([preset].[ColumnKeysJson]) AS [keys]
                WHERE [keys].[value] <> N'activity.amendment_requests'), N'[]')
            FROM [dbo].[CaseListPresets] AS [preset]
            WHERE CHARINDEX(N'"activity.amendment_requests"', [preset].[ColumnKeysJson]) > 0;
            """);

        // The registration fingerprint names the reason, so it is recomputed
        // exactly as EfUnidentifiedStore.Fingerprint does (upper-case SHA-256
        // hex of the UTF-8 pipe-joined origin kind, lower-case origin id,
        // reason, detail and registering actor), or a re-registration of the
        // same origin would conflict instead of replaying.
        migrationBuilder.Sql(
            """
            UPDATE [dbo].[UnidentifiedItems]
            SET [ReasonCode] = N'NoUsableIdentification',
                [RegistrationFingerprint] = CONVERT(nvarchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(max), CONCAT(
                    [OriginKind], N'|', LOWER(CONVERT(nvarchar(36), [OriginId])), N'|', N'NoUsableIdentification', N'|',
                    [SafeDetail], N'|', [CreatedByActorKind], N'|', [CreatedByActorSubjectId])
                    COLLATE Latin1_General_100_CI_AS_SC_UTF8)), 2)
            WHERE [ReasonCode] = N'AmbiguousOwnershipOrDestination';
            """);

        migrationBuilder.DropColumn(
            name: "Direction",
            table: "IntakeMailClassificationDecisions");

        migrationBuilder.DropColumn(
            name: "Family",
            table: "IntakeMailClassificationDecisions");

        migrationBuilder.DropColumn(
            name: "Subtype",
            table: "IntakeMailClassificationDecisions");

        migrationBuilder.DropColumn(
            name: "OtherName",
            table: "IntakeMailClassificationDecisions");

        migrationBuilder.DropColumn(
            name: "OtherReasoning",
            table: "IntakeMailClassificationDecisions");

        migrationBuilder.DropColumn(
            name: "AmbiguousCandidatesJson",
            table: "IntakeMailClassificationDecisions");

        migrationBuilder.DropTable(
            name: "RetainedMailFolderMoves");

        migrationBuilder.DropTable(
            name: "ApprovedMailboxFolderBindings");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "FlatMailCategory is forward-only: the family, subtype, Other and ambiguity detail, the folder "
            + "moves and the folder bindings are gone. Restore from an approved backup instead.");
}
