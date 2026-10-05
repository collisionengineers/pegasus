using System.Globalization;
using Pegasus.Core.Assessment;

namespace Pegasus.Core.Cases;

/// <summary>
/// A Principal's default fee (operator, 5 October 2026): every Principal
/// carries one, and a new Case starts with it as its agreed fee, so the fee
/// note is filled from creation and stays editable on the Case.
/// </summary>
public static class PrincipalDefaultFeePolicy
{
    /// <summary>
    /// The fee every Principal was seeded with, and the one a new Principal
    /// starts with.
    /// </summary>
    public const decimal Standard = 180.00m;

    /// <summary>
    /// The Automation actor a new Case's agreed fee records, so the Case can
    /// tell its Principal's fee from any other automation's.
    /// </summary>
    public const string RecorderId = "principal-default-fee";

    /// <summary>
    /// The agreed fee's own rule: more than £0, in whole pence.
    /// </summary>
    public static bool IsValid(decimal fee) => fee > 0m && decimal.Round(fee, 2) == fee;

    public static decimal Require(decimal fee, string parameterName) =>
        IsValid(fee)
            ? fee
            : throw new ArgumentOutOfRangeException(
                parameterName,
                "A Principal's default fee must be more than £0 with at most two decimal places.");

    /// <summary>
    /// The fee as the <see cref="AssessmentVocabulary.AgreedFee"/> field
    /// stores it.
    /// </summary>
    public static string AgreedFeeValue(decimal fee) =>
        Require(fee, nameof(fee)).ToString("0.00", CultureInfo.InvariantCulture);
}
