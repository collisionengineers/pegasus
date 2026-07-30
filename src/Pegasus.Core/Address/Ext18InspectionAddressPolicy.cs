using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Address;

public static class Ext18InspectionAddressPolicy
{
    public const string PolicyKey = "ext-18-inspection-address";
    public const int PolicyVersion = 1;
    public const string ImageBasedAssessment = "Image Based Assessment";

    private const string InspectionAddressFieldName = "Inspection address";
    private const int MaximumAddressLength = 1000;

    public static InspectionAddressEvaluation Evaluate(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Evaluate(
            receipt.Fields,
            receipt.ExtractionPolicyKey,
            receipt.ExtractionPolicyVersion);
    }

    public static InspectionAddressEvaluation Evaluate(
        IReadOnlyList<InstructionReviewField> fields,
        string? extractionPolicyKey,
        int? extractionPolicyVersion)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (string.IsNullOrWhiteSpace(extractionPolicyKey)
            || extractionPolicyVersion is null or <= 0)
        {
            return Unresolved();
        }

        var addressFields = fields
            .Where(field => string.Equals(field.Name, InspectionAddressFieldName, StringComparison.Ordinal))
            .ToArray();
        if (addressFields.Length != 1)
        {
            return Unresolved();
        }

        var field = addressFields[0];
        var evidence = field.Candidates
            .Where(candidate => IsContentEvidence(candidate.Source)
                && IsSupportedValue(candidate.Value)
                && !string.IsNullOrWhiteSpace(candidate.SourceLabel))
            .Select(candidate => CreateEvidence(
                candidate.Value.Trim(),
                candidate.Source,
                candidate.SourceLabel.Trim(),
                extractionPolicyKey.Trim(),
                extractionPolicyVersion.Value))
            .OrderBy(candidate => candidate.Value, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Provenance.Source)
            .ThenBy(candidate => candidate.Provenance.SourceLabel, StringComparer.Ordinal)
            .ToArray();

        if (evidence.Length == 0)
        {
            return Unresolved();
        }

        var distinctValues = evidence
            .Select(candidate => candidate.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (field.HasConflict || distinctValues.Length != 1)
        {
            return new(null, evidence);
        }

        var value = distinctValues[0];
        var kind = string.Equals(value, ImageBasedAssessment, StringComparison.Ordinal)
            ? InspectionAddressEvidenceKind.ImageBasedAssessment
            : InspectionAddressEvidenceKind.PhysicalAddress;
        var provenance = evidence
            .Select(candidate => candidate.Provenance)
            .Distinct()
            .ToArray();
        return new(
            new(value, kind, provenance, CreateFingerprint(value, kind, provenance)),
            []);
    }

    private static InspectionAddressEvidence CreateEvidence(
        string value,
        IntakeEvidenceSource source,
        string sourceLabel,
        string policyKey,
        int policyVersion)
    {
        var kind = string.Equals(value, ImageBasedAssessment, StringComparison.Ordinal)
            ? InspectionAddressEvidenceKind.ImageBasedAssessment
            : InspectionAddressEvidenceKind.PhysicalAddress;
        return new(
            value,
            kind,
            new(source, sourceLabel, policyKey, policyVersion));
    }

    private static string CreateFingerprint(
        string value,
        InspectionAddressEvidenceKind kind,
        IReadOnlyList<InspectionAddressProvenance> provenance)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, PolicyKey);
        Append(hash, PolicyVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, value);
        Append(hash, kind.ToString());
        foreach (var source in provenance)
        {
            Append(hash, ((int)source.Source).ToString(CultureInfo.InvariantCulture));
            Append(hash, source.SourceLabel);
            Append(hash, source.PolicyKey);
            Append(hash, source.PolicyVersion.ToString(CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool IsSupportedValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaximumAddressLength)
        {
            return false;
        }

        var trimmed = value.Trim();
        return !string.Equals(trimmed, ImageBasedAssessment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, ImageBasedAssessment, StringComparison.Ordinal);
    }

    private static bool IsContentEvidence(IntakeEvidenceSource source) =>
        source is IntakeEvidenceSource.EmailBody
            or IntakeEvidenceSource.PdfContent
            or IntakeEvidenceSource.DocumentContent
            or IntakeEvidenceSource.ImageContent;

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static InspectionAddressEvaluation Unresolved() => new(null, []);
}
