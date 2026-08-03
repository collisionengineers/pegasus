using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The case-match read model: normalized identity keys per accepted case, maintained in
/// the same transaction as the case-data writers so an inbound email can never miss a
/// just-accepted case. Lifecycle state is deliberately not a column — the candidate
/// query joins CaseWorkflows, so state transitions need no index maintenance.
/// </summary>
internal sealed class CaseMatchIndexEntity
{
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string WorkProviderCode { get; set; }
    public string? DurableClaimToken { get; set; }
    public string? NormalizedVrm { get; set; }
    public string? NormalizedSurname { get; set; }
    public string? NormalizedFirstInitial { get; set; }
    public DateOnly? IncidentDate { get; set; }
    public required string MatchPolicyKey { get; set; }
    public int MatchPolicyVersion { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class IntakeCaseMatchDecisionEntity
{
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public required string Outcome { get; set; }
    public Guid? MatchedCaseId { get; set; }
    public Guid? RedirectedFromCaseId { get; set; }
    public required string MatchKeysJson { get; set; }
    public required string CandidatesJson { get; set; }
    public required string Reason { get; set; }
    public required string PolicyKey { get; set; }
    public int PolicyVersion { get; set; }
}

internal static class CaseMatchModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CaseMatchIndexEntity>(entity =>
        {
            entity.ToTable("CaseMatchIndex");
            entity.HasKey(item => item.CaseId);
            entity.Property(item => item.WorkProviderCode).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DurableClaimToken).HasMaxLength(100);
            entity.Property(item => item.NormalizedVrm).HasMaxLength(20);
            entity.Property(item => item.NormalizedSurname).HasMaxLength(100);
            entity.Property(item => item.NormalizedFirstInitial).HasMaxLength(1);
            entity.Property(item => item.MatchPolicyKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.WorkProviderCode, item.DurableClaimToken });
            entity.HasIndex(item => new { item.WorkProviderCode, item.NormalizedVrm });
            entity.HasIndex(item => new { item.WorkProviderCode, item.NormalizedSurname });
            entity.HasOne(item => item.Case)
                .WithOne()
                .HasForeignKey<CaseMatchIndexEntity>(item => item.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IntakeCaseMatchDecisionEntity>(entity =>
        {
            entity.ToTable("IntakeCaseMatchDecisions");
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(item => item.MatchKeysJson).IsRequired();
            entity.Property(item => item.CandidatesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.PolicyKey).HasMaxLength(100).IsRequired();
            entity.HasOne(item => item.IntakeReceipt)
                .WithOne(item => item.CaseMatchDecision)
                .HasForeignKey<IntakeCaseMatchDecisionEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>
/// Derives the index row for a case from its current typed case data through the
/// provider's one normalization grammar (Confirmed value wins over Suggestion over
/// Fact, matching CaseField semantics). Returns null when the case has no provider or
/// the provider has no accepted case-match policy — matching simply is not active there.
/// </summary>
internal static class CaseMatchIndexProjector
{
    public static CaseMatchIndexEntity? Project(
        CaseEntity caseEntity,
        IReadOnlyList<CaseDataFieldEntity> fields,
        IEnumerable<IProviderCaseMatchPolicy> policies,
        DateTimeOffset updatedAtUtc)
    {
        var provider = CurrentValue(fields, CaseDataFieldNames.WorkProviderCode);
        var policy = provider is null
            ? null
            : policies.SingleOrDefault(candidate =>
                string.Equals(candidate.WorkProviderCode, provider, StringComparison.Ordinal));
        if (policy is null)
        {
            return null;
        }

        var keys = policy.DeriveIndexKeys(new(
            CurrentValue(fields, CaseDataFieldNames.ClaimNumber),
            CurrentValue(fields, CaseDataFieldNames.VehicleRegistration),
            CurrentValue(fields, CaseDataFieldNames.ClaimantName),
            ParseDate(CurrentValue(fields, CaseDataFieldNames.IncidentDate))));
        return new()
        {
            CaseId = caseEntity.Id,
            Case = caseEntity,
            WorkProviderCode = provider!,
            DurableClaimToken = keys.DurableClaimToken,
            NormalizedVrm = keys.NormalizedVrm,
            NormalizedSurname = keys.NormalizedSurname,
            NormalizedFirstInitial = keys.NormalizedFirstInitial,
            IncidentDate = keys.IncidentDate,
            MatchPolicyKey = policy.PolicyKey,
            MatchPolicyVersion = policy.PolicyVersion,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    public static void Apply(
        PegasusDbContext context,
        CaseMatchIndexEntity? existing,
        CaseMatchIndexEntity? projected)
    {
        if (projected is null)
        {
            if (existing is not null)
            {
                context.Remove(existing);
            }
            return;
        }

        if (existing is null)
        {
            context.CaseMatchIndex.Add(projected);
            return;
        }

        existing.WorkProviderCode = projected.WorkProviderCode;
        existing.DurableClaimToken = projected.DurableClaimToken;
        existing.NormalizedVrm = projected.NormalizedVrm;
        existing.NormalizedSurname = projected.NormalizedSurname;
        existing.NormalizedFirstInitial = projected.NormalizedFirstInitial;
        existing.IncidentDate = projected.IncidentDate;
        existing.MatchPolicyKey = projected.MatchPolicyKey;
        existing.MatchPolicyVersion = projected.MatchPolicyVersion;
        existing.UpdatedAtUtc = projected.UpdatedAtUtc;
    }

    private static string? CurrentValue(
        IReadOnlyList<CaseDataFieldEntity> fields,
        string fieldName)
    {
        var byKind = fields
            .Where(field => field.FieldName == fieldName)
            .ToDictionary(field => field.ValueKind, field => field.Value);
        var value = byKind.GetValueOrDefault(CaseDataCodes.Confirmed)
            ?? byKind.GetValueOrDefault(CaseDataCodes.Suggestion)
            ?? byKind.GetValueOrDefault(CaseDataCodes.Fact);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
}

public sealed class EfCaseMatchIndex(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseMatchCandidateQueries
{
    public async Task<IReadOnlyList<CaseMatchCandidate>> FindByAnyKeyAsync(
        string workProviderCode,
        CaseMatchKeys keys,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workProviderCode);
        ArgumentNullException.ThrowIfNull(keys);

        var claim = keys.DurableClaimToken;
        var vrm = keys.NormalizedVrm;
        var surname = keys.NormalizedSurname;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.CaseMatchIndex
            .AsNoTracking()
            .Where(item => item.WorkProviderCode == workProviderCode
                && ((claim != null && item.DurableClaimToken == claim)
                    || (vrm != null && item.NormalizedVrm == vrm)
                    || (surname != null && item.NormalizedSurname == surname)))
            .Join(
                context.CaseWorkflows.AsNoTracking(),
                index => index.CaseId,
                workflow => workflow.CaseId,
                (index, workflow) => new
                {
                    Index = index,
                    workflow.State,
                    workflow.ReplacementCaseId
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new CaseMatchCandidate(
                row.Index.CaseId,
                row.Index.WorkProviderCode,
                row.Index.DurableClaimToken,
                row.Index.NormalizedVrm,
                row.Index.NormalizedSurname,
                row.Index.NormalizedFirstInitial,
                row.Index.IncidentDate,
                Enum.Parse<CaseLifecycleState>(row.State),
                row.ReplacementCaseId))
            .ToArray();
    }
}
