namespace Pegasus.Core.Assessment;

public sealed record ImportRawEstimateRequest(
    Pegasus.Core.Identity.ActionActor Actor, Guid CaseId, long ExpectedCaseVersion,
    string LeaseToken, Guid DocumentId, Guid DocumentVersionId, string Sha256,
    RepairSpecificationSourceRoute Route, string OperationKey, string Name);

public interface IImportRawEstimate
{
    Task<Guid> ExecuteAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The deterministic result of parsing one external estimate document:
/// the document's own version identity and its ordered lines, already in
/// the one estimate-line vocabulary (<see cref="EstimateLineCodes"/>).
/// Money and work units are read from the document, never derived; any
/// ambiguity in the document rejects the whole parse rather than landing
/// a value against the wrong line.
/// </summary>
public sealed record ParsedEstimate(
    string SourceVersion,
    IReadOnlyList<EstimateLineInput> Lines);

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
