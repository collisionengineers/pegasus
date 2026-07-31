using System.Collections.ObjectModel;

namespace Pegasus.Core;

/// <summary>
/// Stable marker for the Core assembly.
/// </summary>
public static class CoreAssembly;

public enum QdosAlphaCapabilityEvidenceOutcome
{
    Passed = 1,
    DeferredToExternalGate = 2
}

public sealed record QdosAlphaCapabilityObservation(
    string CapabilityId,
    QdosAlphaCapabilityEvidenceOutcome Outcome,
    string Caller,
    string EvidenceReference,
    string EvidenceSha256);

public sealed record QdosAlphaExternalGateEvidence(
    string GateId,
    string ApprovalReference,
    string EvidenceReference,
    string EvidenceSha256);

public sealed record QdosAlphaAcceptanceRequest(
    int SchemaVersion,
    string Kind,
    string SourceRevision,
    string RunId,
    IReadOnlyCollection<QdosAlphaCapabilityObservation> CapabilityObservations,
    IReadOnlyCollection<QdosAlphaExternalGateEvidence> ExternalGateEvidence);

public sealed record QdosAlphaAcceptanceDecision(
    bool OfflineCandidateAccepted,
    bool ReleaseAccepted,
    IReadOnlyList<string> Blockers);

public interface IQdosAlphaAcceptanceGate
{
    QdosAlphaAcceptanceDecision Evaluate(QdosAlphaAcceptanceRequest request);
}

/// <summary>
/// Evaluates the complete QDOS-owned alpha evidence map without performing an
/// external operation. Evidence references remain assertions until the invoking
/// runner independently verifies their paths and hashes.
/// </summary>
public sealed class QdosAlphaAcceptanceGate : IQdosAlphaAcceptanceGate
{
    public const string AcceptanceManifestKind = "Pegasus.QdosAlpha.AcceptanceEvidence";

    private static readonly ReadOnlyCollection<string> CapabilityIds = Array.AsReadOnly<string>(
    [
        "OPS-10", "MAIL-21", "MAIL-22",
        "ACC-01", "ACC-02", "ACC-03", "ACC-04", "ACC-05", "ACC-06", "ACC-07",
        "ACC-08", "ACC-09", "ACC-10", "ACC-11",
        "INT-01", "INT-02", "INT-03", "INT-08", "INT-09", "INT-10", "INT-11",
        "INT-12", "INT-13", "INT-17", "INT-18", "INT-19", "INT-20", "INT-21",
        "INT-22", "INT-23", "INT-24", "INT-25", "INT-26", "INT-27", "INT-29",
        "INT-30", "MAIL-14", "MAIL-15", "MAIL-16", "MAIL-18",
        "TRI-01", "TRI-02", "TRI-03", "TRI-04", "TRI-05", "TRI-06", "TRI-07",
        "TRI-08", "TRI-09",
        "CASE-01", "CASE-02", "CASE-03", "CASE-04", "CASE-07", "CASE-08", "CASE-09",
        "CASE-10", "CASE-11", "CASE-12", "CASE-13", "CASE-14", "CASE-15", "CASE-16",
        "CASE-17", "CASE-18", "CASE-19", "CASE-20", "CASE-21", "CASE-24", "CASE-25",
        "CASE-26", "CASE-27", "CASE-28", "CASE-29", "CASE-30",
        "UI-01", "UI-02", "UI-03", "UI-04", "UI-05", "UI-06", "UI-07", "UI-08",
        "UI-09", "UI-11", "UI-13",
        "DOC-01", "DOC-02", "DOC-03", "DOC-04", "DOC-05", "DOC-06", "DOC-07",
        "DOC-08",
        "EXT-01", "EXT-02", "EXT-03", "EXT-14", "EXT-18",
        "OPS-01", "OPS-02", "OPS-03", "OPS-04", "OPS-05", "OPS-06", "OPS-07",
        "OPS-08", "OPS-09", "OPS-11", "OPS-13", "OPS-14", "OPS-20", "OPS-24",
        "DATA-01", "OPS-23", "OPS-25", "INT-31"
    ]);

