using System.Collections.Immutable;
using CollisionDocNet.Core;

namespace CollisionDocNet.Model;

public enum ExtractionInputKind
{
    Bytes = 0,
    Stream,
}

/// <summary>A caller-owned source. The extractor never disposes the stream.</summary>
public sealed class ExtractionInput
{
    private ExtractionInput(ImmutableArray<byte> bytes, Stream? stream)
    {
        Bytes = bytes;
        Stream = stream;
        Kind = stream is null ? ExtractionInputKind.Bytes : ExtractionInputKind.Stream;
    }

    public ExtractionInputKind Kind { get; }
    public ImmutableArray<byte> Bytes { get; }
    public Stream? Stream { get; }

    public static ExtractionInput FromBytes(ReadOnlySpan<byte> bytes) =>
        new(ImmutableArray.Create(bytes.ToArray()), null);

    public static ExtractionInput FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(stream));
        }

        return new ExtractionInput([], stream);
    }
}

public sealed record ExtractionPolicy
{
    public const string DefaultPolicyId = "collisiondocnet-extraction/1";

    public ExtractionPolicy(
        string policyId,
        string normalisationPolicyId,
        string stableIdentityPolicyId,
        ResourceLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisationPolicyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stableIdentityPolicyId);
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        PolicyId = policyId;
        NormalisationPolicyId = normalisationPolicyId;
        StableIdentityPolicyId = stableIdentityPolicyId;
    }

    public string PolicyId { get; }
    public string NormalisationPolicyId { get; }
    public string StableIdentityPolicyId { get; }
    public ResourceLimits Limits { get; }

    public static ExtractionPolicy CreateDefault() =>
        new(
            DefaultPolicyId,
            DeterministicText.PolicyId,
            StableIdentity.PolicyId,
            ResourceLimits.CreateCollisionSpikeDefault());
}

public sealed class ExtractionRequest
{
    public ExtractionRequest(
        ExtractionInput input,
        string sourceIdentity,
        string? fileName,
        string? declaredMediaType,
        ExtractionPolicy policy)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentity);
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));

        SourceIdentity = sourceIdentity;
        FileName = NormalizeHint(fileName, nameof(fileName));
        DeclaredMediaType = NormalizeHint(declaredMediaType, nameof(declaredMediaType));
    }

    public ExtractionInput Input { get; }
    public string SourceIdentity { get; }
    public string? FileName { get; }
    public string? DeclaredMediaType { get; }
    public ExtractionPolicy Policy { get; }

    private static string? NormalizeHint(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A supplied hint cannot be empty.", parameterName);
        }

        return value;
    }
}
