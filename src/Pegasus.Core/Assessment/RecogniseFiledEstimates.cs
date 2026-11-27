using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Assessment;

/// <summary>
/// One confirmed, current version the Worker's estimate recognition reads:
/// one not yet read for whether it is an estimate, or, when
/// <paramref name="IsRecognised"/>, one already recorded as an estimate
/// before the format it was read in was kept, read once more for that format.
/// </summary>
public sealed record EstimateRecognitionCandidate(
    Guid CaseId,
    Guid DocumentId,
    Guid VersionId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    bool IsGenerated,
    bool IsRecognised = false);

/// <summary>
/// The versions the Worker's estimate recognition reads, and where it
/// records what it found.
/// </summary>
public interface IEstimateRecognitionCandidates
{
    /// <summary>
    /// Confirmed, current versions with no recorded answer, newest first,
    /// then recognised versions with no recorded format, newest first, at
    /// most <paramref name="maximumItems"/>. A version never read always
    /// comes before one read again for its format.
    /// </summary>
    Task<IReadOnlyList<EstimateRecognitionCandidate>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the answer for a version that has none yet: whether it is an
    /// estimate and, for one that is, the format it was read in
    /// (<see cref="ParsedEstimate.ProviderName"/>).
    /// </summary>
    Task RecordAsync(Guid versionId, bool isEstimate, string? provider, CancellationToken cancellationToken);

    /// <summary>
    /// Records the format of a version already recorded as an estimate whose
    /// format was not kept: its provider name, or empty when reading it again
    /// could not determine one. Whether it is an estimate never changes.
    /// </summary>
    Task RecordProviderAsync(Guid versionId, string provider, CancellationToken cancellationToken);

    /// <summary>
    /// A version that could not be read this time. It is not listed again for
    /// a while, so it cannot take a place in every run ahead of newer files.
    /// </summary>
    void Defer(Guid versionId);

    /// <summary>
    /// Records what a newly recognised Audatex estimate states on the Case it
    /// is filed on, as system work, before its answer is recorded: the type
    /// its header gives each of its untyped files there
    /// (<see cref="FiledEstimateTypePolicy"/>), and the Case's empty mileage
    /// and VIN, never replacing a value (R1140, R1141). It records nothing on
    /// a closed or archived Case, and once only for a version. It throws an
    /// <see cref="IntakeDependencyUnavailableException"/> while a member of
    /// staff is editing the Case, so the version is read again later.
    /// </summary>
    Task RecordReadingAsync(
        EstimateRecognitionCandidate candidate,
        FiledEstimateReading reading,
        CancellationToken cancellationToken);
}

/// <summary>
/// What a filed Audatex estimate states for the Case it is filed on: the type
/// its header gives it, and its VIN and odometer reading (R1140, R1141).
/// </summary>
public sealed record FiledEstimateReading(
    DocumentSemanticRole? HeaderType,
    string? Vin,
    long? Mileage,
    string? MileageUnit)
{
    /// <summary>The policy a Case mileage filled from a filed estimate records (R1141).</summary>
    public const string MileagePolicyKey = "filed-estimate-mileage";
    public const int MileagePolicyVersion = 1;

    /// <summary>
    /// The Case history's line for what the reading recorded (operator,
    /// 26 November 2026; R1282). A VIN it filled records the line of its own
    /// that every filled VIN records.
    /// </summary>
    public static string HistoryLine(string fileName) => $"Estimate read: {fileName}";

    /// <summary>
    /// The reading of a parsed Audatex estimate, or null when it is another
    /// format's or states none of the three.
    /// </summary>
    public static FiledEstimateReading? From(ParsedEstimate parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        if (parsed.Route != RepairSpecificationSourceRoute.AudatexPdf)
        {
            return null;
        }

        var reading = new FiledEstimateReading(
            FiledEstimateTypePolicy.HeaderType(parsed.Header),
            GlassVinFillPolicy.Carried(parsed.Vin),
            parsed.Mileage,
            parsed.Mileage is null ? null : parsed.MileageUnit);
        return reading is { HeaderType: null, Vin: null, Mileage: null } ? null : reading;
    }
}

/// <summary>
/// Which type a filed Audatex estimate takes from its header, and which of
/// its files may take it (operator, 17 and 26 November 2026; R1140, R1281,
/// R1285). A header naming Collision Engineers is a CE estimate, any other
/// header an External estimate, and a report whose header cannot be read
/// stays as it was filed. A type staff or the Automation Actor chose is never
/// overwritten.
/// </summary>
public static class FiledEstimateTypePolicy
{
    /// <summary>The company whose header makes an estimate a CE estimate.</summary>
    public const string CollisionEngineers = "Collision Engineers";

