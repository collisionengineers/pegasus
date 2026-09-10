using Pegasus.Core.Identity;

namespace Pegasus.Core.Documents;

/// <summary>
/// The colour a tag is drawn in. A fixed palette rather than free colour: the
/// chips are read at a glance next to each other, so the set is closed and the
/// same word always paints the same swatch. Each entry is one of the design
/// system's own tints, so a tag introduces no colour the workspace does not
/// already own.
/// </summary>
public enum ImageTagColour
{
    Blue,
    Green,
    Amber,
    Navy,
    Red,
    Grey
}

/// <summary>
/// One entry of the shared image-tag vocabulary. Built-in entries are seeded
/// and cannot be created twice; an operator adds their own with
/// <see cref="CreateImageTagCommand"/>.
/// </summary>
public sealed record ImageTag(
    Guid Id,
    string Name,
    ImageTagColour Colour,
    bool IsBuiltIn,
    long Version);

/// <summary>
/// A tag as it sits on one image occurrence: the vocabulary entry plus when it
/// was applied. The name and colour ride along so a tile draws its chips
/// without a second read of the vocabulary.
/// </summary>
public sealed record ImageTagAssignment(
    Guid TagId,
    string Name,
    ImageTagColour Colour,
    bool IsBuiltIn,
    DateTimeOffset AppliedAtUtc);

/// <summary>
/// The one owner of the built-in vocabulary and of what a tag name may be.
/// </summary>
/// <remarks>
/// The identifiers are fixed rather than generated so the seed, the migration
/// that converts the third-party vehicle flag, and the EVA exclusion all name
/// the same row without a lookup by name.
///
/// <see cref="ThirdPartyId"/> carries the behaviour the removed
/// <c>ThirdPartyVehicleConfirmedAtUtc</c> flag carried: an image wearing it is
/// not sent to EVA (<c>EvaHandoffPolicy.SelectEligibleImages</c>).
/// </remarks>
public static class ImageTagVocabulary
{
    public const int MaximumNameLength = 40;

    public static readonly Guid OverviewId = new("00000000-0000-4000-8000-0000000017a1");
    public static readonly Guid CloseUpId = new("00000000-0000-4000-8000-0000000017a2");
    public static readonly Guid ThirdPartyId = new("00000000-0000-4000-8000-0000000017a3");
    public static readonly Guid ReflectionId = new("00000000-0000-4000-8000-0000000017a4");

    public const string OverviewName = "Overview";
    public const string CloseUpName = "Close-up";
    public const string ThirdPartyName = "Third party";
    public const string ReflectionName = "Reflection";

    /// <summary>The seeded entries, in the order the picker lists them.</summary>
    public static readonly IReadOnlyList<ImageTag> BuiltIn =
    [
        new(OverviewId, OverviewName, ImageTagColour.Blue, IsBuiltIn: true, Version: 1),
        new(CloseUpId, CloseUpName, ImageTagColour.Green, IsBuiltIn: true, Version: 1),
        new(ThirdPartyId, ThirdPartyName, ImageTagColour.Amber, IsBuiltIn: true, Version: 1),
        new(ReflectionId, ReflectionName, ImageTagColour.Navy, IsBuiltIn: true, Version: 1)
    ];

    /// <summary>The stored spelling of a supplied name.</summary>
    public static string Normalize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = name.Trim();
        if (value.Length > MaximumNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                $"A tag name cannot exceed {MaximumNameLength} characters.");
        }
        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("A tag name cannot contain control characters.", nameof(name));
        }

        return value;
    }

    /// <summary>The comparison key: names are unique without regard to case.</summary>
    public static string NormalizeKey(string name) => Normalize(name).ToUpperInvariant();
}

/// <summary>
/// Puts one vocabulary entry on one of a case's image occurrences. Carries the
/// case's edit lease, its expected version and an operation key exactly as
/// every other case mutation does; the case version moves, so the tag is on
/// the record's timeline rather than beside it.
/// </summary>
public sealed record TagCaseImageCommand(
    Guid CaseId,
    Guid OccurrenceId,
    Guid TagId,
    ActionActor Actor,
    string OperationKey,
    long ExpectedCaseVersion,
    string EditLeaseToken);

/// <summary>Takes one tag off one image occurrence, under the same guards.</summary>
public sealed record UntagCaseImageCommand(
    Guid CaseId,
    Guid OccurrenceId,
    Guid TagId,
    ActionActor Actor,
    string OperationKey,
    long ExpectedCaseVersion,
    string EditLeaseToken);

/// <summary>
/// Adds an operator's own entry to the shared vocabulary. The vocabulary is
/// not a case, so this takes no case version and no lease — only the casework
/// right and an operation key for replay.
/// </summary>
public sealed record CreateImageTagCommand(
    string Name,
    ImageTagColour Colour,
    ActionActor Actor,
    string OperationKey);

public sealed record CreateImageTagResult(ImageTag Tag, bool IsReplay);

public interface ITagCaseImage
{
    Task ExecuteAsync(TagCaseImageCommand command, CancellationToken cancellationToken = default);
}

public interface IUntagCaseImage
{
    Task ExecuteAsync(UntagCaseImageCommand command, CancellationToken cancellationToken = default);
}

public interface ICreateImageTag
{
    Task<CreateImageTagResult> ExecuteAsync(
        CreateImageTagCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>The whole vocabulary, built-in entries first, then by name.</summary>
public interface IReadImageTagVocabulary
{
    Task<IReadOnlyList<ImageTag>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed class ImageTagNameInUseException(string name)
    : InvalidOperationException($"An image tag named '{name}' already exists.")
{
    public string Name { get; } = name;
}
