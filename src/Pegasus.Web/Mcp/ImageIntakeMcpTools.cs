using System.ComponentModel;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record ImageIntakeToolSummary(
    Guid Id,
    string Reference,
    string Registration,
    string State,
    DateTimeOffset RegisteredAtUtc,
    int ImageCount,
    string Source,
    string? PrincipalCode,
    Guid OriginReceiptId);

internal sealed record ImageIntakeListToolResult(
    IReadOnlyList<ImageIntakeToolSummary> Items,
    string? NextCursor,
    int Limit,
    string CorrelationId);

internal sealed record ImageIntakeToolImage(
    Guid ReceiptId,
    Guid? AssetId,
    string FileName,
    string MediaType,
    string CustodyState);

internal sealed record ImageIntakeToolCandidate(
    Guid CaseId,
    string CaseReference,
    long CaseVersion,
    string ConfirmedRegistration,
    string? PrincipalCode,
    bool IsTriage);

internal sealed record ImageIntakeToolPrincipal(Guid PrincipalId, string Code);

internal sealed record ImageIntakeToolHistoryEntry(
    string EventType,
    string ActorKind,
    string ActorSubjectId,
    DateTimeOffset OccurredAtUtc,
    string Reason,
    long BeforeVersion,
    long AfterVersion,
    string? CaseReference);

internal sealed record ImageIntakeToolDetail(
    Guid Id,
    string Reference,
    string Registration,
    string State,
    long Version,
    Guid? PrincipalId,
    string? PrincipalCode,
    Guid OriginReceiptId,
    Guid? SubmissionGroupId,
    string Source,
    DateTimeOffset RegisteredAtUtc,
    Guid? AssociatedCaseId,
    string? AssociatedCaseReference,
    string? ClosureReason,
    DateTimeOffset? ClosedAtUtc,
    IReadOnlyList<ImageIntakeToolImage> Images,
    IReadOnlyList<RegistrationReadingToolItem> Readings,
    IReadOnlyList<ImageIntakeToolCandidate> Candidates,
    string? AutomationWithheld,
    Guid? TriageCaseId,
    string? TriageReference,
    IReadOnlyList<ImageIntakeToolPrincipal> PrincipalOptions,
    IReadOnlyList<ImageIntakeToolHistoryEntry> History,
    string CorrelationId);

