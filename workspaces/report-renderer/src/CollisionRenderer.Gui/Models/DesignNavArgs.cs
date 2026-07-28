namespace CollisionRenderer.Gui.Models;

/// <summary>
/// Navigation payload from the gallery to the design screen. Carries the chosen
/// authoring template and, when reopening a recent document, the draft JSON to restore.
/// </summary>
public sealed record DesignNavArgs
{
    public required string AuthoringTemplateId { get; init; }

    /// <summary>Draft JSON to restore (recent document); null means start from the starter.</summary>
    public string? RestoreJson { get; init; }
}
