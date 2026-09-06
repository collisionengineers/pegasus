using System.Globalization;
using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The case-mutation envelope every estimate write carries, plus the retained
/// document the import reads: its occurrence on this case, the exact version
/// and the hash the caller recorded when it retained the file. The reason is
/// the one import reason (<see cref="ImportRawEstimate.ImportReason"/>).
/// </summary>
public sealed record ImportRawEstimateRequest(
    ActionActor Actor,
    Guid CaseId,
    long ExpectedVersion,
    string EditLeaseToken,
    Guid OccurrenceId,
    Guid DocumentVersionId,
    string Sha256,
    RepairSpecificationSourceRoute Route,
    string OperationKey,
    string Name)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, ImportRawEstimate.ImportReason, EditLeaseToken);

public interface IImportRawEstimate
{
    Task<Guid> ExecuteAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The totals a source document prints for itself. They are reconciliation
/// evidence and never an authority: Pegasus costs the estimate from its own
/// rows through <see cref="EstimateTotals"/>, and a printed figure that
/// disagrees is retained beside the calculation rather than dropped or
/// forced to agree. Every member is optional because a document prints only
/// the totals its own format carries.
/// </summary>
public sealed record EstimateSourceTotals(
    decimal? Parts = null,
    decimal? PanelWorkUnits = null,
    decimal? PaintWorkUnits = null,
    decimal? Materials = null,
    decimal? Specialist = null,
    decimal? Net = null,
    decimal? Vat = null,
    decimal? Gross = null);

/// <summary>
/// The deterministic result of parsing one external estimate document:
/// the document's own version identity and its ordered lines, already in
/// the one estimate-line vocabulary (<see cref="EstimateLineCodes"/>).
/// Money and work units are read from the document, never derived; any
/// ambiguity in the document rejects the whole parse rather than landing
/// a value against the wrong line. <see cref="ProviderName"/> names the
/// system the document came from and titles the Draft it lands as.
/// </summary>
public sealed record ParsedEstimate(
    string SourceVersion,
    IReadOnlyList<EstimateLineInput> Lines,
    string ProviderName,
    EstimateSourceTotals? SourceTotals = null);

/// <summary>
/// The whole import is refused with an operator-readable reason. Wrong
/// money is worse than no money, so a parser never drops or repairs an
/// unreadable line — it throws this instead.
/// </summary>
public sealed class EstimateParseRejectedException(string reason) : Exception(reason);

/// <summary>
/// Port for one external estimate document format (ENG-002). The document
/// reader (PDF text extraction) is an external boundary, so the format
/// parsers live in Infrastructure behind this port — the same split as
/// <c>IIntakeSourceReader</c>. Each implementation owns exactly one
/// provenance route from <see cref="RepairSpecificationSourceRoute"/>.
/// </summary>
public interface IEstimateDocumentParser
{
    RepairSpecificationSourceRoute Route { get; }

    /// <summary>Whether this parser recognizes the file by name and media type.</summary>
    bool CanParse(string fileName, string mediaType);

    /// <summary>
    /// Parses the document deterministically, or throws
    /// <see cref="EstimateParseRejectedException"/> naming why nothing was
    /// imported. Never returns a partial line set.
    /// </summary>
    ParsedEstimate Parse(ReadOnlyMemory<byte> content);
}

/// <summary>
/// The one canonical estimate import (plan B04). Every route — a dropped
/// PDF, the JSON document, a completed Glass's session — retains its
/// document first and then calls this with the retained version's identity,
/// so the import never re-reads an external system.
///
/// The mutation envelope, the Engineer authorization and the named route are
/// proven before anything is read, so a replay is never a way to read an
/// estimate the same call could not have created. The bytes are opened at
/// the document and length the case's own record states and at the exact
/// hash the caller recorded, then re-hashed here; the format is auto-detected
/// across the registered parsers, and zero or more than one match fails
/// closed rather than guessing. The parse lands as one new source-labelled
/// Draft beside the existing estimates — importing never touches Current —
/// and the same Case with the same source hash replays to the estimate that
/// import already created.
/// </summary>
public sealed class ImportRawEstimate(
    IEnumerable<IEstimateDocumentParser> parsers,
    IGetCaseDocumentMetadata metadata,
    IReadLogicalDocumentVersion documents,
    IListCaseEstimates estimates,
    ISaveEstimate save) : IImportRawEstimate
{
    /// <summary>The reason recorded against every imported Draft.</summary>
    public const string ImportReason = "Imported an estimate from its retained source document.";

    /// <summary>An estimate document beyond this size is refused unread.</summary>
    public const int MaximumDocumentBytes = 32 * 1024 * 1024;

    public async Task<Guid> ExecuteAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        if (!Enum.IsDefined(request.Route))
        {
            throw new EstimateParseRejectedException(
                "The import names no known estimate source, so nothing was imported.");
        }
        var sha256 = NormalizedHash(request.Sha256);
        var existing = await estimates.ExecuteAsync(request.CaseId, cancellationToken);
        if (existing.FirstOrDefault(estimate =>
                string.Equals(estimate.Source.Sha256, sha256, StringComparison.Ordinal)) is { } replayed)
        {
            return replayed.SpecificationId;
        }

        var (parser, parsed) = await ParseAsync(request, sha256, cancellationToken);
        var artifactIdentity = $"estimate-import:{request.OperationKey}";
        var saved = await save.ExecuteAsync(
            new(request.CaseId,
                request.ExpectedVersion,
                request.Actor,
                request.OperationKey,
                request.Reason,
                request.EditLeaseToken,
                EstimateId: null,
                new EstimateDetails(
                    string.IsNullOrWhiteSpace(request.Name)
                        ? NextName(existing, parsed.ProviderName)
                        : request.Name.Trim(),
                    RepairDays: null, LabourRate: null,
                    PaintMaterials: null, OtherCosts: null,
                    EstimatePolicy.DefaultVatPercent, Notes: null),
                [.. parsed.Lines.Select((line, index) => WithProvenance(
                    line, index + 1, artifactIdentity, request.DocumentVersionId, sha256))],
                new(parser.Route, artifactIdentity, parsed.SourceVersion, sha256)),
            cancellationToken);
        return saved.SpecificationId;
    }

