namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The once-per-case `First sent to Engineer` proxy (CASE-21). One row per
/// case, keyed by the case, written by the first successful export (ENG-016).
///
/// It carried a required foreign key to `EvaHandoffRevisions` and the
/// generating operation's key; both went with the hand-off. The case is the
/// key, so the row itself is the once-per-case guarantee.
/// </summary>
internal sealed class EvaFirstHandoffProxyEntity
{
    public Guid CaseId { get; set; }
    public string AdapterKey { get; set; } = string.Empty;
    public string AdapterVersion { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string ActorSubjectId { get; set; } = string.Empty;
    public bool ClaimsExternalDelivery { get; set; }
    public bool ClaimsEngineerAssignment { get; set; }
}
