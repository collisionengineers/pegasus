namespace Pegasus.Web.Pages.Cases;

/// <summary>What the Assign Engineer dialog offers and posts back.</summary>
public sealed record AssignEngineerViewModel(
    IReadOnlyList<EngineerOption> EngineerOptions,
    bool InstructionsComplete,
    bool ImagesComplete);

public sealed record EngineerOption(Guid Id, string Name);
