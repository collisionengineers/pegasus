using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The persisted codes of <c>Principals.ReferenceScheme</c>: how a Principal's
/// Cases are referenced.
/// </summary>
internal static class PrincipalReferenceSchemes
{
    /// <summary>The code, two-digit year and annual sequence: <c>QDOS26001</c>.</summary>
    public const string PrincipalSequence = "principal_sequence";

    /// <summary>
    /// The vehicle registration, <c>.2</c> and on for a repeat: the standing
    /// Private customer record's one-off Cases (operator, 31 October 2026;
    /// R574, R575).
    /// </summary>
    public const string Registration = "registration";
}

/// <summary>
/// Allocates the permanent portion of a Case identity. Every Case creator —
/// receipt-backed, receiptless, replacement and Triage — shares the same
/// Principal lineage and annual sequence, and each calls this inside its own
/// transaction before it writes the Case, so creators on one lineage queue on
/// the sequence row. The allocation stays in memory until <c>SaveChanges</c>,
/// so a creator that returns early releases the number. The finished Case/PO
/// ends in <see cref="CaseReferenceFormat.ReferenceSuffix"/> while the
/// Administrator's switch is on (R656), read in the creator's transaction.
/// </summary>
/// <remarks>
/// A Principal referenced by registration takes its lineage number the same
/// way, so its Cases keep a year and sequence and the lineage's sequence lock
/// serialises its creators; only the Case/PO differs. The repeat is read
/// under that lock from the references already held, so two staff creating
/// the same registration at once get the registration and <c>.2</c>.
/// </remarks>
internal static class CaseIdentityAllocator
{
    /// <param name="registration">
    /// The Case's normalised vehicle registration, which only New case and
    /// <c>pegasus_case_create</c> carry to allocation. A registration-scheme
    /// Principal without one is refused: a one-off is created from New case
    /// (R581).
    /// </param>
    public static async Task<AllocatedCaseIdentity> AllocateAsync(
        PegasusDbContext context,
        PrincipalEntity principal,
        CaseType caseType,
        string? registration,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var year = LondonCalendar.DateAt(createdAtUtc).Year;
        var lineageId = principal.SequenceLineageId;
        // UPDLOCK, HOLDLOCK: concurrent creators on one lineage queue here
        // instead of both reading the row shared and deadlocking on the update.
        // A missing row takes a range lock, which serialises its insert too.
        var sequence = context.Database.IsSqlServer()
            ? await context.CaseSequences
                .FromSqlInterpolated($"""
                    SELECT * FROM [CaseSequences] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [SequenceLineageId] = {lineageId} AND [Year] = {year}
                    """)
                .SingleOrDefaultAsync(cancellationToken)
            : await context.CaseSequences.SingleOrDefaultAsync(
                item => item.SequenceLineageId == lineageId && item.Year == year,
                cancellationToken);
        if (sequence is null)
        {
            sequence = new CaseSequenceEntity
            {
                SequenceLineageId = lineageId,
                Year = year,
                LastAllocatedSequence = 0
            };
            context.CaseSequences.Add(sequence);
        }

        var number = checked(++sequence.LastAllocatedSequence);
        var reference = principal.ReferenceScheme == PrincipalReferenceSchemes.Registration
            ? await RegistrationReferenceAsync(context, caseType, registration, cancellationToken)
            : CaseReferenceFormat.CasePo(caseType, CaseReferenceFormat.Base(principal.Code, year, number));
        var suffixed = await EfSendToAiControlStore.IsEnabledAsync(
            context,
            SendToAiControlEntity.CaseReferenceSuffixId,
            cancellationToken);
        return new(year, number, CaseReferenceFormat.WithSuffix(reference, suffixed));
    }

    /// <summary>
    /// A one-off's Case/PO: the registration, or <c>.2</c> and on when an
    /// earlier Case already holds it. Read after the sequence lock, so the
    /// previous creator's reference is committed and visible. Every reference
    /// that starts with the registration is read, so one carrying the
    /// temporary suffix (R656) counts; the repeat rule keeps the exact forms.
    /// </summary>
    private static async Task<string> RegistrationReferenceAsync(
        PegasusDbContext context,
        CaseType caseType,
        string? registration,
        CancellationToken cancellationToken)
    {
        // A one-off is never a Triage (R577); a standalone Audit takes the
        // a. prefix on the shared repeat (R600, R601).
        if (caseType is not (CaseType.Inspection or CaseType.InspectionAndAudit or CaseType.Audit)
            || string.IsNullOrWhiteSpace(registration))
        {
            throw new OneOffCaseRouteException();
        }

        var audit = CaseReferenceFormat.AuditPrefix + registration;
        var existing = await context.Cases
            .AsNoTracking()
            .Where(item => item.Reference.StartsWith(registration) || item.Reference.StartsWith(audit))
            .Select(item => item.Reference)
            .ToArrayAsync(cancellationToken);
        return CaseReferenceFormat.CasePo(caseType, CaseReferenceFormat.Registration(
            registration,
            CaseReferenceFormat.NextRegistrationRepeat(registration, existing)));
    }
}

/// <summary>An allocated Case identity; <see cref="Reference"/> is the finished Case/PO.</summary>
internal sealed record AllocatedCaseIdentity(int Year, int Sequence, string Reference);
