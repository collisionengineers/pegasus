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
        var suggestion = evaluation.Suggestion
            ?? throw new InvalidOperationException(
                "Missing or conflicting inspection-address evidence cannot be resolved by this policy.");
        if (!string.Equals(
                suggestion.Fingerprint,
                request.SuggestionFingerprint,
                StringComparison.Ordinal))
        {
            throw new InspectionAddressResolutionConcurrencyException();
        }

        if (receipt.InstructionDraft is null)
        {
            throw new InvalidOperationException(
                "An inspection address can be resolved only for an instruction draft.");
        }

        var previousValue = receipt.InstructionDraft.InspectionAddress;
        var resolvedValue = ResolveValue(request, suggestion.Value);
        var resolvedAtUtc = timeProvider.GetUtcNow();
        if (resolvedAtUtc.Offset != TimeSpan.Zero)
        {
            resolvedAtUtc = resolvedAtUtc.ToUniversalTime();
        }

        var staffId = Guid.Parse(request.Actor.SubjectId);
        var state = request.Decision == InspectionAddressStaffDecision.AcceptSuggestion
            ? InspectionAddressResolutionState.Accepted
            : InspectionAddressResolutionState.Corrected;
        var persistedResolution = new PersistedResolution(
            state == InspectionAddressResolutionState.Accepted ? "accepted" : "corrected",
            resolvedValue,
            suggestion.Fingerprint,
            staffId,
            resolvedAtUtc,
            request.OperationId);
        var humanDetail = state == InspectionAddressResolutionState.Accepted
            ? "Inspection address accepted by staff from extracted intake evidence."
            : "Inspection address corrected by staff from extracted intake evidence.";
        evidence.Add(new(
            ToCode(suggestion.Provenance[0].Source),
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
            EventKind = state == InspectionAddressResolutionState.Accepted
                ? "inspection_address_accepted"
                : "inspection_address_corrected",
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
        var current = latest is not null
            && evaluation.Suggestion is { } suggestion
            && string.Equals(latest.SuggestionFingerprint, suggestion.Fingerprint, StringComparison.Ordinal)
            ? latest
            : null;
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

    private static InspectionAddressEvaluation Evaluate(IntakeReceiptEntity receipt) =>
        Ext18InspectionAddressPolicy.Evaluate(
            DeserializeFields(receipt.FieldsJson),
            receipt.ExtractionPolicyKey,
            receipt.ExtractionPolicyVersion);

    private static string ResolveValue(
        InspectionAddressResolutionRequest request,
        string suggestion)
    {
        if (request.Decision == InspectionAddressStaffDecision.AcceptSuggestion)
        {
            if (!string.IsNullOrWhiteSpace(request.CorrectedValue))
            {
                throw new ArgumentException(
                    "An accepted suggestion cannot also contain a correction.",
                    nameof(request));
            }

            return suggestion;
        }

        var corrected = request.CorrectedValue!.Trim();
        if (corrected.Length > 1000)
        {
            throw new ArgumentException(
                "The corrected inspection address cannot exceed 1000 characters.",
                nameof(request));
        }
        if (string.Equals(
                corrected,
                Ext18InspectionAddressPolicy.ImageBasedAssessment,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Image Based Assessment can be selected only from an exact extracted instruction.",
                nameof(request));
        }
        if (string.Equals(corrected, suggestion, StringComparison.Ordinal))
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
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SuggestionFingerprint);
        if (request.SuggestionFingerprint.Length != 64
            || request.SuggestionFingerprint.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The suggestion fingerprint is invalid.", nameof(request));
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
        if (request.Decision == InspectionAddressStaffDecision.CorrectSuggestion
            && string.IsNullOrWhiteSpace(request.CorrectedValue))
        {
            throw new ArgumentException("A corrected inspection address is required.", nameof(request));
        }
    }

    private static void EnsureDuplicateMatches(
        InspectionAddressResolutionRequest request,
        PersistedResolution duplicate)
    {
        var expectedState = request.Decision == InspectionAddressStaffDecision.AcceptSuggestion
            ? "accepted"
            : "corrected";
        if (!string.Equals(duplicate.State, expectedState, StringComparison.Ordinal)
            || !string.Equals(
                duplicate.SuggestionFingerprint,
                request.SuggestionFingerprint,
                StringComparison.Ordinal)
            || (request.Decision == InspectionAddressStaffDecision.CorrectSuggestion
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

    private static InspectionAddressResolutionState ParseState(string state) => state switch
    {
        "accepted" => InspectionAddressResolutionState.Accepted,
        "corrected" => InspectionAddressResolutionState.Corrected,
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
                    ParseSource(candidate.Source),
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

    private static string ToCode(IntakeEvidenceSource source) => source switch
    {
        IntakeEvidenceSource.EmailBody => "email_body",
        IntakeEvidenceSource.PdfContent => "pdf_content",
        IntakeEvidenceSource.DocumentContent => "document_content",
        IntakeEvidenceSource.ImageContent => "image_content",
        IntakeEvidenceSource.Sender => "sender",
        IntakeEvidenceSource.Subject => "subject",
        IntakeEvidenceSource.FileName => "file_name",
        IntakeEvidenceSource.MimeType => "mime_type",
        IntakeEvidenceSource.SystemDefault => "system_default",
        _ => throw new InvalidOperationException($"Unknown intake evidence source '{(int)source}'.")
    };

    private static IntakeEvidenceSource ParseSource(string source) => source switch
    {
        "email_body" => IntakeEvidenceSource.EmailBody,
        "pdf_content" => IntakeEvidenceSource.PdfContent,
        "document_content" => IntakeEvidenceSource.DocumentContent,
        "image_content" => IntakeEvidenceSource.ImageContent,
        "sender" => IntakeEvidenceSource.Sender,
        "subject" => IntakeEvidenceSource.Subject,
        "file_name" => IntakeEvidenceSource.FileName,
        "mime_type" => IntakeEvidenceSource.MimeType,
        "system_default" => IntakeEvidenceSource.SystemDefault,
        _ => throw new InvalidDataException($"Unknown persisted intake evidence source '{source}'.")
    };

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