    private async Task<(IEstimateDocumentParser Parser, ParsedEstimate Parsed)> ParseAsync(
        ImportRawEstimateRequest request, string sha256, CancellationToken cancellationToken)
    {
        var retained = await metadata.ExecuteAsync(
            new(request.CaseId, request.OccurrenceId, request.DocumentVersionId, request.Actor),
            cancellationToken)
            ?? throw new EstimateParseRejectedException(
                "The case does not hold the document version the import names, so nothing was imported.");
        await using var document = await documents.OpenAsync(
            new(request.Actor, retained.DocumentId, retained.VersionId, IntakeAssetId: null,
                request.CaseId, IntakeReceiptId: null, sha256, retained.ContentLength),
            cancellationToken);
        var content = await ReadAsync(document, cancellationToken);
        var actual = Convert.ToHexStringLower(SHA256.HashData(content.Span));
        if (!string.Equals(actual, sha256, StringComparison.Ordinal))
        {
            throw new EstimateParseRejectedException(
                "The retained document does not match the hash the import recorded, so nothing was imported.");
        }

        var matches = parsers
            .Where(candidate => candidate.CanParse(document.FileName, document.MediaType))
            .ToArray();
        var parser = matches.Length switch
        {
            0 => throw new EstimateParseRejectedException(
                "No estimate format recognized this document, so nothing was imported."),
            1 => matches[0],
            _ => throw new EstimateParseRejectedException(
                "More than one estimate format recognized this document, so nothing was imported."),
        };
        if (RepairSpecificationPolicy.IsDocumentRoute(request.Route) && request.Route != parser.Route)
        {
            throw new EstimateParseRejectedException(
                $"The document reads as {parser.Route} but the import names {request.Route}, so nothing was imported.");
        }
        return (parser, parser.Parse(content));
    }

    private static async Task<ReadOnlyMemory<byte>> ReadAsync(
        LogicalDocumentContent document, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await document.Content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumDocumentBytes)
            {
                throw new EstimateParseRejectedException(
                    $"The document is larger than {MaximumDocumentBytes} bytes, so nothing was imported.");
            }
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>
    /// Imported rows keep where they came from: the retained document, its
    /// version and hash, the row's own identity in that document, and the
    /// values as the document stated them.
    /// </summary>
    private static EstimateLineInput WithProvenance(
        EstimateLineInput line, int position, string artifactIdentity, Guid versionId, string sha256) =>
        line with
        {
            SourceDocumentIdentity = artifactIdentity,
            SourceDocumentVersionId = versionId,
            SourceDocumentSha256 = sha256,
            SourceRowIdentity = line.SourceRowIdentity
                ?? position.ToString(CultureInfo.InvariantCulture),
            Origin = line.Origin ?? new(
                line.Type, line.Description, line.PartNumber, line.Quantity,
                line.WorkUnits, line.PaintWorkUnits, line.Price, line.Materials),
        };

    /// <summary>
    /// The default Draft name when the caller supplies none: "{Provider} {n}",
    /// counting the imports this Case already holds from that provider. A
    /// caller-supplied <see cref="ImportRawEstimateRequest.Name"/> is used as
    /// given (trimmed) and validated by <see cref="EstimatePolicy.ValidateDetails"/>.
    /// </summary>
    private static string NextName(
        IReadOnlyList<RepairSpecificationVersion> existing, string providerName)
    {
        var prefix = $"{providerName} ";
        var highest = 0;
        foreach (var estimate in existing)
        {
            var name = estimate.Details.Name;
            if (name.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(
                    name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal)
                && ordinal > highest)
            {
                highest = ordinal;
            }
        }
        return $"{prefix}{highest + 1}";
    }

    private static string NormalizedHash(string sha256)
    {
        var normalized = sha256?.Trim().ToLowerInvariant();
        return normalized is { Length: 64 } && normalized.All(Uri.IsHexDigit)
            ? normalized
            : throw new ArgumentException("An import requires the retained document's SHA-256.", nameof(sha256));
    }
}