    /// <summary>
    /// The source identity a file dropped on the Repair Spec import is filed
    /// under, followed by the import's operation key.
    /// </summary>
    public const string ImportDropIdentityPrefix = "estimate-import:";

    /// <summary>The type <paramref name="header"/> gives an estimate, or null when it is blank.</summary>
    public static DocumentSemanticRole? HeaderType(string? header) =>
        string.IsNullOrWhiteSpace(header)
            ? null
            : header.Contains(CollisionEngineers, StringComparison.OrdinalIgnoreCase)
                ? DocumentSemanticRole.CeEstimate
                : DocumentSemanticRole.ExternalEstimate;

    /// <summary>
    /// Whether a filed file's type is one nobody chose, so its header may set
    /// it: a mailed attachment as intake filed it (Instruction or
    /// Correspondence: an Audatex estimate is neither, R1285), an upload left
    /// on the picker's Other (R1281), or the Repair Spec import's dropped
    /// file, which offers no choice. Never a file whose type was changed
    /// after filing, one the Automation Actor added with its type, a
    /// generated one, an original source or an Audit report.
    /// </summary>
    public static bool MayType(
        DocumentSource source,
        DocumentSemanticRole role,
        string? sourceOccurrenceIdentity,
        bool typeChangedAfterFiling) =>
        !typeChangedAfterFiling
        && source switch
        {
            DocumentSource.Intake or DocumentSource.ExternalCorrespondence =>
                role is DocumentSemanticRole.Instruction
                    or DocumentSemanticRole.Correspondence
                    or DocumentSemanticRole.Other,
            DocumentSource.StaffUpload =>
                role == DocumentSemanticRole.Other
                || (role == DocumentSemanticRole.ExternalEstimate
                    && sourceOccurrenceIdentity?.StartsWith(ImportDropIdentityPrefix, StringComparison.Ordinal) == true),
            _ => false
        };
}

public sealed record RecogniseFiledEstimatesResult(
    int Candidates,
    int Estimates,
    int NotEstimates,
    int Failures,
    string? FirstFailure);

