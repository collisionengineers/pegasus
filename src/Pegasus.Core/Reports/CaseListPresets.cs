using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>
/// A named set of Case list columns that every Administrator shares (MI-04),
/// e.g. "Invoicing". Choosing it ticks its columns; a download may still
/// change them for that one download.
/// </summary>
public sealed record CaseListPreset(
    Guid Id,
    string Name,
    IReadOnlyList<string> ColumnKeys,
    long Version,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>When the preset was removed. A removed preset leaves the list and frees its name.</summary>
    public DateTimeOffset? RemovedAtUtc { get; init; }
}

/// <summary>
/// Creates or updates one preset. <see cref="ExpectedVersion"/> 0 creates the
/// record the caller minted an identity for, which makes a repeated create
/// idempotent; any other value updates the record at exactly that version.
/// </summary>
public sealed record SaveCaseListPresetRequest(
    Guid PresetId,
    string Name,
    IReadOnlyList<string> ColumnKeys,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey);

public sealed record RemoveCaseListPresetRequest(
    Guid PresetId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey);

public enum CaseListPresetError
{
    NotFound,
    DuplicateName,
    VersionConflict,
    OperationConflict,
    Removed
}

public sealed class CaseListPresetException(CaseListPresetError error)
    : InvalidOperationException("The Case list preset request could not be completed.")
{
    public CaseListPresetError Error { get; } = error;
}

public interface ICaseListPresetStore
{
    /// <summary>Every preset that has not been removed.</summary>
    Task<IReadOnlyList<CaseListPreset>> ListAsync(CancellationToken cancellationToken);

    Task<CaseListPreset> SaveAsync(SaveCaseListPresetRequest request, CancellationToken cancellationToken);

    Task<CaseListPreset> RemoveAsync(RemoveCaseListPresetRequest request, CancellationToken cancellationToken);
}

/// <summary>A preset's rules: a name of 1 to 100 characters, unique among live presets, and at least one known column.</summary>
public static class CaseListPresetPolicy
{
    public const int MaximumNameLength = 100;
    public const string PolicyStamp = "case-list-preset-v1";

    public static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumNameLength)
        {
            throw new ArgumentException($"A preset name is 1 to {MaximumNameLength} characters.", nameof(name));
        }

        return trimmed;
    }

    /// <summary>The keys as the catalogue orders them, each once (<see cref="CaseListColumns.Resolve"/>).</summary>
    public static IReadOnlyList<string> NormalizeColumns(IReadOnlyCollection<string>? keys) =>
        [.. CaseListColumns.Resolve(keys ?? []).Select(column => column.Key)];

    internal static string RequireOperationKey(string? operationKey) =>
        string.IsNullOrWhiteSpace(operationKey) || operationKey.Length > 100
            ? throw new ArgumentException("An operation key of up to 100 characters is required.", nameof(operationKey))
            : operationKey;
}

public sealed class ListCaseListPresets(ICaseListPresetStore store)
{
    public Task<IReadOnlyList<CaseListPreset>> ExecuteAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        return store.ListAsync(cancellationToken);
    }
}

public sealed class SaveCaseListPreset(ICaseListPresetStore store)
{
    public Task<CaseListPreset> ExecuteAsync(SaveCaseListPresetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ViewOperationalReports);
        if (request.PresetId == Guid.Empty || request.ExpectedVersion < 0)
        {
            throw new ArgumentException("A preset identity and expected version are required.", nameof(request));
        }

        return store.SaveAsync(
            request with
            {
                Name = CaseListPresetPolicy.NormalizeName(request.Name),
                ColumnKeys = CaseListPresetPolicy.NormalizeColumns(request.ColumnKeys),
                OperationKey = CaseListPresetPolicy.RequireOperationKey(request.OperationKey)
            },
            cancellationToken);
    }
}

public sealed class RemoveCaseListPreset(ICaseListPresetStore store)
{
    public Task<CaseListPreset> ExecuteAsync(RemoveCaseListPresetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ViewOperationalReports);
        if (request.PresetId == Guid.Empty || request.ExpectedVersion <= 0)
        {
            throw new ArgumentException("A preset identity and the version it was read at are required.", nameof(request));
        }

        return store.RemoveAsync(
            request with { OperationKey = CaseListPresetPolicy.RequireOperationKey(request.OperationKey) },
            cancellationToken);
    }
}
