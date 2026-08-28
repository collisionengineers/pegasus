using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

public sealed class InspectionAddressResolutionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IInspectionAddressResolutionStore
{
    private const int JsonVersion = 1;
    private const string ResolutionSignalPrefix = "ext18-address-resolution/v1/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InspectionAddressResolutionSnapshot?> GetAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        if (intakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(intakeReceiptId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .Include(item => item.InstructionDraft)
            .SingleOrDefaultAsync(item => item.Id == intakeReceiptId, cancellationToken);
        return receipt is null ? null : CreateSnapshot(receipt);
    }

    public async Task<InspectionAddressResolutionSnapshot> ResolveAsync(
        InspectionAddressResolutionRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await context.IntakeReceipts
            .Include(item => item.InstructionDraft)
            .SingleOrDefaultAsync(item => item.Id == request.IntakeReceiptId, cancellationToken)
            ?? throw new InvalidOperationException("The intake receipt does not exist.");

        var evidence = DeserializeEvidence(receipt.EvidenceJson);
        var duplicate = evidence
            .Select(TryReadResolution)
            .LastOrDefault(item => item?.OperationId == request.OperationId);
        if (duplicate is not null)
        {
            EnsureDuplicateMatches(request, duplicate);
            return CreateSnapshot(receipt, evidence);
        }
        if (await context.CaseIntakeLinks.AsNoTracking().AnyAsync(
                item => item.IntakeReceiptId == request.IntakeReceiptId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Accepted intake evidence is immutable; use the versioned SaveCase command.");
        }


        if (receipt.Version != request.ExpectedReceiptVersion)
        {
            throw new InspectionAddressResolutionConcurrencyException();
        }

        var evaluation = Evaluate(receipt);
        InspectionAddressSuggestion? suggestion;
        if (request.Decision == InspectionAddressStaffDecision.SupplyAddress)
        {
            // Supplying answers material that carries no address evidence at
            // all. Where extraction did find one, a person has to look at it
            // and accept or correct it; letting a typed value quietly displace
            // extracted evidence is how an unexamined address gets onto a case.
            if (evaluation.Suggestion is not null)
            {
                throw new InvalidOperationException(
                    "An extracted inspection-address suggestion must be accepted or corrected, not replaced.");
            }

            suggestion = null;
        }
        else
        {
            suggestion = evaluation.Suggestion
                ?? throw new InvalidOperationException(
                    "Missing or conflicting inspection-address evidence cannot be resolved by this policy.");
            if (!string.Equals(
                    suggestion.Fingerprint,
                    request.SuggestionFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InspectionAddressResolutionConcurrencyException();
            }
        }

        if (receipt.InstructionDraft is null)
        {
            throw new InvalidOperationException(
                "An inspection address can be resolved only for an instruction draft.");
        }

        var previousValue = receipt.InstructionDraft.InspectionAddress;
        var resolvedValue = ResolveValue(request, suggestion?.Value);
        var resolvedAtUtc = timeProvider.GetUtcNow();
        if (resolvedAtUtc.Offset != TimeSpan.Zero)
        {
            resolvedAtUtc = resolvedAtUtc.ToUniversalTime();
        }

        var staffId = Guid.Parse(request.Actor.SubjectId);
        var state = request.Decision switch
        {
            InspectionAddressStaffDecision.AcceptSuggestion =>
                InspectionAddressResolutionState.Accepted,
            InspectionAddressStaffDecision.CorrectSuggestion =>
                InspectionAddressResolutionState.Corrected,
            _ => InspectionAddressResolutionState.Supplied
        };
        var persistedResolution = new PersistedResolution(
            ToStateCode(state),
            resolvedValue,
            suggestion?.Fingerprint ?? string.Empty,
            staffId,
            resolvedAtUtc,
            request.OperationId);
        var humanDetail = state switch
        {
            InspectionAddressResolutionState.Accepted =>
                "Inspection address accepted by staff from extracted intake evidence.",
            InspectionAddressResolutionState.Corrected =>
                "Inspection address corrected by staff from extracted intake evidence.",
            _ => "Inspection address supplied by staff; no address evidence was extracted from the source."
        };
        evidence.Add(new(
            // A supplied address has no extracted provenance to name, because
            // there was none; the staff member who typed it is the source.
            IntakeEvidenceSourceCodes.ToCode(suggestion is null
                ? IntakeEvidenceSource.StaffCorrection
                : suggestion.Provenance[0].Source),
            "strong",
            "information",
            ResolutionSignalPrefix + EncodeResolution(persistedResolution),
            humanDetail));

        receipt.InstructionDraft.InspectionAddress = resolvedValue;
        receipt.EvidenceJson = SerializeEvidence(evidence);
        var beforeVersion = receipt.Version;
        receipt.Version++;

        var roles = request.Actor.Roles
            .OrderBy(role => role)
            .Select(role => role.ToString())
            .ToArray();
        context.Set<ActionHistoryEntity>().Add(new()
        {
            Id = request.OperationId,
            AggregateType = "intake_receipt",
            AggregateId = receipt.Id.ToString("D"),
            EventKind = state switch
            {
                InspectionAddressResolutionState.Accepted => "inspection_address_accepted",
                InspectionAddressResolutionState.Corrected => "inspection_address_corrected",
                _ => "inspection_address_supplied"
            },
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(roles, JsonOptions),
            OccurredAtUtc = resolvedAtUtc,
            Outcome = "Succeeded",
            CorrelationId = request.CorrelationId,
            Reason = humanDetail,
            BeforeJson = JsonSerializer.Serialize(
                new AddressHistoryValue(beforeVersion, previousValue, null),
                JsonOptions),
            AfterJson = JsonSerializer.Serialize(
                new AddressHistoryValue(receipt.Version, resolvedValue, suggestion),
                JsonOptions),
            PolicyVersion = $"{Ext18InspectionAddressPolicy.PolicyKey}/v{Ext18InspectionAddressPolicy.PolicyVersion}"
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InspectionAddressResolutionConcurrencyException();
        }

        return CreateSnapshot(receipt, evidence);
    }

    internal static InspectionAddressResolutionSnapshot CreateSnapshot(
        IntakeReceiptEntity receipt) =>
        CreateSnapshot(receipt, DeserializeEvidence(receipt.EvidenceJson));

    private static InspectionAddressResolutionSnapshot CreateSnapshot(
        IntakeReceiptEntity receipt,
        List<PersistedEvidence>? persistedEvidence = null)
    {
        persistedEvidence ??= DeserializeEvidence(receipt.EvidenceJson);
        var evaluation = Evaluate(receipt);
        var latest = persistedEvidence
            .Select(TryReadResolution)
            .LastOrDefault(item => item is not null);
        var current = IsStillCurrent(latest, evaluation) ? latest : null;
        var state = current is null
            ? evaluation.Suggestion is null
                ? InspectionAddressResolutionState.Unresolved
                : InspectionAddressResolutionState.Suggested
            : ParseState(current.State);
        return new(
            receipt.Id,
            receipt.Version,
            state,
            evaluation,
            current?.Value,
            current?.StaffId,
            current?.OccurredAtUtc);
    }

    /// <summary>
    /// Whether a persisted resolution still answers the evidence in front of
    /// the reader.
    /// </summary>
    /// <remarks>
    /// For an accepted or corrected resolution the test is the fingerprint:
    /// the staff member settled a specific suggestion, and if the suggestion
    /// has moved, their answer no longer applies.
    ///
    /// A supplied resolution has no fingerprint, because there was no
    /// suggestion — that absence is exactly what it answers. So it stays
    /// current only while the absence holds. If a later re-evaluation produces
    /// a suggestion, the supplied value is superseded and the item reads as
    /// <see cref="InspectionAddressResolutionState.Suggested"/> again, so a
    /// person looks at the newly found evidence rather than a case being
    /// created against a hand-typed address that the source now contradicts.
    /// Applying the fingerprint rule to a supplied resolution instead would
    /// make every supplied address read back as unresolved, and the feature
    /// would silently do nothing.
    ///
    /// Neither rule can fire after acceptance: accepted intake evidence is
    /// immutable, refused above before anything is written.
    /// </remarks>
    private static bool IsStillCurrent(
        PersistedResolution? latest,
        InspectionAddressEvaluation evaluation)
    {
        if (latest is null)
        {
            return false;
        }

        if (string.Equals(latest.State, "supplied", StringComparison.Ordinal))
        {
            return evaluation.Suggestion is null;
        }

        return evaluation.Suggestion is { } suggestion
            && string.Equals(
                latest.SuggestionFingerprint,
                suggestion.Fingerprint,
                StringComparison.Ordinal);
    }

    private static InspectionAddressEvaluation Evaluate(IntakeReceiptEntity receipt) =>
        Ext18InspectionAddressPolicy.Evaluate(
            DeserializeFields(receipt.FieldsJson),
            receipt.ExtractionPolicyKey,
            receipt.ExtractionPolicyVersion);

    private static string ResolveValue(
        InspectionAddressResolutionRequest request,
        string? suggestion)
    {
        if (request.Decision == InspectionAddressStaffDecision.AcceptSuggestion)
        {
            if (!string.IsNullOrWhiteSpace(request.CorrectedValue))
            {
                throw new ArgumentException(
                    "An accepted suggestion cannot also contain a correction.",
                    nameof(request));
            }

            return suggestion!;
        }

        var corrected = request.CorrectedValue!.Trim();
        if (corrected.Length > 1000)
        {
            throw new ArgumentException(
                "The inspection address cannot exceed 1000 characters.",
                nameof(request));
        }
        // The exact-extraction rule applies to a supplied address as much as a
        // corrected one: the assessment mode is something an instruction says,
        // never something an operator can type their way into.
        if (string.Equals(
                corrected,
                Ext18InspectionAddressPolicy.ImageBasedAssessment,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Image Based Assessment can be selected only from an exact extracted instruction.",
                nameof(request));
        }
        if (suggestion is not null
            && string.Equals(corrected, suggestion, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Use acceptance when the inspection-address suggestion is unchanged.",
                nameof(request));
        }

        return corrected;
    }

    private static void Validate(InspectionAddressResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IntakeReceiptId == Guid.Empty
            || request.OperationId == Guid.Empty
            || request.ExpectedReceiptVersion < 0)
        {
            throw new ArgumentException("The address-resolution identity or version is invalid.", nameof(request));
        }
        if (!Enum.IsDefined(request.Decision))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The staff decision is invalid.");
        }
        if (request.Decision == InspectionAddressStaffDecision.SupplyAddress)
        {
            // A fingerprint says the caller was looking at a suggestion, which
            // is the one situation supplying may not cover. Refuse rather than
            // ignore it: the form and the command have to agree about what the
            // operator was shown.
            if (!string.IsNullOrEmpty(request.SuggestionFingerprint))
            {
                throw new ArgumentException(
                    "A supplied inspection address cannot carry a suggestion fingerprint.",
                    nameof(request));
            }
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SuggestionFingerprint);
            if (request.SuggestionFingerprint.Length != 64
                || request.SuggestionFingerprint.Any(character => !char.IsAsciiHexDigit(character)))
            {
                throw new ArgumentException("The suggestion fingerprint is invalid.", nameof(request));
            }
        }
        ArgumentNullException.ThrowIfNull(request.Actor);
        if (request.Actor.Kind != ActorKind.Staff
            || !Guid.TryParse(request.Actor.SubjectId, out var staffId)
            || staffId == Guid.Empty)
        {
            throw new ArgumentException("Inspection-address resolution requires a staff actor.", nameof(request));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CorrelationId);
        if (request.CorrelationId.Length > 100)
        {
            throw new ArgumentException("The correlation identifier cannot exceed 100 characters.", nameof(request));
        }
        if (request.Decision is InspectionAddressStaffDecision.CorrectSuggestion
                or InspectionAddressStaffDecision.SupplyAddress
            && string.IsNullOrWhiteSpace(request.CorrectedValue))
        {
            throw new ArgumentException("An inspection address is required.", nameof(request));
        }
    }

    private static void EnsureDuplicateMatches(
        InspectionAddressResolutionRequest request,
        PersistedResolution duplicate)
    {
        var isSupply = request.Decision == InspectionAddressStaffDecision.SupplyAddress;
        var expectedState = request.Decision switch
        {
            InspectionAddressStaffDecision.AcceptSuggestion => "accepted",
            InspectionAddressStaffDecision.CorrectSuggestion => "corrected",
            _ => "supplied"
        };
        // A supplied resolution persisted no fingerprint, because there was no
        // suggestion to fingerprint. Comparing one would make every replay of a
        // supplied address throw instead of returning what it already wrote.
        if (!string.Equals(duplicate.State, expectedState, StringComparison.Ordinal)
            || (!isSupply
                && !string.Equals(
                    duplicate.SuggestionFingerprint,
                    request.SuggestionFingerprint,
                    StringComparison.Ordinal))
            || (request.Decision != InspectionAddressStaffDecision.AcceptSuggestion
                && !string.Equals(duplicate.Value, request.CorrectedValue?.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The address-resolution operation identifier was already used for a different command.");
        }
    }


    private static string EncodeResolution(PersistedResolution resolution) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(resolution, JsonOptions))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static PersistedResolution? TryReadResolution(PersistedEvidence evidence)
    {
        if (!evidence.Signal.StartsWith(ResolutionSignalPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var encoded = evidence.Signal[ResolutionSignalPrefix.Length..]
                .Replace('-', '+')
                .Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
            return JsonSerializer.Deserialize<PersistedResolution>(
                Convert.FromBase64String(encoded),
                JsonOptions);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new InvalidDataException("The persisted inspection-address resolution is invalid.", exception);
        }
    }

    private static string ToStateCode(InspectionAddressResolutionState state) => state switch
    {
        InspectionAddressResolutionState.Accepted => "accepted",
        InspectionAddressResolutionState.Corrected => "corrected",
        InspectionAddressResolutionState.Supplied => "supplied",
        _ => throw new InvalidOperationException(
            $"Inspection-address resolution state '{state}' is not persistable.")
    };

    private static InspectionAddressResolutionState ParseState(string state) => state switch
    {
        "accepted" => InspectionAddressResolutionState.Accepted,
        "corrected" => InspectionAddressResolutionState.Corrected,
        "supplied" => InspectionAddressResolutionState.Supplied,
        _ => throw new InvalidDataException($"Unknown inspection-address resolution state '{state}'.")
    };

    private static List<PersistedEvidence> DeserializeEvidence(string json) =>
        DeserializeEnvelope<List<PersistedEvidence>>(json);

    private static string SerializeEvidence(List<PersistedEvidence> evidence) =>
        JsonSerializer.Serialize(new VersionedEnvelope<List<PersistedEvidence>>(JsonVersion, evidence), JsonOptions);

    private static InstructionReviewField[] DeserializeFields(string json) =>
        DeserializeEnvelope<IReadOnlyList<PersistedField>>(json)
            .Select(field => new InstructionReviewField(
                field.Name,
                field.SuggestedValue,
                field.Candidates.Select(candidate => new InstructionFieldCandidate(
                    candidate.Value,
                    IntakeEvidenceSourceCodes.Parse(candidate.Source),
                    candidate.SourceLabel)).ToArray(),
                field.IsDefaulted,
                field.HasConflict))
            .ToArray();

    private static T DeserializeEnvelope<T>(string json)
    {
        var envelope = JsonSerializer.Deserialize<VersionedEnvelope<T>>(json, JsonOptions)
            ?? throw new InvalidDataException("The persisted intake JSON envelope is missing.");
        if (envelope.Version != JsonVersion || envelope.Data is null)
        {
            throw new InvalidDataException("The persisted intake JSON envelope is unsupported or incomplete.");
        }

        return envelope.Data;
    }

    private sealed record VersionedEnvelope<T>(int Version, T Data);
    private sealed record PersistedEvidence(
        string Source,
        string Strength,
        string Finding,
        string Signal,
        string Detail);
    private sealed record PersistedField(
        string Name,
        string? SuggestedValue,
        IReadOnlyList<PersistedFieldCandidate> Candidates,
        bool IsDefaulted,
        bool HasConflict);
    private sealed record PersistedFieldCandidate(
        string Value,
        string Source,
        string SourceLabel);
    private sealed record PersistedResolution(
        string State,
        string Value,
        string SuggestionFingerprint,
        Guid StaffId,
        DateTimeOffset OccurredAtUtc,
        Guid OperationId);
    private sealed record AddressHistoryValue(
        long ReceiptVersion,
        string? Value,
        InspectionAddressSuggestion? Suggestion);
}
