using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
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
///    recorded — which means the record is written after the fact, and the
///    unique index is what makes that safe.
/// 2. **A second submission is refused.** EVA has no idempotency: a second
///    accepted instruction creates a second claim with its own File Reference
///    and no API call can undo it. So the once-per-case rule is checked before
///    the call and enforced by <c>UX_EvaSubmissions_CaseSucceeded</c> after
///    it.
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

        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var caseData = await caseDataQueries.GetAsync(request.CaseId, cancellationToken);
        if (caseData is null)
        {
            return null;
        }
        if (caseData.State != CaseLifecycleState.Review)
        {
            throw new CaseNotInReviewException(request.CaseId);
        }

        var modes = await modeStore.GetForPrincipalAsync(
            caseData.Identity.PrincipalCode,
            cancellationToken);
        if (!EvaSubmissionPolicy.Allows(modes, request.Trigger))
        {
            throw new EvaSubmissionNotEnabledException(request.CaseId);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Replay first: the same operation key must answer the same way rather
        // than submit a second time. This is what makes a double-clicked
        // button, or a queue message delivered twice, harmless.
        var replay = await FindReplayAsync(context, request, cancellationToken);
        if (replay is not null)
        {
            return new(replay, [], []);
        }

        // Then the once-per-case rule. Checked here so an operator is told
        // plainly rather than discovering it as a database error, and enforced
        // again by the unique index because this check is outside the write.
        var delivered = await FindSucceededAsync(context, request.CaseId, cancellationToken);
        if (delivered is not null)
        {
            throw new EvaAlreadySubmittedException(request.CaseId, delivered.FileReference);
        }

        var vehicle = await vehicleEvidenceQueries.GetAsync(request.CaseId, cancellationToken);
        var export = CaseEvaMapping.MapForOperatorExport(
            EvaCaseEvidenceReader.Build(caseData, vehicle),
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

        var images = await imageReader.LoadEligibleImagesAsync(
            context,
            request.CaseId,
            caseData.Identity.Reference,
            cancellationToken);
        if (images.Count == 0)
        {
            return new(null, export.UnrecordedFields, [EvaSubmissionPolicy.NoRetainedImagesReason]);
        }

        var payload = CaseEvaApiMapping.Map(
            export.Source.Fields,
            caseData.Identity.Reference,
            instructionSettings,
            images.Select(ToInstructionFile).ToArray());

        var result = await transport.SubmitInstructionAsync(payload, cancellationToken);
        await RecordSubmissionAsync(context, request, caseData, result, cancellationToken);
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

    private static Task<EvaSubmissionEntity?> FindSucceededAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken) => context.EvaSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == caseId && item.IsSucceeded,
                cancellationToken);

    /// <summary>
    /// A previous attempt under this exact operation key, returned as its own
    /// result. The action-history record is the replay authority — the same
    /// convention the export uses — and the row carries what to say.
    /// </summary>
    private static async Task<EvaSubmissionResult?> FindReplayAsync(
        PegasusDbContext context,
        SubmitCaseToEvaRequest request,
        CancellationToken cancellationToken)
    {
        var aggregateId = request.CaseId.ToString("D");
        var history = await context.ActionHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == "Case"
                    && item.AggregateId == aggregateId
                    && item.EventKind == EventKind
                    && item.CorrelationId == request.OperationKey,
                cancellationToken);
        if (history is null)
        {
            return null;
        }

        var row = await context.EvaSubmissions
            .AsNoTracking()
            .Where(item => item.CaseId == request.CaseId)
            .OrderByDescending(item => item.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new(
                Enum.Parse<EvaSubmissionOutcome>(row.Outcome),
                row.EvaId,
                row.FileReference,
                row.FailureCode,
                row.FailureDetail,
                row.ImagesSent);
    }

    private const string EventKind = "eva_api_submitted";

    /// <summary>
    /// The attempt and its outcome, recorded together.
    ///
    /// The case-workflow row is locked and re-checked exactly as the export
    /// does it, so same-case submissions observe each other in commit order
    /// and a case that left Review while EVA was thinking does not acquire a
    /// submission record it should not have. The window is short: the network
    /// call has already finished by the time this opens.
    /// </summary>
    private async Task RecordSubmissionAsync(
        PegasusDbContext context,
        SubmitCaseToEvaRequest request,
        CaseDataProjection caseData,
        EvaSubmissionResult result,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var attemptCount = await context.EvaSubmissions
            .CountAsync(item => item.CaseId == request.CaseId, cancellationToken);

        context.EvaSubmissions.Add(new()
        {
            Id = Guid.CreateVersion7(),
            CaseId = request.CaseId,
            WorkflowVersion = caseData.Version,
            ExternalRef = caseData.Identity.Reference,
            Outcome = result.Outcome.ToString(),
            IsSucceeded = result.Outcome == EvaSubmissionOutcome.Succeeded,
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
                CaseVersion = caseData.Version,
                Trigger = request.Trigger.ToString(),
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

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
