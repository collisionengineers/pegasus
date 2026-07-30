using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

/// <summary>
/// An immutable Sent item retained by the approved-mailbox ingestion boundary before any
/// staff member associates it with a case.
/// </summary>
public sealed record RetainedApprovedMailboxReportSentEvidence(
    Guid EvidenceId,
    string MailboxIdentity,
    string SentFolderIdentity,
    string ImmutableItemIdentity,
    string InternetMessageIdentity,
    string ConversationIdentity,
    string ReplyChainIdentity,
    string SourceOccurrenceIdentity,
    string SourceSha256,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset DiscoveredAtUtc,
    ActionActor DiscoveredBy);

public sealed record RetainApprovedMailboxReportSentEvidenceRequest(
    Guid EvidenceId,
    string MailboxIdentity,
    string SentFolderIdentity,
    string ImmutableItemIdentity,
    string InternetMessageIdentity,
    string ConversationIdentity,
    string ReplyChainIdentity,
    string SourceOccurrenceIdentity,
    string SourceSha256,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset DiscoveredAtUtc,
    ActionActor DiscoveredBy,
    string OperationKey);

public interface IApprovedMailboxReportSentEvidenceQueries
{
    Task<RetainedApprovedMailboxReportSentEvidence?> GetAsync(
        Guid evidenceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RetainedApprovedMailboxReportSentEvidence>> ListUnlinkedAsync(
        int maximumResults,
        CancellationToken cancellationToken);
}

/// <summary>
/// Trusted persistence boundary used by approved-mailbox ingestion. Staff Web and MCP
/// callers receive only the query interface and cannot manufacture retained evidence.
/// </summary>
public interface IApprovedMailboxReportSentEvidenceStore : IApprovedMailboxReportSentEvidenceQueries
{
    Task<RetainedApprovedMailboxReportSentEvidence> RetainAsync(
        RetainApprovedMailboxReportSentEvidenceRequest request,
        CancellationToken cancellationToken);
}

public interface IRetainApprovedMailboxReportSentEvidence
{
    Task<RetainedApprovedMailboxReportSentEvidence> ExecuteAsync(
        RetainApprovedMailboxReportSentEvidenceRequest request,
        CancellationToken cancellationToken);
}

public sealed class RetainApprovedMailboxReportSentEvidence(
    IApprovedMailboxReportSentEvidenceStore store) : IRetainApprovedMailboxReportSentEvidence
{
    private readonly IApprovedMailboxReportSentEvidenceStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<RetainedApprovedMailboxReportSentEvidence> ExecuteAsync(
        RetainApprovedMailboxReportSentEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        StaffAuthorization.Require(request.DiscoveredBy, StaffAccessRight.ExecuteSystemWork);
        return _store.RetainAsync(request, cancellationToken);
    }

    private static void Validate(RetainApprovedMailboxReportSentEvidenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EvidenceId == Guid.Empty)
        {
            throw new ArgumentException("A stable retained Sent-evidence identifier is required.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.DiscoveredBy);
        RequireText(request.MailboxIdentity, 320, "An approved mailbox identity is required.", nameof(request));
        RequireText(request.SentFolderIdentity, 200, "A Sent-folder identity is required.", nameof(request));
        RequireText(request.ImmutableItemIdentity, 500, "An immutable Sent-item identity is required.", nameof(request));
        RequireText(request.InternetMessageIdentity, 500, "An Internet message identity is required.", nameof(request));
        RequireText(request.ConversationIdentity, 500, "A conversation identity is required.", nameof(request));
        RequireText(request.ReplyChainIdentity, 500, "A reply-chain identity is required.", nameof(request));
        RequireText(request.SourceOccurrenceIdentity, 200, "A source occurrence identity is required.", nameof(request));
        RequireSha256(request.SourceSha256, nameof(request));
        RequireSha256(request.MimeSha256, nameof(request));
        RequireText(request.OperationKey, 100, "A retention operation key is required.", nameof(request));

        if (request.SentAtUtc == default || request.DiscoveredAtUtc == default)
        {
            throw new ArgumentException("Authoritative Sent and discovery times are required.", nameof(request));
        }

        if (request.SentAtUtc.Offset != TimeSpan.Zero || request.DiscoveredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Sent-evidence instants must be UTC.", nameof(request));
        }

        if (request.DiscoveredAtUtc < request.SentAtUtc)
        {
            throw new ArgumentException("Sent evidence cannot be discovered before it was sent.", nameof(request));
        }
    }

    private static void RequireSha256(string value, string parameterName)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "Sent-evidence SHA-256 values must contain 64 hexadecimal characters.",
                parameterName);
        }
    }

    private static void RequireText(
        string value,
        int maximumLength,
        string message,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
