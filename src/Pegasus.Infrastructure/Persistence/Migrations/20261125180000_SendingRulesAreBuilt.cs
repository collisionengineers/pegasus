using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Principal's report sending rules are built (operator, 25 November 2026;
/// R1267 to R1270): an "On every Case" list of actions and numbered rules,
/// each "If" groups of conditions joined by or, a group's conditions joined
/// by and, "Then" actions. Send from and "Report carries vehicle images" leave
/// the rules for their own columns, <c>Principals.SendFromMailbox</c> and
/// <c>Principals.ReportImages</c>, filled from each row's stored rules.
/// Every stored rules text is rewritten to the new shape: the fixed settings
/// become every-Case actions in a fixed order (Send to, Send to only, Reply
/// all, Cc, Never cc, one Attach per file that was on, Hold, one Add a Case
/// task per reminder); each rule keeps its place, its "All" conditions one
/// group and its "Any" conditions one group each, "Sender is not" becoming
/// Sender with "is not"; "Instruction mentions" conditions are dropped (R1075)
/// and a rule left with none is dropped, except RJS's two, which become Claim
/// Source is Claim Specialists and Claim Source is Accident Specialists where
/// those Claim Sources exist (R1268; "Luton" is deferred, R1265, #2204); a
/// rule's actions become Cc, Never cc, Attach or Do not attach per file it
/// named, Hold, Stop, Add a Case task for its reminder and for its Rate card;
/// and Garage figures becomes a last rule "If Outcome is Repairable, then Add
/// a Case task" (R1270). A text already in the new shape is left as it is, so
/// the statement may run twice, and an old rule naming a condition kind this
/// does not know, or adding and removing the same Cc address (which the built
/// rules refuse, so the Principal could not be read), stops it. Non-additive and forward only: the old shape is not
/// kept and no reader for it remains.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261125180000_SendingRulesAreBuilt")]
public partial class SendingRulesAreBuilt : Migration
{
    /// <summary>The task text an old rule's Rate card becomes (<c>ReportDispatchPolicy.RateCardTask</c>).</summary>
    internal const string RateCardTask = "Check the report's estimate against the Connexus Repair Rate Card.";

    /// <summary>The task text Garage figures becomes (<c>ReportDispatchPolicy.GarageFiguresTask</c>).</summary>
    internal const string GarageFiguresTask = "Send the figures to the garage.";

    /// <summary>The stored text of the rules a new Principal starts with.</summary>
    internal const string DefaultRulesJson =
        """{"everyCase":[{"kind":"ReplyAll"},{"kind":"Attach","file":"FeeNoteSeparate"}],"rules":[]}""";

