using System.Text.RegularExpressions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

internal static partial class CaseWebTestSupport
{
    /// <summary>
    /// The facts the v29 Case views read (Stage 2 slice 7): the Case's works as
    /// the frame reports them, the workflow's assigned Engineer and Sent
    /// evidence, the Audit's <c>a.</c> folder state, and the work each page
    /// frame read addressed.
    /// </summary>
    internal sealed partial class RecordingCaseDetailsStore
    {
        /// <summary>The Case's works; null reads as the primary work alone.</summary>
        public CaseWorkSet? Works { get; set; }

        public Guid? AssignedEngineerId { get; set; }

        public ApprovedMailboxReportSentEvidence? ReportSentEvidence { get; set; }

        /// <summary>The Audit's <c>a.</c> folder, present once the Case has its Audit.</summary>
        public CaseCustodyState? AuditCustodyState { get; set; }

        public string? AuditCustodyFolderRemoteId { get; set; }

        /// <summary>The Triage Cases linked to the Case, as the page frame reports them.</summary>
        public IReadOnlyList<CaseLinkedTriage> LinkedTriage { get; set; } = [];

        public List<GetCaseSectionQuery> PageFrameQueries { get; } = [];

        /// <summary>
        /// This Case once Create audit has run: the primary work keeps the
        /// Inspection's Sent evidence (none when the Audit was created before
        /// the Inspection report was sent), the Audit work drives the Case,
        /// and the workflow's own evidence is cleared.
        /// </summary>
        public void GiveAudit(DateTimeOffset inspectionSentAtUtc, bool inspectionSent = true)
        {
            Works = new(
                new CaseWork(CaseId, CaseId, CaseWorkKind.Primary, inspectionSentAtUtc.AddDays(-3),
                    ReportSentEvidence: inspectionSent ? SentEvidence(inspectionSentAtUtc) : null),
                new CaseWork(Guid.NewGuid(), CaseId, CaseWorkKind.Audit, inspectionSentAtUtc.AddDays(1)));
            ReportSentEvidence = null;
        }
    }

    /// <summary>The state chip the ribbon renders, exactly.</summary>
    internal static string RibbonStateChip(string html)
    {
        var chip = Regex.Match(
            html,
            "data-case-ribbon-chips>\\s*(?<chip><span class=\"status status--[a-z]+\">[^<]+</span>)",
            RegexOptions.CultureInvariant);
        Assert.True(chip.Success, "The ribbon's state chip is not rendered.");
        return chip.Groups["chip"].Value;
    }

    /// <summary>Retained Sent evidence of a report, sent at <paramref name="sentAtUtc"/>.</summary>
    internal static ApprovedMailboxReportSentEvidence SentEvidence(DateTimeOffset sentAtUtc) => new(
        Guid.NewGuid(),
        "reports@collision.example",
        "sent-items",
        "immutable-item",
        "<message@collision.example>",
        "conversation",
        "reply-chain",
        "occurrence",
        new string('a', 64),
        new string('b', 64),
        sentAtUtc,
        sentAtUtc.AddMinutes(5),
        ActionActor.SystemWorker("sent-evidence-poller"),
        sentAtUtc.AddMinutes(6),
        ActionActor.SystemWorker("sent-evidence-poller"));

    /// <summary>
    /// The Case's queries (operator, 11 October 2026): the workflow's
    /// open-query mark, the oldest open query the page frame routes to, the
    /// Queries section's rows as the store answers them (in no set order), and
    /// the archive that makes the Case read-only. Log query and Resolve query
    /// are recorded, consume the lease, and answer with the mark they leave.
    /// </summary>
    internal sealed partial class RecordingCaseDetailsStore :
        IGetCaseQueriesSection,
        IRaiseCaseQuery,
        IResolveCaseQueries
    {
        public bool HasOpenQuery { get; set; }

        /// <summary>Read by the page frame only while <see cref="HasOpenQuery"/> is set, as the real reader does.</summary>
        public CaseOpenQueryRoute? OldestOpenQuery { get; set; }

        public IReadOnlyList<CaseQueryItem> Queries { get; set; } = [];

        public CaseArchive? Archive { get; set; }

        public List<RaiseCaseQueryRequest> QueryRaises { get; } = [];

        public List<ResolveCaseQueriesRequest> QueryResolutions { get; } = [];

        Task<CaseQueriesSection?> IGetCaseQueriesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseQueriesSection?>(query.CaseId == CaseId
                ? new(query.Frame ?? FocusedFrame(), Queries)
                : null);

        Task<CaseWorkflowRecord> IRaiseCaseQuery.ExecuteAsync(
            RaiseCaseQueryRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            QueryRaises.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow() with { HasOpenQuery = true });
        }

        Task<CaseWorkflowRecord> IResolveCaseQueries.ExecuteAsync(
            ResolveCaseQueriesRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            QueryResolutions.Add(request);
            ConsumeLease();
            return Task.FromResult(CreateWorkflow() with { HasOpenQuery = false });
        }
    }

    /// <summary>A staff mail sender the Case page reads as available; the page never sends through it.</summary>
    internal sealed class OfferedStaffMailSend : IStaffMailSend
    {
        public Task<StaffMailOperation> SendAsync(StaffMailSendCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation?> GetAsync(ActionActor actor, Guid operationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation> CancelAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
