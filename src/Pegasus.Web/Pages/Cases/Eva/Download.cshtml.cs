using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Cases.Eva;

/// <summary>
/// Downloads one generated EVA handoff revision as a file. It is the one case
/// action that answers with content rather than a redirect, so it lives on its
/// own page like a document download; a refusal still returns to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed partial class DownloadModel(
    IDownloadEvaHandoff downloadEvaHandoff,
    ILogger<DownloadModel> logger) : CaseMutationPageModel(logger)
{
    public async Task<IActionResult> OnPostAsync(
        Guid id,
        int revision,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty || revision <= 0)
        {
            return NotFound();
        }

        try
        {
            var result = await downloadEvaHandoff.ExecuteAsync(
                new(id, revision, expectedVersion, actor, operationKey, reason, editLeaseToken),
                cancellationToken);
            if (result.Outcome is DownloadEvaHandoffOutcome.NotFound)
            {
                return NotFound();
            }
            if (result.Outcome is DownloadEvaHandoffOutcome.Conflict or DownloadEvaHandoffOutcome.Refused
                || result.Artifact is not { } artifact
                || SafeEvaFileName(artifact.FileName) is not { } fileName)
            {
                PreserveLeaseState(id, editLeaseToken);
                TempData["CaseError"] = result.Message;
                return RedirectToDetails(id);
            }

            ClearLeaseState();
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["Content-Digest"] =
                $"sha-256=:{Convert.ToBase64String(SHA256.HashData(artifact.Content))}:";
            Response.ContentLength = artifact.ContentLength;
            return File(artifact.Content, EvaHandoffRevisionArtifact.MediaType, fileName);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEvaDownloadFailed(logger, id, exception);
            return NotFound();
        }
    }

    private static string? SafeEvaFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 255
            || value is "." or ".."
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Any(char.IsControl)
            || !value.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The EVA handoff download failed for case {CaseId}.")]
    private static partial void LogEvaDownloadFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);
}
