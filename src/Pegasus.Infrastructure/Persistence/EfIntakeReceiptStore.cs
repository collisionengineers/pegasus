using System.Data;
using System.Text.Json;
using Pegasus.Core.Intake;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfIntakeReceiptStore(IDbContextFactory<PegasusDbContext> contextFactory)
    : IIntakeReceiptStore, IIntakeReceiptQueries
{
    private const int JsonVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntakeReceipt> StoreAsync(
        IntakeReceiptDraft draft,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await StoreOnceAsync(draft, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                var duplicate = await FindBySourceIdentityAsync(draft.SourceIdentity, cancellationToken);
                if (duplicate is not null)
                {
                    EnsureMatchingContent(duplicate.SourceHash, draft.SourceHash);
                    return duplicate with { IsDuplicate = true };
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("The intake receipt could not be stored after the concurrency retry limit.");
    }

    public async Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var decisions = await context.IntakeReceipts
            .AsNoTracking()
            .Select(item => item.Decision)
            .ToListAsync(cancellationToken);
        var parsedDecisions = decisions.Select(ParseDecision).ToArray();
        return new(
            parsedDecisions.Count(item => item == IntakeDecision.DraftReady),
            parsedDecisions.Count(item => item == IntakeDecision.NeedsSorting));
    }

    public async Task<IReadOnlyList<IntakeReceiptSummary>> ListAsync(
        IntakeDecision? decision,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (decision is not null)
        {
            _ = ToCode(decision.Value);
        }

        var entities = await context.IntakeReceipts
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var summaries = entities
            .Select(item => new IntakeReceiptSummary(
                item.Id,
                item.SourceFileName,
                item.ReceivedAtUtc,
                ParseDecision(item.Decision),
                item.FailureReason))
            .ToArray();
        return summaries
            .Where(item => decision is null || item.Decision == decision.Value)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .Take(100)
            .ToArray();
    }

    public async Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity, false);
    }

    public async Task<IntakeReceipt?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken)
    {
        var channelCode = ToCode(sourceIdentity.Channel);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .SingleOrDefaultAsync(
                item => item.SourceChannel == channelCode
                    && item.ExternalReceiptToken == sourceIdentity.ExternalReceiptToken,
                cancellationToken);
        return entity is null ? null : Map(entity, false);
    }

    public async Task<IntakeAssetRecord?> GetAssetAsync(
        Guid receiptId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IntakeReceiptId == receiptId && item.Id == assetId,
                cancellationToken);
        return entity is null ? null : MapAsset(entity);
    }

    private async Task<IntakeReceipt> StoreOnceAsync(
        IntakeReceiptDraft draft,
        CancellationToken cancellationToken)
    {
        var channelCode = ToCode(draft.SourceIdentity.Channel);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingQuery = context.IntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision);
        if (context.Database.IsSqlServer())
        {
            existingQuery = context.IntakeReceipts
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM [IntakeReceipts] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [SourceChannel] = {channelCode}
                      AND [ExternalReceiptToken] = {draft.SourceIdentity.ExternalReceiptToken}
                """)
                .AsNoTracking()
                .Include(item => item.Assets)
                .Include(item => item.InstructionDraft)
                .Include(item => item.MailRouteDecision);
        }

        var existing = await existingQuery.SingleOrDefaultAsync(
            item => item.SourceChannel == channelCode
                && item.ExternalReceiptToken == draft.SourceIdentity.ExternalReceiptToken,
            cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingContent(existing.SourceHash, draft.SourceHash);
            return Map(existing, true);
        }

        var receipt = new IntakeReceiptEntity
        {
            Id = Guid.NewGuid(),
            SourceFileName = draft.SourceFileName,
            MediaType = draft.MediaType,
            SourceLength = draft.SourceLength,
            SourceHash = draft.SourceHash,
            SourceChannel = channelCode,
            ExternalReceiptToken = draft.SourceIdentity.ExternalReceiptToken,
            ReceivedAtUtc = draft.ReceivedAtUtc,
            ProcessedAtUtc = draft.ProcessedAtUtc,
            SourceReaderKey = draft.SourceReaderKey,
            SourceReaderVersion = draft.SourceReaderVersion,
            ExtractionPolicyKey = draft.ExtractionPolicyKey,
            ExtractionPolicyVersion = draft.ExtractionPolicyVersion,
            Decision = ToCode(draft.Decision),
            DecisionReason = draft.DecisionReason,
            EvidenceJson = SerializeEvidence(draft.Evidence),
            FieldsJson = SerializeFields(draft.Fields),
            OcrCandidatesJson = SerializeEnvelope(draft.ScannedPdfPages),
            FailureCode = draft.FailureCode,
            FailureReason = draft.FailureReason
        };
        if (draft.InstructionDraft is not null)
        {
            receipt.InstructionDraft = new()
            {
                IntakeReceiptId = receipt.Id,
                IntakeReceipt = receipt,
                SuggestedPrincipalCode = draft.InstructionDraft.SuggestedPrincipalCode,
                ClaimantName = draft.InstructionDraft.ClaimantName,
                ClaimNumber = draft.InstructionDraft.ClaimNumber,
                VehicleRegistration = draft.InstructionDraft.VehicleRegistration,
                VehicleMake = draft.InstructionDraft.VehicleMake,
                VehicleModel = draft.InstructionDraft.VehicleModel,
                VehicleMileage = draft.InstructionDraft.VehicleMileage,
                AccidentCircumstances = draft.InstructionDraft.AccidentCircumstances,
                DateOfIncident = draft.InstructionDraft.DateOfIncident,
                InstructionDate = draft.InstructionDraft.InstructionDate,
                InspectionAddress = draft.InstructionDraft.InspectionAddress
            };
        }

        if (draft.MailRouteDecision is not null)
        {
            receipt.MailRouteDecision = MapMailRouteDecision(draft.MailRouteDecision, receipt);
        }

        receipt.Assets.AddRange(draft.AssetRecords.Select(asset => new IntakeAssetEntity
        {
            Id = asset.Id,
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            SourceLabel = asset.SourceLabel,
            FileName = asset.FileName,
            MediaType = asset.MediaType,
            Kind = ToCode(asset.Kind),
            Disposition = ToCode(asset.Disposition),
            ContentLength = asset.ContentLength,
            ContentHash = asset.ContentHash,
            StorageKey = asset.StorageKey,
            PageNumber = asset.PageNumber,
            BoundsJson = asset.Bounds is null ? null : SerializeEnvelope(asset.Bounds),
            WidthPixels = asset.WidthPixels,
            HeightPixels = asset.HeightPixels
        }));
        context.IntakeReceipts.Add(receipt);
        context.IntakeReceiptEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            EventType = "intake_receipt_recorded",
            Actor = draft.Actor,
            OccurredAtUtc = draft.ProcessedAtUtc,
            DetailsJson = SerializeEnvelope(new IntakeReceiptEventDetails(
                ToCode(draft.Decision),
                channelCode,
                draft.SourceIdentity.ExternalReceiptToken,
                draft.SourceHash))
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(receipt, false);
    }

    private static IntakeReceipt Map(IntakeReceiptEntity entity, bool isDuplicate)
    {
        var fields = DeserializeFields(entity.FieldsJson);
        return new(
            entity.Id,
            entity.SourceFileName,
            entity.MediaType,
            entity.SourceLength,
            entity.SourceHash,
            new(ParseSourceChannel(entity.SourceChannel), entity.ExternalReceiptToken),
            entity.ReceivedAtUtc,
            entity.ProcessedAtUtc,
            ParseDecision(entity.Decision),
            entity.DecisionReason,
            DeserializeEvidence(entity.EvidenceJson),
            fields,
            entity.InstructionDraft is null ? null : MapInstructionDraft(entity.InstructionDraft),
            fields.Where(field => field.Candidates.Count == 0).Select(field => field.Name).ToArray(),
            entity.FailureCode,
            entity.FailureReason,
            isDuplicate,
            entity.SourceReaderKey,
            entity.SourceReaderVersion,
            entity.ExtractionPolicyKey,
            entity.ExtractionPolicyVersion,
            entity.Assets.OrderBy(asset => asset.Id).Select(MapAsset).ToArray(),
            DeserializeEnvelope<IReadOnlyList<ScannedPdfOcrCandidate>>(entity.OcrCandidatesJson) ?? [],
            entity.MailRouteDecision is null ? null : MapMailRouteDecision(entity.MailRouteDecision));
    }

    private static InstructionDraft MapInstructionDraft(InstructionDraftEntity entity) => new(
        entity.SuggestedPrincipalCode,
        entity.ClaimantName,
        entity.ClaimNumber,
        entity.VehicleRegistration,
        entity.VehicleMake,
        entity.VehicleModel,
        entity.VehicleMileage,
        entity.AccidentCircumstances,
        entity.DateOfIncident,
        entity.InstructionDate,
        entity.InspectionAddress);

    private static IntakeMailRouteDecisionEntity MapMailRouteDecision(
        MailRouteEvaluationResult decision,
        IntakeReceiptEntity receipt) =>
        new()
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            Disposition = ToCode(decision.Disposition),
            RouteOwnerCode = decision.SelectedRoute?.RouteOwnerCode,
            RouteKind = decision.SelectedRoute is null ? null : ToCode(decision.SelectedRoute.Kind),
            WorkProviderCode = decision.SelectedRoute?.WorkProviderCode,
            PredicatesJson = SerializeEnvelope(decision.Predicates),
            Reason = decision.Reason,
            PolicyKey = decision.PolicyKey,
            PolicyVersion = decision.PolicyVersion,
            TransportIdentitiesJson = SerializeEnvelope(decision.TransportIdentities),
            OriginalIdentitiesJson = SerializeEnvelope(decision.OriginalIdentities),
            EffectiveSenderAddress = decision.EffectiveSender?.Address,
            EffectiveSenderSourceLabel = decision.EffectiveSender?.SourceLabel
        };

    private static MailRouteEvaluationResult MapMailRouteDecision(
        IntakeMailRouteDecisionEntity entity)
    {
        var hasAnySelectionValue = entity.RouteOwnerCode is not null
            || entity.RouteKind is not null
            || entity.WorkProviderCode is not null;
        var hasCompleteSelection = entity.RouteOwnerCode is not null
            && entity.RouteKind is not null
            && entity.WorkProviderCode is not null;
        if (hasAnySelectionValue != hasCompleteSelection)
        {
            throw new InvalidDataException(
                "The persisted mail-route selection is incomplete.");
        }

        var hasAnyEffectiveSenderValue = entity.EffectiveSenderAddress is not null
            || entity.EffectiveSenderSourceLabel is not null;
        var hasCompleteEffectiveSender = entity.EffectiveSenderAddress is not null
            && entity.EffectiveSenderSourceLabel is not null;
        if (hasAnyEffectiveSenderValue != hasCompleteEffectiveSender)
        {
            throw new InvalidDataException(
                "The persisted effective sender identity is incomplete.");
        }

        return new(
            ParseMailRouteDisposition(entity.Disposition),
            hasCompleteSelection
                ? new(
                    entity.RouteOwnerCode!,
                    ParseMailRouteKind(entity.RouteKind!),
                    entity.WorkProviderCode!)
                : null,
            DeserializeEnvelope<IReadOnlyList<MailRoutePredicateResult>>(entity.PredicatesJson),
            entity.Reason,
            entity.PolicyKey,
            entity.PolicyVersion,
            DeserializeEnvelope<IReadOnlyList<MailRouteIdentity>>(entity.TransportIdentitiesJson),
            DeserializeEnvelope<IReadOnlyList<MailRouteIdentity>>(entity.OriginalIdentitiesJson),
            hasCompleteEffectiveSender
                ? new(entity.EffectiveSenderAddress!, entity.EffectiveSenderSourceLabel!)
                : null);
    }

    private static IntakeAssetRecord MapAsset(IntakeAssetEntity entity) => new(
        entity.Id,
        entity.SourceLabel,
        entity.FileName,
        entity.MediaType,
        ParseAssetKind(entity.Kind),
        ParseAssetDisposition(entity.Disposition),
        entity.ContentLength,
        entity.ContentHash,
        entity.StorageKey,
        entity.PageNumber,
        entity.BoundsJson is null ? null : DeserializeEnvelope<IntakeAssetBounds>(entity.BoundsJson),
        entity.WidthPixels,
        entity.HeightPixels);

    private static string SerializeEvidence(IReadOnlyList<IntakeEvidence> evidence) =>
        SerializeEnvelope<IReadOnlyList<PersistedEvidence>>(evidence.Select(item => new PersistedEvidence(
            ToCode(item.Source),
            ToCode(item.Strength),
            ToCode(item.Finding),
            item.Signal,
            item.Detail)).ToArray());

    private static IntakeEvidence[] DeserializeEvidence(string json) =>
        (DeserializeEnvelope<IReadOnlyList<PersistedEvidence>>(json) ?? [])
        .Select(item => new IntakeEvidence(
            ParseEvidenceSource(item.Source),
            ParseEvidenceStrength(item.Strength),
            ParseEvidenceFinding(item.Finding),
            item.Signal,
            item.Detail))
        .ToArray();

    private static string SerializeFields(IReadOnlyList<InstructionReviewField> fields) =>
        SerializeEnvelope<IReadOnlyList<PersistedField>>(fields.Select(field => new PersistedField(
            field.Name,
            field.SuggestedValue,
            field.Candidates.Select(candidate => new PersistedFieldCandidate(
                candidate.Value,
                ToCode(candidate.Source),
                candidate.SourceLabel)).ToArray(),
            field.IsDefaulted,
            field.HasConflict)).ToArray());

    private static InstructionReviewField[] DeserializeFields(string json) =>
        (DeserializeEnvelope<IReadOnlyList<PersistedField>>(json) ?? [])
        .Select(field => new InstructionReviewField(
            field.Name,
            field.SuggestedValue,
            field.Candidates.Select(candidate => new InstructionFieldCandidate(
                candidate.Value,
                ParseEvidenceSource(candidate.Source),
                candidate.SourceLabel)).ToArray(),
            field.IsDefaulted,
            field.HasConflict))
        .ToArray();

    private static string SerializeEnvelope<T>(T data) =>
        JsonSerializer.Serialize(new VersionedEnvelope<T>(JsonVersion, data), JsonOptions);

    private static T DeserializeEnvelope<T>(string json)
    {
        var envelope = JsonSerializer.Deserialize<VersionedEnvelope<T>>(json, JsonOptions)
            ?? throw new InvalidDataException("The persisted intake JSON envelope is missing.");
        if (envelope.Version != JsonVersion)
        {
            throw new InvalidDataException($"Unsupported persisted intake JSON version '{envelope.Version}'.");
        }

        return envelope.Data
            ?? throw new InvalidDataException("The persisted intake JSON envelope has no data.");
    }

    private static string ToCode(MailRouteDisposition value) => value switch
    {
        MailRouteDisposition.Accepted => "accepted",
        MailRouteDisposition.NoMatch => "no_match",
        MailRouteDisposition.NeedsSorting => "needs_sorting",
        _ => throw UnknownEnum(value)
    };

    private static MailRouteDisposition ParseMailRouteDisposition(string value) => value switch
    {
        "accepted" => MailRouteDisposition.Accepted,
        "no_match" => MailRouteDisposition.NoMatch,
        "needs_sorting" => MailRouteDisposition.NeedsSorting,
        _ => throw UnknownCode("mail-route disposition", value)
    };

    private static string ToCode(MailRouteKind value) => value switch
    {
        MailRouteKind.DirectProvider => "direct_provider",
        MailRouteKind.Intermediary => "intermediary",
        _ => throw UnknownEnum(value)
    };

    private static MailRouteKind ParseMailRouteKind(string value) => value switch
    {
        "direct_provider" => MailRouteKind.DirectProvider,
        "intermediary" => MailRouteKind.Intermediary,
        _ => throw UnknownCode("mail-route kind", value)
    };

    private static string ToCode(IntakeDecision value) => value switch
    {
        IntakeDecision.DraftReady => "draft_ready",
        IntakeDecision.NeedsSorting => "needs_sorting",
        IntakeDecision.Unsupported => "unsupported",
        IntakeDecision.OcrRequired => "ocr_required",
        IntakeDecision.TechnicalFailure => "technical_failure",
        _ => throw UnknownEnum(value)
    };

    private static IntakeDecision ParseDecision(string value) => value switch
    {
        "draft_ready" => IntakeDecision.DraftReady,
        "needs_sorting" => IntakeDecision.NeedsSorting,
        "unsupported" => IntakeDecision.Unsupported,
        "ocr_required" => IntakeDecision.OcrRequired,
        "technical_failure" => IntakeDecision.TechnicalFailure,
        _ => throw UnknownCode("decision", value)
    };

    private static string ToCode(IntakeSourceChannel value) => value switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        IntakeSourceChannel.Mailbox => "mailbox",
        _ => throw UnknownEnum(value)
    };

    private static IntakeSourceChannel ParseSourceChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        _ => throw UnknownCode("source channel", value)
    };

    private static string ToCode(IntakeEvidenceSource value) => value switch
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
        _ => throw UnknownEnum(value)
    };

    private static IntakeEvidenceSource ParseEvidenceSource(string value) => value switch
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
        _ => throw UnknownCode("evidence source", value)
    };

    private static string ToCode(IntakeEvidenceStrength value) => value switch
    {
        IntakeEvidenceStrength.Strong => "strong",
        IntakeEvidenceStrength.Weak => "weak",
        _ => throw UnknownEnum(value)
    };

    private static IntakeEvidenceStrength ParseEvidenceStrength(string value) => value switch
    {
        "strong" => IntakeEvidenceStrength.Strong,
        "weak" => IntakeEvidenceStrength.Weak,
        _ => throw UnknownCode("evidence strength", value)
    };

    private static string ToCode(IntakeEvidenceFinding value) => value switch
    {
        IntakeEvidenceFinding.SupportsPrincipal => "supports_principal",
        IntakeEvidenceFinding.ContradictsTransport => "contradicts_transport",
        IntakeEvidenceFinding.ExtractedField => "extracted_field",
        IntakeEvidenceFinding.ConflictingField => "conflicting_field",
        IntakeEvidenceFinding.MissingField => "missing_field",
        IntakeEvidenceFinding.Information => "information",
        _ => throw UnknownEnum(value)
    };

    private static IntakeEvidenceFinding ParseEvidenceFinding(string value) => value switch
    {
        "supports_principal" => IntakeEvidenceFinding.SupportsPrincipal,
        "contradicts_transport" => IntakeEvidenceFinding.ContradictsTransport,
        "extracted_field" => IntakeEvidenceFinding.ExtractedField,
        "conflicting_field" => IntakeEvidenceFinding.ConflictingField,
        "missing_field" => IntakeEvidenceFinding.MissingField,
        "information" => IntakeEvidenceFinding.Information,
        _ => throw UnknownCode("evidence finding", value)
    };

    private static string ToCode(IntakeAssetKind value) => value switch
    {
        IntakeAssetKind.Source => "source",
        IntakeAssetKind.Attachment => "attachment",
        IntakeAssetKind.InlineImage => "inline_image",
        IntakeAssetKind.EmbeddedImage => "embedded_image",
        _ => throw UnknownEnum(value)
    };

    private static IntakeAssetKind ParseAssetKind(string value) => value switch
    {
        "source" => IntakeAssetKind.Source,
        "attachment" => IntakeAssetKind.Attachment,
        "inline_image" => IntakeAssetKind.InlineImage,
        "embedded_image" => IntakeAssetKind.EmbeddedImage,
        _ => throw UnknownCode("asset kind", value)
    };

    private static string ToCode(IntakeAssetDisposition value) => value switch
    {
        IntakeAssetDisposition.Source => "source",
        IntakeAssetDisposition.Attachment => "attachment",
        IntakeAssetDisposition.Inline => "inline",
        IntakeAssetDisposition.Embedded => "embedded",
        _ => throw UnknownEnum(value)
    };

    private static IntakeAssetDisposition ParseAssetDisposition(string value) => value switch
    {
        "source" => IntakeAssetDisposition.Source,
        "attachment" => IntakeAssetDisposition.Attachment,
        "inline" => IntakeAssetDisposition.Inline,
        "embedded" => IntakeAssetDisposition.Embedded,
        _ => throw UnknownCode("asset disposition", value)
    };

    private static InvalidDataException UnknownCode(string kind, string value) =>
        new($"Unknown persisted intake {kind} code '{value}'.");

    private static InvalidOperationException UnknownEnum<T>(T value) where T : struct, Enum =>
        new($"Unknown {typeof(T).Name} value '{Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)}'.");

    private static void EnsureMatchingContent(string existingSourceHash, string sourceHash)
    {
        if (!string.Equals(existingSourceHash, sourceHash, StringComparison.Ordinal))
        {
            throw new IntakeSourceIdentityConflictException();
        }
    }

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        SqliteException { SqliteErrorCode: 5 or 6 } => true,
        SqliteException { SqliteExtendedErrorCode: 1555 or 2067 } => true,
        _ when exception.InnerException is not null => IsRetryableConcurrencyFailure(exception.InnerException),
        _ => false
    };

    private sealed record VersionedEnvelope<T>(int Version, T Data);
    private sealed record PersistedEvidence(string Source, string Strength, string Finding, string Signal, string Detail);
    private sealed record PersistedField(
        string Name,
        string? SuggestedValue,
        IReadOnlyList<PersistedFieldCandidate> Candidates,
        bool IsDefaulted,
        bool HasConflict);
    private sealed record PersistedFieldCandidate(string Value, string Source, string SourceLabel);
    private sealed record IntakeReceiptEventDetails(
        string Decision,
        string SourceChannel,
        string ExternalReceiptToken,
        string SourceHash);
}
