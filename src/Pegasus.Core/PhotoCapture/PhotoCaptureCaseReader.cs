namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// What a photo link needs to know about its Case when HQ issues it: the
/// reference and registration the message names, the facts the Send form
/// prefills, and whether the Case can still take evidence. The Principal's
/// organisation is the contact whose saved addresses a quick link for
/// Accident management offers (R798). <see cref="IsClosed"/> is what the Case
/// ribbon calls closed (<c>CaseLifecycleRules.IsTerminal</c>): such a Case
/// sends no photo link (R847).
/// </summary>
public sealed record PhotoCaptureCaseFacts(
    Guid CaseId,
    string Reference,
    string? NormalizedRegistration,
    DateOnly? IncidentDate,
    string? ClaimantName,
    string? ClaimantContactNumber,
    string? PrincipalName,
    Guid? PrincipalId,
    Guid? PrincipalOrganizationId,
    bool IsArchived,
    bool IsTriage,
    bool IsClosed);

public interface IPhotoCaptureCaseReader
{
    Task<PhotoCaptureCaseFacts?> GetAsync(Guid caseId, CancellationToken cancellationToken);
}
