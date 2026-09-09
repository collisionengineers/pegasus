namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One active staff edit claim for one typed mutable record. Rows are removed
/// as the owning mutation commits or the editor cancels; expired rows are
/// treated as unheld and replaced by a later claim.
/// </summary>
internal sealed class EditScopeEntity
{
    public required string ScopeKind { get; set; }
    public Guid RecordId { get; set; }
    public required string HolderKind { get; set; }
    public required string Holder { get; set; }
    public required string TokenHash { get; set; }
    public long ExpectedVersion { get; set; }
    public long Generation { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
