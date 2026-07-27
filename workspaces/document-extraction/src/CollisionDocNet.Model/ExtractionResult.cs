using System.Collections.Immutable;
using CollisionDocNet.Core;

namespace CollisionDocNet.Model;

public sealed class ExtractionResult
{
    public const string CurrentSchemaVersion = "collisiondocnet-result/1";

    public ExtractionResult(
        DetectedContainer detectedContainer,
        DetectedFormat detectedFormat,
        ExtractionOutcome outcome,
        Sha256Digest sourceHash,
        string extractorVersion,
        string specificationIdentity,
        string policyIdentity,
        ResourceMeasurements measurements,
        IEnumerable<ContentSegment>? content = null,
        IEnumerable<MetadataEntry>? metadata = null,
        IEnumerable<Participant>? participants = null,
        IEnumerable<EvidenceRelationship>? relationships = null,
        IEnumerable<ReviewAsset>? assets = null,
        IEnumerable<ExtractionIssue>? issues = null,
        IEnumerable<ExtractionResult>? nestedResults = null)
    {
        if (!Enum.IsDefined(detectedContainer))
        {
            throw new ArgumentOutOfRangeException(nameof(detectedContainer));
        }

        if (!Enum.IsDefined(detectedFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(detectedFormat));
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(extractorVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyIdentity);
        ArgumentNullException.ThrowIfNull(measurements);
        if (!Sha256Digest.TryParse(sourceHash.Hex, out _))
        {
            throw new ArgumentException("A canonical SHA-256 source hash is required.", nameof(sourceHash));
        }

        SchemaVersion = CurrentSchemaVersion;
        DetectedContainer = detectedContainer;
        DetectedFormat = detectedFormat;
        Outcome = outcome;
        SourceHash = sourceHash;
        ExtractorVersion = extractorVersion;
        SpecificationIdentity = specificationIdentity;
        PolicyIdentity = policyIdentity;
        Measurements = measurements;
        Content = Order(content, ContentComparer.Instance);
        Metadata = Order(metadata, MetadataComparer.Instance);
        Participants = Order(participants, ParticipantComparer.Instance);
        Relationships = Order(relationships, RelationshipComparer.Instance);
        Assets = OrderUniqueAssets(assets);
        Issues = Order(issues, IssueComparer.Instance);
        NestedResults = OrderNested(nestedResults);
    }

    public string SchemaVersion { get; }
    public DetectedContainer DetectedContainer { get; }
    public DetectedFormat DetectedFormat { get; }
    public ExtractionOutcome Outcome { get; }
    public Sha256Digest SourceHash { get; }
    public string ExtractorVersion { get; }
    public string SpecificationIdentity { get; }
    public string PolicyIdentity { get; }
    public ResourceMeasurements Measurements { get; }
    public ImmutableArray<ContentSegment> Content { get; }
    public ImmutableArray<MetadataEntry> Metadata { get; }
    public ImmutableArray<Participant> Participants { get; }
    public ImmutableArray<EvidenceRelationship> Relationships { get; }
    public ImmutableArray<ReviewAsset> Assets { get; }
    public ImmutableArray<ExtractionIssue> Issues { get; }
    public ImmutableArray<ExtractionResult> NestedResults { get; }

    private static ImmutableArray<T> Order<T>(IEnumerable<T>? values, IComparer<T> comparer)
    {
        if (values is null)
        {
            return [];
        }

        var items = values.ToArray();
        foreach (T item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
        }

        Array.Sort(items, comparer);
        return ImmutableArray.Create(items);
    }

    private static ImmutableArray<ExtractionResult> OrderNested(
        IEnumerable<ExtractionResult>? values)
    {
        if (values is null)
        {
            return [];
        }

        ExtractionResult[] items = values.ToArray();
        var keyed = new KeyedNestedResult[items.Length];
        for (int index = 0; index < items.Length; index++)
        {
            ExtractionResult value = items[index];
            ArgumentNullException.ThrowIfNull(value);
            keyed[index] = new KeyedNestedResult(
                value,
                ExtractionResultJson.SerializeToUtf8Bytes(value));
        }

        Array.Sort(keyed, static (left, right) =>
        {
            int comparison = StringComparer.Ordinal.Compare(
                left.Value.SourceHash.Hex,
                right.Value.SourceHash.Hex);
            return comparison != 0
                ? comparison
                : left.Key.AsSpan().SequenceCompareTo(right.Key);
        });
        var ordered = ImmutableArray.CreateBuilder<ExtractionResult>(keyed.Length);
        foreach (KeyedNestedResult item in keyed)
        {
            ordered.Add(item.Value);
        }

        return ordered.MoveToImmutable();
    }

    private static ImmutableArray<ReviewAsset> OrderUniqueAssets(
        IEnumerable<ReviewAsset>? assets)
    {
        ImmutableArray<ReviewAsset> ordered = Order(assets, AssetComparer.Instance);
        for (int index = 1; index < ordered.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(
                ordered[index - 1].StableId,
                ordered[index].StableId))
            {
                throw new ArgumentException(
                    "Asset stable identities must be unique within an extraction result.",
                    nameof(assets));
            }
        }

        return ordered;
    }

