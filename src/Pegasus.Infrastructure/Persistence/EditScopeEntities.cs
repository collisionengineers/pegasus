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

    /// <summary>
    /// The token itself, retained as the Case lease retains its own, so the holder's own claim
    /// resumes it and a replayed claim returns it (R518, R519). Only a token the retained hash
    /// proves is ever handed back.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>The details of the claim that last issued or resumed the scope, which a replay repeats.</summary>
    public string? ClaimRequestHash { get; set; }
    public long ExpectedVersion { get; set; }
    public long Generation { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