/// <summary>
/// The Image Intake record (staff label Vehicle images) for the Automation
/// Actor: the list the Cases page's images awaiting instruction shows, the
/// record as its page shows it, and the page's two changes, recording the
/// known Principal and Close with reason, through the same store commands
/// under <c>automation.intake</c>. Each change presents an Image Intake lease
/// from <c>pegasus_edit_begin</c>, or holds the record's edit scope for its
/// one command (FRD-14 Record edit scopes).
/// </summary>
[McpServerToolType]
internal sealed class ImageIntakeMcpTools(
    IImageIntakeStore imageIntakes,
    IVrmSuggestionStore vrmSuggestions,
    IImageIntakeCaseCandidates caseCandidates,
    ITriageQueries triages,
    OneCommandEdit leases,
    ICursorProtector cursors,
    AutomationMcpAuditor auditor)
{
    private const string IdDescription = "The Image Intake record identifier (id from pegasus_image_intake_list).";

    [McpServerTool(
        Name = "pegasus_image_intake_list",
        Title = "List Vehicle images awaiting instruction",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists the Image Intake records (staff label Vehicle images) awaiting instruction with no Case, newest first, as the Cases page lists them. Each item carries its id, Image Intake Reference, registration and state.")]
    public async Task<ImageIntakeListToolResult> ListAsync(
        [Description(AutomationToolText.Cursor)] string? cursor = null,
        [Description(AutomationToolText.PageSize)] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_image_intake_list",
            "image-intake",
            async context =>
            {
                var effectiveLimit = CursorPaging.NormalizeLimit(limit);
                var scope = CursorPaging.CreateScope(
                    "pegasus_image_intake_list", context.Actor, "unassociated", "awaiting-instruction", "newest-first");
                var skip = 0;
                if (!string.IsNullOrWhiteSpace(cursor))
                {
                    var position = cursors.Unprotect(cursor, scope);
                    // The page resumes after the last record it returned, wherever
                    // that record now stands; one that has left the list resumes
                    // at the offset the cursor recorded.
                    skip = await imageIntakes.ListPositionAsync(
                            associated: false,
                            ImageInitiatedCaseState.AwaitingInstruction,
                            position.Id,
                            cancellationToken) is { } place
                        ? place + 1
                        : int.TryParse(position.SortKey, NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                            ? offset
                            : throw new CursorRejectedException();
                }

                var window = await imageIntakes.ListWindowAsync(
                    associated: false,
                    ImageInitiatedCaseState.AwaitingInstruction,
                    skip,
                    effectiveLimit + 1,
                    cancellationToken);
                var items = window.Take(effectiveLimit).ToArray();
                var next = window.Count > effectiveLimit
                    ? cursors.Protect(
                        scope,
                        (skip + items.Length).ToString(CultureInfo.InvariantCulture),
                        items[^1].Id)
                    : null;
                return new ImageIntakeListToolResult(
                    items.Select(item => new ImageIntakeToolSummary(
                        item.Id,
                        item.ImageIntakeReference,
                        item.NormalizedVehicleRegistration,
                        item.State.ToString(),
                        item.RegisteredAtUtc,
                        item.ImageCount,
                        item.Source.ToString(),
                        item.PrincipalCode,
                        item.OriginReceiptId)).ToArray(),
                    next,
                    effectiveLimit,
                    context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_image_intake_get",
        Title = "Get a Vehicle images record",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets one Image Intake record (staff label Vehicle images) as its page shows it: Image Intake Reference, registration, state and version, the known Principal, the Case it is associated with, its images, its registration readings, the eligible Cases its registration matches while it has none, why automatic association leaves it to staff, the Triage its origin opened, its history, and the active Principals pegasus_image_intake_action set_principal takes.")]
    public async Task<ImageIntakeToolDetail> GetAsync(
        [Description(IdDescription)] Guid imageIntakeId,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_image_intake_get",
            AutomationMcpAuditor.Resource(imageIntakeId),
            context => ReadDetailAsync(
                AutomationMcpErrors.RequireId(imageIntakeId, "Image Intake record identifier"),
                context.TraceIdentifier,
                cancellationToken),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_image_intake_action",
        Title = "Act on a Vehicle images record",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("One change to an Image Intake record (staff label Vehicle images), through the same store commands as its page. set_principal records, replaces or clears the known Principal: principalId from pegasus_image_intake_get principalOptions, or omitted for Not known; it takes no reason. close is Close with reason: the record leaves those awaiting instruction for good. Each needs the record version from pegasus_image_intake_get and returns the updated record.")]
    public async Task<ImageIntakeToolDetail> ActAsync(
        [Description(IdDescription)] Guid imageIntakeId,
        [Description("set_principal or close.")] string action,
        [Description("The record version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("set_principal only: the active Principal's identifier; omit for Not known.")] Guid? principalId = null,
        [Description("close only: why the record is closed, at most 500 characters.")] string? reason = null,
        [Description("Edit lease token from pegasus_edit_begin (recordKind ImageIntake) for multi-step work; omit it and the tool holds the record's edit scope for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_image_intake_action",
            AutomationMcpAuditor.Resource(imageIntakeId),
            operationKey,
            async (context, key) =>
            {
                var id = AutomationMcpErrors.RequireId(imageIntakeId, "Image Intake record identifier");
                var close = action?.Trim() switch
                {
                    "set_principal" => false,
                    "close" => true,
                    _ => throw new McpException("action must be set_principal or close.")
                };
                if (close ? principalId is not null : reason is not null)
                {
                    throw new McpException(close
                        ? "close takes reason and no principalId."
                        : "set_principal takes principalId and no reason.");
                }

                _ = await imageIntakes.GetAsync(id, cancellationToken)
                    ?? throw new McpException("The Image Intake record was not found.");
                try
                {
                    await leases.RunImageIntakeAsync(
                        id,
                        expectedVersion,
                        editLeaseToken,
                        context.Actor,
                        key,
                        async token => close
                            ? await imageIntakes.CloseAsync(
                                new(id, context.Actor, key, reason ?? string.Empty, expectedVersion)
                                {
                                    EditLeaseToken = token
                                },
                                cancellationToken)
                            : await imageIntakes.SetPrincipalAsync(
                                new(id, principalId, context.Actor, expectedVersion)
                                {
                                    EditLeaseToken = token
                                },
                                cancellationToken),
                        cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // The store refuses a record that moved past the version read
                    // as changed; the refusal names the version it is at now.
                    var current = await imageIntakes.GetAsync(id, cancellationToken);
                    throw new EditScopeVersionConflictException(
                        EditScopeKind.ImageIntake,
                        id,
                        expectedVersion,
                        current?.LifecycleVersion ?? expectedVersion);
                }

                return await ReadDetailAsync(id, AutomationMcpAuditor.CorrelationId(context, key), cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>The record and what its page reads beside it.</summary>
    private async Task<ImageIntakeToolDetail> ReadDetailAsync(
        Guid id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var detail = await imageIntakes.GetAsync(id, cancellationToken)
            ?? throw new McpException("The Image Intake record was not found.");
        var record = detail.Record;
        var images = await imageIntakes.ListImagesAsync(id, cancellationToken);
        var readings = await vrmSuggestions.ListForReceiptAsync(record.Origin.ReceiptId, cancellationToken);
        IReadOnlyList<ImageIntakeCaseCandidate> candidates = detail.AssociatedCaseId is null
            ? await caseCandidates.FindEligibleByRegistrationAsync(record.NormalizedVehicleRegistration, cancellationToken)
            : [];
        var triage = await triages.GetByOriginReceiptAsync(record.Origin.ReceiptId, cancellationToken);
        var principals = await imageIntakes.ListActivePrincipalsAsync(cancellationToken);
        var history = await imageIntakes.ListHistoryAsync(id, cancellationToken);
        var withheld = detail.State == ImageInitiatedCaseState.AwaitingInstruction
            ? ImageIntakeCasePairing.ExplainWithheld(
                candidates,
                record.NormalizedVehicleRegistration,
                record.PrincipalId,
                detail.GroupExpectedMemberCount,
                detail.DeclaredIncidentDate)
            : null;
        return new ImageIntakeToolDetail(
            record.Id,
            record.ImageIntakeReference,
            record.NormalizedVehicleRegistration,
            detail.State.ToString(),
            detail.LifecycleVersion,
            record.PrincipalId,
            detail.PrincipalCode,
            record.Origin.ReceiptId,
            record.SubmissionGroupId,
            record.Origin.SourceIdentity.Channel.ToString(),
            detail.RegisteredAtUtc,
            detail.AssociatedCaseId,
            detail.AssociatedCaseReference,
            detail.ClosureReason,
            detail.ClosedAtUtc,
            images.Select(image => new ImageIntakeToolImage(
                image.ReceiptId,
                image.AssetId,
                image.FileName,
                image.MediaType,
                image.CustodyState.ToString())).ToArray(),
            readings.Select(RegistrationReadingToolItem.From).ToArray(),
            candidates.Select(candidate => new ImageIntakeToolCandidate(
                candidate.CaseId,
                candidate.CaseReference,
                candidate.CaseVersion,
                candidate.ConfirmedRegistration,
                candidate.PrincipalCode,
                candidate.IsTriage)).ToArray(),
            withheld?.ToString(),
            triage?.CaseId,
            triage?.Reference,
            principals.Select(principal => new ImageIntakeToolPrincipal(principal.Id, principal.Code)).ToArray(),
            history.Select(entry => new ImageIntakeToolHistoryEntry(
                entry.EventType,
                entry.Actor.Kind.ToString(),
                entry.Actor.SubjectId,
                entry.OccurredAtUtc,
                entry.Reason,
                entry.BeforeVersion,
                entry.AfterVersion,
                entry.CaseReference)).ToArray(),
            correlationId);
    }
}
