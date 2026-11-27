using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Assessment;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record MarketResearchAppToolInput(
    [property: Description("The application's name.")] string? Name = null,
    [property: Description("The application's version.")] string? Version = null);

internal sealed record MarketResearchGuideToolInput(
    [property: Description("The guide price the research was given, in pounds.")] decimal Amount);

internal sealed record MarketResearchSiteToolInput(
    [property: Description("The site searched.")] string? Label = null,
    [property: Description("How the site's search ended.")] string? Status = null,
    [property: Description("How many adverts the site returned.")] int Count = 0);

internal sealed record MarketResearchSearchToolInput(
    [property: Description("When the search finished.")] string? At = null,
    [property: Description("Each site's part in the search.")] IReadOnlyList<MarketResearchSiteToolInput>? Sites = null,
    [property: Description("What the sites were asked for, by filter name.")] JsonElement? Filters = null);

internal sealed record MarketResearchAdvertToolInput(
    [property: Description("The advert's number in the research, from 1, each used once.")] int Number,
    [property: Description("The advert's price in pounds.")] decimal Price,
    [property: Description("How the advert was chosen: search or custom.")] string? Origin = null,
    [property: Description("The site the advert is from.")] string? Source = null,
    [property: Description("The advert's address.")] string? Url = null,
    [property: Description("The vehicle as the advert describes it.")] string? Vehicle = null,
    [property: Description("The advertised vehicle's year.")] int? Year = null,
    [property: Description("The advertised vehicle's mileage.")] long? Mileage = null,
    [property: Description("The seller, and where.")] string? Seller = null,
    [property: Description("The comment made on the advert.")] string? Comment = null,
    [property: Description("Whether the advert counts in the average.")] bool InAverage = false,
    [property: Description("How closely the advert matches the vehicle, as the application scored it.")] int MatchScore = 0,
    [property: Description("When the advert was screenshotted.")] string? CapturedAt = null,
    [property: Description("The fileName of each of this advert's Screenshot files; every Screenshot file is named by one advert, once.")] IReadOnlyList<string>? Screenshots = null,
    [property: Description("The SHA-256 of the advert's first screenshot.")] string? Sha256 = null);

internal sealed record MarketResearchValueToolInput(
    [property: Description("The proposed retail value in pounds, above zero; it becomes the AI market research card's retail value.")] decimal? Proposed,
    [property: Description("How many adverts count in the average.")] int Count = 0,
    [property: Description("The mean of their prices, in pounds.")] decimal? Mean = null,
    [property: Description("The median of their prices, in pounds.")] decimal? Median = null,
    [property: Description("The lowest price, in pounds.")] decimal? Min = null,
    [property: Description("The highest price, in pounds.")] decimal? Max = null,
    [property: Description("The average, rounded.")] decimal? Rounded = null,
    [property: Description("Where proposed comes from: average or override.")] string? Basis = null,
    [property: Description("Why the value is not the average, for an override.")] string? OverrideReason = null);

internal sealed record MarketResearchRecordToolInput(
    [property: Description("The version of this record's shape: 1.")] int SchemaVersion,
    [property: Description("The adverts chosen, each with its comment and the names of its screenshots.")] IReadOnlyList<MarketResearchAdvertToolInput> Adverts,
    [property: Description("The figures the research arrived at.")] MarketResearchValueToolInput Value,
    [property: Description("The application that sent the research.")] MarketResearchAppToolInput? App = null,
    [property: Description("When the application sent the research.")] string? SubmittedAt = null,
    [property: Description("The vehicle's registration.")] string? Registration = null,
    [property: Description("The vehicle researched.")] string? Vehicle = null,
    [property: Description("The mileage the research assumes; it becomes the card's mileage.")] long? Mileage = null,
    [property: Description("Where that mileage comes from.")] string? MileageBasis = null,
    [property: Description("The guide price the research was given.")] MarketResearchGuideToolInput? Guide = null,
    [property: Description("The search the adverts came from.")] MarketResearchSearchToolInput? Search = null,
    [property: Description("The fileName of the EvidencePack file, when one is sent.")] string? EvidencePack = null);

internal sealed record MarketResearchFileToolInput(
    [property: Description("A staged document from pegasus_upload_begin whose parts are all sent, staged by this caller.")] Guid UploadId,
    [property: Description("Leaf name for the file, at most 255 characters; each used once.")] string FileName,
    [property: Description("Media type for the file, at most 128 characters.")] string MediaType,
    [property: Description("Report (the Market Valuation Evidence report, at most one), EvidencePack (the Advert Evidence Pack, at most one) or Screenshot.")] string Kind);

internal sealed record MarketResearchSubmittedFileToolItem(
    string FileName,
    string Kind,
    Guid OccurrenceId,
    Guid VersionId);

internal sealed record MarketResearchSubmissionToolResult(
    Guid SubmissionId,
    Guid CaseId,
    DateTimeOffset SubmittedAtUtc,
    string SubmittedBy,
    Guid ValuationId,
    IReadOnlyList<MarketResearchSubmittedFileToolItem> Files,
    bool IsReplay,
    string CorrelationId);

