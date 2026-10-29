using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
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
    IImageIntakeCaseCandidates imageIntakeCaseCandidates,
    IIntakeSubmissionGroupStore submissionGroups,
    IVrmSuggestionStore vrmSuggestions,
    ThirdPartyReportCaseSuggestions reportSuggestions) : IIntakeAssociationDestinationQueries
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
        else if (receipt.SourceIdentity.Channel == IntakeSourceChannel.ManualUpload
            && (ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt)
                || ImageIntakeLifecycleRules.IsPhotographReadEligible(receipt))
            && await ReadUnregisteredImageRegistrationAsync(receipt, cancellationToken) is { } registration)
        {
            // A manual upload whose images matched Cases waits unregistered
            // (operator, 11 October 2026): offer the Cases its confident read
            // would have registered against. An upload filed as a file still
            // has its photographs read, and the one registration they agree
            // on offers the same Cases (operator, 28 October 2026, R472, R473).
            var imageCandidates = await imageIntakeCaseCandidates.FindEligibleByRegistrationAsync(
                registration, cancellationToken);
            candidateIds.AddRange(imageCandidates.Select(candidate => candidate.CaseId));
        }
        // A manually uploaded third-party report offers the Cases its printed
        // registration and claim reference match (operator, 12 October 2026).
        candidateIds.AddRange(await reportSuggestions.FindAsync(receipt, cancellationToken));
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

    /// <summary>
    /// The one registration the upload's image members confidently read, or
    /// null when they read none or disagree. An upload filed as a file reads
    /// only its own photographs: a batch's decision belongs to its image
    /// members (FRD-19 non-image members are excluded).
    /// </summary>
    private async Task<string?> ReadUnregisteredImageRegistrationAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        var group = ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt)
            ? await submissionGroups.FindForMemberSourceAsync(receipt.SourceIdentity, cancellationToken)
            : null;
        var receiptIds = group is null
            ? [receipt.Id]
            : group.Members.Select(member => member.ProcessedReceiptId).OfType<Guid>().ToArray();
        var suggestions = (await vrmSuggestions.ListForReceiptsAsync(receiptIds, cancellationToken))
            .Values
            .SelectMany(readings => readings);
        return VrmRecognitionProvisionalBar.SingleAcceptedRegistration(suggestions);
    }

    public Task<IReadOnlyList<IntakeAssociationDestination>> SearchAsync(
        IntakeReceipt receipt,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return FindAsync(IntakeAssociationDestinationPolicy.CanOffer(receipt), receipt, term, actor, cancellationToken);
    }

    public Task<IReadOnlyList<IntakeAssociationDestination>> SearchToChangeAsync(
        IntakeReceipt receipt,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return FindAsync(IntakeAssociationDestinationPolicy.CanOfferToChange(receipt), receipt, term, actor, cancellationToken);
    }

    /// <summary>
    /// The one typed search behind <see cref="SearchAsync"/> and
    /// <see cref="SearchToChangeAsync"/>; <paramref name="offered"/> is the
    /// policy's answer for the receipt. The Case a linked receipt is linked to
    /// is never listed (for an unlinked receipt there is none).
    /// </summary>
    private async Task<IReadOnlyList<IntakeAssociationDestination>> FindAsync(
        bool offered,
        IntakeReceipt receipt,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (!offered || string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
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

            foreach (var item in searched.Items)
            {
                if (item.CaseId == receipt.CurrentCaseId)
                {
                    continue;
                }

                IntakeAssociationDestination? destination = null;
                if (item.TriageState is { } triageState
                    && item.TriageVersion is { } triageVersion)
                {
                    destination = new(item.CaseId, item.Reference, item.Registration, item.Claimant, null, triageVersion)
                    {
                        TriageState = triageState,
                        Principal = item.Principal
                    };
                }
                else if (item.WorkflowVersion is { } workflowVersion
                    && IntakeAssociationDestinationPolicy.IsViable(item.Archived))
                {
                    destination = new(
                        item.CaseId, item.Reference, item.Registration, item.Claimant, item.State, workflowVersion)
                    {
                        Principal = item.Principal
                    };
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

        return await GetAsync(caseId, actor, cancellationToken);
    }

    /// <summary>
    /// The Case as a destination before any receipt exists for it (Add
    /// evidence on a Case page): the same workflow or Triage authority row the
    /// receipt overload reads, with the registration, claimant and Principal
    /// the destination card shows, read from the Case search by exact
    /// reference. A failed facts read leaves the card with the reference and
    /// state it certainly has.
    /// </summary>
    public async Task<IntakeAssociationDestination?> GetAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        var destination = await GetAuthorityAsync(caseId, actor, cancellationToken);
        if (destination is null)
        {
            return null;
        }

        try
        {
            var searched = await caseQueries.SearchAsync(
                new(actor, new(CaseReference: destination.Reference, IncludeTriage: true), Page: 1, PageSize: 2),
                cancellationToken);
            var facts = searched.Items.FirstOrDefault(item => item.CaseId == caseId);
            return facts is null
                ? destination
                : destination with
                {
                    Registration = facts.Registration ?? destination.Registration,
                    Claimant = facts.Claimant,
                    Principal = facts.Principal
                };
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException
            && !cancellationToken.IsCancellationRequested
            && IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return destination;
        }
    }

    public async Task<IntakeAssociationDestination?> GetAuthorityAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return caseId == Guid.Empty
            ? null
            : await ReadAuthorityAsync(caseId, cancellationToken);
    }

    private async Task<IntakeAssociationDestination?> ReadAuthorityAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
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