/// <summary>
/// Reads each newly filed Case file once and records whether it is an
/// estimate and the format it was read in, so the Files row offers Import as
/// repair spec only for a file the import itself would read
/// (<see cref="EstimateFormats"/>) and a delivery can tell an Audatex
/// estimate from any other (<see cref="EstimateFormats.AudatexProvider"/>).
/// A newly recognised Audatex estimate first records what it states on its
/// Case (<see cref="IEstimateRecognitionCandidates.RecordReadingAsync"/>).
/// </summary>
/// <remarks>
/// <para>
/// It runs as the system worker. A file Pegasus generated, one beyond the
/// import's size bound, or one no single format names is recorded as not an
/// estimate without being read. A read that fails for now is deferred and
/// read again later; nothing is recorded for it.
/// </para>
/// <para>
/// An estimate recognised before its format was kept is read once more, by
/// this same pass, for its format alone (operator, 6 October 2026). When that
/// read definitely cannot determine one (no single format names the file any
/// more, every format rejects its content, or its content is permanently
/// unreadable: see <see cref="IsPermanentlyUnreadable"/>) its format is
/// recorded as empty, meaning not determined, so it is not listed again. Any
/// other failure is deferred exactly as a first read's is, and it is read
/// again on a later run. It stays recorded as an estimate either way, and is
/// counted as an estimate when its format is found and as a failure when it
/// is not.
/// </para>
/// </remarks>
public sealed class RecogniseFiledEstimates(
    IEstimateRecognitionCandidates candidates,
    IReadLogicalDocumentVersion documents,
    IEnumerable<IEstimateDocumentParser> parsers)
{
    public const string ActorId = "estimate-recognition";

    private readonly IEstimateRecognitionCandidates candidates =
        candidates ?? throw new ArgumentNullException(nameof(candidates));
    private readonly IReadLogicalDocumentVersion documents =
        documents ?? throw new ArgumentNullException(nameof(documents));
    private readonly IEstimateDocumentParser[] parsers =
        [.. parsers ?? throw new ArgumentNullException(nameof(parsers))];

    public async Task<RecogniseFiledEstimatesResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var actor = ActionActor.SystemWorker(ActorId);
        var listed = await candidates.ListAsync(maximumItems, cancellationToken);
        var estimates = 0;
        var notEstimates = 0;
        var failures = 0;
        string? firstFailure = null;
        foreach (var candidate in listed)
        {
            if (candidate.IsGenerated
                || candidate.ContentLength is <= 0 or > ImportRawEstimate.MaximumDocumentBytes
                || EstimateFormats.NamedBy(parsers, candidate.FileName, candidate.MediaType) is null)
            {
                if (candidate.IsRecognised)
                {
                    // No single format names it any more, so its format
                    // cannot be determined.
                    await candidates.RecordProviderAsync(candidate.VersionId, string.Empty, cancellationToken);
                    failures++;
                    continue;
                }
                await candidates.RecordAsync(candidate.VersionId, isEstimate: false, provider: null, cancellationToken);
                notEstimates++;
                continue;
            }

            ReadOnlyMemory<byte> content;
            try
            {
                await using var document = await documents.OpenAsync(
                    new(actor, candidate.DocumentId, candidate.VersionId, IntakeAssetId: null,
                        candidate.CaseId, IntakeReceiptId: null, candidate.Sha256, candidate.ContentLength),
                    cancellationToken);
                content = await EstimateFormats.ReadVerifiedAsync(
                    document, candidate.ContentLength, candidate.Sha256, cancellationToken);
            }
            catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
            {
                // A read that failed is not an answer: the version is read
                // again later and the sweep carries on.
                failures++;
                firstFailure ??= exception.GetType().Name;
                await NotReadAsync(candidate, IsPermanentlyUnreadable(exception), cancellationToken);
                continue;
            }

            ParsedEstimate? parsed;
            try
            {
                parsed = EstimateFormats.Parse(parsers, candidate.FileName, candidate.MediaType, content);
            }
            catch (EstimateParseRejectedException)
            {
                parsed = null;
            }
            catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
            {
                // A parser fault is not a refusal either: the import would
                // fail the same way, so the version waits rather than being
                // recorded as no estimate.
                failures++;
                firstFailure ??= exception.GetType().Name;
                await NotReadAsync(candidate, permanently: false, cancellationToken);
                continue;
            }

            var provider = parsed?.ProviderName;
            if (candidate.IsRecognised)
            {
                await candidates.RecordProviderAsync(
                    candidate.VersionId, provider ?? string.Empty, cancellationToken);
                if (provider is null) failures++; else estimates++;
                continue;
            }
            // What the estimate states is recorded on its Case first, so a
            // Case being edited defers the version and nothing is lost; an
            // estimate recognised before is never read for it again (R1281).
            if (parsed is not null && FiledEstimateReading.From(parsed) is { } reading)
            {
                try
                {
                    await candidates.RecordReadingAsync(candidate, reading, cancellationToken);
                }
                catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
                {
                    failures++;
                    firstFailure ??= exception.GetType().Name;
                    candidates.Defer(candidate.VersionId);
                    continue;
                }
            }
            await candidates.RecordAsync(candidate.VersionId, provider is not null, provider, cancellationToken);
            if (provider is not null) estimates++; else notEstimates++;
        }
        return new(listed.Count, estimates, notEstimates, failures, firstFailure);
    }

    /// <summary>
    /// A version that could not be read this time is deferred and read again
    /// later, unless it is a recognised one read again only for its format
    /// whose content is <paramref name="permanently"/> unreadable: its format
    /// is then recorded as not determined, so it is not listed for ever. A
    /// version never read is always deferred.
    /// </summary>
    private async Task NotReadAsync(
        EstimateRecognitionCandidate candidate, bool permanently, CancellationToken cancellationToken)
    {
        if (candidate.IsRecognised && permanently)
        {
            await candidates.RecordProviderAsync(candidate.VersionId, string.Empty, cancellationToken);
            return;
        }
        candidates.Defer(candidate.VersionId);
    }

    /// <summary>
    /// A read failure no later attempt can cure: the exact version is gone
    /// (<see cref="FileNotFoundException"/>), or the bytes custody returns
    /// are not the version recorded (<see cref="InvalidDataException"/> from
    /// custody's own verification, or <see cref="EstimateParseRejectedException"/>
    /// from <see cref="EstimateFormats.ReadVerifiedAsync"/> for a wrong length
    /// or hash, or a document beyond the import's size bound). Every other
    /// failure, an I/O, timeout, database, HTTP or cancelled-request fault
    /// among them, may pass.
    /// </summary>
    private static bool IsPermanentlyUnreadable(Exception exception) =>
        exception is FileNotFoundException or InvalidDataException or EstimateParseRejectedException;

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken) =>
        IntakeExceptionPolicy.IsRecoverable(exception)
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}
