using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.AiWork;

/// <summary>
/// Policy for the AI job ledger. Staff create jobs; the Automation Actor
/// creates only the scheduled Unidentified-queue pass (EPIC-011 D5) and is
/// the only actor that takes, progresses, completes, fails or releases;
/// staff cancel and confirm. A Taken job whose lease has lapsed reads as
/// Queued regardless of the job's own expiry (ADR-0035: taken jobs expire
/// back to Queued), and a Queued job past its own expiry reads as Expired,
/// so no timer is needed for either rule.
/// </summary>
public static class AiJobPolicy
{
    public const int MaximumInstructionLength = 500;
    public const int MaximumReasonLength = 500;
    public const int MaximumProgressNoteLength = 500;
    public const int MaximumResultReferenceLength = 200;
    public const int MaximumResultTextLength = 4000;
    public const int MaximumSubjectReferenceLength = 40;
    public const string QueueSubjectReference = "unidentified-queue";
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    public static AiJobSubjectKind SubjectKindFor(AiJobKind kind) => kind switch
    {
        AiJobKind.Estimate or AiJobKind.QueryResponse => AiJobSubjectKind.Case,
        AiJobKind.UnidentifiedResolution => AiJobSubjectKind.Unidentified,
        AiJobKind.UnidentifiedQueuePass => AiJobSubjectKind.Queue,
        _ => throw new ArgumentException("The AI job kind is not recognized.", nameof(kind))
    };

    public static AiJobResultKind ResultKindFor(AiJobKind kind) => kind switch
    {
        AiJobKind.Estimate => AiJobResultKind.Estimate,
        AiJobKind.UnidentifiedResolution or AiJobKind.UnidentifiedQueuePass =>
            AiJobResultKind.ProposedResolution,
        AiJobKind.QueryResponse => AiJobResultKind.DraftReply,
        _ => throw new ArgumentException("The AI job kind is not recognized.", nameof(kind))
    };

    /// <summary>
    /// The state a reader sees: a lapsed lease returns the job to the queue
    /// and an untaken job past its expiry is expired, before any row is
    /// touched.
    /// </summary>
    public static AiJobState EffectiveState(
        AiJobState persisted,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset? leaseExpiresAtUtc,
        DateTimeOffset now) => persisted switch
    {
        AiJobState.Taken when leaseExpiresAtUtc is { } lease && lease <= now => AiJobState.Queued,
        AiJobState.Queued when expiresAtUtc <= now => AiJobState.Expired,
        _ => persisted
    };

    public static bool IsLegalTransition(AiJobState from, AiJobState to) =>
        (from, to) switch
        {
            (AiJobState.Queued, AiJobState.Taken) => true,
            (AiJobState.Queued, AiJobState.Cancelled) => true,
            (AiJobState.Queued, AiJobState.Expired) => true,
            (AiJobState.Taken, AiJobState.Taken) => true,
            (AiJobState.Taken, AiJobState.Queued) => true,
            (AiJobState.Taken, AiJobState.DraftReady) => true,
            (AiJobState.Taken, AiJobState.Failed) => true,
            (AiJobState.Taken, AiJobState.Cancelled) => true,
            (AiJobState.DraftReady, AiJobState.Completed) => true,
            (AiJobState.DraftReady, AiJobState.Cancelled) => true,
            _ => false
        };

