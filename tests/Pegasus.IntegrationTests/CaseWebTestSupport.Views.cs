using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
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

        public List<GetCaseSectionQuery> PageFrameQueries { get; } = [];

        /// <summary>
        /// This Case once Create audit has run: the primary work keeps the
        /// Inspection's Sent evidence, the Audit work drives the Case, and the
        /// workflow's own evidence is cleared.
        /// </summary>
        public void GiveAudit(DateTimeOffset inspectionSentAtUtc)
        {
            Works = new(
                new CaseWork(CaseId, CaseId, CaseWorkKind.Primary, inspectionSentAtUtc.AddDays(-3),
                    ReportSentEvidence: SentEvidence(inspectionSentAtUtc)),
                new CaseWork(Guid.NewGuid(), CaseId, CaseWorkKind.Audit, inspectionSentAtUtc.AddDays(1)));
            ReportSentEvidence = null;
        }
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
}