    /// <summary>
    /// The whole statement, held apart so a test, and a check on a copy of a
    /// real database, can run it. One batch for SQL Server 2022 and Azure
    /// SQL: the statement that fills the new columns is dynamic, since the
    /// batch is compiled before they exist.
    /// </summary>
    public static readonly string Sql =
        $$"""
        IF EXISTS (
            SELECT 1
            FROM [dbo].[Principals] AS [p]
            CROSS APPLY OPENJSON([p].[ReportSendingRulesJson], N'$.rules') AS [r]
            WHERE JSON_QUERY([p].[ReportSendingRulesJson], N'$.everyCase') IS NULL
                AND (ISNULL(JSON_VALUE([r].[value], N'$.match'), N'') NOT IN (N'All', N'Any')
                    OR EXISTS (
                        SELECT 1
                        FROM OPENJSON([r].[value], N'$.if') AS [c]
                        WHERE ISNULL(JSON_VALUE([c].[value], N'$.kind'), N'') NOT IN (
                            N'ClaimSource', N'Repairer', N'Mentions', N'SenderNot',
                            N'Outcome', N'CaseType', N'RefPrefix', N'PreviousJob'))))
            THROW 50000, N'A Principal''s report sending rules name a match or condition kind the restatement does not know; restate that rule on the Principal contact before migrating.', 1;

        IF EXISTS (
            SELECT 1
            FROM [dbo].[Principals] AS [p]
            CROSS APPLY OPENJSON([p].[ReportSendingRulesJson], N'$.rules') AS [r]
            CROSS APPLY OPENJSON([r].[value], N'$.then.ccAdd') AS [added]
            CROSS APPLY OPENJSON([r].[value], N'$.then.ccRemove') AS [removed]
            WHERE JSON_QUERY([p].[ReportSendingRulesJson], N'$.everyCase') IS NULL
                AND LOWER(LTRIM(RTRIM([added].[value]))) = LOWER(LTRIM(RTRIM([removed].[value]))))
            THROW 50000, N'A Principal''s report sending rule both adds and removes the same Cc address, which a built rule refuses; take it out of one of the two on the Principal contact before migrating.', 1;

        IF COL_LENGTH(N'dbo.Principals', N'SendFromMailbox') IS NULL
            ALTER TABLE [dbo].[Principals] ADD [SendFromMailbox] nvarchar(320) NULL;

        IF COL_LENGTH(N'dbo.Principals', N'ReportImages') IS NULL
            ALTER TABLE [dbo].[Principals] ADD [ReportImages] bit NOT NULL DEFAULT CAST(1 AS bit);

        EXEC (N'
        UPDATE [p]
        SET [SendFromMailbox] = NULLIF(LTRIM(RTRIM([o].[SendFromMailbox])), N''''),
            [ReportImages] = CASE WHEN [o].[ReportImages] = 0 THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END
        FROM [dbo].[Principals] AS [p]
        CROSS APPLY OPENJSON([p].[ReportSendingRulesJson]) WITH (
            [SendFromMailbox] nvarchar(max) N''$.sendFromMailbox'',
            [ReportImages] bit N''$.attach.reportImages'') AS [o]
        WHERE JSON_QUERY([p].[ReportSendingRulesJson], N''$.everyCase'') IS NULL;');

        DECLARE @claimSpecialists nvarchar(36) = (
            SELECT TOP (1) LOWER(CONVERT(nvarchar(36), [Id])) FROM [dbo].[Organizations]
            WHERE [Role] = N'claim_source' AND [Active] = 1 AND [NormalizedName] = N'CLAIM SPECIALISTS'
            ORDER BY [Id]);
        DECLARE @accidentSpecialists nvarchar(36) = (
            SELECT TOP (1) LOWER(CONVERT(nvarchar(36), [Id])) FROM [dbo].[Organizations]
            WHERE [Role] = N'claim_source' AND [Active] = 1 AND [NormalizedName] = N'ACCIDENT SPECIALISTS'
            ORDER BY [Id]);

        UPDATE [p]
        SET [ReportSendingRulesJson] =
            N'{"everyCase":[' + ISNULL([e].[Actions], N'') + N'],"rules":[' + ISNULL([x].[Rules], N'') + N']}'
        FROM [dbo].[Principals] AS [p]
        CROSS APPLY OPENJSON([p].[ReportSendingRulesJson]) WITH (
            [SendTo] nvarchar(max) N'$.sendTo' AS JSON,
            [SendToOnly] bit N'$.sendToOnly',
            [ReplyAll] bit N'$.replyAll',
            [Cc] nvarchar(max) N'$.cc' AS JSON,
            [NeverCc] nvarchar(max) N'$.neverCc' AS JSON,
            [FeeNoteSeparate] bit N'$.attach.feeNoteSeparate',
            [Estimate] bit N'$.attach.estimate',
            [Audatex] bit N'$.attach.audatex',
            [VehicleImagesDocument] bit N'$.attach.vehicleImagesDocument',
            [FigureBreakdown] bit N'$.attach.figureBreakdown',
            [OfferExternalEstimates] bit N'$.attach.offerExternalEstimates',
            [OfferRepairInvoices] bit N'$.attach.offerRepairInvoices',
            [GarageFigures] bit N'$.garageFigures',
            [Hold] nvarchar(max) N'$.hold',
            [Reminders] nvarchar(max) N'$.reminders' AS JSON,
            [Rules] nvarchar(max) N'$.rules' AS JSON) AS [o]
        CROSS APPLY (
            SELECT STRING_AGG([a].[Action], N',') WITHIN GROUP (ORDER BY [a].[Ord], [a].[Sub]) AS [Actions]
            FROM (
                SELECT 1 AS [Ord], 0 AS [Sub],
                    CONVERT(nvarchar(max), N'{"kind":"SendTo","addresses":' + [o].[SendTo] + N'}') AS [Action]
                WHERE EXISTS (SELECT 1 FROM OPENJSON([o].[SendTo]))
                UNION ALL
                SELECT 2, 0, N'{"kind":"SendToOnly"}' WHERE [o].[SendToOnly] = 1
                UNION ALL
                SELECT 3, 0, N'{"kind":"ReplyAll"}' WHERE [o].[ReplyAll] = 1
                UNION ALL
                SELECT 4, 0, N'{"kind":"Cc","addresses":' + [o].[Cc] + N'}'
                WHERE EXISTS (SELECT 1 FROM OPENJSON([o].[Cc]))
                UNION ALL
                SELECT 5, 0, N'{"kind":"NeverCc","addresses":' + [o].[NeverCc] + N'}'
                WHERE EXISTS (SELECT 1 FROM OPENJSON([o].[NeverCc]))
                UNION ALL
                SELECT 6, [f].[Sub], N'{"kind":"Attach","file":"' + [f].[File] + N'"}'
                FROM (VALUES
                    (1, N'FeeNoteSeparate', [o].[FeeNoteSeparate]),
                    (2, N'Estimate', [o].[Estimate]),
                    (3, N'Audatex', [o].[Audatex]),
                    (4, N'VehicleImagesDocument', [o].[VehicleImagesDocument]),
                    (5, N'FigureBreakdown', [o].[FigureBreakdown]),
                    (6, N'OfferExternalEstimates', [o].[OfferExternalEstimates]),
                    (7, N'OfferRepairInvoices', [o].[OfferRepairInvoices])) AS [f]([Sub], [File], [On])
                WHERE [f].[On] = 1
                UNION ALL
                SELECT 7, 0, N'{"kind":"Hold","text":"' + STRING_ESCAPE([o].[Hold], 'json') + N'"}'
                WHERE NULLIF(LTRIM(RTRIM([o].[Hold])), N'') IS NOT NULL
                UNION ALL
                SELECT 8, CONVERT(int, [m].[key]), N'{"kind":"Task","text":"' + STRING_ESCAPE([m].[value], 'json') + N'"}'
                FROM OPENJSON([o].[Reminders]) AS [m]
                WHERE NULLIF(LTRIM(RTRIM([m].[value])), N'') IS NOT NULL) AS [a]) AS [e]
        CROSS APPLY (
            SELECT STRING_AGG([k].[Rule], N',') WITHIN GROUP (ORDER BY [k].[Ord]) AS [Rules]
            FROM (
                SELECT CONVERT(int, [rule].[key]) AS [Ord],
                    CONVERT(nvarchar(max), N'{"if":[' + [g].[Groups] + N'],"then":[' + [t].[Actions] + N']}') AS [Rule]
                FROM OPENJSON([o].[Rules]) AS [rule]
                CROSS APPLY OPENJSON([rule].[value]) WITH (
                    [Match] nvarchar(10) N'$.match',
                    [If] nvarchar(max) N'$.if' AS JSON,
                    [CcAdd] nvarchar(max) N'$.then.ccAdd' AS JSON,
                    [CcRemove] nvarchar(max) N'$.then.ccRemove' AS JSON,
                    [Remind] nvarchar(max) N'$.then.remind',
                    [Hold] nvarchar(max) N'$.then.hold',
                    [Stop] nvarchar(max) N'$.then.stop',
                    [RateCard] bit N'$.then.rateCard',
                    [FeeNoteSeparate] bit N'$.then.attach.feeNoteSeparate',
                    [Estimate] bit N'$.then.attach.estimate',
                    [Audatex] bit N'$.then.attach.audatex',
                    [VehicleImagesDocument] bit N'$.then.attach.vehicleImagesDocument',
                    [FigureBreakdown] bit N'$.then.attach.figureBreakdown',
                    [OfferExternalEstimates] bit N'$.then.attach.offerExternalEstimates',
                    [OfferRepairInvoices] bit N'$.then.attach.offerRepairInvoices') AS [ro]
                CROSS APPLY (
                    SELECT CASE
                        WHEN [ro].[Match] = N'Any'
                            THEN STRING_AGG(N'[' + [c].[Condition] + N']', N',') WITHIN GROUP (ORDER BY [c].[Ord])
                        ELSE N'[' + STRING_AGG([c].[Condition], N',') WITHIN GROUP (ORDER BY [c].[Ord]) + N']'
                    END AS [Groups]
                    FROM (
                        SELECT CONVERT(int, [ci].[key]) AS [Ord],
                            CONVERT(nvarchar(max),
                                N'{"fact":"' + CASE [co].[Kind] WHEN N'SenderNot' THEN N'Sender' ELSE [co].[Kind] END
                                + N'","values":' + ISNULL([co].[Values], N'[]')
                                + CASE WHEN [co].[Kind] = N'SenderNot' THEN N',"not":true' ELSE N'' END
                                + N'}') AS [Condition]
                        FROM OPENJSON([ro].[If]) AS [ci]
                        CROSS APPLY OPENJSON([ci].[value]) WITH (
                            [Kind] nvarchar(40) N'$.kind',
                            [Values] nvarchar(max) N'$.values' AS JSON) AS [co]
                        WHERE [co].[Kind] <> N'Mentions'
                        UNION ALL
                        SELECT 0, N'{"fact":"ClaimSource","values":["' + [rjs].[Id] + N'"]}'
                        FROM (VALUES (0, @claimSpecialists), (1, @accidentSpecialists)) AS [rjs]([Rule], [Id])
                        WHERE [p].[Code] = N'RJS'
                            AND [rjs].[Rule] = CONVERT(int, [rule].[key])
                            AND [rjs].[Id] IS NOT NULL
                            AND NOT EXISTS (
                                SELECT 1 FROM OPENJSON([ro].[If]) AS [mention]
                                WHERE JSON_VALUE([mention].[value], N'$.kind') <> N'Mentions')) AS [c]) AS [g]
                CROSS APPLY (
                    SELECT STRING_AGG([ta].[Action], N',') WITHIN GROUP (ORDER BY [ta].[Ord], [ta].[Sub]) AS [Actions]
                    FROM (
                        SELECT 1 AS [Ord], 0 AS [Sub],
                            CONVERT(nvarchar(max), N'{"kind":"Cc","addresses":' + [ro].[CcAdd] + N'}') AS [Action]
                        WHERE EXISTS (SELECT 1 FROM OPENJSON([ro].[CcAdd]))
                        UNION ALL
                        SELECT 2, 0, N'{"kind":"NeverCc","addresses":' + [ro].[CcRemove] + N'}'
                        WHERE EXISTS (SELECT 1 FROM OPENJSON([ro].[CcRemove]))
                        UNION ALL
                        SELECT 3, [f].[Sub],
                            N'{"kind":"' + CASE WHEN [f].[On] = 1 THEN N'Attach' ELSE N'DoNotAttach' END
                            + N'","file":"' + [f].[File] + N'"}'
                        FROM (VALUES
                            (1, N'FeeNoteSeparate', [ro].[FeeNoteSeparate]),
                            (2, N'Estimate', [ro].[Estimate]),
                            (3, N'Audatex', [ro].[Audatex]),
                            (4, N'VehicleImagesDocument', [ro].[VehicleImagesDocument]),
                            (5, N'FigureBreakdown', [ro].[FigureBreakdown]),
                            (6, N'OfferExternalEstimates', [ro].[OfferExternalEstimates]),
                            (7, N'OfferRepairInvoices', [ro].[OfferRepairInvoices])) AS [f]([Sub], [File], [On])
                        WHERE [f].[On] IS NOT NULL
                        UNION ALL
                        SELECT 4, 0, N'{"kind":"Hold","text":"' + STRING_ESCAPE([ro].[Hold], 'json') + N'"}'
                        WHERE NULLIF(LTRIM(RTRIM([ro].[Hold])), N'') IS NOT NULL
                        UNION ALL
                        SELECT 5, 0, N'{"kind":"Stop","text":"' + STRING_ESCAPE([ro].[Stop], 'json') + N'"}'
                        WHERE NULLIF(LTRIM(RTRIM([ro].[Stop])), N'') IS NOT NULL
                        UNION ALL
                        SELECT 6, 0, N'{"kind":"Task","text":"' + STRING_ESCAPE([ro].[Remind], 'json') + N'"}'
                        WHERE NULLIF(LTRIM(RTRIM([ro].[Remind])), N'') IS NOT NULL
                        UNION ALL
                        SELECT 7, 0, N'{"kind":"Task","text":"{{SqlJsonText(RateCardTask)}}"}'
                        WHERE [ro].[RateCard] = 1) AS [ta]) AS [t]
                WHERE [g].[Groups] IS NOT NULL AND [t].[Actions] IS NOT NULL
                UNION ALL
                SELECT 2147483647,
                    N'{"if":[[{"fact":"Outcome","values":["repairable"]}]],"then":[{"kind":"Task","text":"{{SqlJsonText(GarageFiguresTask)}}"}]}'
                WHERE [o].[GarageFigures] = 1) AS [k]) AS [x]
        WHERE JSON_QUERY([p].[ReportSendingRulesJson], N'$.everyCase') IS NULL;

        DECLARE @rulesDefault sysname = (
            SELECT [d].[name]
            FROM sys.default_constraints AS [d]
            INNER JOIN sys.columns AS [c]
                ON [c].[object_id] = [d].[parent_object_id] AND [c].[column_id] = [d].[parent_column_id]
            WHERE [d].[parent_object_id] = OBJECT_ID(N'[dbo].[Principals]') AND [c].[name] = N'ReportSendingRulesJson');
        -- EXEC takes variables and literals only, so the statement is built first.
        DECLARE @dropRulesDefault nvarchar(max) =
            N'ALTER TABLE [dbo].[Principals] DROP CONSTRAINT ' + QUOTENAME(@rulesDefault) + N';';
        IF @rulesDefault IS NOT NULL
            EXEC (@dropRulesDefault);
        ALTER TABLE [dbo].[Principals] ADD DEFAULT N'{{DefaultRulesJson}}' FOR [ReportSendingRulesJson];
        """;

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(Sql);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Report sending rules are built; the earlier shape and its fixed settings are not restored.");

    /// <summary>A text as it sits inside a JSON string inside a T-SQL Unicode literal.</summary>
    private static string SqlJsonText(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal);
}