    public static void ValidateNew(NewAiJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!Enum.IsDefined(job.Kind))
        {
            throw new ArgumentException("The AI job kind is not recognized.", nameof(job));
        }
        if (job.SubjectKind != SubjectKindFor(job.Kind))
        {
            throw new ArgumentException("The AI job subject does not match its kind.", nameof(job));
        }
        if (job.SubjectKind != AiJobSubjectKind.Queue && (job.SubjectId is null || job.SubjectId == Guid.Empty))
        {
            throw new ArgumentException("A subject identifier is required.", nameof(job));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(job.SubjectReference);
        if (job.SubjectReference.Length > MaximumSubjectReferenceLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(job),
                $"The subject reference cannot exceed {MaximumSubjectReferenceLength} characters.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(job.Instruction);
        if (job.Instruction.Trim().Length > MaximumInstructionLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(job),
                $"The instruction cannot exceed {MaximumInstructionLength} characters.");
        }
        if (job.Kind == AiJobKind.Estimate)
        {
            if (job.TargetPercentOfEngineerValue is not (>= 1 and <= 100))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(job),
                    "The target must be between 1 and 100 percent of the Engineer's Value.");
            }
            if (job.EngineerValueAtSend is not > 0)
            {
                throw new InvalidOperationException(
                    "An estimate job needs a confirmed Engineer's Value on the case.");
            }
        }
        if (job.Expiry <= TimeSpan.Zero || job.Expiry > TimeSpan.FromDays(7))
        {
            throw new ArgumentOutOfRangeException(
                nameof(job),
                "The job expiry must be between one second and seven days.");
        }
        RequireCreator(job.Actor, job.Kind);
        ValidateOperationKey(job.OperationKey);
    }

    public static void ValidateTransition(AiJobTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.JobId == Guid.Empty)
        {
            throw new ArgumentException("A job identifier is required.", nameof(transition));
        }
        if (transition.ExpectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transition),
                "The expected job version cannot be negative.");
        }
        StaffAuthorization.Require(transition.Actor, StaffAccessRight.PerformCasework);
        ValidateOperationKey(transition.OperationKey);
        if (!Enum.IsDefined(transition.TargetState))
        {
            throw new ArgumentException("The job transition target is invalid.", nameof(transition));
        }
        switch (transition.TargetState)
        {
            case AiJobState.Taken when transition.LeaseExpiresAtUtc is null:
                throw new ArgumentException("A claim carries a lease expiry.", nameof(transition));
            case AiJobState.Taken or AiJobState.Queued or AiJobState.DraftReady or AiJobState.Failed
                when transition.Actor.Kind != ActorKind.Automation:
                throw new InvalidOperationException(
                    "Only the Automation Actor takes, releases, progresses, completes or fails an AI job.");
            case AiJobState.Cancelled or AiJobState.Completed
                when transition.Actor.Kind != ActorKind.Staff:
                throw new InvalidOperationException(
                    "Cancelling or confirming an AI job is a staff action.");
            case AiJobState.Cancelled or AiJobState.Failed
                when string.IsNullOrWhiteSpace(transition.Reason):
                throw new ArgumentException(
                    $"Marking an AI job {transition.TargetState} requires a reason.",
                    nameof(transition));
            case AiJobState.DraftReady when transition.Result is null:
                throw new ArgumentException("A Draft ready job names its result.", nameof(transition));
        }
        if (transition.Reason is { Length: > MaximumReasonLength })
        {
            throw new ArgumentOutOfRangeException(
                nameof(transition),
                $"A job reason cannot exceed {MaximumReasonLength} characters.");
        }
        if (transition.ProgressNote is { Length: > MaximumProgressNoteLength })
        {
            throw new ArgumentOutOfRangeException(
                nameof(transition),
                $"A progress note cannot exceed {MaximumProgressNoteLength} characters.");
        }
        if (transition.Result is { } result)
        {
            ValidateResult(result);
        }
    }

    public static void ValidateResult(AiJobResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!Enum.IsDefined(result.Kind))
        {
            throw new ArgumentException("The job result kind is not recognized.", nameof(result));
        }
        if (string.IsNullOrWhiteSpace(result.Reference) && string.IsNullOrWhiteSpace(result.Text))
        {
            throw new ArgumentException(
                "A job result names a reference, carries text, or both.",
                nameof(result));
        }
        if (result.Reference is { Length: > MaximumResultReferenceLength })
        {
            throw new ArgumentOutOfRangeException(
                nameof(result),
                $"A result reference cannot exceed {MaximumResultReferenceLength} characters.");
        }
        if (result.Text is { Length: > MaximumResultTextLength })
        {
            throw new ArgumentOutOfRangeException(
                nameof(result),
                $"Result text cannot exceed {MaximumResultTextLength} characters.");
        }
    }

    public static bool IsEligibleEstimateCaseState(CaseLifecycleState state) =>
        state is CaseLifecycleState.ReportPreparation or CaseLifecycleState.PostReport;

    public static bool IsEligibleQueryResponseCaseState(CaseLifecycleState state) =>
        state is CaseLifecycleState.PostReport or CaseLifecycleState.PostReportComplete;

    private static void RequireCreator(ActionActor actor, AiJobKind kind)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (actor.Kind == ActorKind.Automation && kind != AiJobKind.UnidentifiedQueuePass)
        {
            throw new InvalidOperationException(
                "The Automation Actor creates only Unidentified-queue pass jobs.");
        }
    }

    private static void ValidateOperationKey(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (operationKey.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationKey),
                "The operation key cannot exceed 100 characters.");
        }
    }
}

