using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Repair specifications and named estimates share one table and one
/// aggregate. <see cref="StartDraftAsync"/> / <see cref="AcceptAsync"/> are
/// the ENG-002 single-canonical-draft path (import, typed acceptance,
/// reasoned correction); the estimate methods are the ENG-026 named-estimate
/// path where a case holds several Drafts and Accepted estimates and exactly
/// one is Current. Both paths write the same history and the same
/// replay-by-operation-key.
/// </summary>
public sealed class EfRepairSpecificationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IRepairSpecificationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RepairSpecificationVersion> StartDraftAsync(
        StartRepairSpecificationDraftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        var source = request.Source.Route == RepairSpecificationSourceRoute.LegacyUnresolved
            ? request.Source
            : RepairSpecificationPolicy.ValidateSource(request.Source);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        Guard(workflow, request.ExpectedCaseVersion, request.Actor, request.EditLeaseToken, Now());
        if (await DraftQuery(context, request.CaseId).AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("A current repair-specification draft already exists for this case.");
        }

        CaseRepairSpecificationEntity? predecessor = null;
        if (request.SupersedesSpecificationId is { } predecessorId)
        {
            predecessor = await context.CaseRepairSpecifications
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(
                    item => item.Id == predecessorId && item.CaseId == request.CaseId,
                    cancellationToken)
                ?? throw new InvalidOperationException("The repair specification being corrected was not found.");
            if (predecessor.State != RepairSpecificationState.Accepted.ToString())
            {
                throw new InvalidOperationException("A correction must supersede the accepted repair specification.");
            }
        }
        else if (await AcceptedQuery(context, request.CaseId).AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The accepted repair specification is immutable; start a reasoned correction that identifies it.");
        }

        var nextVersion = await NextVersionAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        var entity = new CaseRepairSpecificationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Case = workflow.Case,
            Version = nextVersion,
            State = RepairSpecificationState.Draft.ToString(),
            SourceRoute = source.Route.ToString(),
            SourceArtifactReference = source.ArtifactReference,
            SourceVersion = source.SourceVersion,
            SourceSha256 = source.Sha256,
            CreatedBy = request.Actor.SubjectId,
            CreationOperationKey = request.OperationKey,
            CreatedAtUtc = now,
            SupersedesSpecificationId = predecessor?.Id,
            SupersessionReason = predecessor is null ? null : RequiredReason(request.Reason),
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? predecessor?.Name ?? DefaultName(nextVersion)
                : request.Name.Trim(),
            VatPercent = predecessor?.VatPercent ?? EstimatePolicy.DefaultVatPercent,
            RepairDays = predecessor?.RepairDays,
            LabourRate = predecessor?.LabourRate,
            PaintLabourRate = predecessor?.PaintLabourRate,
            PaintMaterials = predecessor?.PaintMaterials,
            OtherCosts = predecessor?.OtherCosts,
            Notes = predecessor?.Notes,
        };
        context.CaseRepairSpecifications.Add(entity);
        if (predecessor is not null)
        {
            foreach (var line in predecessor.Lines.OrderBy(item => item.Position))
            {
                context.CaseEstimateLines.Add(CloneLine(line, entity, request.Actor, now));
            }
        }
        else if (request.Lines is { } suppliedLines)
        {
            AddLines(context, entity, AssessmentPolicy.NormalizeRepairSpecificationLines(suppliedLines), request.Actor, now);
        }
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "repair_specification_draft_started", requestHash,
            new { entity.Id, entity.Version }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion> AcceptAsync(
        AcceptRepairSpecificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        var source = RepairSpecificationPolicy.ValidateSource(request.Source);
        var basis = RepairSpecificationPolicy.ValidateCalculationBasis(request.CalculationBasis);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedCaseVersion, request.Actor, request.EditLeaseToken, now);
        var entity = await RequiredEstimateAsync(context, request.CaseId, request.SpecificationId, cancellationToken);
        if (entity.Version != request.ExpectedSpecificationVersion)
        {
            throw new InvalidOperationException("The repair-specification version is stale.");
        }
        var candidate = Map(entity) with { Source = source, CalculationBasis = basis };
        RepairSpecificationPolicy.ValidateAcceptance(candidate, request.Actor);
        if (await context.CaseRepairSpecifications.AnyAsync(
                item => item.CaseId == request.CaseId && item.Id != entity.Id
                    && item.IsCurrent
                    && item.Id != entity.SupersedesSpecificationId,
                cancellationToken))
        {
            throw new InvalidOperationException("A current accepted repair specification already exists; start a reasoned correction.");
        }
        if (entity.SupersedesSpecificationId is { } predecessorId)
        {
            var predecessor = await context.CaseRepairSpecifications.SingleAsync(
                item => item.Id == predecessorId,
                cancellationToken);
            predecessor.State = RepairSpecificationState.Superseded.ToString();
            predecessor.IsCurrent = false;
            await context.SaveChangesAsync(cancellationToken);
        }
        entity.SourceRoute = source.Route.ToString();
        entity.SourceArtifactReference = source.ArtifactReference;
        entity.SourceVersion = source.SourceVersion;
        entity.SourceSha256 = source.Sha256;
        Accept(entity, basis, request.Actor, now);
        entity.IsCurrent = true;
        entity.LastOperationKey = request.OperationKey;
        // The current estimate's accepted breakdown is a frozen report
        // input: a new acceptance stales the current generation here, in the
        // same transaction.
        await EfCaseReportGenerationStore.MarkStaleAsync(
            context, request.CaseId, "estimate_accepted", now, cancellationToken);
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "repair_specification_accepted", requestHash,
            new { entity.Id, entity.Version }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion> SaveEstimateAsync(
        SaveEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);

        CaseRepairSpecificationEntity entity;
        string eventType;
        var editingCurrent = false;
        if (request.EstimateId is { } estimateId)
        {
            entity = await RequiredEstimateAsync(context, request.CaseId, estimateId, cancellationToken);
            editingCurrent = entity.IsCurrent;
            EstimatePolicy.ValidateEditable(Map(entity), request.Actor);
            context.CaseEstimateLines.RemoveRange(entity.Lines);
            entity.Lines.Clear();
            eventType = "estimate_updated";
        }
        else
        {
            entity = new CaseRepairSpecificationEntity
            {
                Id = Guid.NewGuid(),
                CaseId = request.CaseId,
                Case = workflow.Case,
                Version = await NextVersionAsync(context, request.CaseId, cancellationToken),
                State = RepairSpecificationState.Draft.ToString(),
                SourceRoute = request.Source.Route.ToString(),
                CreatedBy = request.Actor.SubjectId,
                CreationOperationKey = request.OperationKey,
                CreatedAtUtc = now,
                Name = request.Details.Name,
                AiJobId = request.AiJobId,
            };
            context.CaseRepairSpecifications.Add(entity);
            eventType = "estimate_created";
        }
        entity.SourceRoute = request.Source.Route.ToString();
        entity.SourceArtifactReference = request.Source.ArtifactReference;
        entity.SourceVersion = request.Source.SourceVersion;
        entity.SourceSha256 = request.Source.Sha256;
        entity.AiJobId = request.AiJobId ?? entity.AiJobId;
        entity.LastOperationKey = request.OperationKey;
        ApplyDetails(entity, request.Details);
        AddLines(context, entity, request.Lines, request.Actor, now);
        if (editingCurrent)
        {
            // Editing the current estimate changes the breakdown a frozen
            // report pinned; a Draft-only save never stales a generation.
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, request.CaseId, "current_estimate_saved", now, cancellationToken);
        }
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            eventType, requestHash, new { entity.Id, entity.Version, entity.Name, Lines = request.Lines.Count }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion> DuplicateEstimateAsync(
        DuplicateEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var original = await RequiredEstimateAsync(context, request.CaseId, request.EstimateId, cancellationToken);
        EstimatePolicy.ValidateDuplicate(Map(original));

        // A copy is the Engineer's own working estimate: it keeps the figures
        // and lines but not the document provenance or the AI job of the
        // original, and its name is bounded like any typed name.
        var name = original.Name + EstimatePolicy.CopySuffix;
        var entity = new CaseRepairSpecificationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Case = workflow.Case,
            Version = await NextVersionAsync(context, request.CaseId, cancellationToken),
            State = RepairSpecificationState.Draft.ToString(),
            SourceRoute = RepairSpecificationSourceRoute.Manual.ToString(),
            CreatedBy = request.Actor.SubjectId,
            CreationOperationKey = request.OperationKey,
            CreatedAtUtc = now,
            LastOperationKey = request.OperationKey,
            Name = name.Length <= EstimatePolicy.MaximumNameLength
                ? name
                : name[..EstimatePolicy.MaximumNameLength],
            RepairDays = original.RepairDays,
            LabourRate = original.LabourRate,
            PaintLabourRate = original.PaintLabourRate,
            PaintMaterials = original.PaintMaterials,
            OtherCosts = original.OtherCosts,
            VatPercent = original.VatPercent,
            Notes = original.Notes,
        };
        context.CaseRepairSpecifications.Add(entity);
        foreach (var line in original.Lines.OrderBy(item => item.Position))
        {
            context.CaseEstimateLines.Add(CloneLine(line, entity, request.Actor, now));
        }
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "estimate_duplicated", requestHash,
            new { entity.Id, entity.Version, entity.Name, SourceEstimateId = original.Id }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion> DiscardEstimateAsync(
        DiscardEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var entity = await RequiredEstimateAsync(context, request.CaseId, request.EstimateId, cancellationToken);
        EstimatePolicy.ValidateDiscard(Map(entity));
        var discardingCurrent = entity.IsCurrent;
        entity.State = RepairSpecificationState.Discarded.ToString();
        entity.DiscardedBy = request.Actor.SubjectId;
        entity.DiscardedAtUtc = now;
        entity.DiscardReason = RequiredReason(request.Reason);
        entity.LastOperationKey = request.OperationKey;
        if (discardingCurrent)
        {
            // Discarding the current estimate removes the breakdown a frozen
            // report pinned; discarding a Draft changes nothing it froze.
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, request.CaseId, "current_estimate_discarded", now, cancellationToken);
        }
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "estimate_discarded", requestHash, new { entity.Id, entity.Version, entity.Name }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion> SetCurrentEstimateAsync(
        SetCurrentEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var entity = await RequiredEstimateAsync(context, request.CaseId, request.EstimateId, cancellationToken);

        // "Use estimate" is the Engineer's acceptance of a Draft: their act
        // confirms every line it carries, and the calculation basis is the
        // one totals owner's figures at this moment.
        if (entity.State == RepairSpecificationState.Draft.ToString())
        {
            foreach (var line in entity.Lines)
            {
                line.ConfirmedBy = request.Actor.SubjectId;
                line.ConfirmedAtUtc = now;
            }
        }
        var candidate = Map(entity);
        EstimatePolicy.ValidateSetCurrent(candidate, request.Actor);
        if (candidate.State == RepairSpecificationState.Draft)
        {
            Accept(entity, EstimatePolicy.BasisFor(candidate), request.Actor, now);
        }

        // The previous Current is cleared in the same transaction; the
        // filtered unique index refuses two Current rows on one case.
        var previous = await context.CaseRepairSpecifications
            .Where(item => item.CaseId == request.CaseId && item.IsCurrent && item.Id != entity.Id)
            .ToListAsync(cancellationToken);
        foreach (var item in previous)
        {
            item.IsCurrent = false;
        }
        if (previous.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        entity.IsCurrent = true;
        entity.LastOperationKey = request.OperationKey;
        // Currency moved: the newly current estimate's figures are what the
        // next generation freezes, so any existing one is stale now.
        await EfCaseReportGenerationStore.MarkStaleAsync(
            context, request.CaseId, "estimate_set_current", now, cancellationToken);
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "estimate_set_current", requestHash,
            new { entity.Id, entity.Version, entity.Name, Previous = previous.Select(item => item.Id).ToArray() }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<RepairSpecificationVersion>> ListEstimatesAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.Version)
            .ToArrayAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    /// <summary>
    /// The keyset-paged sibling of <see cref="ListEstimatesAsync"/>
    /// (CASE-047): newest version first, then estimate id. Projects the
    /// bounded <see cref="CaseEstimatePageItem"/> header (Stream A review)
    /// and never includes <see cref="CaseRepairSpecificationEntity.Lines"/> —
    /// a case can carry many superseded versions, each with an unbounded
    /// line list a keyset page never needs.
    /// </summary>
    public async Task<IReadOnlyList<CaseEstimatePageItem>> ListByCursorAsync(
        Guid caseId,
        int? afterVersion,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = context.CaseRepairSpecifications.AsNoTracking()
            .Where(item => item.CaseId == caseId);
        if (afterId is { } id)
        {
            var afterValue = afterVersion!.Value;
            rows = rows.Where(item =>
                item.Version < afterValue
                || (item.Version == afterValue && item.Id < id));
        }

        var entities = await rows
            .OrderByDescending(item => item.Version)
            .ThenByDescending(item => item.Id)
            .Take(fetchCount)
            .ToArrayAsync(cancellationToken);
        return entities.Select(MapPageItem).ToArray();
    }

    public async Task<RepairSpecificationVersion?> GetVersionAsync(
        Guid caseId, Guid specificationId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.CaseId == caseId && item.Id == specificationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(
        Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await AcceptedQuery(context, caseId).AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<RepairSpecificationVersion?> GetCurrentDraftAsync(
        Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await DraftQuery(context, caseId).AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    /// <summary>
    /// The current-draft and current-accepted predicates are the single
    /// owner of "what row is the current specification for a case", shared
    /// with <see cref="EfCaseAssessmentStore"/>'s legacy implicit-draft path
    /// so the two stores never diverge on what "current" means. With named
    /// estimates a case may hold several drafts; the current draft is the
    /// latest one, and the current accepted specification is the estimate
    /// marked Current.
    /// </summary>
    internal static IQueryable<CaseRepairSpecificationEntity> DraftQuery(
        PegasusDbContext context, Guid caseId) => context.CaseRepairSpecifications
        .Where(item => item.CaseId == caseId
            && item.State == RepairSpecificationState.Draft.ToString())
        .OrderByDescending(item => item.Version)
        .Take(1);

    internal static IQueryable<CaseRepairSpecificationEntity> AcceptedQuery(
        PegasusDbContext context, Guid caseId) => context.CaseRepairSpecifications
        .Where(item => item.CaseId == caseId && item.IsCurrent);

    internal static async Task<int> NextVersionAsync(
        PegasusDbContext context, Guid caseId, CancellationToken cancellationToken) =>
        (await context.CaseRepairSpecifications
            .Where(item => item.CaseId == caseId)
            .MaxAsync(item => (int?)item.Version, cancellationToken) ?? 0) + 1;

    /// <summary>
    /// The one shape a repair specification takes when a legacy assessment
    /// save implicitly opens it (no explicit source evidence yet, actor
    /// authority already checked by the caller). Kept separate from
    /// <see cref="StartDraftAsync"/>'s entity construction, which is the
    /// explicit, source-validated, supersession-aware workflow.
    /// </summary>
    internal static CaseRepairSpecificationEntity NewLegacyDraft(
        Guid caseId, CaseEntity @case, int version, string createdBy, string operationKey, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        Case = @case,
        Version = version,
        State = RepairSpecificationState.Draft.ToString(),
        SourceRoute = RepairSpecificationSourceRoute.LegacyUnresolved.ToString(),
        CreatedBy = createdBy,
        CreationOperationKey = operationKey,
        CreatedAtUtc = now,
        Name = DefaultName(version),
        VatPercent = EstimatePolicy.DefaultVatPercent,
    };

    private static string DefaultName(int version) =>
        string.Create(CultureInfo.InvariantCulture, $"Estimate {version}");

    /// <summary>
    /// The one place a Draft estimate header takes its edited values. The Case
    /// workspace save applies the same header inside its own transaction, so a
    /// header saved through the workspace and one saved through the estimate
    /// command cannot end up meaning different things.
    /// </summary>
    internal static void ApplyDetails(CaseRepairSpecificationEntity entity, EstimateDetails details)
    {
        entity.Name = details.Name;
        entity.RepairDays = details.RepairDays;
        entity.LabourRate = details.LabourRate;
        entity.PaintLabourRate = details.PaintLabourRate;
        entity.PaintMaterials = details.PaintMaterials;
        entity.OtherCosts = details.OtherCosts;
        entity.VatPercent = details.VatPercent;
        entity.Notes = details.Notes;
    }

    private static void Accept(
        CaseRepairSpecificationEntity entity, RepairCalculationBasis basis, ActionActor actor, DateTimeOffset now)
    {
        entity.CalculationLabour = basis.Labour;
        entity.CalculationParts = basis.Parts;
        entity.CalculationPaintMaterials = basis.PaintMaterials;
        entity.CalculationSpecialistOther = basis.SpecialistOther;
        entity.RepairerVatRegistered = basis.RepairerVatRegistered;
        entity.CalculationVat = basis.Vat;
        entity.CalculationTotal = basis.Total;
        entity.CalculationPolicyVersion = basis.PolicyVersion;
        entity.State = RepairSpecificationState.Accepted.ToString();
        entity.AcceptedBy = actor.SubjectId;
        entity.AcceptedAtUtc = now;
    }

    private static void AddLines(
        PegasusDbContext context, CaseRepairSpecificationEntity target,
        IReadOnlyList<EstimateLineInput> lines, ActionActor actor, DateTimeOffset now)
    {
        var position = 0;
        foreach (var line in lines)
        {
            position++;
            context.CaseEstimateLines.Add(NewLine(line, position, target, actor, now));
        }
    }

    private static async Task<RepairSpecificationVersion> ReplayedAsync(
        PegasusDbContext context, Guid caseId, string operationKey, CancellationToken cancellationToken) =>
        Map(await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .SingleAsync(item => item.CaseId == caseId
                && (item.CreationOperationKey == operationKey || item.LastOperationKey == operationKey),
                cancellationToken));

    private static async Task<CaseRepairSpecificationEntity> RequiredEstimateAsync(
        PegasusDbContext context, Guid caseId, Guid estimateId, CancellationToken cancellationToken) =>
        await context.CaseRepairSpecifications.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == estimateId && item.CaseId == caseId, cancellationToken)
        ?? throw new InvalidOperationException("The estimate was not found on this case.");

    private static async Task<CaseWorkflowEntity> RequiredWorkflowAsync(
        PegasusDbContext context, Guid caseId, CancellationToken cancellationToken) =>
        await context.CaseWorkflows.Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
        ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");

    private static void Guard(
        CaseWorkflowEntity workflow, long expectedVersion, ActionActor actor, string lease, DateTimeOffset now)
    {
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);
        CaseMutationGuard.RequireLease(workflow, actor, lease, now);
        ArchivedCaseGuard.RequireMutable(workflow);
        workflow.Version++;
        CaseMutationGuard.ClearLease(workflow);
    }

    private static string RequiredReason(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A reason is required.") : value.Trim();

    private DateTimeOffset Now()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string Hash<T>(T request) =>
        CaseOperationReplay.Hash(JsonSerializer.Serialize(request, JsonOptions));

    private static CaseEstimateLineEntity CloneLine(
        CaseEstimateLineEntity line, CaseRepairSpecificationEntity target, ActionActor actor, DateTimeOffset now) =>
        NewLine(new(
            line.LineType, line.GuideCode, line.Description, line.WorkUnits, line.Price,
            line.Unpriced, line.PartNumber, line.Betterment, line.Status,
            line.EvidenceLabel, line.Justification, line.PaintWorkUnits, line.Quantity),
            line.Position, target, actor, now);

    private static CaseEstimateLineEntity NewLine(
        EstimateLineInput line, int position, CaseRepairSpecificationEntity target,
        ActionActor actor, DateTimeOffset now) =>
        EstimateLineWriter.NewLine(line, position, target.CaseId, target.Case, target, actor, now);

    internal static RepairSpecificationVersion Map(CaseRepairSpecificationEntity entity) => new(
        entity.Id, entity.CaseId, entity.Version,
        Enum.Parse<RepairSpecificationState>(entity.State),
        new(Enum.Parse<RepairSpecificationSourceRoute>(entity.SourceRoute),
            entity.SourceArtifactReference, entity.SourceVersion, entity.SourceSha256),
        entity.Lines.OrderBy(line => line.Position).Select(line => new CaseEstimateLineRecord(
            line.Id, line.Position, line.LineType, line.GuideCode, line.Description,
            line.WorkUnits, line.Price, line.Unpriced, line.PartNumber, line.Betterment,
            line.Status, line.EvidenceLabel, line.Justification,
            Enum.Parse<ActorKind>(line.RecordedByKind), line.RecordedBy, line.RecordedAtUtc,
            line.ConfirmedBy, line.ConfirmedAtUtc, line.PaintWorkUnits, line.Quantity)).ToArray(),
        entity.CalculationLabour is { } labour ? new(
            labour, entity.CalculationParts!.Value, entity.CalculationPaintMaterials!.Value,
            entity.CalculationSpecialistOther!.Value, entity.RepairerVatRegistered!.Value,
            entity.CalculationVat!.Value, entity.CalculationTotal!.Value,
            entity.CalculationPolicyVersion!) : null,
        entity.CreatedBy, entity.CreatedAtUtc, entity.AcceptedBy, entity.AcceptedAtUtc,
        entity.SupersedesSpecificationId, entity.SupersessionReason,
        new(entity.Name, entity.RepairDays, entity.LabourRate, entity.PaintLabourRate,
            entity.PaintMaterials, entity.OtherCosts, entity.VatPercent, entity.Notes),
        entity.IsCurrent, entity.AiJobId, entity.DiscardReason);

    /// <summary>
    /// The bounded <see cref="CaseEstimatePageItem"/> sibling of <see
    /// cref="Map"/> (CASE-047, Stream A review), read without
    /// <c>entity.Lines</c> ever being included.
    /// </summary>
    internal static CaseEstimatePageItem MapPageItem(CaseRepairSpecificationEntity entity) => new(
        entity.Id, entity.CaseId, entity.Version,
        Enum.Parse<RepairSpecificationState>(entity.State),
        new(Enum.Parse<RepairSpecificationSourceRoute>(entity.SourceRoute),
            entity.SourceArtifactReference, entity.SourceVersion, entity.SourceSha256),
        entity.Name,
        entity.IsCurrent,
        entity.CalculationLabour is { } labour ? new(
            labour, entity.CalculationParts!.Value, entity.CalculationPaintMaterials!.Value,
            entity.CalculationSpecialistOther!.Value, entity.RepairerVatRegistered!.Value,
            entity.CalculationVat!.Value, entity.CalculationTotal!.Value,
            entity.CalculationPolicyVersion!) : null);

    private static void AddHistory(
        PegasusDbContext context, CaseWorkflowEntity workflow, ActionActor actor,
        string operationKey, string reason, string eventType, string requestHash, object after,
        DateTimeOffset now) =>
        CaseMutationHistory.Add(
            context,
            workflow,
            actor,
            operationKey,
            RequiredReason(reason),
            eventType,
            requestHash,
            workflow.Version - 1,
            workflow.Version,
            "{}",
            JsonSerializer.Serialize(after, JsonOptions),
            $"{RepairSpecificationPolicy.PolicyKey}/v{RepairSpecificationPolicy.PolicyVersion}",
            now);
}

/// <summary>
/// The one owner of an estimate's line rows. Replacing the lines of a Draft is
/// a whole-list operation — positions are contiguous and start at one — and a
/// staff line is confirmed by the act of saving it while an Automation line
/// stays unconfirmed working data until an Engineer accepts it. The estimate
/// commands and the Case workspace save write lines through here, so the two
/// routes cannot record different provenance for the same edit.
/// </summary>
internal static class EstimateLineWriter
{
    public static CaseEstimateLineEntity NewLine(
        EstimateLineInput line,
        int position,
        Guid caseId,
        CaseEntity owningCase,
        CaseRepairSpecificationEntity? specification,
        ActionActor actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(actor);
        var confirmedBy = actor.Kind == ActorKind.Staff ? actor.SubjectId : null;
        return new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Case = owningCase,
            RepairSpecificationId = specification?.Id,
            RepairSpecification = specification,
            Position = position,
            LineType = line.Type,
            GuideCode = line.GuideCode,
            Description = line.Description,
            WorkUnits = line.WorkUnits,
            PaintWorkUnits = line.PaintWorkUnits,
            Quantity = line.Quantity,
            Price = line.Price,
            Unpriced = line.Unpriced,
            PartNumber = line.PartNumber,
            Betterment = line.Betterment,
            Status = line.Status,
            EvidenceLabel = line.EvidenceLabel,
            Justification = line.Justification,
            RecordedByKind = actor.Kind.ToString(),
            RecordedBy = actor.SubjectId,
            RecordedAtUtc = now,
            ConfirmedBy = confirmedBy,
            ConfirmedAtUtc = confirmedBy is null ? null : now
        };
    }

    /// <summary>
    /// Replaces every line of one estimate and returns the before/after
    /// evidence the history record carries. <paramref name="tracked"/> is
    /// updated in place so the caller's projection sees the new rows.
    /// </summary>
    public static (object Before, object After) Replace(
        PegasusDbContext context,
        Guid caseId,
        CaseEntity owningCase,
        CaseRepairSpecificationEntity? specification,
        List<CaseEstimateLineEntity> tracked,
        IReadOnlyList<EstimateLineInput> replacement,
        ActionActor actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tracked);
        ArgumentNullException.ThrowIfNull(replacement);
        var before = tracked.Select(Evidence).ToArray();
        context.CaseEstimateLines.RemoveRange(tracked);
        tracked.Clear();
        var position = 0;
        foreach (var line in replacement)
        {
            position++;
            var entity = NewLine(line, position, caseId, owningCase, specification, actor, now);
            context.CaseEstimateLines.Add(entity);
            tracked.Add(entity);
        }

        return (before, tracked.Select(Evidence).ToArray());
    }

    public static object Evidence(CaseEstimateLineEntity line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new
        {
            line.Position,
            line.LineType,
            line.GuideCode,
            line.Description,
            line.WorkUnits,
            line.PaintWorkUnits,
            line.Quantity,
            line.Price,
            line.Unpriced,
            line.PartNumber,
            line.Betterment,
            line.Status,
            line.EvidenceLabel,
            line.Justification,
            line.ConfirmedBy
        };
    }
}
