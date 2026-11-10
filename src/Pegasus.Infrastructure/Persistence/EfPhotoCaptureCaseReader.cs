using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// What a photo link issued from a Case needs to know about it: its
/// reference, registration and the facts the Send form prefills, read from
/// the Case's current work, and whether it is archived or a Triage.
/// </summary>
public sealed class EfPhotoCaptureCaseReader(
    IDbContextFactory<PegasusDbContext> contextFactory) : IPhotoCaptureCaseReader
{
    private static readonly string[] FieldNames =
    [
        CaseDataFieldNames.VehicleRegistration,
        CaseDataFieldNames.IncidentDate,
        CaseDataFieldNames.ClaimantName,
        CaseDataFieldNames.ClaimantContactNumber
    ];

    public async Task<PhotoCaptureCaseFacts?> GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (
            from caseEntity in context.Cases.AsNoTracking()
            join principal in context.Principals.AsNoTracking() on caseEntity.PrincipalId equals principal.Id
            join organization in context.Organizations.AsNoTracking() on principal.OrganizationId equals organization.Id
            join workflow in context.CaseWorkflows.AsNoTracking() on caseEntity.Id equals workflow.CaseId into workflows
            from workflow in workflows.DefaultIfEmpty()
            where caseEntity.Id == caseId
            select new
            {
                caseEntity.Reference,
                caseEntity.Type,
                caseEntity.PrincipalId,
                PrincipalName = organization.Name,
                PrincipalOrganizationId = organization.Id,
                HasWorkflow = workflow != null,
                ArchivedAtUtc = workflow == null ? null : workflow.ArchivedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var fields = await context.CaseDataFields.AsNoTracking()
            .Where(field => field.WorkId == workId && FieldNames.Contains(field.FieldName))
            .Select(field => new { field.FieldName, field.ValueKind, field.Value })
            .ToListAsync(cancellationToken);

        string? Current(string fieldName)
        {
            var byKind = fields
                .Where(field => field.FieldName == fieldName)
                .GroupBy(field => field.ValueKind)
                .ToDictionary(group => group.Key, group => group.First().Value);
            var value = byKind.GetValueOrDefault(CaseDataCodes.Confirmed)
                ?? byKind.GetValueOrDefault(CaseDataCodes.Suggestion)
                ?? byKind.GetValueOrDefault(CaseDataCodes.Fact);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        var registration = EfVehicleWorkflowStore.CurrentRegistration(
            fields
                .Where(field => field.FieldName == CaseDataFieldNames.VehicleRegistration)
                .Select(field => (field.ValueKind, field.Value)));
        var isTriage = !row.HasWorkflow
            || string.Equals(row.Type, CaseTypeCodes.Triage, StringComparison.Ordinal);
        return new(
            caseId,
            row.Reference,
            registration,
            DateOnly.TryParseExact(
                Current(CaseDataFieldNames.IncidentDate),
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var incidentDate)
                ? incidentDate
                : null,
            Current(CaseDataFieldNames.ClaimantName),
            Current(CaseDataFieldNames.ClaimantContactNumber),
            row.PrincipalName,
            row.PrincipalId,
            row.PrincipalOrganizationId,
            row.ArchivedAtUtc is not null,
            isTriage);
    }
}
