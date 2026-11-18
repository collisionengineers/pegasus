namespace Pegasus.Core.Cases;

/// <summary>
/// What a standalone Audit of a Principal works from (operator, 17 November
/// 2026; R1138). <see cref="OriginalEngineerReport"/> is the Audit of another
/// engineering firm's original report. <see cref="GarageAuthorisationAndInvoice"/>
/// is the Audit of a garage's repair: it waits for the garage's estimate, the
/// Engineer's authorisation of the garage and the repairer's invoice, and has
/// no original report. The setting is the Principal's; a standalone Audit
/// takes it when it is created and keeps it, so a later change to the
/// Principal never changes a Case that already exists.
/// </summary>
public enum AuditWorkflowKind
{
    OriginalEngineerReport,
    GarageAuthorisationAndInvoice
}

/// <summary>
/// The invoice Generate report also makes in the Principal's own name, for the
/// Principal's client (operator, 17 November 2026; R1147, R1162).
/// </summary>
public enum PrincipalInvoiceKind
{
    None,
    UlaInvoice
}

/// <summary>
/// The stored codes of <see cref="AuditWorkflowKind"/> and the one rule for
/// which workflow a new Case takes (R1138).
/// </summary>
public static class PrincipalAuditWorkflowPolicy
{
    public const string OriginalEngineerReportCode = "original_engineer_report";
    public const string GarageAuthorisationAndInvoiceCode = "garage_authorisation_and_invoice";

    /// <summary>
    /// The refusal on a Garage authorisation and invoice Audit, which has no
    /// original report to mark or to record figures for. No page offers
    /// either act there; this is what a caller that names one anyway is told.
    /// </summary>
    public const string NoOriginalReport =
        "An original report can be recorded only for an Audit of an original engineer report.";

    public static string ToCode(AuditWorkflowKind workflow) => workflow switch
    {
        AuditWorkflowKind.OriginalEngineerReport => OriginalEngineerReportCode,
        AuditWorkflowKind.GarageAuthorisationAndInvoice => GarageAuthorisationAndInvoiceCode,
        _ => throw new ArgumentOutOfRangeException(
            nameof(workflow),
            "The Audit workflow is invalid.")
    };

    public static AuditWorkflowKind Parse(string code) => code switch
    {
        OriginalEngineerReportCode => AuditWorkflowKind.OriginalEngineerReport,
        GarageAuthorisationAndInvoiceCode => AuditWorkflowKind.GarageAuthorisationAndInvoice,
        _ => throw new InvalidDataException(
            $"Unknown Audit workflow code '{code}'.")
    };

    /// <summary>
    /// The workflow a Case of <paramref name="caseType"/> is created with: the
    /// Principal's for a standalone Audit, and the original engineer's report
    /// for every other type, whose Audit (where it has one) is Create audit's.
    /// </summary>
    public static AuditWorkflowKind ForNewCase(CaseType caseType, AuditWorkflowKind principalWorkflow) =>
        caseType == CaseType.Audit
            ? principalWorkflow
            : AuditWorkflowKind.OriginalEngineerReport;
}

/// <summary>
/// The stored codes of <see cref="PrincipalInvoiceKind"/> and the bounds of
/// the two values the invoice prints from the Principal (R1148).
/// </summary>
public static class PrincipalInvoicePolicy
{
    public const string NoneCode = "none";
    public const string UlaInvoiceCode = "ula_invoice";
    public const int MaximumVatNumberLength = 20;
    public const int MaximumPaymentTermsLength = 100;

    public static string ToCode(PrincipalInvoiceKind invoice) => invoice switch
    {
        PrincipalInvoiceKind.None => NoneCode,
        PrincipalInvoiceKind.UlaInvoice => UlaInvoiceCode,
        _ => throw new ArgumentOutOfRangeException(
            nameof(invoice),
            "The Principal's own invoice is invalid.")
    };

    public static PrincipalInvoiceKind Parse(string code) => code switch
    {
        NoneCode => PrincipalInvoiceKind.None,
        UlaInvoiceCode => PrincipalInvoiceKind.UlaInvoice,
        _ => throw new InvalidDataException(
            $"Unknown Principal invoice code '{code}'.")
    };

    /// <summary>The Invoice VAT number as typed, trimmed; null when the box is empty.</summary>
    public static string? NormalizeVatNumber(string? vatNumber) =>
        Normalize(vatNumber, MaximumVatNumberLength, nameof(vatNumber), "An invoice VAT number");

    /// <summary>The Invoice payment terms as typed, trimmed; null when the box is empty.</summary>
    public static string? NormalizePaymentTerms(string? paymentTerms) =>
        Normalize(paymentTerms, MaximumPaymentTermsLength, nameof(paymentTerms), "Invoice payment terms");

    private static string? Normalize(string? value, int maximumLength, string parameterName, string subject)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (trimmed.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{subject} can have at most {maximumLength} characters.", parameterName);
        }
        return trimmed;
    }
}
