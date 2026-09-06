using System.Globalization;
using F = Pegasus.Core.Intake.ThirdPartyReports.ThirdPartyReportFields;
using R = Pegasus.Core.Intake.ThirdPartyReports.ThirdPartyEstimateRoles;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// Why a finding was raised. None of them is an error in the reading: a
/// conflict is a property of the source document, and a reconciliation result
/// is an independent check beside it.
/// </summary>
public enum ThirdPartyFindingKind
{
    Information,
    Reconciliation,
    Conflict
}

/// <summary>
/// One data-quality observation about a source document. Findings are recorded
/// beside the source rows and never change a printed value.
/// </summary>
public sealed record ThirdPartyReportFinding(
    string Code,
    ThirdPartyFindingKind Kind,
    string Message,
    IReadOnlyList<SourceFieldCandidate> Evidence);

/// <summary>
/// The stable finding codes. They are part of the contract with the Case UI and
/// with the corpus regression, so they are named once here.
/// </summary>
public static class ThirdPartyFindingCodes
{
    public const string SourceRequiresOcr = "source-requires-ocr";
    public const string PageRequiresHumanVerification = "page-requires-human-verification";
    public const string DocumentSignatureAmbiguous = "document-signature-ambiguous";
    public const string ReportFieldsUnavailableWithoutOcr = "report-fields-unavailable-without-ocr";
    public const string FieldConflict = "field-conflict";
    public const string LabourHoursRateMismatch = "labour-hours-rate-mismatch";
    public const string LabourHoursRateReconciles = "labour-hours-rate-reconciles";
    public const string ComponentSumMismatch = "component-sum-does-not-match-net";
    public const string ComponentSumReconciles = "component-sum-reconciles-with-net";
    public const string NetNotPrinted = "net-not-printed";
    public const string NetVatGrossMismatch = "net-vat-gross-mismatch";
    public const string NetVatGrossReconciles = "net-vat-gross-reconciles";
    public const string VatRateMismatch = "vat-rate-does-not-match-amount";
    public const string InitialAndAgreedDiffer = "initial-and-agreed-amounts-differ";
    public const string ZeroTotalsWithContractRepair = "zero-totals-with-contract-repair";
    public const string ContractRepairBasisNotPrinted = "contract-repair-basis-not-printed";
    public const string ModelOdometerConflict = "model-and-odometer-conflict";
    public const string MakeAndModelNotSeparated = "make-and-model-not-separately-printed";
    public const string SupplementWithoutProvedBase = "supplement-without-proved-base";
    public const string ValuationAdjustmentReconciles = "valuation-adjustment-reconciles";
    public const string ValuationAdjustmentMismatch = "valuation-adjustment-does-not-reconcile";
}

/// <summary>
/// Independent arithmetic and cross-field reconciliation over a third-party
/// report candidate (INTK-056). Every result is a separate finding: a source
/// value is never silently repaired, and a contradiction is preserved beside
/// the figures that do reconcile.
/// </summary>
public static class ThirdPartyReportValidation
{
    /// <summary>Versioned with the finding rules.</summary>
    public const string PolicyVersion = "third-party-report-validation/1";

    /// <summary>Printed money is exact to the penny; hours times rate is not rounded.</summary>
    private const decimal Tolerance = 0.01m;

    /// <summary>A model value within this fraction of the odometer is a suspected column slip.</summary>
    private const decimal OdometerProximity = 0.10m;

    public static IReadOnlyList<ThirdPartyReportFinding> Check(
        ThirdPartyReportSelection selection,
        ThirdPartyReportCandidate? candidate,
        IReadOnlyList<SourceFieldCandidate> rows,
        bool requiresOcr)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(rows);

        var findings = new List<ThirdPartyReportFinding>();
        if (requiresOcr)
        {
            findings.Add(new(
                ThirdPartyFindingCodes.SourceRequiresOcr,
                ThirdPartyFindingKind.Information,
                "The source has scan-only pages; their fields are unavailable until OCR text exists.",
                [selection.Issuer]));
        }

