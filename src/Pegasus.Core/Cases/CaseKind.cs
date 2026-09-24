namespace Pegasus.Core.Cases;

/// <summary>
/// The kind of Case a Case identity names, read from the Case row alone. A
/// Triage Case has no Case workflow, so the Case record dispatches on this
/// before it reads anything workflow-shaped.
/// </summary>
public interface ICaseKindQueries
{
    /// <summary>The Case's type, or null when no Case has this identity.</summary>
    Task<CaseType?> GetAsync(Guid caseId, CancellationToken cancellationToken);
}

public interface IGetCaseKind
{
    /// <summary>The Case's type, or null when no Case has this identity.</summary>
    Task<CaseType?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken);
}

public sealed class GetCaseKind(ICaseKindQueries queries) : IGetCaseKind
{
    private readonly ICaseKindQueries _queries = queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<CaseType?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken) =>
        caseId == Guid.Empty
            ? Task.FromResult<CaseType?>(null)
            : _queries.GetAsync(caseId, cancellationToken);
}
