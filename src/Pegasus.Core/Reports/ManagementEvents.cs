using System.Text.Json;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Reports;

/// <summary>
/// What a management event carries: the documents the send carried, by
/// <see cref="CaseReportArtifactKind"/> name, how many of them are reports
/// (one, or two when both works' reports went in one email) and how many
/// distinct addresses it went to. The sender is the event's actor.
/// </summary>
public sealed record ManagementEventPayload(
    IReadOnlyList<string> Documents, int Reports, int Recipients);

/// <summary>
/// The management event after each report or fee-note send (FRD-21,
/// <c>MAIL-17</c>'s management-event clause; operator rulings, 15 October 2026
/// and R205, 22 October 2026): one history row on the Case, written when the
/// send's Sent item is observed and credited to the sender, read in Action
/// logs. A send that ends Failed, Cancelled or Unknown is never observed Sent
/// and records none. A fee note ends its report or goes beside it, so the
/// report send is the fee-note send.
/// </summary>
public static class ManagementEventPolicy
{
    public const string AggregateType = "case";
    public const string EventKind = "management_event";
    public const string PolicyVersion = "management_event/v1";

    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    /// <summary>The event's correlation id: one per send.</summary>
    public static string CorrelationId(Guid operationId) => $"management-event:{operationId:N}";

    /// <summary>Only a Case report send records a management event.</summary>
    public static bool IsManagementEvent(StaffMailPurpose purpose) =>
        purpose == StaffMailPurpose.CaseReport;

    /// <summary>
    /// The event of one observed report send: its documents sorted by kind,
    /// the assessment reports among them, and the To and Cc addresses counted
    /// once each, compared without case.
    /// </summary>
    public static ManagementEventPayload ForSend(
        IEnumerable<CaseReportArtifactKind> documents,
        IEnumerable<StaffMailRecipient> to,
        IEnumerable<StaffMailRecipient> cc)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(cc);
        var kinds = documents.Order().ToArray();
        return new(
            kinds.Select(kind => kind.ToString()).ToArray(),
            kinds.Count(kind => kind == CaseReportArtifactKind.AssessmentReport),
            to.Concat(cc)
                .Select(recipient => recipient.Address)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
    }

    /// <summary>The payload as the event row holds it.</summary>
    public static string Serialize(ManagementEventPayload payload) =>
        JsonSerializer.Serialize(payload, PayloadJson);
}
