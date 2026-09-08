using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Mcp;

internal static class AutomationDocumentStreaming
{
    private const string ExportTicketPurpose = "Pegasus.Automation.DocumentExports.v1";
    internal const int MaximumExportSelections = 32;

    internal sealed record ExportTicket(
        Guid CaseId,
        IReadOnlyList<DocumentExportSelection> Selections,
        long ExpectedCaseVersion,
        string EditLeaseToken,
        string OperationKey,
        string GrantId,
        DateTimeOffset ExpiresAtUtc);

    public static async Task<IResult> GetAsync(
        Guid occurrenceId,
        Guid versionId,
        Guid caseId,
        AutomationActorResolver resolver,
        IGetCaseDocumentMetadata metadataReader,
        IReadLogicalDocumentVersion contentReader,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var metadata = await metadataReader.ExecuteAsync(
            new(caseId, occurrenceId, versionId, actor.Actor), cancellationToken);
        if (metadata is null) return Results.NotFound();

        var content = await contentReader.OpenAsync(
            new(
                Actor: actor.Actor,
                DocumentId: metadata.DocumentId,
                VersionId: versionId,
                IntakeAssetId: null,
                CaseId: caseId,
                IntakeReceiptId: null,
                ExpectedSha256: metadata.Sha256,
                ExpectedContentLength: metadata.ContentLength),
            cancellationToken);
        httpContext.Response.Headers.ETag = $"\"{metadata.Sha256}\"";
        return Results.Stream(
            content.Content,
            metadata.MediaType,
            metadata.FileName,
            enableRangeProcessing: true);
    }

    public static async Task<IResult> GetExportAsync(
        string ticket,
        AutomationActorResolver resolver,
        IExportCaseDocuments exportDocuments,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        ExportTicket request;
        try
        {
            request = UnprotectExport(dataProtectionProvider, ticket);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.NotFound();
        }
        if (!string.Equals(request.GrantId, context.GrantId, StringComparison.Ordinal)
            || request.Selections is not { Count: > 0 and <= MaximumExportSelections }
            || request.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return Results.NotFound();
        }
        var export = await exportDocuments.ExecuteAsync(
            new(
                request.CaseId,
                request.Selections,
                context.Actor,
                request.OperationKey,
                20 * 1024 * 1024,
                request.ExpectedCaseVersion,
                request.EditLeaseToken),
            cancellationToken);
        return Results.Stream(
            export.Content,
            "application/zip",
            export.FileName,
            enableRangeProcessing: false);
    }

    internal static string ProtectExport(
        IDataProtectionProvider provider,
        ExportTicket ticket) =>
        provider.CreateProtector(ExportTicketPurpose)
            .Protect(JsonSerializer.Serialize(ticket));

    private static ExportTicket UnprotectExport(
        IDataProtectionProvider provider,
        string ticket)
    {
        try
        {
            return JsonSerializer.Deserialize<ExportTicket>(
                provider.CreateProtector(ExportTicketPurpose).Unprotect(ticket))
                ?? throw new InvalidDataException("The export ticket is invalid.");
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException
            or JsonException or FormatException or InvalidDataException)
        {
            throw new UnauthorizedAccessException("The export ticket is invalid or stale.", exception);
        }
    }
}
