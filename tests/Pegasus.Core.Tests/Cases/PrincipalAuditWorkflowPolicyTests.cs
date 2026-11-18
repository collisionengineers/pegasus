using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// The Audit workflow a Principal's standalone Audits take, and the invoice
/// Generate report also makes in the Principal's own name (operator, 17
/// November 2026; R1138, R1147, R1148).
/// </summary>
public sealed class PrincipalAuditWorkflowPolicyTests
{
    [Theory]
    [InlineData(AuditWorkflowKind.OriginalEngineerReport, "original_engineer_report")]
    [InlineData(AuditWorkflowKind.GarageAuthorisationAndInvoice, "garage_authorisation_and_invoice")]
    public void AnAuditWorkflowIsStoredByItsCode(AuditWorkflowKind workflow, string code)
    {
        Assert.Equal(code, PrincipalAuditWorkflowPolicy.ToCode(workflow));
        Assert.Equal(workflow, PrincipalAuditWorkflowPolicy.Parse(code));
    }

    [Theory]
    [InlineData(PrincipalInvoiceKind.None, "none")]
    [InlineData(PrincipalInvoiceKind.UlaInvoice, "ula_invoice")]
    public void APrincipalInvoiceIsStoredByItsCode(PrincipalInvoiceKind invoice, string code)
    {
        Assert.Equal(code, PrincipalInvoicePolicy.ToCode(invoice));
        Assert.Equal(invoice, PrincipalInvoicePolicy.Parse(code));
    }

    [Fact]
    public void AnUnknownCodeIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => PrincipalAuditWorkflowPolicy.Parse("garage"));
        Assert.Throws<InvalidDataException>(() => PrincipalInvoicePolicy.Parse("invoice"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrincipalAuditWorkflowPolicy.ToCode((AuditWorkflowKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrincipalInvoicePolicy.ToCode((PrincipalInvoiceKind)99));
    }

    /// <summary>
    /// Only a standalone Audit takes its Principal's workflow (R1138); an
    /// Inspection, an Inspection + Audit and a Triage Case of the same
    /// Principal keep the original engineer's report.
    /// </summary>
    [Theory]
    [InlineData(CaseType.Audit, AuditWorkflowKind.GarageAuthorisationAndInvoice)]
    [InlineData(CaseType.Inspection, AuditWorkflowKind.OriginalEngineerReport)]
    [InlineData(CaseType.InspectionAndAudit, AuditWorkflowKind.OriginalEngineerReport)]
    [InlineData(CaseType.Triage, AuditWorkflowKind.OriginalEngineerReport)]
    public void OnlyAStandaloneAuditTakesItsPrincipalsWorkflow(CaseType caseType, AuditWorkflowKind taken)
    {
        Assert.Equal(
            taken,
            PrincipalAuditWorkflowPolicy.ForNewCase(caseType, AuditWorkflowKind.GarageAuthorisationAndInvoice));
        Assert.Equal(
            AuditWorkflowKind.OriginalEngineerReport,
            PrincipalAuditWorkflowPolicy.ForNewCase(caseType, AuditWorkflowKind.OriginalEngineerReport));
    }

    /// <summary>
    /// The Invoice VAT number and payment terms are kept as typed, trimmed; an
    /// empty box is no value, which leaves the row off the invoice (R1148).
    /// </summary>
    [Fact]
    public void TheInvoiceValuesAreTrimmedAndBounded()
    {
        Assert.Equal("GB 123 4567 89", PrincipalInvoicePolicy.NormalizeVatNumber("  GB 123 4567 89 "));
        Assert.Null(PrincipalInvoicePolicy.NormalizeVatNumber("   "));
        Assert.Null(PrincipalInvoicePolicy.NormalizeVatNumber(null));
        Assert.Equal("30 days", PrincipalInvoicePolicy.NormalizePaymentTerms(" 30 days "));
        Assert.Null(PrincipalInvoicePolicy.NormalizePaymentTerms(string.Empty));

        var longestVatNumber = new string('7', PrincipalInvoicePolicy.MaximumVatNumberLength);
        var longestTerms = new string('t', PrincipalInvoicePolicy.MaximumPaymentTermsLength);
        Assert.Equal(20, longestVatNumber.Length);
        Assert.Equal(100, longestTerms.Length);
        Assert.Equal(longestVatNumber, PrincipalInvoicePolicy.NormalizeVatNumber(longestVatNumber));
        Assert.Equal(longestTerms, PrincipalInvoicePolicy.NormalizePaymentTerms(longestTerms));
        Assert.Throws<ArgumentException>(() => PrincipalInvoicePolicy.NormalizeVatNumber(longestVatNumber + "7"));
        Assert.Throws<ArgumentException>(() => PrincipalInvoicePolicy.NormalizePaymentTerms(longestTerms + "t"));
    }
}
