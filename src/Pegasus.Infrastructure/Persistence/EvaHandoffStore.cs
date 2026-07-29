using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Eva;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads the accepted case and retained intake-image boundary for EVA preparation.
/// Generation remains fail closed while the external source mapping gate is unresolved.
/// </summary>
public sealed class EvaHandoffStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IEvaHandoffStore
{
    public async Task<EvaHandoffPreparation?> GetPreparationAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var caseRecord = await context.Cases
            .AsNoTracking()
            .Include(item => item.Principal)
            .SingleOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (caseRecord is null)
        {
            return null;
        }

        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .Include(item => item.InstructionDraft)
            .Include(item => item.Assets)
            .SingleOrDefaultAsync(
                item => item.Id == caseRecord.OriginIntakeReceiptId,
                cancellationToken);

        var draft = receipt?.InstructionDraft;
        var suggested = EvaEvidenceStatus.Suggested;
        var sourceVersion = receipt?.ExtractionPolicyVersion?.ToString(CultureInfo.InvariantCulture)
            ?? "unversioned";
        var source = receipt?.ExtractionPolicyKey ?? "intake-extraction";
        EvaEvidenceValue Evidence(string? value) => new(value, suggested, source, sourceVersion);

        var inspectionValue = draft?.InspectionAddress;
        var inspectionMode = string.Equals(
            inspectionValue?.Trim(),
            CaseEvaMapping.ImageBasedAssessment,
            StringComparison.Ordinal)
            ? EvaInspectionMode.ImageBasedAssessment
            : EvaInspectionMode.PhysicalAddress;

        var evidence = new EvaAcceptedCaseEvidence(
            caseRecord.Id,
            caseRecord.Version,
            CaseAccepted: receipt is not null,
            caseRecord.InstructionComplete && caseRecord.InstructionConfirmedByStaff,
            caseRecord.ImagesComplete && caseRecord.ImagesConfirmedByStaff,
            caseRecord.Reference,
            new(caseRecord.Principal.Code, EvaEvidenceStatus.Accepted, "accepted-principal", caseRecord.Version.ToString(CultureInfo.InvariantCulture)),
            Evidence(draft?.VehicleRegistration),
            Evidence(JoinVehicleModel(draft?.VehicleMake, draft?.VehicleModel)),
            Evidence(draft?.ClaimantName),
            Evidence(draft?.DateOfIncident?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Evidence(draft?.InstructionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Evidence(null),
            new(inspectionMode, Evidence(inspectionValue)),
            Evidence(draft?.AccidentCircumstances),
            Evidence(null),
            Evidence(draft?.VehicleMileage?.ToString(CultureInfo.InvariantCulture)),
            Evidence(draft?.VehicleMileage is null ? null : "miles"));

        var reasons = CaseEvaMapping.MapForProduction(evidence).BlockingReasons.ToList();
        if (!string.Equals(caseRecord.CustodyState, "confirmed", StringComparison.Ordinal)
            || caseRecord.CustodyConfirmedAtUtc is null)
        {
            reasons.Add("Case custody has not been confirmed.");
        }

        var images = receipt?.Assets
            .Where(IsImage)
            .OrderBy(item => item.SourceLabel, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .Select(item => new EvaHandoffImageOption(
                item.Id,
                item.FileName,
                item.MediaType,
                item.ContentLength,
                item.ContentHash,
                CustodyConfirmed: false))
            .ToArray() ?? [];

        if (images.Length == 0)
        {
            reasons.Add("No retained intake images are available for selection.");
        }
        else
        {
            reasons.Add("Selected intake images do not yet have per-version custody confirmation.");
        }

        return new(
            caseRecord.Id,
            caseRecord.Version,
            caseRecord.Reference,
            images,
            reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    public async Task<GenerateEvaHandoffResult> GenerateAsync(
        GenerateEvaHandoffRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentNullException.ThrowIfNull(request.SelectedImageIds);

        var preparation = await GetPreparationAsync(request.CaseId, cancellationToken);
        if (preparation is null)
        {
            return new(GenerateEvaHandoffOutcome.NotFound, null, ["The case was not found."]);
        }

        if (preparation.CaseVersion != request.ExpectedCaseVersion)
        {
            return new(
                GenerateEvaHandoffOutcome.Conflict,
                null,
                ["The case changed after the EVA handoff page was loaded. Reload before retrying."]);
        }

        var reasons = preparation.BlockingReasons.ToList();
        var knownIds = preparation.Images.Select(item => item.AssetId).ToHashSet();
        if (request.SelectedImageIds.Count == 0)
        {
            reasons.Add("Select at least one custody-confirmed image.");
        }
        else if (request.SelectedImageIds.Any(id => id == Guid.Empty || !knownIds.Contains(id))
                 || request.SelectedImageIds.Distinct().Count() != request.SelectedImageIds.Count)
        {
            reasons.Add("The selected images are invalid or no longer belong to this case intake.");
        }

        return new(
            GenerateEvaHandoffOutcome.Blocked,
            null,
            reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool IsImage(IntakeAssetEntity asset) =>
        asset.MediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || asset.MediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase);

    private static string? JoinVehicleModel(string? make, string? model)
    {
        var values = new[] { make, model }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        return values.Length == 0 ? null : string.Join(' ', values);
    }
}
