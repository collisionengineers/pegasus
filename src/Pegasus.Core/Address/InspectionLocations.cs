using System.Text.RegularExpressions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Address;

/// <summary>
/// A reviewed inspection location as a Case offers it: one of the register's
/// remainder locations held in the <c>InspectionLocations</c> reference table
/// (R22, operator, 15 October 2026), linked to the Case's Principal.
/// </summary>
public sealed record DirectoryInspectionLocation(Guid Id, string Name, string Address);

/// <summary>
/// One reviewed inspection location from the register, keyed by the row it
/// came from (<paramref name="Source"/>, <paramref name="SourceRecordId"/>).
/// </summary>
/// <param name="ReviewedAtUtc">
/// When the row was reviewed. A save whose review is not later than the
/// stored one keeps the stored row (FRD-06: the pipeline "keeps
/// operator-confirmed rows across refreshes").
/// </param>
/// <param name="PrincipalIds">The Principals whose Cases offer the location.</param>
public sealed record SaveInspectionLocationRequest(
    ActionActor Actor,
    string Name,
    string Address,
    string Postcode,
    bool Active,
    string Source,
    string SourceRecordId,
    DateTimeOffset ReviewedAtUtc,
    IReadOnlyList<Guid> PrincipalIds,
    string OperationKey);

public enum InspectionLocationSaveOutcome
{
    Created,
    Updated,
    KeptExisting
}

public sealed record InspectionLocationSaveResult(
    Guid Id,
    long Version,
    InspectionLocationSaveOutcome Outcome);

/// <summary>
/// The one writer of the reviewed inspection locations and their Principal
/// links. It has no delete, so a row a refresh leaves out is never touched.
/// </summary>
public interface IInspectionLocationAdministration
{
    Task<InspectionLocationSaveResult> SaveAsync(
        SaveInspectionLocationRequest request,
        CancellationToken cancellationToken);
}

public static partial class InspectionLocationPolicy
{
    public const int MaximumSourceLength = 100;

    /// <summary>
    /// An Administrator's request, trimmed, with its postcode normalised. A
    /// blank, over-length or repeated value is refused, never truncated or
    /// dropped. Whether an address is a reviewed full address is the
    /// register review's decision, not this policy's.
    /// </summary>
    public static SaveInspectionLocationRequest Normalize(SaveInspectionLocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (request.ReviewedAtUtc == default)
        {
            throw new ArgumentException("A review time is required.", nameof(request));
        }
        var principalIds = request.PrincipalIds ?? [];
        if (principalIds.Any(id => id == Guid.Empty) || principalIds.Distinct().Count() != principalIds.Count)
        {
            throw new ArgumentException("Each linked principal must be selected once.", nameof(request));
        }

        return request with
        {
            Name = ContactDirectoryPolicy.Required(request.Name, ContactDirectoryPolicy.MaximumNameLength, nameof(request.Name)),
            Address = ContactDirectoryPolicy.Required(request.Address, ContactDirectoryPolicy.MaximumAddressLength, nameof(request.Address)),
            Postcode = ContactDirectoryPolicy.Required(
                NormalizePostcode(request.Postcode), ContactDirectoryPolicy.MaximumPostcodeLength, nameof(request.Postcode)),
            Source = ContactDirectoryPolicy.Required(request.Source, MaximumSourceLength, nameof(request.Source)),
            SourceRecordId = ContactDirectoryPolicy.Required(request.SourceRecordId, MaximumSourceLength, nameof(request.SourceRecordId)),
            PrincipalIds = principalIds,
            OperationKey = ContactDirectoryPolicy.Required(
                request.OperationKey, ContactDirectoryPolicy.MaximumOperationKeyLength, nameof(request.OperationKey))
        };
    }

    /// <summary>
    /// Trimmed, whitespace collapsed and upper-cased; where its letters and
    /// digits fit the UK postcode shape, one space before the last three
    /// (<c>ol13qr</c> is <c>OL1 3QR</c>). Any other value is kept as it then
    /// reads, never refused for its shape.
    /// </summary>
    public static string NormalizePostcode(string postcode)
    {
        ArgumentNullException.ThrowIfNull(postcode);
        var collapsed = Whitespace().Replace(postcode.Trim(), " ").ToUpperInvariant();
        var compact = collapsed.Replace(" ", string.Empty, StringComparison.Ordinal);
        return UkPostcode().IsMatch(compact)
            ? $"{compact[..^3]} {compact[^3..]}"
            : collapsed;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Whitespace();

    [GeneratedRegex("^[A-Z]{1,2}[0-9][A-Z0-9]?[0-9][A-Z]{2}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex UkPostcode();
}
