using System.Data;
using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// EXT-04: one submission of one case to EVA over the API.
///
/// The sibling of <see cref="EvaHandoffStore"/>, and deliberately its twin
/// where it can be: the same Review gate, the same case-data mapping, the same
/// eligible photographs, the same operation-key replay, the same permanent
/// action history. What an operator sends by drag-and-drop and what Pegasus
/// sends over the wire are the same case, and reusing the export's own
/// machinery is what keeps that true.
///
/// Two things differ, and both follow from EVA being a real remote system:
///
/// 1. **The network call happens outside the transaction.** Holding a
///    serializable case lock across an HTTP round trip carrying every
///    photograph of a case would block casework for as long as EVA is slow.
///    So the case is read and gated, the call is made, and the result is
///    recorded after the fact.
/// 2. **Manual re-sends are distinct handoffs.** Every explicit operator
///    operation key retains its own outcome and EVA identifiers.
/// </summary>
public sealed class EvaSubmissionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ICaseDataQueries caseDataQueries,
    IVehicleEvidenceQueries vehicleEvidenceQueries,
    IEvaSubmissionModeStore modeStore,
    EvaCaseImageReader imageReader,
    IEvaApiTransport transport,
    EvaInstructionSettings instructionSettings,
    TimeProvider timeProvider) : ISubmitCaseToEva
{

    public async Task<SubmitCaseToEvaResult?> ExecuteAsync(
        SubmitCaseToEvaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            return null;
        }
        if (!Guid.TryParseExact(request.OperationKey, "N", out _))
        {
            throw new ArgumentException("The operation key is invalid.", nameof(request));
        }

        var caseData = await caseDataQueries.GetAsync(request.CaseId, cancellationToken);
        if (caseData is null)
        {
            return null;
        }
        EvaSubmissionPolicy.RequireAuthorizedActor(request.Actor, request.Initiator);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // A completed operation is a replay before any mutable first-send
        // prerequisite is read. A completed handoff remains the answer even
        // if the case, claimant, images or staff assignment have since moved.
        var replay = await FindReplayAsync(context, request, cancellationToken);
        if (replay is not null)
        {
            return new(replay, [], []);
        }

        var modes = await modeStore.GetForPrincipalAsync(
            caseData.Identity.PrincipalCode,
            cancellationToken);
        var automaticFailureRetry = request.Initiator == EvaSubmissionInitiator.Manual
            && EvaSubmissionPolicy.AllowsAutomaticSubmission(modes)
            && (await context.EvaSubmissions.AnyAsync(item => item.CaseId == request.CaseId
                    && !item.IsDelivered, cancellationToken)
                || await context.Set<AutomaticEvaReviewSubmissionEntity>().AnyAsync(item =>
                    item.CaseId == request.CaseId
                    && item.State == nameof(AutomaticEvaReviewSubmissionState.ReconciliationRequired),
                    cancellationToken));
        if (request.Initiator == EvaSubmissionInitiator.Manual
            && !EvaSubmissionPolicy.AllowsManualSubmission(modes)
            && !automaticFailureRetry)
        {
            throw new EvaSubmissionNotEnabledException(request.CaseId);
        }
        EvaSubmissionPolicy.RequireAuthorizedInitiator(modes, request.Actor, request.Initiator);

        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == request.CaseId)
            .Select(item => new
            {
                item.State,
                item.AssignedEngineerId,
                item.SignOffEngineerId
            })
            .SingleAsync(cancellationToken);
        var initialState = Enum.Parse<CaseLifecycleState>(workflow.State);
        var initialResultingState = EvaSubmissionPolicy.StateAfterSend(initialState);
        var profiles = await new EfStaffAccountQueries(context)
            .ListSignOffEngineersAsync(cancellationToken);
        var signOffEngineer = EvaHandoffPolicy.ResolveRequiredSignOffEngineer(
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            profiles);
        if (initialResultingState != initialState)
        {
            await CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync(
                new EfCaseEngineerEligibility(contextFactory),
                initialState,
                workflow.AssignedEngineerId,
                cancellationToken);
        }

        var vehicle = await vehicleEvidenceQueries.GetAsync(request.CaseId, cancellationToken);
        var export = CaseEvaMapping.MapForOperatorExport(
            EvaCaseEvidenceReader.Build(caseData, vehicle),
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

        var claimantAddress = EvaSubmissionPolicy.AcceptedClaimantAddress(caseData.Claimant.Address);
        if (claimantAddress is null)
        {
            return new(null, export.UnrecordedFields, [EvaSubmissionPolicy.InvalidClaimantAddressReason]);
        }

        // Reading a case's photographs goes to Box, so a transport failure is
        // an ordinary way for this to fail. It is translated here rather than
        // left to escape as a provider-shaped exception. An unreachable
        // document store is an I/O failure distinct from EVA's own outcome.
        List<EvaBundleImage> images;
        try
        {
            images = await imageReader.LoadEligibleImagesAsync(
                context,
                request.CaseId,
                caseData.Identity.Reference,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new IOException(
                "The case's stored images could not be read for EVA submission.",
                exception);
        }
        if (images.Count == 0)
        {
            return new(null, export.UnrecordedFields, [EvaSubmissionPolicy.NoRetainedImagesReason]);
        }

        var payload = CaseEvaApiMapping.Map(
            export.Source.Fields,
            caseData.Identity.Reference,
            caseData.Identity.PrincipalCode,
            claimantAddress,
            instructionSettings,
            images.Select(ToInstructionFile).ToArray());


        var result = await transport.SubmitInstructionAsync(payload, cancellationToken);
        await RecordSubmissionAsync(
            context,
            request,
            caseData,
            result,
            signOffEngineer.StaffId,
            cancellationToken);
        return new(result, export.UnrecordedFields, []);
    }

    /// <summary>
    /// One eligible photograph as an EVA file. The name is split from its
    /// extension because EVA's file model wants them apart, and the ordinal
    /// prefix the archive uses is kept so the two routes present the same
    /// photographs in the same order under the same names.
    /// </summary>
    private static EvaInstructionFile ToInstructionFile(EvaBundleImage image)
    {
        var extension = Path.GetExtension(image.FileName);
        var name = Path.GetFileNameWithoutExtension(image.FileName);
        return new(
            string.IsNullOrWhiteSpace(name)
                ? $"{image.Ordinal:000}"
                : $"{image.Ordinal:000} {name}",
            string.IsNullOrWhiteSpace(extension)
                ? MediaTypeExtension(image.MediaType)
                : extension.ToLowerInvariant(),
            image.Content);
    }

    /// <summary>
    /// An extension for a file whose name carries none. The media type is
    /// already constrained to JPEG or PNG by the eligibility policy, so there
    /// is no third case to guess at.
    /// </summary>
    private static string MediaTypeExtension(string mediaType) =>
        mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

    /// <summary>A completed operation's exact retained provider result.</summary>
    private static async Task<EvaSubmissionResult?> FindReplayAsync(
        PegasusDbContext context,
        SubmitCaseToEvaRequest request,
        CancellationToken cancellationToken)
    {
        var row = await context.EvaSubmissions
            .AsNoTracking()
            .Where(item => item.CaseId == request.CaseId
                && item.OperationKey == request.OperationKey)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new(
                Enum.Parse<EvaSubmissionOutcome>(row.Outcome!),
                row.EvaId,
                row.FileReference,
                row.FailureCode,
                row.FailureDetail,
                row.ImagesSent);
    }


    private const string EventKind = "eva_api_submitted";

    /// <summary>
    /// The completed operation becomes a retained handoff with its outcome
    /// and action history in one transaction.
    ///
    /// Deliberately unconditional. The export re-checks Review under a row
    /// lock before it writes, because it can still decline to produce the
    /// archive; this cannot decline anything, because by the time it runs the
    /// request has already reached EVA. Refusing to record it for a case that
    /// left Review while EVA was thinking would lose the fact of delivery and
    /// let the same case be submitted a second time - which EVA, having no
    /// idempotency, would turn into a second claim.
    ///
    /// So the record states what happened. The locked workflow re-check below
    /// owns the local state transition and prevents a partial local handoff.
    /// </summary>
    private async Task RecordSubmissionAsync(
        PegasusDbContext context,
        SubmitCaseToEvaRequest request,
        CaseDataProjection caseData,
        EvaSubmissionResult result,
        Guid submittedSignOffEngineerId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var workflows = context.Database.IsSqlServer()
            ? context.CaseWorkflows.FromSqlInterpolated($"""
                SELECT *
                FROM [CaseWorkflows] WITH (UPDLOCK, HOLDLOCK)
                WHERE [CaseId] = {request.CaseId}
                """)
            : context.CaseWorkflows.Where(item => item.CaseId == request.CaseId);
        var workflow = await workflows.SingleAsync(cancellationToken);
        var currentState = Enum.Parse<CaseLifecycleState>(workflow.State);
        var resultingState = currentState;
        Exception? transitionFailure = null;
        try
        {
            // Gated on delivery (CASE-040 review), not on state alone: a
            // Rejected or Unknown outcome never reached EVA, so it
            // is not a handoff and must not move the case out of Review.
            resultingState = EvaSubmissionPolicy.StateAfterSend(
                currentState,
                result.IsDelivered);
            if (resultingState != currentState)
            {
                await CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync(
                    new EfCaseEngineerEligibility(contextFactory),
                    currentState,
                    workflow.AssignedEngineerId,
                    cancellationToken);
            }
            var eligibleProfiles = await new EfStaffAccountQueries(context)
                .ListSignOffEngineersAsync(cancellationToken);
            EvaHandoffPolicy.ResolveRequiredSignOffEngineer(
                workflow.SignOffEngineerId,
                workflow.AssignedEngineerId,
                eligibleProfiles);
            if (workflow.Version != caseData.Version)
            {
                throw new CaseVersionConflictException(
                    request.CaseId,
                    caseData.Version,
                    workflow.Version);
            }
        }
        // EvaHandoffStateException, EvaSignOffEngineerRequiredException and
        // CaseVersionConflictException are all InvalidOperationException
        // (CASE-040 review NIT); this filter is deliberately just the base
        // type because the block above is a fixed, closed set of local
        // re-checks and every failure it can raise already derives from it.
        catch (InvalidOperationException exception)
        {
            transitionFailure = exception;
        }

        var resultingVersion = transitionFailure is null && resultingState != currentState
            ? checked(workflow.Version + 1)
            : workflow.Version;

        var attemptCount = await context.EvaSubmissions
            .CountAsync(item => item.CaseId == request.CaseId, cancellationToken);

        context.EvaSubmissions.Add(new()
        {
            Id = Guid.CreateVersion7(),
            CaseId = request.CaseId,
            WorkflowVersion = resultingVersion,
            ExternalRef = caseData.Identity.Reference,
            OperationKey = request.OperationKey,
            Outcome = result.Outcome.ToString(),
            IsDelivered = result.IsDelivered,
            EvaId = result.EvaId,
            FileReference = result.FileReference,
            FailureCode = result.FailureCode,
            FailureDetail = result.FailureDetail,
            ImagesSent = result.ImagesSent,
            AttemptCount = attemptCount + 1,
            ActorSubjectId = request.Actor.SubjectId,
            SubmittedAtUtc = timeProvider.GetUtcNow()
        });

        var history = DocumentActionHistory.Succeeded(
            "Case",
            request.CaseId.ToString("D"),
            EventKind,
            request.Actor,
            timeProvider.GetUtcNow(),
            request.OperationKey,
            afterJson: DocumentActionHistory.Serialize(new
            {
                CaseVersion = resultingVersion,
                AssignedEngineerId = workflow.AssignedEngineerId,
                SignOffEngineerId = submittedSignOffEngineerId,
                Outcome = result.Outcome.ToString(),
                result.EvaId,
                result.FileReference,
                result.FailureCode,
                result.ImagesSent,
                Mapping = new
                {
                    CaseEvaApiMapping.MappingKey,
                    CaseEvaApiMapping.MappingVersion
                }
            }));
        history.PolicyVersion =
            $"{EvaSubmissionPolicy.PolicyKey}/v{EvaSubmissionPolicy.PolicyVersion}";
        context.ActionHistory.Add(history);

        if (transitionFailure is null && resultingState != currentState)
        {
            workflow.State = resultingState.ToString();
            workflow.Version = resultingVersion;
            workflow.EditLeaseToken = null;
            workflow.EditLeaseTokenHash = null;
            workflow.EditLeaseRequestHash = null;
            workflow.EditLeaseHolder = null;
            workflow.EditLeaseHolderKind = null;
            workflow.EditLeaseOperationKey = null;
            workflow.EditLeaseExpiresAtUtc = null;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (transitionFailure is not null)
        {
            ExceptionDispatchInfo.Capture(transitionFailure).Throw();
        }
    }
}
