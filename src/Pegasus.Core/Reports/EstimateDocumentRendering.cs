using System.Security.Cryptography;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

public static class EstimateDocumentContract
{
    /// <summary>
    /// v2 (6 November 2026): the snapshot carries the EVA breakdown's
    /// figures, and no longer the claim number, part numbers, source route,
    /// discounts, other costs or unpriced count, which the printout does not
    /// print.
    /// </summary>
    public const string PayloadVersion = "estimate-payload-v2";
    /// <summary>
    /// v3 (6 November 2026): the Repair Spec printout is the EVA breakdown
    /// (R750 to R754), not the report's page.
    /// </summary>
    public const string TemplateVersion = "estimate-v3";
    public const int MaximumLines = 250;
    public const int MaximumAggregateCharacters = 80_000;
    public const int MaximumFieldCharacters = 4_000;
}

public sealed record EstimateDocumentLine(
    int Position,
    int Quantity,
    string Description,
    string LineType,
    EstimateOperation Operation,
    decimal? Hours,
    decimal? PaintHours,
    decimal? Materials,
    decimal? UnitPrice,
    bool Unpriced)
{
    /// <summary>
    /// The line's Price column: its unit amount times its quantity, the amount
    /// <see cref="EstimateTotals"/> counts. An unpriced line has none.
    /// </summary>
    public decimal? Price => Unpriced || UnitPrice is not { } unit ? null : unit * Quantity;
}

public sealed record EstimateDocumentSnapshot(
    string OurReference,
    DateOnly DocumentDate,
    string ClaimantName,
    string VehicleDescription,
    string Registration,
    Guid EstimateId,
    int EstimateVersion,
    string EstimateName,
    RepairSpecificationState State,
    bool IsCurrent,
    IReadOnlyList<EstimateDocumentLine> Lines,
    EstimateHours Hours,
    decimal HourlyRate,
    EstimateTotals Totals,
    EstimatePrintedBreakdown Breakdown,
    string PayloadVersion = EstimateDocumentContract.PayloadVersion)
{
    public static EstimateDocumentSnapshot For(
        RepairSpecificationVersion estimate,
        string ourReference,
        DateOnly documentDate,
        string claimantName,
        string vehicleDescription,
        string registration)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        var lines = estimate.Lines
            .OrderBy(line => line.Position)
            .Select(line => new EstimateDocumentLine(
                line.Position,
                line.Quantity is > 0 ? line.Quantity.Value : 1,
                !string.IsNullOrWhiteSpace(line.Description) ? line.Description.Trim()
                    : line.GuideCode?.Trim() ?? string.Empty,
                line.Type,
                EstimateOperations.FromLineType(line.Type),
                line.WorkUnits,
                line.PaintWorkUnits,
                line.Materials,
                line.Price,
                line.Unpriced))
            .ToArray();
        return new(
            ourReference.Trim(),
            documentDate,
            string.IsNullOrWhiteSpace(claimantName) ? "—" : claimantName.Trim(),
            string.IsNullOrWhiteSpace(vehicleDescription) ? "—" : vehicleDescription.Trim(),
            registration.Trim(),
            estimate.SpecificationId,
            estimate.Version,
            estimate.Details.Name.Trim(),
            estimate.State,
            estimate.IsCurrent,
            lines,
            EstimateHours.Of(estimate),
            estimate.Details.HourlyRate,
            EstimateTotals.Compute(estimate),
            EstimateTotals.Breakdown(estimate));
    }

    public string Status => IsCurrent ? "CURRENT" : State switch
    {
        RepairSpecificationState.Draft => "DRAFT",
        _ => throw new ReportRenderRejectedException("The estimate document has an unsupported state."),
    };

    public void Validate()
    {
        Required(OurReference, "The Case reference is required.");
        Required(Registration, "The vehicle registration is required.");
        Required(EstimateName, "The estimate name is required.");
        if (EstimateId == Guid.Empty || EstimateVersion <= 0)
        {
            throw new ReportRenderRejectedException("The estimate identity is incomplete.");
        }
        if (PayloadVersion != EstimateDocumentContract.PayloadVersion)
        {
            throw new ReportRenderRejectedException("The estimate document payload version is unsupported.");
        }
        if (Lines.Count == 0)
        {
            throw new ReportRenderRejectedException("The estimate has no lines.");
        }
        if (Lines.Count > EstimateDocumentContract.MaximumLines)
        {
            throw new ReportRenderRejectedException(
                $"The estimate document carries more than {EstimateDocumentContract.MaximumLines} lines.");
        }
        if (HourlyRate <= 0m)
        {
            throw new ReportRenderRejectedException("The labour rate is required for the estimate document.");
        }
        if (Hours.PricedTotal < 0m || Hours.UnpricedSpecialist < 0m)
        {
            throw new ReportRenderRejectedException("Estimate hours cannot be negative.");
        }
        var characters = OurReference.Length + Registration.Length + EstimateName.Length
            + ClaimantName.Length + VehicleDescription.Length;
        foreach (var line in Lines)
        {
            Required(line.Description, $"Estimate line {line.Position} requires a description or guide code.");
            if (!EstimateLineCodes.Types.Contains(line.LineType, StringComparer.Ordinal))
            {
                throw new ReportRenderRejectedException(
                    $"Estimate line {line.Position} has an unsupported type.");
            }
            if (line.Unpriced && line.UnitPrice is not null)
            {
                throw new ReportRenderRejectedException(
                    $"Estimate line {line.Position} cannot be both unpriced and carry a unit amount.");
            }
            characters += line.Description.Length;
            if (line.Description.Length > EstimateDocumentContract.MaximumFieldCharacters)
            {
                throw new ReportRenderRejectedException(
                    $"Estimate line {line.Position} contains a field which is too long to render.");
            }
        }
        if (characters > EstimateDocumentContract.MaximumAggregateCharacters)
        {
            throw new ReportRenderRejectedException("The estimate document text is too large to render.");
        }
        var printed = Totals.Printed;
        if (printed.Parts + printed.PanelLabour + printed.PaintLabour
                + printed.Materials + printed.Specialist != printed.Net
            || printed.Net + printed.Vat != printed.Gross
            || Breakdown.Labour + Breakdown.Materials + Breakdown.Parts + Breakdown.Other
                - Breakdown.TotalDiscount != printed.Net)
        {
            throw new ReportRenderRejectedException("The estimate's printed components do not reconcile.");
        }
        _ = Status;
    }

    private static void Required(string? value, string reason)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReportRenderRejectedException(reason);
        }
    }
}

