using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Case-filing replay's page and history rows. The page's eligibility
/// predicate is <see cref="CaseFilingTargetRule"/> in SQL, built from the same
/// state list (<see cref="IntakePromotionTarget.AutomaticPromotionEligibleStates"/>).
/// The replay window is read on the SQL server's clock (R379) and the edit
/// lease at the time Core passes in, so this adapter reads no clock of its
/// own.
/// </summary>
internal sealed class EfCaseFilingReplayStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseFilingReplayStore
{
    internal const string ReplayFailedEventType = "replay_failed";
    internal const string ReplayExhaustedEventType = "replay_exhausted";
    private const string PromotionStartedEventType = "intake_case_evidence_promotion_started";
    private const string PromotionCompletedEventType = "intake_case_evidence_promotion_completed";
    private const string SystemWorkerKind = nameof(ActorKind.SystemWorker);

    public async Task<IReadOnlyList<CaseFilingReplayCandidate>> ListDueAsync(
        CaseFilingReplayCursor? after,
        TimeSpan systemAssociationWindow,
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var automaticStates = IntakePromotionTarget.AutomaticPromotionEligibleStates;
        var windowStartSeconds = -systemAssociationWindow.TotalSeconds;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var listed =
            from association in context.IntakeManualAssociations.AsNoTracking()
            join work in context.IntakeWorkItems.AsNoTracking()
                on (Guid?)association.IntakeReceiptId equals work.ProcessedReceiptId
            join receipt in context.IntakeReceipts.AsNoTracking()
                on association.IntakeReceiptId equals receipt.Id
            join workflowRow in context.CaseWorkflows.AsNoTracking()
                on association.CaseId equals workflowRow.CaseId into workflows
            from workflow in workflows.DefaultIfEmpty()
            where association.IsActive
                && work.State == "completed"
                && receipt.Decision != "image_intake_registered"
                // R189, R377: a pipeline association files within two hours
                // of being made; a member of staff's has no window.
                && (association.ActorKind != SystemWorkerKind
                    || association.LinkedAtUtc >= DateTimeOffset.UtcNow.AddSeconds(windowStartSeconds))
                // CaseFilingTargetRule: not the Case's own origin receipt; a
                // workflow Case that is not archived, in a state the
                // association files into and not being edited; or a Triage
                // Case a member of staff linked.
                && !context.Cases.Any(caseRow => caseRow.Id == association.CaseId
                    && caseRow.OriginIntakeReceiptId == receipt.Id)
                && ((workflow != null
                        && workflow.ArchivedAtUtc == null
                        && (association.ActorKind != SystemWorkerKind
                            || (automaticStates.Contains(workflow.State)
                                && workflow.ReportSentEvidenceId == null))
                        && (workflow.EditLeaseExpiresAtUtc == null
                            || workflow.EditLeaseExpiresAtUtc <= nowUtc))
                    || (workflow == null
                        && association.ActorKind != SystemWorkerKind
                        && context.Triage.Any(triage => triage.CaseId == association.CaseId)))
                && context.IntakeAssets.Any(asset => asset.IntakeReceiptId == receipt.Id
                    && asset.Kind == "source"
                    && asset.Disposition == "source"
                    && asset.ContentLength > 0
                    && asset.StorageKey != "")
                && !context.IntakeSubmissionGroups.Any(submissionGroup => submissionGroup.DiscardedAtUtc != null
                    && context.IntakeSubmissionGroupMembers.Any(member => member.GroupId == submissionGroup.Id
                        && member.StagedReceiptId == work.StagedReceiptId))
                && !context.IntakeMutationHistory.Any(history => history.IntakeReceiptId == receipt.Id
                    && history.CaseId == association.CaseId
                    && history.EventType == PromotionCompletedEventType)
                // R190: an exhausted replay ends this association's attempts;
                // a later link starts again.
                && !context.IntakeMutationHistory.Any(history => history.IntakeReceiptId == receipt.Id
                    && history.CaseId == association.CaseId
                    && history.EventType == ReplayExhaustedEventType
                    && history.OccurredAtUtc > association.LinkedAtUtc)
            select new { association, receipt.Version };
        if (after is { } cursor)
        {
            listed = listed.Where(row => row.association.LinkedAtUtc > cursor.LinkedAtUtc
                || (row.association.LinkedAtUtc == cursor.LinkedAtUtc
                    && row.association.IntakeReceiptId.CompareTo(cursor.ReceiptId) > 0));
        }

        var page = await listed
            .OrderBy(row => row.association.LinkedAtUtc)
            .ThenBy(row => row.association.IntakeReceiptId)
            .Take(maximumItems)
            .Select(row => new
            {
                row.association.IntakeReceiptId,
                row.association.CaseId,
                row.association.LinkedAtUtc,
                AssociationVersion = row.association.Version,
                IntakeVersion = row.Version,
                FailedAttempts = context.IntakeMutationHistory.Count(history =>
                    history.IntakeReceiptId == row.association.IntakeReceiptId
                    && history.CaseId == row.association.CaseId
                    && history.EventType == ReplayFailedEventType
                    && history.OccurredAtUtc > row.association.LinkedAtUtc),
                LastFailedAtUtc = context.IntakeMutationHistory
                    .Where(history => history.IntakeReceiptId == row.association.IntakeReceiptId
                        && history.CaseId == row.association.CaseId
                        && history.EventType == ReplayFailedEventType
                        && history.OccurredAtUtc > row.association.LinkedAtUtc)
                    .Max(history => (DateTimeOffset?)history.OccurredAtUtc),
                PlanJson = context.IntakeMutationHistory
                    .Where(history => history.IntakeReceiptId == row.association.IntakeReceiptId
                        && history.CaseId == row.association.CaseId
                        && history.EventType == PromotionStartedEventType)
                    .Select(history => history.AfterJson)
                    .FirstOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        // Each pair's planned files, and one grouped read of which of them the
        // Case already records and which still wait for custody.
        var planned = page.ToDictionary(
            row => row.IntakeReceiptId,
            row => PlannedKeys(row.CaseId, row.IntakeReceiptId, row.PlanJson));
        var keys = planned.Values.SelectMany(value => value).ToArray();
        var caseIds = page.Where(row => planned[row.IntakeReceiptId].Length > 0)
            .Select(row => row.CaseId)
            .Distinct()
            .ToArray();
        var recorded = keys.Length == 0
            ? new Dictionary<string, bool>(StringComparer.Ordinal)
            : (await (
                    from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                    join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                        on occurrence.VersionId equals version.Id
                    where caseIds.Contains(occurrence.CaseId)
                        && keys.Contains(occurrence.OperationKey)
                        && !version.IsLogicallyRemoved
                    select new { occurrence.OperationKey, version.CustodyStatus })
                    .ToArrayAsync(cancellationToken))
                .GroupBy(item => item.OperationKey, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Any(item => item.CustodyStatus == DocumentCustodyStatus.Pending),
                    StringComparer.Ordinal);

        return page.Select(row =>
        {
            var rowKeys = planned[row.IntakeReceiptId];
            return new CaseFilingReplayCandidate(
                row.IntakeReceiptId,
                row.CaseId,
                row.LinkedAtUtc,
                row.AssociationVersion,
                row.IntakeVersion,
                row.FailedAttempts,
                row.LastFailedAtUtc,
                rowKeys.Length,
                rowKeys.Count(recorded.ContainsKey),
                rowKeys.Count(key => recorded.TryGetValue(key, out var pending) && pending));
        }).ToArray();
    }

    public async Task RecordAsync(
        CaseFilingReplayRecord entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.FailureCode);
        var operationKey = entry.OperationKey;
        var intakeVersion = entry.Candidate.IntakeVersion;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = entry.Candidate.ReceiptId,
            CaseId = entry.Candidate.CaseId,
            EventType = entry.Kind == CaseFilingReplayEvent.Exhausted
                ? ReplayExhaustedEventType
                : ReplayFailedEventType,
            ActorKind = SystemWorkerKind,
            ActorSubjectId = "intake-processing",
            ActorRolesJson = "[]",
            Reason = entry.FailureCode,
            OperationKey = operationKey,
            RequestFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationKey))),
            OccurredAtUtc = entry.OccurredAtUtc,
            ExpectedIntakeVersion = intakeVersion,
            BeforeIntakeVersion = intakeVersion,
            AfterIntakeVersion = intakeVersion
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            // Already recorded under this operation key.
        }
    }

    private static string[] PlannedKeys(Guid caseId, Guid receiptId, string? planJson)
    {
        if (string.IsNullOrWhiteSpace(planJson))
        {
            return [];
        }

        var plan = JsonSerializer.Deserialize<AutomaticCaseEvidencePromotionPlan>(planJson);
        return (plan?.AssetIds ?? [])
            .Distinct()
            .Select(assetId => AutomaticCaseEvidencePromotionOperationKey.For(caseId, receiptId, assetId))
            .ToArray();
    }
}