/// <summary>
/// Resolves the subject the caller names, checks the kind's precondition
/// against the current record, and queues the job. The Administrator
/// Send to AI switch refuses new jobs immediately.
/// </summary>
public sealed class CreateAiJob(
    IAiJobStore store,
    ISendToAiControl control,
    ICaseWorkflowQueries workflow,
    ICaseAssessmentStore assessment,
    IUnidentifiedStore unidentified) : ICreateAiJob
{
    public async Task<AiJobRecord> ExecuteAsync(
        CreateAiJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(command.Kind))
        {
            throw new ArgumentException("The AI job kind is not recognized.", nameof(command));
        }
        if (!await control.IsEnabledAsync(cancellationToken))
        {
            throw new InvalidOperationException("AI work is disabled by an Administrator.");
        }

        var job = command.Kind switch
        {
            AiJobKind.Estimate => await ForEstimateAsync(command, cancellationToken),
            AiJobKind.QueryResponse => await ForQueryResponseAsync(command, cancellationToken),
            AiJobKind.UnidentifiedResolution => await ForUnidentifiedAsync(command, cancellationToken),
            _ => new NewAiJob(
                AiJobKind.UnidentifiedQueuePass,
                AiJobSubjectKind.Queue,
                null,
                AiJobPolicy.QueueSubjectReference,
                command.Instruction,
                null,
                null,
                command.Actor,
                command.OperationKey,
                AiJobPolicy.DefaultExpiry)
        };
        return await store.CreateAsync(job, cancellationToken);
    }

    private async Task<NewAiJob> ForEstimateAsync(
        CreateAiJobCommand command,
        CancellationToken cancellationToken)
    {
        var record = await RequireCaseAsync(command, cancellationToken);
        if (!AiJobPolicy.IsEligibleEstimateCaseState(record.State))
        {
            throw new InvalidOperationException(
                "An estimate job needs a case that is With Engineer.");
        }

        var projection = await assessment.GetAsync(record.CaseId, cancellationToken);
        var engineerValue = projection?.Field(AssessmentVocabulary.ValueEngineer);
        decimal? valueAtSend = engineerValue is { IsConfirmed: true }
            && decimal.TryParse(
                engineerValue.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : null;
        return new(
            AiJobKind.Estimate,
            AiJobSubjectKind.Case,
            record.CaseId,
            record.Identity.Reference,
            command.Instruction,
            command.TargetPercentOfEngineerValue,
            valueAtSend,
            command.Actor,
            command.OperationKey,
            AiJobPolicy.DefaultExpiry);
    }

    private async Task<NewAiJob> ForQueryResponseAsync(
        CreateAiJobCommand command,
        CancellationToken cancellationToken)
    {
        var record = await RequireCaseAsync(command, cancellationToken);
        if (!AiJobPolicy.IsEligibleQueryResponseCaseState(record.State))
        {
            throw new InvalidOperationException(
                "A query response job needs a case in post-report work.");
        }

        return new(
            AiJobKind.QueryResponse,
            AiJobSubjectKind.Case,
            record.CaseId,
            record.Identity.Reference,
            command.Instruction,
            null,
            null,
            command.Actor,
            command.OperationKey,
            AiJobPolicy.DefaultExpiry);
    }

    private async Task<NewAiJob> ForUnidentifiedAsync(
        CreateAiJobCommand command,
        CancellationToken cancellationToken)
    {
        var item = command.SubjectId is { } id && id != Guid.Empty
            ? await unidentified.GetAsync(id, cancellationToken)
            : !string.IsNullOrWhiteSpace(command.SubjectReference)
                ? await unidentified.GetByReferenceAsync(command.SubjectReference.Trim(), cancellationToken)
                : null;
        if (item is null)
        {
            throw new KeyNotFoundException("The Unidentified item was not found.");
        }
        if (item.State != UnidentifiedState.Open)
        {
            throw new InvalidOperationException(
                "An Unidentified resolution job needs an open Unidentified item.");
        }

        return new(
            AiJobKind.UnidentifiedResolution,
            AiJobSubjectKind.Unidentified,
            item.Id,
            item.Reference,
            command.Instruction,
            null,
            null,
            command.Actor,
            command.OperationKey,
            AiJobPolicy.DefaultExpiry);
    }

    private async Task<CaseWorkflowRecord> RequireCaseAsync(
        CreateAiJobCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SubjectId is not { } caseId || caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(command));
        }

        return await workflow.GetAsync(caseId, cancellationToken)
            ?? throw new KeyNotFoundException("The case was not found.");
    }
}