    private static readonly HashSet<string> CapabilityIdSet =
        new(CapabilityIds, StringComparer.Ordinal);

    private static readonly HashSet<string> ExternallyCompletedCapabilityIds =
        new(["OPS-10", "OPS-24", "OPS-25"], StringComparer.Ordinal);

    private static readonly ReadOnlyCollection<string> OfflineGateIds = Array.AsReadOnly<string>(
    [
        "approved-capacity-dataset",
        "accepted-genuine-route-evidence"
    ]);

    private static readonly ReadOnlyCollection<string> ReleaseGateIds = Array.AsReadOnly<string>(
    [
        "approved-capacity-dataset",
        "accepted-genuine-route-evidence",
        "graph-scope-and-contract",
        "box-scope-and-contract",
        "dvla-dvsa-contract",
        "azure-deployment-and-recovery",
        "exact-head-independent-review",
        "qdos-operator-acceptance",
        "collision-engineers-management-approval"
    ]);

    public static IReadOnlyList<string> RequiredCapabilityIds => CapabilityIds;

    public static IReadOnlyList<string> RequiredOfflineGateIds => OfflineGateIds;

    public static IReadOnlyList<string> RequiredReleaseGateIds => ReleaseGateIds;

    public QdosAlphaAcceptanceDecision Evaluate(QdosAlphaAcceptanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != 1
            || !string.Equals(request.Kind, AcceptanceManifestKind, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The acceptance request must use schema version 1 and kind '{AcceptanceManifestKind}'.",
                nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RunId);
        ArgumentNullException.ThrowIfNull(request.CapabilityObservations);
        ArgumentNullException.ThrowIfNull(request.ExternalGateEvidence);

        if (!IsLowerHex(request.SourceRevision, 40))
        {
            throw new ArgumentException(
                "The acceptance request source revision must be a lowercase 40-character hexadecimal SHA.",
                nameof(request));
        }

        if (!IsLowerHex(request.RunId, 32))
        {
            throw new ArgumentException(
                "The acceptance request run identifier must be a lowercase 32-character hexadecimal value.",
                nameof(request));
        }

        var blockers = new List<string>();
        var observations = IndexObservations(request.CapabilityObservations, blockers);
        foreach (var capabilityId in CapabilityIds)
        {
            if (!observations.TryGetValue(capabilityId, out var observation))
            {
                AddBlocker(blockers, $"capability:{capabilityId}:missing");
                continue;
            }

            ValidateObservation(observation, blockers);
            if (observation.Outcome == QdosAlphaCapabilityEvidenceOutcome.DeferredToExternalGate
                && !ExternallyCompletedCapabilityIds.Contains(capabilityId))
            {
                AddBlocker(blockers, $"capability:{capabilityId}:cannot-defer");
            }
        }

        var externalEvidence = IndexExternalEvidence(request.ExternalGateEvidence, blockers);
        foreach (var gateId in OfflineGateIds)
        {
            if (!externalEvidence.TryGetValue(gateId, out var evidence))
            {
                AddBlocker(blockers, $"external-gate:{gateId}:missing");
                continue;
            }

            ValidateExternalEvidence(evidence, blockers);
        }

        var offlineAccepted = blockers.Count == 0;
        var releaseBlockers = new List<string>(blockers);
        foreach (var capabilityId in ExternallyCompletedCapabilityIds)
        {
            if (observations.TryGetValue(capabilityId, out var observation)
                && observation.Outcome != QdosAlphaCapabilityEvidenceOutcome.Passed)
            {
                AddBlocker(releaseBlockers, $"capability:{capabilityId}:external-evidence-required");
            }
        }

        foreach (var gateId in ReleaseGateIds)
        {
            if (!externalEvidence.TryGetValue(gateId, out var evidence))
            {
                AddBlocker(releaseBlockers, $"external-gate:{gateId}:missing");
                continue;
            }

            ValidateExternalEvidence(evidence, releaseBlockers);
        }

        releaseBlockers.Sort(StringComparer.Ordinal);

        return new(
            offlineAccepted,
            releaseBlockers.Count == 0,
            releaseBlockers.ToArray());
    }

