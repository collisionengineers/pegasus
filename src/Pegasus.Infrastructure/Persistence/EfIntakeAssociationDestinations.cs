using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Keeps the upload suggestion list behind the same general Case search while
/// applying the current destination eligibility facts from the workflow row.
/// Staff linking reaches every Case in any state and every Triage Case; a
/// Triage Case destination carries its Triage state and version. The mutation
/// store remains the final transaction-time authority.
/// </summary>
public sealed class EfIntakeAssociationDestinations(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ICaseQueryStore caseQueries,
    IImageIntakeQueries imageIntakeQueries,
    IImageIntakeCaseCandidates imageIntakeCaseCandidates) : IIntakeAssociationDestinationQueries
{
    private const int SearchLimit = 8;
    // The broad Case search is only an input set. Continue through its pages
    // until the small destination list is full or the source is exhausted;
    // a first page of archived rows must not hide a viable Case behind it.
    private const int CandidatePageSize = 64;

    public async Task<IReadOnlyList<IntakeAssociationDestination>> GetSuggestedAsync(
        IntakeReceipt receipt,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (!IntakeAssociationDestinationPolicy.CanOffer(receipt))
        {
            return [];
        }

        var candidateIds = new List<Guid>();
        if (receipt.CaseMatchDecision?.MatchedCaseId is { } matchedCaseId)
        {
            candidateIds.Add(matchedCaseId);
        }
        candidateIds.AddRange(receipt.CaseMatchDecision?.Candidates
            .Select(candidate => candidate.CaseId) ?? []);
        // Image registration retains its normalised VRM on the Image Intake
        // record rather than inventing a CaseMatchDecision.  A registered
        // image can therefore offer the same eligible instructed Cases a
        // worker would consider, but still only as a staff confirmation.
        var imageIntake = await imageIntakeQueries.GetByOriginReceiptAsync(
            receipt.Id, cancellationToken);
        if (imageIntake is not null)
        {
            var imageCandidates = await imageIntakeCaseCandidates.FindEligibleByRegistrationAsync(
                imageIntake.Record.NormalizedVehicleRegistration, cancellationToken);
            candidateIds.AddRange(imageCandidates.Select(candidate => candidate.CaseId));
        }
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var orderedIds = candidateIds.Distinct().ToArray();
        var suggestions = new List<IntakeAssociationDestination>(orderedIds.Length);
        foreach (var caseId in orderedIds)
        {
            var destination = await GetAsync(receipt, caseId, actor, cancellationToken);
            if (destination is not null)
            {
                suggestions.Add(destination);
            }
        }
        return suggestions;
    }

    public async Task<IReadOnlyList<IntakeAssociationDestination>> SearchAsync(
        IntakeReceipt receipt,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (!IntakeAssociationDestinationPolicy.CanOffer(receipt)
            || string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
        {
            return [];
        }

        var destinations = new List<IntakeAssociationDestination>(SearchLimit);
        for (var page = 1; destinations.Count < SearchLimit; page++)
        {
            var searched = await caseQueries.SearchAsync(
                new(actor, new(Query: term.Trim(), IncludeTriage: true), Page: page, PageSize: CandidatePageSize),
                cancellationToken);
            if (searched.Items.Count == 0)
            {
                break;
            }

            var candidateIds = searched.Items.Select(item => item.CaseId).ToArray();
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var states = (await context.CaseWorkflows
                .AsNoTracking()
                .Where(item => candidateIds.Contains(item.CaseId))
                .Select(item => new { item.CaseId, item.State, item.ArchivedAtUtc, item.Version })
                .ToArrayAsync(cancellationToken))
                .ToDictionary(item => item.CaseId);
            var triageVersions = (await context.Triage
                .AsNoTracking()
                .Where(item => candidateIds.Contains(item.CaseId))
                .Select(item => new { item.CaseId, item.Version })
                .ToArrayAsync(cancellationToken))
                .ToDictionary(item => item.CaseId, item => item.Version);
            foreach (var item in searched.Items)
            {
                IntakeAssociationDestination? destination = null;
                if (item.TriageState is { } triageState
                    && triageVersions.TryGetValue(item.CaseId, out var triageVersion))
                {
                    destination = new(item.CaseId, item.Reference, item.Registration, item.Claimant, null, triageVersion)
                    {
                        TriageState = triageState
                    };
                }
                else if (states.TryGetValue(item.CaseId, out var state)
                    && Enum.TryParse<CaseLifecycleState>(state.State, false, out var lifecycleState)
                    && IntakeAssociationDestinationPolicy.IsViable(state.ArchivedAtUtc is not null))
                {
                    destination = new(
                        item.CaseId, item.Reference, item.Registration, item.Claimant, lifecycleState, state.Version);
                }

                if (destination is not null)
                {
                    destinations.Add(destination);
                    if (destinations.Count == SearchLimit)
                    {
                        break;
                    }
                }
            }

            if (!searched.HasNextPage)
            {
                break;
            }
        }

        return destinations;
    }

    public async Task<IntakeAssociationDestination?> GetAsync(
        IntakeReceipt receipt,
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (!IntakeAssociationDestinationPolicy.CanOffer(receipt) || caseId == Guid.Empty)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.CaseWorkflows.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => new
            {
                item.CaseId,
                item.Case.Reference,
                item.State,
                item.ArchivedAtUtc,
                item.Version
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is not null)
        {
            return Enum.TryParse<CaseLifecycleState>(row.State, false, out var state)
                && IntakeAssociationDestinationPolicy.IsViable(row.ArchivedAtUtc is not null)
                    ? new(row.CaseId, row.Reference, null, null, state, row.Version)
                    : null;
        }

        // A Triage Case has no workflow; its Triage version is the authority.
        var triage = await context.Triage.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => new
            {
                item.CaseId,
                item.Case.Reference,
                item.NormalizedVehicleRegistration,
                item.State,
                item.Version
            })
            .SingleOrDefaultAsync(cancellationToken);
        return triage is null
            ? null
            : new(triage.CaseId, triage.Reference, triage.NormalizedVehicleRegistration, null, null, triage.Version)
            {
                TriageState = EfTriageStore.ParseState(triage.State)
            };
    }
}
