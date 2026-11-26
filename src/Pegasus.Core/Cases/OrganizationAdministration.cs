using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Cases;

public enum OrganizationAdministrationError
{
    DuplicateOrganizationName,
    DuplicatePrincipalCode,
    PrincipalNotFound,
    PrincipalInactive,
    PrincipalAlreadyReplaced,
    StaleVersion,
    OperationConflict
}

public sealed class OrganizationAdministrationException(
    OrganizationAdministrationError error)
    : Exception("The organization or principal administration request could not be completed.")
{
    public OrganizationAdministrationError Error { get; } = error;
}

public sealed record PrincipalAdministrationSummary(
    Guid Id,
    Guid OrganizationId,
    string Code,
    Guid SequenceLineageId,
    Guid? PredecessorId,
    Guid? SuccessorId,
    bool IsActive,
    long Version,
    int AllocatedCaseCount,
    CaseInspectionMode InspectionMode = CaseInspectionMode.PhysicalAddress,
    PrincipalReportSendingRules? ReportSending = null,
    string? DefaultInspectionLocationLabel = null,
    string? DefaultInspectionAddress = null,
    string? DefaultInspectionPostcode = null,
    string? DefaultInspectionSourceKind = null,
    Guid? DefaultInspectionSourceRecordId = null,
    long? DefaultInspectionSourceVersion = null,
    string? NotesOnEveryCase = null,
    decimal DefaultFee = PrincipalDefaultFeePolicy.Standard,
    PrincipalReportNames? ReportNames = null,
    string? FeeNoteVatNumber = null,
    AuditWorkflowKind AuditWorkflow = AuditWorkflowKind.OriginalEngineerReport,
    PrincipalInvoiceKind PrincipalInvoice = PrincipalInvoiceKind.None,
    string? InvoiceVatNumber = null,
    string? InvoicePaymentTerms = null,
    string? SendFromMailbox = null,
    bool ReportImages = true)
{
    /// <summary>The Principal's report sending rules; the defaults when none were named.</summary>
    public PrincipalReportSendingRules ReportSending { get; init; } =
        ReportSending ?? PrincipalReportSendingRules.Default;

    /// <summary>What the Principal's reports are called; none when it names none (R740).</summary>
    public PrincipalReportNames ReportNames { get; init; } =
        ReportNames ?? PrincipalReportNames.None;
}

public sealed record PrincipalAdministrationDetails(
    string Name,
    PrincipalAdministrationSummary Principal);

public interface IGetPrincipal
{
    Task<PrincipalAdministrationDetails?> ExecuteAsync(
        ActionActor actor, Guid principalId, CancellationToken cancellationToken);
}

public interface IOrganizationAdministrationQueries
{
    Task<PrincipalAdministrationDetails?> GetPrincipalAsync(
        Guid principalId, CancellationToken cancellationToken);

}

