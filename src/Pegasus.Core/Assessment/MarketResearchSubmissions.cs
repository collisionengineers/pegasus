using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// Which way a submission came (operator, 26 November 2026; R1309, R1310):
/// the member of staff accepted the report in Pegasus Desktop, or nobody
/// reviewed it there, which is both a report filed as soon as it was written
/// and research filed with no report. Pegasus keeps the mark with the
/// submission and does not check or show it (R1313).
/// </summary>
public enum MarketResearchReview
{
    Accepted,
    NotReviewed
}

public enum MarketResearchFileKind
{
    /// <summary>The Market Valuation Evidence report, written by Pegasus Desktop's own agent (R1239).</summary>
    Report,

    /// <summary>The Advert Evidence Pack.</summary>
    EvidencePack,

    /// <summary>A screenshot of one advert.</summary>
    Screenshot
}

public sealed record MarketResearchRecordApp(string? Name, string? Version);

public sealed record MarketResearchRecordGuide(decimal Amount);

public sealed record MarketResearchRecordSite(string? Label, string? Status, int Count);

/// <param name="Filters">What the sites were asked for, by filter name, as the application wrote it.</param>
public sealed record MarketResearchRecordSearch(
    string? At,
    IReadOnlyList<MarketResearchRecordSite>? Sites,
    JsonElement? Filters);

/// <param name="Screenshots">The names of this advert's screenshot files in the submission.</param>
public sealed record MarketResearchRecordAdvert(
    int Number,
    string? Origin,
    string? Source,
    string? Url,
    string? Vehicle,
    int? Year,
    long? Mileage,
    decimal Price,
    string? Seller,
    string? Comment,
    bool InAverage,
    int MatchScore,
    string? CapturedAt,
    IReadOnlyList<string>? Screenshots,
    string? Sha256);

/// <param name="Proposed">The proposed retail value, in pounds: the one figure Pegasus takes for the card.</param>
/// <param name="Basis">Whether <paramref name="Proposed"/> is the rounded average or the person's own figure.</param>
public sealed record MarketResearchRecordValue(
    int Count,
    decimal? Mean,
    decimal? Median,
    decimal? Min,
    decimal? Max,
    decimal? Rounded,
    decimal? Proposed,
    string? Basis,
    string? OverrideReason);

/// <summary>
/// The research record Pegasus Desktop sends, schema version 1: the adverts
/// with their comments, the proposed value and its basis, the free valuation
/// figure and the search summary (issue 2166). Pegasus keeps it with the
/// submission as it was sent and reads the proposed value, the mileage and
/// the files it names.
/// </summary>
/// <param name="EvidencePack">The name of the Advert Evidence Pack among the files, when one was exported.</param>
public sealed record MarketResearchRecord(
    int SchemaVersion,
    MarketResearchRecordApp? App,
    string? SubmittedAt,
    string? Registration,
    string? Vehicle,
    long? Mileage,
    string? MileageBasis,
    MarketResearchRecordGuide? Guide,
    MarketResearchRecordSearch? Search,
    IReadOnlyList<MarketResearchRecordAdvert>? Adverts,
    MarketResearchRecordValue? Value,
    string? EvidencePack);

/// <summary>One file of a submission, staged first as a document upload its sender owns.</summary>
public sealed record MarketResearchSubmissionFile(
    Guid UploadId,
    string FileName,
    string MediaType,
    MarketResearchFileKind Kind);

/// <summary>
/// Market research submitted to a Case in one act from Pegasus Desktop (issue
/// 2166; R1238, R1242 to R1244, R1309 to R1315): the record, the advert
/// screenshots, the Advert Evidence Pack and, when one was written, the
/// report. Like a research result from the AI job list it takes no Case
/// version and no edit lease, so the Engineer's session is left as it was.
/// </summary>
/// <param name="UploadOwner">Who the staged uploads belong to: the member of staff who sent them.</param>
public sealed record SubmitMarketResearchCommand(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey,
    MarketResearchReview Review,
    DateOnly GuideMonth,
    MarketResearchRecord Record,
    IReadOnlyList<MarketResearchSubmissionFile> Files,
    string UploadOwner);

public sealed record MarketResearchSubmittedFile(
    string FileName,
    MarketResearchFileKind Kind,
    Guid OccurrenceId,
    Guid VersionId);

public sealed record MarketResearchSubmission(
    Guid SubmissionId,
    Guid CaseId,
    DateTimeOffset SubmittedAtUtc,
    string SubmittedBy,
    Guid ValuationId,
    IReadOnlyList<MarketResearchSubmittedFile> Files,
    bool IsReplay);

/// <summary>
/// A validated submission with the bytes of its files, opened before the
/// store's transaction begins. <see cref="Contents"/> is in the order of the
/// command's files.
/// </summary>
public sealed record MarketResearchSubmissionFiling(
    SubmitMarketResearchCommand Command,
    string InputHash,
    string RecordJson,
    IReadOnlyList<ReadOnlyMemory<byte>> Contents);

