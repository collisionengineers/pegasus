using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One work of a Case: the data a report is made from. Every Case has its
/// primary work, whose <see cref="Id"/> is the Case id; an Inspection + Audit
/// Case gains a second, Audit, work at Create audit. The per-work tables
/// (typed data, assessment fields, proposals, specifications and lines,
/// specification snapshots, valuations, applied valuations, report wording)
/// key on the work, not the Case.
/// </summary>
internal sealed class CaseWorkEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string Kind { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    // Set only on the primary row, by Create audit: the Inspection report's
    // approval and sent evidence, moved off the workflow when the Audit
    // becomes the current work.
    public Guid? ReportApprovalId { get; set; }
    public CaseReportApprovalEntity? ReportApproval { get; set; }
    public Guid? ReportSentEvidenceId { get; set; }
    public CaseReportSentEvidenceEntity? ReportSentEvidence { get; set; }
}

/// <summary>The persisted vocabulary of a work's kind, parsed exactly.</summary>
internal static class CaseWorkKinds
{
    public const string Primary = "primary";
    public const string Audit = "audit";

    public static string ToCode(CaseWorkKind kind) => kind switch
    {
        CaseWorkKind.Primary => Primary,
        CaseWorkKind.Audit => Audit,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown work kind.")
    };

    public static CaseWorkKind Parse(string value) => value switch
    {
        Primary => CaseWorkKind.Primary,
        Audit => CaseWorkKind.Audit,
        _ => throw new InvalidDataException($"Unknown persisted work kind '{value}'.")
    };
}
