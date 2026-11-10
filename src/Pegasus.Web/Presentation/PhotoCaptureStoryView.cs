using Pegasus.Core.PhotoCapture;

namespace Pegasus.Web.Presentation;

/// <summary>One stage of a damaged area, or the whole of any other group: its sub-heading (none outside damage) and its items in story order.</summary>
public sealed record PhotoCaptureStoryStage(PhotoCaptureStage? Stage, string? Label, IReadOnlyList<PhotoCaptureItem> Items);

/// <summary>One story group: its heading from <see cref="PhotoCaptureStory.GroupLabel"/> and its stages.</summary>
public sealed record PhotoCaptureStoryGroup(int Group, string Label, IReadOnlyList<PhotoCaptureStoryStage> Stages);

/// <summary>
/// A capture's items as the story the app took them in (FRD-28, FRD-16 Files):
/// the groups in story order, a damaged area split by its four stages, and
/// each group's items by their story sequence. The order and the words are
/// <see cref="PhotoCaptureStory"/>'s; this only arranges them for a page.
/// </summary>
public static class PhotoCaptureStoryView
{
    public static IReadOnlyList<PhotoCaptureStoryGroup> Groups(
        IReadOnlyList<PhotoCaptureItem> items,
        IReadOnlyList<string> markedAreas)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(markedAreas);
        return
        [
            .. items
                .GroupBy(item => item.StoryGroup)
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    var ordered = group
                        .OrderBy(item => item.StorySequence)
                        .ThenBy(item => item.Batch)
                        .ThenBy(item => item.ReceivedAtUtc)
                        .ToArray();
                    IReadOnlyList<PhotoCaptureStoryStage> stages = ordered.All(item => item.Kind == PhotoCaptureItemKind.Damage)
                        ? [.. ordered
                            .GroupBy(item => item.Stage ?? PhotoCaptureStage.Extra)
                            .OrderBy(stage => stage.Key)
                            .Select(stage => new PhotoCaptureStoryStage(stage.Key, StageHeading(stage.Key), [.. stage]))]
                        : [new PhotoCaptureStoryStage(null, null, ordered)];
                    return new PhotoCaptureStoryGroup(group.Key, PhotoCaptureStory.GroupLabel(group.Key, markedAreas), stages);
                })
        ];
    }

    /// <summary>A stage's sub-heading: <see cref="PhotoCaptureStory.StageLabel"/> with its first letter raised.</summary>
    public static string StageHeading(PhotoCaptureStage stage)
    {
        var label = PhotoCaptureStory.StageLabel(stage);
        return label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];
    }
}