/// <summary>
/// The Case has no Box folder to file into yet. The caller words the refusal
/// from the folder's state, as the Case page does.
/// </summary>
public sealed class CaseFolderUnavailableException(CaseCustodyState state) : Exception
{
    public CaseCustodyState State { get; } = state;
}

public interface IMarketResearchSubmissionStore
{
    /// <summary>
    /// The submission already made on the Case under the operation key, or
    /// null. The same key with other inputs is refused.
    /// </summary>
    Task<MarketResearchSubmission?> FindAsync(
        Guid caseId,
        string operationKey,
        string inputHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Files the submission in one transaction: its files, the research card
    /// for the guide month, its own rows and the Case history line, and the
    /// completion of a market research request waiting on the Case.
    /// </summary>
    Task<MarketResearchSubmission> SubmitAsync(
        MarketResearchSubmissionFiling filing,
        CancellationToken cancellationToken);
}

public static class MarketResearchSubmissionPolicy
{
    public const int SchemaVersion = 1;
    public const int MaximumAdverts = 50;
    public const int MaximumFiles = 100;
    public const int MaximumFileNameLength = 255;
    public const int MaximumMediaTypeLength = 128;

    /// <summary>The longest research record kept with a submission, as JSON.</summary>
    public const int MaximumRecordLength = 262_144;

    /// <summary>
    /// The Case history event of a submission. Like a research result from
    /// the AI job list it changes no Case field and leaves the version and
    /// any edit lease alone, so it is exempt from the per-version index.
    /// </summary>
    public const string SubmittedEventType = "market_research_submitted";

    /// <summary>The Case history line of a submission (operator, 26 November 2026; R1314).</summary>
    public const string HistoryReason = "Market research submitted from Pegasus Desktop";

    /// <summary>
    /// The reason shown on a market research request that a submission
    /// completed (operator, 26 November 2026; R1312).
    /// </summary>
    public const string RequestAnsweredReason = "Answered by market research from Pegasus Desktop";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Refuses a submission Pegasus cannot file as sent. Only a member of
    /// staff submits: an automated agent files its research through the AI
    /// job list (R28, R1309). The record carries a proposed retail value above
    /// zero, there is at most one report and one pack, every screenshot an
    /// advert names is exactly one of the files, and no file is left unnamed.
    /// </summary>
    public static void Validate(SubmitMarketResearchCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Actor);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        if (command.Actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        if (command.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(command));
        }
        if (string.IsNullOrWhiteSpace(command.OperationKey))
        {
            throw new ArgumentException("An operation key is required.", nameof(command));
        }
        if (command.OperationKey.Trim().Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The operation key cannot exceed 100 characters.");
        }
        if (!Enum.IsDefined(command.Review))
        {
            throw Invalid(nameof(command.Review));
        }
        Required(command.UploadOwner, nameof(command.UploadOwner));
        MarketResearchPolicy.ValidateGuideMonth(command.GuideMonth);

        var record = command.Record ?? throw Missing(nameof(command.Record));
        if (record.SchemaVersion != SchemaVersion)
        {
            throw Invalid(nameof(record.SchemaVersion));
        }
        if (record.Value?.Proposed is not > 0)
        {
            throw new ArgumentException(
                "A market research valuation carries its retail value.",
                nameof(command));
        }
        _ = Card(command, DateOnly.MinValue, TimeOnly.MinValue);

        var adverts = record.Adverts ?? throw Missing(nameof(record.Adverts));
        if (adverts.Count > MaximumAdverts
            || adverts.Any(advert => advert is null || advert.Number <= 0 || advert.Screenshots is null)
            || adverts.Select(advert => advert.Number).Distinct().Count() != adverts.Count)
        {
            throw Invalid(nameof(record.Adverts));
        }