public interface IEstimateDocumentRenderer
{
    string EngineVersion { get; }
    Task<RenderedReportArtifact> RenderAsync(
        EstimateDocumentSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

public enum RenderCaseEstimateDocumentOutcome
{
    Rendered,
    NotRenderable,
    NotFound,
}

public sealed record RenderCaseEstimateDocumentResult(
    RenderCaseEstimateDocumentOutcome Outcome,
    RenderedReportArtifact? Artifact,
    IReadOnlyList<string> Reasons,
    int? EstimateVersion = null);

/// <summary>
/// Saved-estimate preview. The workspace read is the normal staff Case-read
/// authority; no assessment lifecycle gate or edit lease is involved.
/// </summary>
public interface IRenderCaseEstimateDocument
{
    Task<RenderCaseEstimateDocumentResult> ExecuteAsync(
        Guid caseId,
        Guid estimateId,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

public sealed class RenderCaseEstimateDocument(
    IGetAssessmentWorkspace workspaces,
    IGetAssessmentWorkspaceVersion workspaceVersions,
    IRepairSpecificationStore estimates,
    IEstimateDocumentRenderer renderer,
    TimeProvider clock) : IRenderCaseEstimateDocument
{
    public async Task<RenderCaseEstimateDocumentResult> ExecuteAsync(
        Guid caseId,
        Guid estimateId,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty || estimateId == Guid.Empty)
        {
            return NotFound();
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var workspace = await workspaces.ExecuteAsync(new(caseId, actor), cancellationToken);
            if (workspace is null)
            {
                return NotFound();
            }
            var estimate = await estimates.GetVersionAsync(caseId, estimateId, cancellationToken);
            if (estimate is null || estimate.CaseId != caseId)
            {
                return NotFound();
            }
            var currentVersion = await workspaceVersions.ExecuteAsync(new(caseId, actor), cancellationToken);
            if (currentVersion is null)
            {
                return NotFound();
            }
            if (currentVersion != workspace.Header.Version)
            {
                continue;
            }

            try
            {
                var make = workspace.Data.Vehicle.Make.Current?.Value;
                var model = workspace.Data.Vehicle.Model.Current?.Value;
                var description = string.Join(" ", new[] { make, model }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = workspace.Data.Vehicle.Description.Current?.Value ?? "—";
                }
                var snapshot = EstimateDocumentSnapshot.For(
                    estimate,
                    workspace.Header.Reference,
                    LondonCalendar.DateAt(clock.GetUtcNow()),
                    workspace.Data.Claimant.Name.Current?.Value ?? "—",
                    description,
                    workspace.Header.Registration ?? string.Empty);
                snapshot.Validate();
                var artifact = await renderer.RenderAsync(snapshot, cancellationToken);
                var hash = Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf));
                if (!hash.Equals(artifact.Sha256, StringComparison.Ordinal))
                {
                    throw new ReportRenderRejectedException(
                        "The renderer returned an artifact with mismatched provenance.");
                }
                return new(RenderCaseEstimateDocumentOutcome.Rendered, artifact, [], estimate.Version);
            }
            catch (Exception exception) when (exception is ReportRenderRejectedException
                or InvalidOperationException
                or IOException
                or TimeoutException)
            {
                return new(RenderCaseEstimateDocumentOutcome.NotRenderable, null, [exception.Message]);
            }
        }

        return new(
            RenderCaseEstimateDocumentOutcome.NotRenderable,
            null,
            ["The Case changed while the estimate document was prepared."]);
    }

    private static RenderCaseEstimateDocumentResult NotFound() =>
        new(RenderCaseEstimateDocumentOutcome.NotFound, null, []);
}

public sealed record RecordEstimateDocumentPreviewRequest(
    ActionActor Actor,
    Guid CaseId,
    Guid EstimateId,
    int EstimateVersion,
    DateTimeOffset OccurredAtUtc);

public interface IEstimateDocumentPresentationStore
{
    Task RecordPreviewedAsync(
        RecordEstimateDocumentPreviewRequest request,
        CancellationToken cancellationToken = default);
}