        // Named page by page, because "some pages need checking" is not
        // actionable and the operator has to open a specific page.
        var scanned = rows
            .Where(row => row.Field == F.PageRequiresHumanVerification)
            .ToList();
        if (scanned.Count > 0)
        {
            findings.Add(new(
                ThirdPartyFindingCodes.PageRequiresHumanVerification,
                ThirdPartyFindingKind.Information,
                "A person must check these pages against the original; their text could not be read: "
                + string.Join(
                    ", ",
                    scanned.Select(row => row.Page?.ToString(CultureInfo.InvariantCulture) ?? "unnumbered")),
                scanned));
        }

        if (selection.Outcome == ThirdPartySelectionOutcome.Ambiguous)
        {
            findings.Add(new(
                ThirdPartyFindingCodes.DocumentSignatureAmbiguous,
                ThirdPartyFindingKind.Conflict,
                "More than one document signature matched: "
                + string.Join(", ", selection.Matches.Select(match => match.SignatureKey)),
                [selection.Issuer]));
        }

        if (candidate is null)
        {
            return findings;
        }

        if (!rows.Any(row => row.Disposition == SourceCandidateDisposition.Usable
                             && row.Field != F.Issuer))
        {
            findings.Add(new(
                ThirdPartyFindingCodes.ReportFieldsUnavailableWithoutOcr,
                ThirdPartyFindingKind.Information,
                "The issuer was identified but no field could be read from this document.",
                [selection.Issuer]));
        }

