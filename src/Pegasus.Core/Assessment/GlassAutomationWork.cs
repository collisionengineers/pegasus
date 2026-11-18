namespace Pegasus.Core.Assessment;

/// <summary>
/// What a Glass's session the Automation owns has done, kept beside the
/// session so the Automation and the Case page read it without the protected
/// state (design 07 section 2). Nothing in it is secret.
/// </summary>
/// <remarks>
/// The position acts of a later slice join this record; a stored record
/// without them reads as none.
/// </remarks>
public sealed record GlassAutomationWork
{
    /// <summary>
    /// The operation key of the finish that claimed the session. The same key
    /// again answers the session as it stands and sends nothing to Glass's.
    /// </summary>
    public string? FinishOperationKey { get; init; }

    /// <summary>The repair spec the finish landed as: a Draft, which only staff put in use.</summary>
    public Guid? DraftSpecificationId { get; init; }
}