        var files = command.Files ?? throw Missing(nameof(command.Files));
        if (files.Count > MaximumFiles || files.Any(file => file is null))
        {
            throw Invalid(nameof(command.Files));
        }
        foreach (var file in files)
        {
            if (file.UploadId == Guid.Empty)
            {
                throw Missing(nameof(file.UploadId));
            }
            Text(file.FileName, MaximumFileNameLength, nameof(file.FileName));
            Text(file.MediaType, MaximumMediaTypeLength, nameof(file.MediaType));
            if (!Enum.IsDefined(file.Kind))
            {
                throw Invalid(nameof(file.Kind));
            }
        }
        if (files.Select(file => file.FileName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count
            || files.Count(file => file.Kind == MarketResearchFileKind.Report) > 1)
        {
            throw Invalid(nameof(command.Files));
        }

        // The pack is the one the record names, and each screenshot is the
        // one file an advert names.
        var packs = files.Where(file => file.Kind == MarketResearchFileKind.EvidencePack).ToArray();
        if (packs.Length > 1
            || (packs.Length == 1 && !SameName(packs[0].FileName, record.EvidencePack)))
        {
            throw Invalid(nameof(record.EvidencePack));
        }
        var named = adverts.SelectMany(advert => advert.Screenshots!).ToArray();
        if (named.Any(string.IsNullOrWhiteSpace))
        {
            throw Invalid(nameof(MarketResearchRecordAdvert.Screenshots));
        }
        var namedOnce = named.Select(name => name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var screenshots = files
            .Where(file => file.Kind == MarketResearchFileKind.Screenshot)
            .Select(file => file.FileName.Trim())
            .ToArray();
        if (namedOnce.Count != named.Length
            || screenshots.Length != named.Length
            || !namedOnce.IsSupersetOf(screenshots))
        {
            throw Invalid(nameof(MarketResearchRecordAdvert.Screenshots));
        }
    }

    /// <summary>
    /// The one card a submission writes: AI market research for the guide
    /// month, its retail value the record's proposed value and no trade value
    /// (R185, R311, R1243), recorded at the office's date and time. It is
    /// written whether or not a report came with the research (R1311), and it
    /// never sets the Engineer's Value (FRD-27).
    /// </summary>
    public static ValuationDetails Card(SubmitMarketResearchCommand command, DateOnly date, TimeOnly time)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ValuationPolicy.ValidateMarketResearch(new(
            ValuationSource.AiMarketResearch,
            date,
            time,
            command.Record.Mileage,
            command.Record.Value!.Proposed,
            null,
            command.GuideMonth));
    }

    /// <summary>The advert a screenshot belongs to; null for a file that is not a screenshot.</summary>
    public static int? AdvertNumberOf(MarketResearchRecord record, MarketResearchSubmissionFile file)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(file);
        return file.Kind == MarketResearchFileKind.Screenshot
            ? record.Adverts!.Single(advert => advert.Screenshots!.Any(name => SameName(name, file.FileName))).Number
            : null;
    }

    /// <summary>The record as it is kept with the submission.</summary>
    public static string RecordJson(MarketResearchRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var json = JsonSerializer.Serialize(record, JsonOptions);
        return json.Length > MaximumRecordLength
            ? throw new ArgumentException($"The value cannot exceed {MaximumRecordLength} characters.", nameof(record))
            : json;
    }

    /// <summary>
    /// What makes a second call under one operation key the same submission:
    /// who sent it, the Case, the review mark, the month, the record and each
    /// file's upload, name, type and kind. A staged upload's identifier is
    /// derived from its sender and its own key, so the bytes are not read to
    /// tell a replay.
    /// </summary>
    public static string InputHash(SubmitMarketResearchCommand command, string recordJson)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordJson);
        var material = JsonSerializer.Serialize(
            new
            {
                command.CaseId,
                OperationKey = command.OperationKey.Trim(),
                Review = command.Review.ToString(),
                command.GuideMonth,
                Record = recordJson,
                Files = command.Files.Select(file => new
                {
                    file.UploadId,
                    FileName = file.FileName.Trim(),
                    MediaType = file.MediaType.Trim(),
                    Kind = file.Kind.ToString()
                }),
                ActorKind = command.Actor.Kind.ToString(),
                command.Actor.SubjectId
            },
            JsonOptions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static bool SameName(string? left, string? right) =>
        left is not null
        && right is not null
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void Required(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Missing(parameterName);
        }
    }

    private static void Text(string? value, int maximumLength, string parameterName)
    {
        Required(value, parameterName);
        if (value!.Trim().Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }

    private static ArgumentException Missing(string parameterName) =>
        new("A value is required.", parameterName);

    private static ArgumentException Invalid(string parameterName) =>
        new("The value is invalid.", parameterName);
}

/// <summary>
/// Submits market research from Pegasus Desktop. A call under a key already
/// used answers with the first submission before any staged upload is opened,
/// so a replay still answers once its uploads have expired. Otherwise the
/// files are opened whole, each checked against its declaration, before the
/// store's transaction begins.
/// </summary>
public sealed class SubmitMarketResearch(IMarketResearchSubmissionStore store, DocumentUploads uploads)
{
    public async Task<MarketResearchSubmission> ExecuteAsync(
        SubmitMarketResearchCommand command,
        CancellationToken cancellationToken)
    {
        MarketResearchSubmissionPolicy.Validate(command);
        var recordJson = MarketResearchSubmissionPolicy.RecordJson(command.Record);
        var inputHash = MarketResearchSubmissionPolicy.InputHash(command, recordJson);
        var replay = await store.FindAsync(
            command.CaseId,
            command.OperationKey.Trim(),
            inputHash,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var contents = new ReadOnlyMemory<byte>[command.Files.Count];
        for (var index = 0; index < contents.Length; index++)
        {
            var opened = await uploads.ExecuteAsync(
                new OpenDocumentUploadRequest(command.Files[index].UploadId, command.UploadOwner),
                cancellationToken);
            contents[index] = opened.Content;
        }

        return await store.SubmitAsync(new(command, inputHash, recordJson, contents), cancellationToken);
    }
}