        findings.AddRange(Conflicts(rows));
        findings.AddRange(MakeAndModel(rows));
        findings.AddRange(Arithmetic(candidate, rows));
        findings.AddRange(Roles(candidate, rows));
        findings.AddRange(ModelOdometer(candidate, rows));
        findings.AddRange(Supplement(candidate));
        findings.AddRange(Valuation(candidate, rows));
        return findings;
    }

    /// <summary>Each field that observed two competing values keeps both.</summary>
    private static IEnumerable<ThirdPartyReportFinding> Conflicts(IReadOnlyList<SourceFieldCandidate> rows) =>
        rows
            .Where(row => row.Disposition == SourceCandidateDisposition.Conflicting)
            .GroupBy(row => (row.Field, row.ReferenceRole, row.PartyRole))
            .Select(group => new ThirdPartyReportFinding(
                ThirdPartyFindingCodes.FieldConflict,
                ThirdPartyFindingKind.Conflict,
                $"'{group.Key.Field}' has {group.Count()} competing printed values; both are retained.",
                [.. group]));

    private static IEnumerable<ThirdPartyReportFinding> MakeAndModel(IReadOnlyList<SourceFieldCandidate> rows)
    {
        var model = Row(rows, F.Model);
        var make = Row(rows, F.Make);
        if (model?.Disposition == SourceCandidateDisposition.Ambiguous
            && (make is null || make.Disposition == SourceCandidateDisposition.Missing))
        {
            yield return new(
                ThirdPartyFindingCodes.MakeAndModelNotSeparated,
                ThirdPartyFindingKind.Information,
                "The source prints one combined vehicle description; make and model are not separated.",
                [model]);
        }
    }

    /// <summary>
    /// Per printed amount role: hours times rate against the printed labour,
    /// the component sum against the printed (or derived) net, and net plus VAT
    /// against the printed gross. Each is its own finding.
    /// </summary>
    private static IEnumerable<ThirdPartyReportFinding> Arithmetic(
        ThirdPartyReportCandidate candidate,
        IReadOnlyList<SourceFieldCandidate> rows)
    {
        foreach (var estimate in candidate.Estimates)
        {
            var role = R.Code(estimate.Role);
            if (estimate.LabourHours?.Value is { } hours
                && estimate.LabourRate?.Value is { } rate
                && estimate.LabourAmount?.Value is { } labour)
            {
                var expected = decimal.Round(hours * rate, 2, MidpointRounding.AwayFromZero);
                yield return Math.Abs(expected - labour) <= Tolerance
                    ? new(
                        ThirdPartyFindingCodes.LabourHoursRateReconciles,
                        ThirdPartyFindingKind.Reconciliation,
                        $"{role}: {Text(hours)} hours at {Text(rate)} matches the printed labour {Text(labour)}.",
                        Evidence(rows, role, F.LabourHours, F.LabourRate, F.LabourAmount))
                    : new(
                        ThirdPartyFindingCodes.LabourHoursRateMismatch,
                        ThirdPartyFindingKind.Conflict,
                        $"{role}: {Text(hours)} hours at {Text(rate)} is {Text(expected)}, "
                        + $"not the printed labour {Text(labour)}. Both printed values are retained.",
                        Evidence(rows, role, F.LabourHours, F.LabourRate, F.LabourAmount));
            }

            var components = new[]
            {
                estimate.LabourAmount?.Value,
                estimate.PaintMaterials?.Value,
                estimate.Parts?.Value,
                estimate.SpecialistCharges?.Value,
                estimate.AdditionalCharges?.Value
            };
            var sum = components.Any(component => component is not null)
                ? components.Sum(component => component ?? 0m) - (estimate.Discounts?.Value ?? 0m)
                : (decimal?)null;

            var net = estimate.Net?.Value;
            if (sum is { } total && net is { } printed)
            {
                yield return Math.Abs(total - printed) <= Tolerance
                    ? new(
                        ThirdPartyFindingCodes.ComponentSumReconciles,
                        ThirdPartyFindingKind.Reconciliation,
                        $"{role}: the printed components total {Text(total)} and match the printed net.",
                        Evidence(rows, role, F.LabourAmount, F.PaintMaterials, F.Parts, F.SpecialistCharges, F.Net))
                    : new(
                        ThirdPartyFindingCodes.ComponentSumMismatch,
                        ThirdPartyFindingKind.Conflict,
                        $"{role}: the printed components total {Text(total)} against a printed net of {Text(printed)}.",
                        Evidence(rows, role, F.LabourAmount, F.PaintMaterials, F.Parts, F.SpecialistCharges, F.Net));
            }
            else if (sum is { } derived && net is null)
            {
                yield return new(
                    ThirdPartyFindingCodes.NetNotPrinted,
                    ThirdPartyFindingKind.Information,
                    $"{role}: no net is printed; the printed components total {Text(derived)}.",
                    Evidence(rows, role, F.LabourAmount, F.PaintMaterials, F.Parts, F.SpecialistCharges));
            }

            var basis = net ?? sum;
            if (basis is { } netBasis && estimate.VatAmount?.Value is { } vat
                                      && estimate.Gross?.Value is { } gross)
            {
                var source = net is null ? "the derived net" : "the printed net";
                yield return Math.Abs(netBasis + vat - gross) <= Tolerance
                    ? new(
                        ThirdPartyFindingCodes.NetVatGrossReconciles,
                        ThirdPartyFindingKind.Reconciliation,
                        $"{role}: {source} {Text(netBasis)} plus VAT {Text(vat)} equals the printed gross {Text(gross)}.",
                        Evidence(rows, role, F.Net, F.VatAmount, F.Gross))
                    : new(
                        ThirdPartyFindingCodes.NetVatGrossMismatch,
                        ThirdPartyFindingKind.Conflict,
                        $"{role}: {source} {Text(netBasis)} plus VAT {Text(vat)} is {Text(netBasis + vat)}, "
                        + $"not the printed gross {Text(gross)}.",
                        Evidence(rows, role, F.Net, F.VatAmount, F.Gross));
            }

            // Only documents that print both a VAT rate and a VAT amount can
            // have the two checked against each other.
            if (estimate.VatRate?.Value is { } vatRate
                && estimate.VatAmount?.Value is { } vatAmount
                && basis is { } rateBasis
                && rateBasis > 0m)
            {
                var expected = decimal.Round(rateBasis * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
                if (Math.Abs(expected - vatAmount) > Tolerance)
                {
                    yield return new(
                        ThirdPartyFindingCodes.VatRateMismatch,
                        ThirdPartyFindingKind.Conflict,
                        $"{role}: the printed VAT rate {Text(vatRate)}% of {Text(rateBasis)} is {Text(expected)}, "
                        + $"not the printed VAT {Text(vatAmount)}.",
                        Evidence(rows, role, F.VatRate, F.VatAmount, F.Net));
                }
            }
        }
    }

    /// <summary>
    /// Cross-role observations: an initial figure that differs from the agreed
    /// one, and a contract repair proposed beside zero ordinary totals.
    /// </summary>
    private static IEnumerable<ThirdPartyReportFinding> Roles(
        ThirdPartyReportCandidate candidate,
        IReadOnlyList<SourceFieldCandidate> rows)
    {
        var initial = Estimate(candidate, ThirdPartyEstimateRole.Initial);
        var agreed = Estimate(candidate, ThirdPartyEstimateRole.Agreed);
        if (initial?.LabourAmount?.Value is { } initialLabour
            && agreed?.LabourAmount?.Value is { } agreedLabour
            && initialLabour != agreedLabour)
        {
            yield return new(
                ThirdPartyFindingCodes.InitialAndAgreedDiffer,
                ThirdPartyFindingKind.Information,
                $"Initial labour {Text(initialLabour)} and agreed labour {Text(agreedLabour)} are both printed "
                + "and both retained.",
                [
                    .. rows.Where(row => row.Field == F.LabourAmount
                                         && (row.ReferenceRole == R.Initial || row.ReferenceRole == R.Agreed))
                ]);
        }

        var contract = Estimate(candidate, ThirdPartyEstimateRole.ContractRepair);
        if (contract?.Net?.Value is not { } contractAmount)
        {
            yield break;
        }

        yield return new(
            ThirdPartyFindingCodes.ContractRepairBasisNotPrinted,
            ThirdPartyFindingKind.Information,
            $"A contract repair figure of {Text(contractAmount)} is printed without a stated VAT basis.",
            [.. rows.Where(row => row.ReferenceRole == R.ContractRepair)]);

        var ordinary = candidate.Estimates
            .Where(estimate => estimate.Role != ThirdPartyEstimateRole.ContractRepair)
            .ToList();
        // Each amount must be PRESENT and zero. A printed 0.00 is evidence; an
        // amount the document does not print is unavailable, and treating the
        // absence as a zero would manufacture the one conflict this reading
        // exists to keep honest.
        if (ordinary.Exists(estimate =>
                estimate.Net?.Value is 0m
                && estimate.Gross?.Value is 0m
                && estimate.LabourAmount?.Value is 0m))
        {
            yield return new(
                ThirdPartyFindingCodes.ZeroTotalsWithContractRepair,
                ThirdPartyFindingKind.Conflict,
                $"The ordinary repair totals are zero while a contract repair of {Text(contractAmount)} is proposed; "
                + "the zero totals are not the agreed repair cost.",
                [.. rows.Where(row => row.Field == F.Net || row.Field == F.Gross)]);
        }
    }

    private static IEnumerable<ThirdPartyReportFinding> ModelOdometer(
        ThirdPartyReportCandidate candidate,
        IReadOnlyList<SourceFieldCandidate> rows)
    {
        if (candidate.Vehicle.Mileage?.Value is not { } mileage
            || mileage <= 0m
            || candidate.Vehicle.Model?.Value is not { } model)
        {
            yield break;
        }

        var digits = model.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var modelValue))
        {
            yield break;
        }

        if (Math.Abs(modelValue - mileage) / mileage <= OdometerProximity)
        {
            yield return new(
                ThirdPartyFindingCodes.ModelOdometerConflict,
                ThirdPartyFindingKind.Conflict,
                $"The printed model '{model}' is a mileage-like value close to the odometer {Text(mileage)}; "
                + "both printed values are retained and neither is corrected.",
                [.. rows.Where(row => row.Field == F.Model || row.Field == F.Mileage)]);
        }
    }

    private static IEnumerable<ThirdPartyReportFinding> Supplement(ThirdPartyReportCandidate candidate)
    {
        if (candidate.Identity.Revision?.Value is not { } revision
            || candidate.Identity.BaseReportDocumentId is not null)
        {
            yield break;
        }

        if (revision.Contains("Supplementary", StringComparison.OrdinalIgnoreCase)
            || revision.Contains("Amended", StringComparison.OrdinalIgnoreCase))
        {
            yield return new(
                ThirdPartyFindingCodes.SupplementWithoutProvedBase,
                ThirdPartyFindingKind.Information,
                $"The document declares itself '{revision}' but prints no base report reference, "
                + "so no base report is linked.",
                candidate.Identity.Revision.Source is { } source ? [source] : []);
        }
    }

    private static IEnumerable<ThirdPartyReportFinding> Valuation(
        ThirdPartyReportCandidate candidate,
        IReadOnlyList<SourceFieldCandidate> rows)
    {
        var valuation = candidate.Valuation;
        if (valuation.PreAccidentValue?.Value is not { } pav
            || valuation.FinalValue?.Value is not { } final)
        {
            yield break;
        }

        // Adjustments come from the typed mileage and condition slots plus
        // every other printed adjustment row, which has no typed slot in the
        // frozen projection. Reading the rows keeps the printed label intact
        // instead of filing "Urban edition adjustment" under a slot it is not.
        var adjustmentRows = rows
            .Where(row => row.Field == F.ValuationAdjustment
                          && row.Disposition != SourceCandidateDisposition.Missing)
            .ToList();
        var adjustments = (valuation.MileageAdjustment?.Value ?? 0m)
                          + (valuation.ConditionAdjustment?.Value ?? 0m)
                          + adjustmentRows.Sum(Amount);
        var evidence = rows
            .Where(row => row.Field == F.PreAccidentValue
                          || row.Field == F.FinalValue
                          || row.Field == F.MileageAdjustment
                          || row.Field == F.ConditionAdjustment
                          || row.Field == F.ValuationAdjustment)
            .ToList();
        yield return Math.Abs(pav + adjustments - final) <= Tolerance
            ? new(
                ThirdPartyFindingCodes.ValuationAdjustmentReconciles,
                ThirdPartyFindingKind.Reconciliation,
                $"The base value {Text(pav)} plus adjustments {Text(adjustments)} equals the final value {Text(final)}.",
                evidence)
            : new(
                ThirdPartyFindingCodes.ValuationAdjustmentMismatch,
                ThirdPartyFindingKind.Conflict,
                $"The base value {Text(pav)} plus adjustments {Text(adjustments)} is {Text(pav + adjustments)}, "
                + $"not the printed final value {Text(final)}.",
                evidence);
    }

    private static ThirdPartyReportEstimate? Estimate(
        ThirdPartyReportCandidate candidate,
        ThirdPartyEstimateRole role) =>
        candidate.Estimates.FirstOrDefault(estimate => estimate.Role == role);

    private static SourceFieldCandidate? Row(IReadOnlyList<SourceFieldCandidate> rows, string field) =>
        rows.FirstOrDefault(row => row.Field == field);

    private static IReadOnlyList<SourceFieldCandidate> Evidence(
        IReadOnlyList<SourceFieldCandidate> rows,
        string role,
        params string[] fields) =>
        [
            .. rows.Where(row =>
                row.ReferenceRole == role
                && Array.Exists(fields, field => field == row.Field)
                && row.Disposition != SourceCandidateDisposition.Missing)
        ];

    private static string Text(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal Amount(SourceFieldCandidate row) =>
        decimal.TryParse(
            row.NormalizedValue,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;
}
