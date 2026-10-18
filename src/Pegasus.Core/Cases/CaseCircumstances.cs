using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

/// <summary>
/// The accident circumstances of one Case, read for the Search preview of the
/// Case that is selected rather than for every row of a page.
/// </summary>
public interface ICaseCircumstancesQueries
{
    /// <summary>
    /// The instruction draft's text when the Case came from an instruction
    /// draft, else the confirmed field; null when there is none or no Case has
    /// this identity.
    /// </summary>
    Task<string?> GetAsync(Guid caseId, CancellationToken cancellationToken);
}

public interface IGetCaseCircumstances
{
    /// <summary>The Case's accident circumstances, or null when there are none or no Case has this identity.</summary>
    Task<string?> ExecuteAsync(ActionActor actor, Guid caseId, CancellationToken cancellationToken);
}

/// <summary>
/// Requires <see cref="StaffAccessRight.PerformCasework"/>, as
/// <see cref="SearchCases"/> does, then reads the one Case's circumstances.
/// </summary>
public sealed class GetCaseCircumstances(ICaseCircumstancesQueries queries) : IGetCaseCircumstances
{
    private readonly ICaseCircumstancesQueries _queries = queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<string?> ExecuteAsync(ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return _queries.GetAsync(caseId, cancellationToken);
    }
}