    private static Dictionary<string, QdosAlphaCapabilityObservation> IndexObservations(
        IReadOnlyCollection<QdosAlphaCapabilityObservation> observations,
        List<string> blockers)
    {
        var indexed = new Dictionary<string, QdosAlphaCapabilityObservation>(StringComparer.Ordinal);
        foreach (var observation in observations)
        {
            if (observation is null)
            {
                AddBlocker(blockers, "capability:null");
                continue;
            }

            if (!CapabilityIdSet.Contains(observation.CapabilityId))
            {
                AddBlocker(blockers, $"capability:{observation.CapabilityId}:not-qdos-owned");
                continue;
            }

            if (!indexed.TryAdd(observation.CapabilityId, observation))
            {
                AddBlocker(blockers, $"capability:{observation.CapabilityId}:duplicate");
            }
        }

        return indexed;
    }

    private static Dictionary<string, QdosAlphaExternalGateEvidence> IndexExternalEvidence(
        IReadOnlyCollection<QdosAlphaExternalGateEvidence> evidenceItems,
        List<string> blockers)
    {
        var knownGateIds = new HashSet<string>(ReleaseGateIds, StringComparer.Ordinal);
        var indexed = new Dictionary<string, QdosAlphaExternalGateEvidence>(StringComparer.Ordinal);
        foreach (var evidence in evidenceItems)
        {
            if (evidence is null)
            {
                AddBlocker(blockers, "external-gate:null");
                continue;
            }

            if (!knownGateIds.Contains(evidence.GateId))
            {
                AddBlocker(blockers, $"external-gate:{evidence.GateId}:unknown");
                continue;
            }

            if (!indexed.TryAdd(evidence.GateId, evidence))
            {
                AddBlocker(blockers, $"external-gate:{evidence.GateId}:duplicate");
            }
        }

        return indexed;
    }

    private static void ValidateObservation(
        QdosAlphaCapabilityObservation observation,
        List<string> blockers)
    {
        if (!Enum.IsDefined(observation.Outcome))
        {
            AddBlocker(blockers, $"capability:{observation.CapabilityId}:invalid-outcome");
        }

        if (string.IsNullOrWhiteSpace(observation.Caller))
        {
            AddBlocker(blockers, $"capability:{observation.CapabilityId}:caller-missing");
        }

        if (string.IsNullOrWhiteSpace(observation.EvidenceReference))
        {
            AddBlocker(blockers, $"capability:{observation.CapabilityId}:evidence-reference-missing");
        }

        if (!IsLowerHex(observation.EvidenceSha256, 64))
        {
            AddBlocker(blockers, $"capability:{observation.CapabilityId}:evidence-hash-invalid");
        }
    }

    private static void ValidateExternalEvidence(
        QdosAlphaExternalGateEvidence evidence,
        List<string> blockers)
    {
        if (string.IsNullOrWhiteSpace(evidence.ApprovalReference))
        {
            AddBlocker(blockers, $"external-gate:{evidence.GateId}:approval-reference-missing");
        }

        if (string.IsNullOrWhiteSpace(evidence.EvidenceReference))
        {
            AddBlocker(blockers, $"external-gate:{evidence.GateId}:evidence-reference-missing");
        }

        if (!IsLowerHex(evidence.EvidenceSha256, 64))
        {
            AddBlocker(blockers, $"external-gate:{evidence.GateId}:evidence-hash-invalid");
        }
    }

    private static void AddBlocker(List<string> blockers, string blocker)
    {
        if (!blockers.Contains(blocker))
        {
            blockers.Add(blocker);
        }
    }

    private static bool IsLowerHex(string? value, int length) =>
        value is { Length: var actualLength }
        && actualLength == length
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
