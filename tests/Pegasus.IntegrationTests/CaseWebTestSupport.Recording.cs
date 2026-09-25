using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

internal static partial class CaseWebTestSupport
{
    internal sealed partial class RecordingCaseDetailsStore :
        ICaseAssetPreparationQueries
    {
        /// <summary>The case's image preparations, when a test supplies them.</summary>
        public IReadOnlyList<CaseAssetPreparation> Preparations { get; set; } = [];

        Task<IReadOnlyList<CaseAssetPreparation>> ICaseAssetPreparationQueries.ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Current());

        Task<CaseAssetPreparation?> ICaseAssetPreparationQueries.GetForOccurrenceAsync(
            Guid caseId,
            Guid occurrenceId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Preparations.SingleOrDefault(item =>
                item.CaseId == caseId && item.OccurrenceId == occurrenceId));

        private IReadOnlyList<CaseAssetPreparation> Current() =>
        [
            .. Preparations
                .OrderBy(item => item.Role)
                .ThenBy(item => item.Order ?? int.MaxValue)
        ];
    }

    internal sealed partial class RecordingCaseDetailsStore :
        ICloseCase,
        IReopenCase,
        IArchiveCase
    {
        public List<CloseCaseRequest> Closures { get; } = [];
        public List<ReopenCaseRequest> Reopenings { get; } = [];
        public List<ArchiveCaseRequest> Archives { get; } = [];

        Task<CaseWorkflowRecord> ICloseCase.ExecuteAsync(
            CloseCaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            Closures.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow() with
            {
                State = CaseLifecycleState.ProviderCancelled,
                ClosureOutcome = request.Outcome
            });
        }

        Task<CaseWorkflowRecord> IReopenCase.ExecuteAsync(
            ReopenCaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            Reopenings.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow() with
            {
                State = request.Destination == CaseReopenDestination.Review
                    ? CaseLifecycleState.Review
                    : CaseLifecycleState.NotReady
            });
        }

        Task<CaseWorkflowRecord> IArchiveCase.ExecuteAsync(
            ArchiveCaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            Archives.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow() with
            {
                Archive = new(_now, request.Actor, request.Reason)
            });
        }
    }

    internal sealed partial class RecordingCaseDetailsStore :
        IRetryCaseCustody,
        IAddCaseDocument,
        ILogicallyRemoveDocument,
        ITagCaseImage,
        IUntagCaseImage,
        ICreateImageTag,
        IMarkAsOriginalReportStore
    {
        /// <summary>The case's documents, when a test supplies them.</summary>
        public IReadOnlyList<CaseDocument> CaseDocuments { get; set; } = [];

        public List<RetryCaseCustodyRequest> CustodyRetries { get; } = [];
        public List<AddCaseDocumentCommand> DocumentUploads { get; } = [];
        public List<LogicallyRemoveDocumentCommand> DocumentRemovals { get; } = [];
        public List<TagCaseImageCommand> ImageTagsApplied { get; } = [];
        public List<UntagCaseImageCommand> ImageTagsRemoved { get; } = [];
        public List<CreateImageTagCommand> ImageTagsCreated { get; } = [];
        public List<MarkAsOriginalReportCommand> OriginalReportMarks { get; } = [];

        Task<RetryCaseCustodyResult> IRetryCaseCustody.ExecuteAsync(
            RetryCaseCustodyRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            CustodyRetries.Add(request);
            ConsumeLease();
            return Task.FromResult(new RetryCaseCustodyResult(
                RetryCaseCustodyOutcome.Pending,
                CaseVersion + 1,
                "Custody retry was queued."));
        }

        Task<AddCaseDocumentResult> IAddCaseDocument.ExecuteAsync(
            AddCaseDocumentCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            DocumentUploads.Add(command with { Content = command.Content.ToArray() });
            ConsumeLease();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            return Task.FromResult(new AddCaseDocumentResult(
                new(
                    Guid.NewGuid(),
                    CaseId,
                    documentId,
                    versionId,
                    command.SemanticRole,
                    command.Source,
                    command.SourceOccurrenceIdentity,
                    _now,
                    []),
                new(
                    versionId,
                    documentId,
                    1,
                    command.FileName,
                    command.MediaType,
                    command.Content.Length,
                    new string('c', 64),
                    DocumentCustodyStatus.Pending,
                    _now,
                    command.Actor.SubjectId,
                    true,
                    false,
                    null),
                IsReplay: false));
        }

        Task ILogicallyRemoveDocument.ExecuteAsync(
            LogicallyRemoveDocumentCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            DocumentRemovals.Add(command);
            ConsumeLease();
            return Task.CompletedTask;
        }

        Task ITagCaseImage.ExecuteAsync(
            TagCaseImageCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            ImageTagsApplied.Add(command);
            ConsumeLease();
            return Task.CompletedTask;
        }

        Task IUntagCaseImage.ExecuteAsync(
            UntagCaseImageCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            ImageTagsRemoved.Add(command);
            ConsumeLease();
            return Task.CompletedTask;
        }

        Task<CreateImageTagResult> ICreateImageTag.ExecuteAsync(
            CreateImageTagCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            ImageTagsCreated.Add(command);
            return Task.FromResult(new CreateImageTagResult(
                new ImageTag(Guid.NewGuid(), command.Name, command.Colour, IsBuiltIn: false, Version: 1),
                IsReplay: false));
        }

        Task<OriginalReportRecorded> IMarkAsOriginalReportStore.MarkAsOriginalReportAsync(
            MarkAsOriginalReportCommand command,
            OriginalReportReading? reading,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            OriginalReportMarks.Add(command);
            ConsumeLease();
            var document = CaseDocuments.Single(value =>
                value.Occurrences.Any(occurrence => occurrence.Id == command.DocumentOccurrenceId));
            var occurrence = document.Occurrences.Single(value => value.Id == command.DocumentOccurrenceId);
            var version = document.Versions.Single(value => value.Id == occurrence.VersionId);
            CaseDocuments = CaseDocuments
                .Select(value => value.Id == document.Id
                    ? value with
                    {
                        Occurrences = value.Occurrences
                            .Select(item => item.Id == occurrence.Id
                                ? item with { SemanticRole = DocumentSemanticRole.AuditReport }
                                : item)
                            .ToArray()
                    }
                    : value)
                .ToArray();
            return Task.FromResult(new OriginalReportRecorded(
                command.CaseId,
                occurrence.Id,
                version.FileName,
                command.ExpectedVersion + 1));
        }
    }

    internal sealed partial class RecordingCaseDetailsStore :
        IRenewCaseEditLease,
        IHeartbeatCaseEditLease,
        IReleaseCaseEditLease,
        IGetAssessmentAccess,
        IGetAssessmentWorkspace,
        ICaseReportSnapshotSource,
        ICaseWorkspaceStore
    {
        public int MetadataReads { get; private set; }

        Task<SaveCaseWorkspaceResult> ICaseWorkspaceStore.SaveAsync(
            SaveCaseWorkspaceRequest request, CancellationToken cancellationToken) =>
            ((ISaveCaseWorkspace)this).ExecuteAsync(request, cancellationToken);

        private CaseAssessmentProjection EngineeringAssessment() => new(
            CaseId, "QDOS3100042", CaseVersion, State, null,
            [new(AssessmentVocabulary.ReportDate, "2031-05-06", ActorKind.Staff,
                "recorded-engineer", _now)],
            [], new("AB12CDE", null, null, null, null, null, "tbc", null, DateOnly.FromDateTime(_now.UtcDateTime), null, null,
                null, "Case claimant", "CLM-42"));

        Task<AssessmentAccessState?> IGetAssessmentAccess.ExecuteAsync(
            GetAssessmentAccessQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<AssessmentAccessState?>(new(State));

        Task<AssessmentWorkspace?> IGetAssessmentWorkspace.ExecuteAsync(
            GetAssessmentWorkspaceQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<AssessmentWorkspace?>(AssessmentWorkspaceTestData.Create(EngineeringAssessment()) with
            {
                Data = DataOverride ?? CreateData(),
                LatestVehicleObservation = VehicleLookupEvidence?.LatestObservation
            });

        Task<CaseReportFreezeInputs?> ICaseReportSnapshotSource.GetAsync(
            Guid caseId, ActionActor actor, CaseWorkSelector work, CancellationToken cancellationToken)
        {
            MetadataReads++;
            var assessment = EngineeringAssessment();
            return Task.FromResult<CaseReportFreezeInputs?>(new CaseReportFreezeInputs(
                new(assessment, assessment.Reference, [], null, [], []),
                new(assessment, null, null, [], null, null, [], new Dictionary<Guid, Pegasus.Core.Documents.DocumentVersion>()),
                assessment.Reference, CaseVersion));
        }

        public string RenewedLeaseToken { get; } = "opaque-renewed-case-lease";
        public List<RenewCaseEditLeaseRequest> LeaseRenewals { get; } = [];
        public List<HeartbeatCaseEditLeaseRequest> LeaseHeartbeats { get; } = [];
        public List<ReleaseCaseEditLeaseRequest> LeaseReleases { get; } = [];

        Task<CaseEditLease> IHeartbeatCaseEditLease.ExecuteAsync(
            HeartbeatCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseHeartbeats.Add(request);
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                request.LeaseToken,
                request.Actor.SubjectId,
                CaseVersion,
                _now.AddMinutes(5)));
        }

        Task<CaseEditLease> IRenewCaseEditLease.ExecuteAsync(
            RenewCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseRenewals.Add(request);
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                RenewedLeaseToken,
                request.Actor.SubjectId,
                request.ExpectedVersion,
                _now.AddMinutes(10)));
        }

        Task IReleaseCaseEditLease.ExecuteAsync(
            ReleaseCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseReleases.Add(request);
            _leaseHolder = null;
            _leaseOperationKey = null;
            return Task.CompletedTask;
        }
    }

    internal sealed partial class RecordingCaseDetailsStore :
        ICreateCaseTask,
        IAssignCaseTask,
        ICompleteCaseTask,
        ICancelCaseTask,
        ILinkReportEvidence,
        IUnlinkReportEvidence
    {
        public List<CreateCaseTaskRequest> TaskCreations { get; } = [];
        public List<AssignCaseTaskRequest> TaskAssignments { get; } = [];
        public List<CompleteCaseTaskRequest> TaskCompletions { get; } = [];
        public List<CancelCaseTaskRequest> TaskCancellations { get; } = [];
        public List<LinkReportEvidenceRequest> EvidenceLinks { get; } = [];
        public List<UnlinkReportEvidenceRequest> EvidenceUnlinks { get; } = [];

        Task<CaseTaskRecord> ICreateCaseTask.ExecuteAsync(
            CreateCaseTaskRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            TaskCreations.Add(request);
            ConsumeLease();
            return Task.FromResult(TaskRecord(request.TaskId, request.Description, request.AssigneeId, CaseTaskState.Open, 1));
        }

        Task<CaseTaskRecord> IAssignCaseTask.ExecuteAsync(
            AssignCaseTaskRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            TaskAssignments.Add(request);
            ConsumeLease();
            return Task.FromResult(TaskRecord(request.TaskId, "task", request.AssigneeId, CaseTaskState.Open, request.ExpectedTaskVersion + 1));
        }

        Task<CaseTaskRecord> ICompleteCaseTask.ExecuteAsync(
            CompleteCaseTaskRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            TaskCompletions.Add(request);
            ConsumeLease();
            return Task.FromResult(TaskRecord(request.TaskId, "task", null, CaseTaskState.Completed, request.ExpectedTaskVersion + 1));
        }

        Task<CaseTaskRecord> ICancelCaseTask.ExecuteAsync(
            CancelCaseTaskRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            TaskCancellations.Add(request);
            ConsumeLease();
            return Task.FromResult(TaskRecord(request.TaskId, "task", null, CaseTaskState.Cancelled, request.ExpectedTaskVersion + 1));
        }

        Task<CaseWorkflowRecord> ILinkReportEvidence.ExecuteAsync(
            LinkReportEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            EvidenceLinks.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow());
        }

        Task<CaseWorkflowRecord> IUnlinkReportEvidence.ExecuteAsync(
            UnlinkReportEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            EvidenceUnlinks.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow());
        }

        private CaseTaskRecord TaskRecord(Guid taskId, string description, Guid? assigneeId, CaseTaskState state, long version) =>
            new(taskId, CaseId, description, assigneeId, state, version, CaseVersion + 1);
    }

    internal sealed partial class RecordingCaseDetailsStore : IAssignCaseToMe
    {
        /// <summary>The Case type the summary reports; a plain Inspection unless a test says otherwise.</summary>
        public CaseType SummaryCaseType { get; init; } = CaseType.Inspection;

        /// <summary>The retained standalone Audit evidence, when intake supplied one.</summary>
        public Guid? StandaloneAuditEvidenceId { get; init; }

        /// <summary>The Principal and Claim source records' notes the Case reads live.</summary>
        public CaseRecordNotes RecordNotes { get; init; } = CaseRecordNotes.None;

        /// <summary>The hold's review date the workflow reports.</summary>
        public DateOnly? HoldReviewOn { get; set; }

        public List<AssignCaseToMeRequest> SelfAssignments { get; } = [];

        Task<CaseWorkflowRecord> IAssignCaseToMe.ExecuteAsync(
            AssignCaseToMeRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            SelfAssignments.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow() with { AssignedEngineerId = Guid.NewGuid() });
        }
    }

    internal sealed partial class RecordingCaseDetailsStore : IRequestVehicleLookup
    {
        public List<RequestVehicleLookupCommand> LookupRequests { get; } = [];

        /// <summary>The case's recorded vehicle lookups, when a test supplies them.</summary>
        public CaseVehicleEvidence? VehicleLookupEvidence { get; init; }

        /// <summary>
        /// Drops the vehicle values from the projection, so the section renders
        /// the state a case with no registration is actually in.
        /// </summary>
        public bool OmitVehicleValues { get; init; }

        Task<RequestedVehicleLookup> IRequestVehicleLookup.ExecuteAsync(
            RequestVehicleLookupCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LookupRequests.Add(command);
            return Task.FromResult(new RequestedVehicleLookup(
                Guid.NewGuid(),
                CaseId,
                command.Registration,
                VehicleLookupWorkState.Pending,
                CaseVersion + 1,
                IsReplay: false));
        }
    }

    internal sealed partial class RecordingCaseDetailsStore :
        IAssignCaseEngineer,
        ISetCaseSignOffEngineer,
        ICreateLinkedReplacement
    {
        public List<AssignCaseEngineerRequest> EngineerAssignments { get; } = [];
        public List<SetCaseSignOffEngineerRequest> SignOffSelections { get; } = [];
        public List<CreateLinkedReplacementRequest> LinkedReplacements { get; } = [];

        Task<CaseWorkflowRecord> IAssignCaseEngineer.ExecuteAsync(
            AssignCaseEngineerRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            EngineerAssignments.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow() with
            {
                AssignedEngineerId = request.EngineerId,
                State = CaseLifecycleState.ReportPreparation
            });
        }

        Task<CaseWorkflowRecord> ISetCaseSignOffEngineer.ExecuteAsync(
            SetCaseSignOffEngineerRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            SignOffSelections.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow() with
            {
                SignOffEngineerId = request.SignOffEngineerId
            });
        }

        Task<CaseAcceptanceOutcome> ICreateLinkedReplacement.ExecuteAsync(
            CreateLinkedReplacementRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LinkedReplacements.Add(request);
            ConsumeLease();
            var replacementId = Guid.NewGuid();
            return Task.FromResult(new CaseAcceptanceOutcome(
                new(replacementId, request.ReplacementPrincipalCode, 2031, 1, $"{request.ReplacementPrincipalCode}3100001"),
                CaseInitialState.NotReady,
                CaseCustodyState.Pending,
                Guid.NewGuid(),
                IsDuplicate: false));
        }
    }
}