public interface IOrganizationAdministrationStore
{
    Task<Principal> ReplacePrincipalAsync(
        ReplacePrincipalRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Change an existing principal's report settings. Unlike
    /// a replacement this creates no new principal and moves no reference — it
    /// is the one principal attribute that may change in place.
    /// </summary>
    Task<Principal> UpdatePrincipalReportSettingsAsync(
        UpdatePrincipalReportSettingsRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replace the principal's report sending rules in place, as its report
    /// settings change in place.
    /// </summary>
    Task<Principal> UpdatePrincipalReportSendingAsync(
        UpdatePrincipalReportSendingRequest request,
        CancellationToken cancellationToken);
}

public sealed class GetPrincipal(IOrganizationAdministrationQueries queries) : IGetPrincipal
{
    public Task<PrincipalAdministrationDetails?> ExecuteAsync(
        ActionActor actor, Guid principalId, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("A principal identifier is required.", nameof(principalId));
        }
        return queries.GetPrincipalAsync(principalId, cancellationToken);
    }
}

public sealed class ReplacePrincipal(IOrganizationAdministrationStore store)
    : IReplacePrincipal
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Principal> ExecuteAsync(
        ReplacePrincipalRequest request,
        CancellationToken cancellationToken) =>
        _store.ReplacePrincipalAsync(
            OrganizationAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class UpdatePrincipalReportSettings(IOrganizationAdministrationStore store)
    : IUpdatePrincipalReportSettings
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Principal> ExecuteAsync(
        UpdatePrincipalReportSettingsRequest request,
        CancellationToken cancellationToken) =>
        _store.UpdatePrincipalReportSettingsAsync(
            OrganizationAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class UpdatePrincipalReportSending(IOrganizationAdministrationStore store)
    : IUpdatePrincipalReportSending
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Principal> ExecuteAsync(
        UpdatePrincipalReportSendingRequest request,
        CancellationToken cancellationToken) =>
        _store.UpdatePrincipalReportSendingAsync(
            OrganizationAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed record PrincipalReplacementPlan(
    Principal Predecessor,
    Principal Successor);

public static class OrganizationAdministrationPolicy
{
    public const int MaximumPrincipalCodeLength = 20;
    public const int MaximumOperationKeyLength = 100;
    public const int MaximumReasonLength = 500;

    public static PrincipalReplacementPlan PlanPrincipalReplacement(
        Principal predecessor,
        long expectedVersion,
        Guid successorId,
        string successorCode,
        bool codeAlreadyExists)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        RequireExpectedVersion(expectedVersion, nameof(expectedVersion));
        RequireIdentifier(successorId, nameof(successorId));
        if (predecessor.Version != expectedVersion)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.StaleVersion);
        }
        if (predecessor.SuccessorId is not null)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.PrincipalAlreadyReplaced);
        }
        if (!predecessor.IsActive)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.PrincipalInactive);
        }

        RequireUniquePrincipalCode(codeAlreadyExists);
        var normalizedCode = NormalizePrincipalCode(successorCode, nameof(successorCode));
        return new(
            predecessor with
            {
                SuccessorId = successorId,
                IsActive = false,
                Version = checked(predecessor.Version + 1)
            },
            new(
                successorId,
                predecessor.OrganizationId,
                normalizedCode,
                predecessor.SequenceLineageId,
                predecessor.Id,
                null,
                true,
                0,
                predecessor.InspectionMode,
                predecessor.ReportSending,
                DefaultFee: predecessor.DefaultFee,
                SendFromMailbox: predecessor.SendFromMailbox,
                ReportImages: predecessor.ReportImages,
                ReportNames: predecessor.ReportNames,
                FeeNoteVatNumber: predecessor.FeeNoteVatNumber,
                AuditWorkflow: predecessor.AuditWorkflow,
                PrincipalInvoice: predecessor.PrincipalInvoice,
                InvoiceVatNumber: predecessor.InvoiceVatNumber,
                InvoicePaymentTerms: predecessor.InvoicePaymentTerms));
    }

    public static void RequireUniquePrincipalCode(bool alreadyExists)
    {
        if (alreadyExists)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.DuplicatePrincipalCode);
        }
    }

    public static UpdatePrincipalReportSettingsRequest Normalize(
        UpdatePrincipalReportSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.PrincipalId, nameof(request.PrincipalId));
        RequireExpectedVersion(request.ExpectedContactVersion, nameof(request.ExpectedContactVersion));
        RequireDefined(request.AuditWorkflow, nameof(request.AuditWorkflow));
        RequireDefined(request.PrincipalInvoice, nameof(request.PrincipalInvoice));
        return request with
        {
            ReportNames = PrincipalReportNames.Normalize(request.ReportNames),
            FeeNoteVatNumber = AssessmentReportContract.NormalizeVatNumber(request.FeeNoteVatNumber),
            InvoiceVatNumber = PrincipalInvoicePolicy.NormalizeVatNumber(request.InvoiceVatNumber),
            InvoicePaymentTerms = PrincipalInvoicePolicy.NormalizePaymentTerms(request.InvoicePaymentTerms),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Reason = NormalizeOptionalText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason))
        };
    }

    public static UpdatePrincipalReportSendingRequest Normalize(
        UpdatePrincipalReportSendingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.PrincipalId, nameof(request.PrincipalId));
        RequireExpectedVersion(request.ExpectedVersion, nameof(request.ExpectedVersion));
        RequireExpectedVersion(request.ExpectedContactVersion, nameof(request.ExpectedContactVersion));
        return request with
        {
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Rules = PrincipalReportSendingRules.Normalize(request.Rules)
        };
    }

    /// <summary>
    /// Report images, the report names, the fee note VAT number, the Audit
    /// workflow and the Principal's own invoice with its VAT number and
    /// payment terms change, and nothing else does. The default fee is the
    /// contact's Save's (R1266).
    /// The code, the organization, its notes, the lineage and the allocation
    /// history are untouched. A Case already created keeps the Audit workflow
    /// it took (R1138).
    /// </summary>
    public static Principal PlanPrincipalReportSettingsUpdate(
        Principal current,
        long expectedVersion,
        bool reportImages,
        PrincipalReportNames? reportNames,
        string? feeNoteVatNumber = null,
        AuditWorkflowKind auditWorkflow = AuditWorkflowKind.OriginalEngineerReport,
        PrincipalInvoiceKind principalInvoice = PrincipalInvoiceKind.None,
        string? invoiceVatNumber = null,
        string? invoicePaymentTerms = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        RequireExpectedVersion(expectedVersion, nameof(expectedVersion));
        if (current.Version != expectedVersion)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.StaleVersion);
        }

        // A replaced principal keeps its settings as a record of what it did;
        // changing them would rewrite history for work already allocated, and
        // the successor is the one that decides what happens next.
        if (!current.IsActive)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.PrincipalInactive);
        }

        var names = PrincipalReportNames.Normalize(reportNames);
        var vatNumber = AssessmentReportContract.NormalizeVatNumber(feeNoteVatNumber);
        RequireDefined(auditWorkflow, nameof(auditWorkflow));
        RequireDefined(principalInvoice, nameof(principalInvoice));
        var invoiceVat = PrincipalInvoicePolicy.NormalizeVatNumber(invoiceVatNumber);
        var invoiceTerms = PrincipalInvoicePolicy.NormalizePaymentTerms(invoicePaymentTerms);
        var changed = current.ReportImages != reportImages
            || current.ReportNames != names
            || !string.Equals(current.FeeNoteVatNumber, vatNumber, StringComparison.Ordinal)
            || current.AuditWorkflow != auditWorkflow
            || current.PrincipalInvoice != principalInvoice
            || !string.Equals(current.InvoiceVatNumber, invoiceVat, StringComparison.Ordinal)
            || !string.Equals(current.InvoicePaymentTerms, invoiceTerms, StringComparison.Ordinal);
        return current with
        {
            ReportImages = reportImages,
            ReportNames = names,
            FeeNoteVatNumber = vatNumber,
            AuditWorkflow = auditWorkflow,
            PrincipalInvoice = principalInvoice,
            InvoiceVatNumber = invoiceVat,
            InvoicePaymentTerms = invoiceTerms,
            Version = changed ? checked(current.Version + 1) : current.Version
        };
    }

    /// <summary>
    /// The report sending rules change, and nothing else does. A replaced
    /// principal keeps its rules as it keeps its other settings.
    /// </summary>
    public static Principal PlanPrincipalReportSendingUpdate(
        Principal current,
        long expectedVersion,
        PrincipalReportSendingRules rules)
    {
        ArgumentNullException.ThrowIfNull(current);
        RequireExpectedVersion(expectedVersion, nameof(expectedVersion));
        if (current.Version != expectedVersion)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.StaleVersion);
        }
        if (!current.IsActive)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.PrincipalInactive);
        }

        var normalized = PrincipalReportSendingRules.Normalize(rules);
        return current with
        {
            ReportSending = normalized,
            Version = Equals(current.ReportSending, normalized)
                ? current.Version
                : checked(current.Version + 1)
        };
    }

    public static ReplacePrincipalRequest Normalize(ReplacePrincipalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.PrincipalId, nameof(request.PrincipalId));
        RequireExpectedVersion(request.ExpectedVersion, nameof(request.ExpectedVersion));
        RequireExpectedVersion(request.ExpectedContactVersion, nameof(request.ExpectedContactVersion));
        return request with
        {
            SuccessorCode = NormalizePrincipalCode(request.SuccessorCode, nameof(request.SuccessorCode)),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Reason = NormalizeOptionalText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason))
        };
    }

    /// <summary>
    /// A principal code: required, at most <see cref="MaximumPrincipalCodeLength"/>
    /// characters, ASCII letters and digits only, upper-cased. A refusal names
    /// <paramref name="parameterName"/>, the field the caller read the code from.
    /// </summary>
    public static string NormalizePrincipalCode(string value, string parameterName)
    {
        var normalized = NormalizeRequiredText(
            value,
            MaximumPrincipalCodeLength,
            parameterName).ToUpperInvariant();
        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException(
                "A principal code can contain only ASCII letters and digits.",
                parameterName);
        }

        return normalized;
    }

    private static void RequireAdministrator(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(
            actor,
            StaffAccessRight.ManageOrganizationsAndPrincipals);
    }

    private static void RequireIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "A stable identifier is required.",
                parameterName);
        }
    }

    private static void RequireDefined<TChoice>(TChoice value, string parameterName)
        where TChoice : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The value is not one of the choices.");
        }
    }

    private static void RequireExpectedVersion(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The expected version cannot be negative.");
        }
    }

    internal static string NormalizeRequiredText(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    internal static string? NormalizeOptionalText(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeRequiredText(value, maximumLength, parameterName);
    }

}
