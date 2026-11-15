namespace Pegasus.Core.Intake;

/// <summary>
/// The case-match policy of a Principal with no instruction profile, so with
/// no route-owned <see cref="PrincipalCaseMatchPolicy"/>: such a Principal
/// instructs over the Principal API (API-01) and states its identity facts, so
/// there is nothing to extract, but its Cases are indexed and a declared
/// instruction is matched against them by the same normalization every other
/// Principal's full reference takes (operator, 13 November 2026; R1055).
/// It is never one of the registered policies: mail matching and the
/// cross-Principal suggestions read only those.
/// </summary>
public sealed class DeclaredCaseMatchPolicy(string principalCode) : IPrincipalCaseMatchPolicy
{
    public string PrincipalCode { get; } = string.IsNullOrWhiteSpace(principalCode)
        ? throw new ArgumentException("A Principal code is required.", nameof(principalCode))
        : principalCode;

    public string PolicyKey => PrincipalCaseMatchPolicy.Key;

    public int PolicyVersion => PrincipalCaseMatchPolicy.Version;

    /// <summary>Nothing is read out of a message for a Principal with no profile.</summary>
    public CaseMatchKeys ExtractMatchKeys(IntakeSourceReadResult readResult) =>
        new(null, null, null, null, null);

    public CaseMatchIndexKeys DeriveIndexKeys(CaseMatchSourceData caseData) =>
        PrincipalCaseMatchPolicy.DeriveIndexKeys(PrincipalCode, caseData);

    /// <summary>
    /// The registered policy that owns <paramref name="principalCode"/>, or
    /// this default for it.
    /// </summary>
    public static IPrincipalCaseMatchPolicy For(
        IEnumerable<IPrincipalCaseMatchPolicy> policies,
        string principalCode)
    {
        ArgumentNullException.ThrowIfNull(policies);
        return policies.SingleOrDefault(candidate =>
                string.Equals(candidate.PrincipalCode, principalCode, StringComparison.Ordinal))
            ?? new DeclaredCaseMatchPolicy(principalCode);
    }
}