/// <summary>
/// Market research submitted to a Case by a member of staff signed in from
/// Pegasus Desktop (issue 2166, FRD-10, FRD-27): the research record, the
/// advert screenshots, the Advert Evidence Pack and the report, in one act.
/// It is in the assessment scope beside the valuation cards it writes one of.
/// The Automation Actor is refused it and files its research through
/// <c>pegasus_ai_job_complete_market_research</c>.
/// </summary>
[McpServerToolType]
internal sealed class MarketResearchMcpTools(
    SubmitMarketResearch submit,
    AutomationMcpAuditor auditor)
{
    private const string ReviewValues = "review must be Accepted or NotReviewed.";
    private const string KindValues = "kind must be Report, EvidencePack or Screenshot.";

    [McpServerTool(
        Name = "pegasus_market_research_submit",
        Title = "Submit market research",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Files market research on a case in one act, for a member of staff signed in from Pegasus Desktop: the research record, the advert screenshots, the Advert Evidence Pack and, when one was written, the Market Valuation Evidence report. The report is filed with the case's documents; the pack and the screenshots go in the market-research folder of the case's Box folder. It writes one AI market research valuation for the guide month from the record's proposed value, retail only, replacing an earlier research card for that month, and never the Engineer's Value. It completes a market research request waiting on the case. It takes no Case edit lease and no Case version, so it never waits on or ends an Engineer's session, and it is refused on a closed case. Every file is staged first with pegasus_upload_begin and pegasus_upload_part. Replaying the same operation key returns the first submission.")]
    public async Task<MarketResearchSubmissionToolResult> SubmitAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("Accepted when the member of staff accepted the report in the application before it was sent; NotReviewed for a report sent without that, and for research sent with no report.")] string review,
        [Description("The guide month the research is for, yyyy-MM.")] string guideMonth,
        [Description("The research record, schema version 1.")] MarketResearchRecordToolInput record,
        [Description("The files of the submission, at most 100: at most one Report, at most one EvidencePack, and the Screenshot files the record's adverts name.")] IReadOnlyList<MarketResearchFileToolInput>? files = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_market_research_submit",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                var command = new SubmitMarketResearchCommand(
                    AutomationMcpErrors.RequireId(caseId, "case identifier"),
                    context.Actor,
                    key,
                    AutomationMcpErrors.ParseName<MarketResearchReview>(review, ReviewValues),
                    AutomationMcpErrors.RequireMonth(guideMonth, "guideMonth must use yyyy-MM."),
                    MapRecord(record),
                    [.. (files ?? []).Select(MapFile)],
                    context.UploadOwner);
                MarketResearchSubmission submission;
                try
                {
                    submission = await submit.ExecuteAsync(command, cancellationToken);
                }
                catch (CaseFolderUnavailableException exception)
                {
                    // The Case page's own words for a folder that is not there to file into.
                    throw new McpException(OperatorLabels.CustodyFolderState(exception.State));
                }

                return new MarketResearchSubmissionToolResult(
                    submission.SubmissionId,
                    submission.CaseId,
                    submission.SubmittedAtUtc,
                    submission.SubmittedBy,
                    submission.ValuationId,
                    [.. submission.Files.Select(file => new MarketResearchSubmittedFileToolItem(
                        file.FileName,
                        file.Kind.ToString(),
                        file.OccurrenceId,
                        file.VersionId))],
                    submission.IsReplay,
                    AutomationMcpAuditor.CorrelationId(context, key));
            },
            cancellationToken);
    }

    // A file, an advert or the record itself sent as null is passed on as
    // none, and the submission's own validation refuses it.
    private static MarketResearchSubmissionFile MapFile(MarketResearchFileToolInput file) =>
        file is null
            ? null!
            : new MarketResearchSubmissionFile(
                file.UploadId,
                file.FileName,
                file.MediaType,
                AutomationMcpErrors.ParseName<MarketResearchFileKind>(file.Kind, KindValues));

    private static MarketResearchRecord MapRecord(MarketResearchRecordToolInput record) =>
        record is null
            ? null!
            : new MarketResearchRecord(
                record.SchemaVersion,
                record.App is null ? null : new MarketResearchRecordApp(record.App.Name, record.App.Version),
                record.SubmittedAt,
                record.Registration,
                record.Vehicle,
                record.Mileage,
                record.MileageBasis,
                record.Guide is null ? null : new MarketResearchRecordGuide(record.Guide.Amount),
                record.Search is null
                    ? null
                    : new MarketResearchRecordSearch(
                        record.Search.At,
                        record.Search.Sites?
                            .Where(site => site is not null)
                            .Select(site => new MarketResearchRecordSite(site.Label, site.Status, site.Count))
                            .ToArray(),
                        record.Search.Filters),
                record.Adverts?.Select(MapAdvert).ToArray(),
                record.Value is null
                    ? null
                    : new MarketResearchRecordValue(
                        record.Value.Count,
                        record.Value.Mean,
                        record.Value.Median,
                        record.Value.Min,
                        record.Value.Max,
                        record.Value.Rounded,
                        record.Value.Proposed,
                        record.Value.Basis,
                        record.Value.OverrideReason),
                record.EvidencePack);

    private static MarketResearchRecordAdvert MapAdvert(MarketResearchAdvertToolInput advert) =>
        advert is null
            ? null!
            : new MarketResearchRecordAdvert(
                advert.Number,
                advert.Origin,
                advert.Source,
                advert.Url,
                advert.Vehicle,
                advert.Year,
                advert.Mileage,
                advert.Price,
                advert.Seller,
                advert.Comment,
                advert.InAverage,
                advert.MatchScore,
                advert.CapturedAt,
                advert.Screenshots,
                advert.Sha256);
}