/// <summary>
/// The Automation client's lifecycle over one job. Claims and progress are
/// refused while the Administrator switch is off; finishing a job already
/// held (complete, fail, release) stays permitted so held work is never
/// stranded. Every call is optimistic on the version the client last read.
/// </summary>
public sealed class WorkAiJob(
    IAiJobStore store,
    ISendToAiControl control,
    TimeProvider timeProvider) : IWorkAiJob
{
    public async Task<AiJobRecord> TakeAsync(
        TakeAiJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await RequireEnabledAsync(cancellationToken);
        return await store.TransitionAsync(
            new(
                command.JobId,
                command.ExpectedVersion,
                AiJobState.Taken,
                command.Actor,
                command.OperationKey,
                LeaseExpiresAtUtc: timeProvider.GetUtcNow() + AiJobPolicy.LeaseDuration),
            cancellationToken);
    }

    public Task<AiJobRecord> ReleaseAsync(
        ReleaseAiJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return store.TransitionAsync(
            new(
                command.JobId,
                command.ExpectedVersion,
                AiJobState.Queued,
                command.Actor,
                command.OperationKey,
                command.Reason),
            cancellationToken);
    }

    public async Task<AiJobRecord> ReportProgressAsync(
        ReportAiJobProgressCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await RequireEnabledAsync(cancellationToken);
        return await store.TransitionAsync(
            new(
                command.JobId,
                command.ExpectedVersion,
                AiJobState.Taken,
                command.Actor,
                command.OperationKey,
                ProgressNote: command.ProgressNote,
                LeaseExpiresAtUtc: timeProvider.GetUtcNow() + AiJobPolicy.LeaseDuration),
            cancellationToken);
    }

    public Task<AiJobRecord> CompleteAsync(
        CompleteAiJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return store.TransitionAsync(
            new(
                command.JobId,
                command.ExpectedVersion,
                AiJobState.DraftReady,
                command.Actor,
                command.OperationKey,
                Result: command.Result),
            cancellationToken);
    }

    public Task<AiJobRecord> FailAsync(
        FailAiJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return store.TransitionAsync(
            new(
                command.JobId,
                command.ExpectedVersion,
                AiJobState.Failed,
                command.Actor,
                command.OperationKey,
                command.Reason),
            cancellationToken);
    }

    private async Task RequireEnabledAsync(CancellationToken cancellationToken)
    {
        if (!await control.IsEnabledAsync(cancellationToken))
        {
            throw new InvalidOperationException("AI work is disabled by an Administrator.");
        }
    }
}

public sealed class CancelAiJob(IAiJobStore store) : ICancelAiJob
{
    public Task<AiJobRecord> ExecuteAsync(
        CancelAiJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return store.TransitionAsync(
            new(
                command.JobId,
                command.ExpectedVersion,
                AiJobState.Cancelled,
                command.Actor,
                command.OperationKey,
                command.Reason),
            cancellationToken);
    }
}

public sealed class ConfirmAiJob(IAiJobStore store) : IConfirmAiJob
{
    public Task<AiJobRecord> ExecuteAsync(
        ConfirmAiJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return store.TransitionAsync(
            new(
                command.JobId,
                command.ExpectedVersion,
                AiJobState.Completed,
                command.Actor,
                command.OperationKey),
            cancellationToken);
    }
}
