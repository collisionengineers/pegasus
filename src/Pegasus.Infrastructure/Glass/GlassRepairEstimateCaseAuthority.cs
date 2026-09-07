using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The two Case facts a Glass's launch is made of: the vehicle it is about and
/// how far it has run, in the whole miles the provider asks for.
/// </summary>
public sealed record GlassRepairEstimateCaseFacts(string Registration, long MileageMiles);

/// <summary>
/// Proves the Engineer holds the Case's edit authority and hands back the
/// vehicle facts the launch needs, in one read.
///
/// <para>
/// A Glass's launch is not a Case mutation — it writes nothing to the Case —
/// but it acts on the operator's behalf against an external account and its
/// result is imported straight onto the Case, so it stands on exactly the
/// authority a Case write stands on: the presented version and the live edit
/// lease. Asking that question here rather than inventing a second, weaker
/// check is the point of this port.
/// </para>
/// </summary>
public interface IGlassRepairEstimateCaseAuthority
{
    Task<GlassRepairEstimateCaseFacts> RequireEditAuthorityAsync(
        ActionActor actor,
        Guid caseId,
        long expectedCaseVersion,
        string editLeaseToken,
        CancellationToken cancellationToken);
}

/// <summary>
/// Reads the Case's workflow row and vehicle fields and puts the presented
/// version and lease through <c>CaseMutationGuard</c> — the same persistence-side
/// adapter every Case write uses, so the refusals a Glass's launch gets are the
/// refusals a save would get, in the same exception types.
/// </summary>
public sealed class EfGlassRepairEstimateCaseAuthority(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IGlassRepairEstimateCaseAuthority
{
    public async Task<GlassRepairEstimateCaseFacts> RequireEditAuthorityAsync(
        ActionActor actor,
        Guid caseId,
        long expectedCaseVersion,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
        CaseMutationGuard.Require(
            workflow, actor, expectedCaseVersion, editLeaseToken, timeProvider.GetUtcNow());

        var fields = await context.Set<CaseDataFieldEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync(cancellationToken);
        return new(RequireRegistration(fields), RequireMileageMiles(fields));
    }

    private static string RequireRegistration(IReadOnlyList<CaseDataFieldEntity> fields) =>
        Current(fields, CaseDataFieldNames.VehicleRegistration) is { Length: > 0 } registration
            ? registration
            : throw new InvalidOperationException(
                "The case records no vehicle registration, so no Glass's estimate can be started for it.");

    /// <summary>
    /// The reading in whole miles, converted through Core's one owner of
    /// odometer units. A case that records no unit records a reading in miles —
    /// that is what the estate's odometer field means — but a unit that is
    /// stated and unreadable is ambiguous, and an ambiguous mileage would start
    /// the estimate against the wrong vehicle condition.
    /// </summary>
    private static long RequireMileageMiles(IReadOnlyList<CaseDataFieldEntity> fields)
    {
        if (Current(fields, CaseDataFieldNames.VehicleMileage) is not { Length: > 0 } stated
            || !long.TryParse(stated, NumberStyles.None, CultureInfo.InvariantCulture, out var mileage))
        {
            throw new InvalidOperationException(
                "The case records no vehicle mileage, so no Glass's estimate can be started for it.");
        }

        var statedUnit = Current(fields, CaseDataFieldNames.VehicleMileageUnit);
        var unit = CaseOdometerUnit.Miles;
        if (statedUnit is { Length: > 0 } && !CaseOdometer.TryParseUnit(statedUnit, out unit))
        {
            throw new InvalidOperationException(
                "The case records an unrecognized mileage unit, so no Glass's estimate can be started for it.");
        }

        return (long)Math.Round(
            CaseOdometer.Display(mileage, unit, CaseOdometerUnit.Miles),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The value the Case currently stands on: a confirmed value, else an
    /// extracted fact, else a suggestion — the same precedence
    /// <c>EfCaseAssessmentStore</c> reads these fields at.
    /// </summary>
    private static string? Current(IReadOnlyList<CaseDataFieldEntity> fields, string fieldName)
    {
        var values = fields.Where(item => item.FieldName == fieldName).ToArray();
        var current = values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Confirmed)
            ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Fact)
            ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Suggestion);
        return current?.Value;
    }
}
