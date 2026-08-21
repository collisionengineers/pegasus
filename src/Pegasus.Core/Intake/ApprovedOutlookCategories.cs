using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public enum ApprovedOutlookCategoryState { Active, Disabled }

public sealed record ApprovedOutlookCategory(
    Guid Id,
    string DisplayName,
    ApprovedOutlookCategoryState State,
    int Version);

public sealed record UpdateApprovedOutlookCategoryRequest(
    Guid CategoryId,
    string DisplayName,
    ApprovedOutlookCategoryState State,
    int ExpectedVersion,
    ActionActor Actor,
    string Reason,
    string OperationKey);

public interface IApprovedOutlookCategoryStore
{
    Task<IReadOnlyList<ApprovedOutlookCategory>> ListAsync(CancellationToken cancellationToken);
    Task<ApprovedOutlookCategory> UpdateAsync(UpdateApprovedOutlookCategoryRequest request, CancellationToken cancellationToken);
}

public interface IApprovedOutlookCategoryResolver
{
    Task<ApprovedOutlookCategory?> ResolveActiveAsync(Guid categoryId, CancellationToken cancellationToken);
}

public sealed class ListApprovedOutlookCategories(IApprovedOutlookCategoryStore store)
{
    public Task<IReadOnlyList<ApprovedOutlookCategory>> ExecuteAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedOutlookCategories);
        return store.ListAsync(cancellationToken);
    }
}

public sealed class UpdateApprovedOutlookCategory(IApprovedOutlookCategoryStore store)
{
    public Task<ApprovedOutlookCategory> ExecuteAsync(
        UpdateApprovedOutlookCategoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageApprovedOutlookCategories);
        if (request.CategoryId == Guid.Empty || request.ExpectedVersion < 0 || !Enum.IsDefined(request.State))
        {
            throw new ArgumentException("Select a supported Outlook category policy.", nameof(request));
        }

        return store.UpdateAsync(request with
        {
            DisplayName = RequireText(request.DisplayName, 255, "A display name is required."),
            Reason = RequireText(request.Reason, 1000, "A reason is required."),
            OperationKey = RequireText(request.OperationKey, 100, "An operation key is required.")
        }, cancellationToken);
    }

    private static string RequireText(string value, int maximumLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters or contain control characters.");
        return normalized;
    }
}

/// <summary>MAIL-13 posts only an internal id; Core reloads the active server-owned display name.</summary>
public sealed class ResolveApprovedOutlookCategory(IApprovedOutlookCategoryResolver resolver)
{
    public async Task<ApprovedOutlookCategory> ExecuteAsync(
        Guid categoryId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (categoryId == Guid.Empty) throw new ApprovedOutlookCategoryUnavailableException();
        return await resolver.ResolveActiveAsync(categoryId, cancellationToken)
            ?? throw new ApprovedOutlookCategoryUnavailableException();
    }
}

public enum ApprovedOutlookCategoryUpdateError { NotFound, DuplicateDisplayName, VersionConflict, OperationConflict }

public sealed class ApprovedOutlookCategoryUpdateException(
    ApprovedOutlookCategoryUpdateError error,
    int? currentVersion = null) : InvalidOperationException("The Outlook category policy could not be saved.")
{
    public ApprovedOutlookCategoryUpdateError Error { get; } = error;
    public int? CurrentVersion { get; } = currentVersion;
}

public sealed class ApprovedOutlookCategoryUnavailableException()
    : InvalidOperationException("The selected Outlook category is not active.");