    private readonly record struct KeyedNestedResult(ExtractionResult Value, byte[] Key);

    private static int CompareLocation(SourceLocation? left, SourceLocation? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        int comparison = StringComparer.Ordinal.Compare(left.Domain, right.Domain);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.Ordinal.Compare(left.Path, right.Path);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Offset.CompareTo(right.Offset);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Length.CompareTo(right.Length);
        return comparison != 0 ? comparison : left.Kind.CompareTo(right.Kind);
    }

    private sealed class ContentComparer : IComparer<ContentSegment>
    {
        public static readonly ContentComparer Instance = new();
        public int Compare(ContentSegment? x, ContentSegment? y)
        {
            int comparison = CompareOrdered(
                x?.Order,
                y?.Order,
                x?.SourceLocation,
                y?.SourceLocation,
                x?.Kind,
                y?.Kind);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(x?.Text, y?.Text);
        }
    }

    private sealed class MetadataComparer : IComparer<MetadataEntry>
    {
        public static readonly MetadataComparer Instance = new();
        public int Compare(MetadataEntry? x, MetadataEntry? y)
        {
            int comparison = CompareOrdered(
                x?.Order,
                y?.Order,
                x?.SourceLocation,
                y?.SourceLocation,
                x?.Name,
                y?.Name);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(x?.Value, y?.Value);
        }
    }

    private sealed class ParticipantComparer : IComparer<Participant>
    {
        public static readonly ParticipantComparer Instance = new();
        public int Compare(Participant? x, Participant? y)
        {
            int comparison = CompareOrdered(
                x?.Order,
                y?.Order,
                x?.SourceLocation,
                y?.SourceLocation,
                x?.Role,
                y?.Role);
            comparison = comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(x?.DisplayName, y?.DisplayName);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(x?.Address, y?.Address);
        }
    }

    private sealed class RelationshipComparer : IComparer<EvidenceRelationship>
    {
        public static readonly RelationshipComparer Instance = new();
        public int Compare(EvidenceRelationship? x, EvidenceRelationship? y)
        {
            int comparison = CompareOrdered(
                x?.Order,
                y?.Order,
                x?.SourceLocation,
                y?.SourceLocation,
                x?.Kind,
                y?.Kind);
            comparison = comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(x?.SourceIdentity, y?.SourceIdentity);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(x?.TargetIdentity, y?.TargetIdentity);
        }
    }

    private sealed class AssetComparer : IComparer<ReviewAsset>
    {
        public static readonly AssetComparer Instance = new();
        public int Compare(ReviewAsset? x, ReviewAsset? y)
        {
            int comparison = StringComparer.Ordinal.Compare(x?.StableId, y?.StableId);
            comparison = comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(x?.ContentHash.Hex, y?.ContentHash.Hex);
            comparison = comparison != 0 ? comparison : (x?.Length ?? -1).CompareTo(y?.Length ?? -1);
            comparison = comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(x?.Kind, y?.Kind);
            comparison = comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(x?.MediaType, y?.MediaType);
            comparison = comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(x?.OriginalName, y?.OriginalName);
            comparison = comparison != 0
                ? comparison
                : CompareLocation(x?.SourceLocation, y?.SourceLocation);
            if (comparison != 0 || x is null || y is null)
            {
                return comparison;
            }

            return x.Content.AsSpan().SequenceCompareTo(y.Content.AsSpan());
        }
    }

    private sealed class IssueComparer : IComparer<ExtractionIssue>
    {
        public static readonly IssueComparer Instance = new();
        public int Compare(ExtractionIssue? x, ExtractionIssue? y)
        {
            int comparison = CompareOrdered(
                x?.Order,
                y?.Order,
                x?.SourceLocation,
                y?.SourceLocation,
                x?.Code,
                y?.Code);
            comparison = comparison != 0
                ? comparison
                : (x?.Severity ?? ExtractionIssueSeverity.Information).CompareTo(
                    y?.Severity ?? ExtractionIssueSeverity.Information);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(x?.Message, y?.Message);
        }
    }

    private static int CompareOrdered(
        int? leftOrder,
        int? rightOrder,
        SourceLocation? leftLocation,
        SourceLocation? rightLocation,
        string? leftText,
        string? rightText)
    {
        if (leftOrder is null)
        {
            return rightOrder is null ? 0 : -1;
        }

        if (rightOrder is null)
        {
            return 1;
        }

        int comparison = leftOrder.Value.CompareTo(rightOrder.Value);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = CompareLocation(leftLocation, rightLocation);
        return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(leftText, rightText);
    }
}
