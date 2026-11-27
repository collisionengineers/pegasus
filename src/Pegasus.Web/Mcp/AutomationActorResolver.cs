using System.Security.Claims;
using ModelContextProtocol;
using OpenIddict.Abstractions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Authentication;

namespace Pegasus.Web.Mcp;

/// <summary>
/// Who a connector call is made for, and everything a tool reads from that
/// instead of assuming the Automation Actor: the Core actor, the scopes the
/// token holds, and the labels a document, an estimate, a staged upload and a
/// submitted source carry. A call is made for the Automation Actor (one of
/// its grants) or for a member of staff signed in from Pegasus Desktop
/// (ADR-0092); the resolver builds the one that applies and nothing else does.
/// <para>
/// What the assistant in Pegasus Desktop does for a member of staff is
/// recorded under their name and marked AI (operator, 26 November 2026;
/// R1304, R1306): the actor is the person, everywhere, and what a tool files
/// keeps the provenance mark the Automation Actor's carries, the document
/// source, its identity prefix, the AI-draft route and an estimate's wait for
/// Use repair spec. Each is decided here, in one place. A field that holds
/// one actor and nothing else (a note, an assessment value's recorder, a
/// settled address) names the person and so carries no AI mark.
/// </para>
/// </summary>
internal sealed class AutomationActorContext
{
    private AutomationActorContext(
        ActionActor actor,
        ActionActor uploadActor,
        string uploadOwner,
        string intakeSubmitter,
        IReadOnlyList<string> scopes,
        string traceIdentifier)
    {
        Actor = actor;
        UploadActor = uploadActor;
        UploadOwner = uploadOwner;
        IntakeSubmitter = intakeSubmitter;
        Scopes = scopes;
        TraceIdentifier = traceIdentifier;
    }

    /// <summary>
    /// The Automation Actor, named by the grant its token came from. A staged
    /// upload belongs to the Automation client, not the grant, so a part
    /// staged under one grant is used under another of the same client.
    /// </summary>
    public static AutomationActorContext ForAutomation(
        string clientId,
        string grantId,
        IReadOnlyList<string> scopes,
        string traceIdentifier) =>
        new(
            ActionActor.Automation(grantId),
            ActionActor.Automation(clientId),
            clientId,
            $"automation:{clientId}",
            scopes,
            traceIdentifier);

    /// <summary>
    /// A member of staff signed in from Pegasus Desktop, with the one role the
    /// account holds now. A staged upload and a submitted source belong to
    /// that person, written as the Upload page writes its uploader.
    /// </summary>
    public static AutomationActorContext ForStaff(
        Guid staffId,
        StaffRole role,
        IReadOnlyList<string> scopes,
        string traceIdentifier)
    {
        var actor = ActionActor.Staff(staffId, [role]);
        var uploader = IntakeActorIdentity.Staff(actor.SubjectId);
        return new(actor, actor, uploader, uploader, scopes, traceIdentifier);
    }

    public ActionActor Actor { get; }

    /// <summary>Whose key a staged upload's identifier is derived under.</summary>
    public ActionActor UploadActor { get; }

    /// <summary>Who a staged upload belongs to: only its owner sends its parts and uses it.</summary>
    public string UploadOwner { get; }

    /// <summary>Who a source submitted to intake is staged under.</summary>
    public string IntakeSubmitter { get; }

    /// <summary>The automation scopes the token holds, in the order of <see cref="AutomationMcp.Scopes"/>.</summary>
    public IReadOnlyList<string> Scopes { get; }

    public string TraceIdentifier { get; }

    /// <summary>Whether the call is made for a member of staff, not the Automation Actor.</summary>
    public bool IsStaff => Actor.Kind == ActorKind.Staff;

    /// <summary>
    /// The Action logs area a call is recorded in: the Automation Actor's
    /// calls in its own, a member of staff's in Pegasus Desktop's, under
    /// their own name.
    /// </summary>
    public string AuditArea => IsStaff
        ? AutomationClientRegistry.DesktopAggregateType
        : AutomationMcpAuditor.AutomationAggregateType;

    /// <summary>
    /// The provenance a document added through the connector carries, which
    /// draws its AI tag: the Automation source, whoever the call is made for
    /// (R1306).
    /// </summary>
    public DocumentSource DocumentSource { get; } = DocumentSource.Automation;

    /// <summary>The prefix of such a document's source-occurrence identity, for either kind (R1306).</summary>
    public string SourceIdentityPrefix { get; } = "automation:";

    /// <summary>
    /// The route an estimate typed in through the connector lands on: an AI
    /// draft, whoever it is recorded under (R1306).
    /// </summary>
    public RepairSpecificationSourceRoute EstimateRoute { get; } = RepairSpecificationSourceRoute.AiDraft;

    /// <summary>
    /// Whether an estimate made through the connector waits for Use repair
    /// spec in Pegasus itself: always, for either kind (R1306). The
    /// Automation Actor's waits by its kind; a member of staff's by this mark.
    /// </summary>
    public bool EstimateAwaitsUse { get; } = true;
}

/// <summary>
/// Resolves the authenticated principal of a connector call into the Core
/// actor before any tool touches a use case: the Automation Actor for the
/// Automation client's tokens, and the member of staff for a token issued by
/// a Pegasus Desktop sign-in (ADR-0092, R1304). Fails closed on a missing
/// principal, a disabled registration (the immediate kill switch, which is the
/// Automation client's alone), a member of staff whose account could no
/// longer sign in to Pegasus or whose security stamp has moved since the
/// sign-in, or a token that lacks the tool's per-area scope, writing an
/// attributable security event for every material denial. A tool's scope is
/// read from <see cref="AutomationToolScopes"/>, the table the tools/list
/// filter reads.
/// </summary>
internal sealed class AutomationActorResolver(
    IHttpContextAccessor httpContextAccessor,
    AutomationClientRegistry registry,
    AutomationMcpOptions options,
    ISecurityEventWriter securityEvents,
    TimeProvider timeProvider)
{
    public Task<AutomationActorContext> RequireAsync(
        string toolName,
        CancellationToken cancellationToken) =>
        RequireScopeAsync(AutomationToolScopes.RequiredScope(toolName), cancellationToken);

    /// <summary>
    /// The edit-lease tools, whose call takes the scope of the record kind:
    /// <paramref name="recordScope"/> must be one its table row lists.
    /// </summary>
    public Task<AutomationActorContext> RequireAsync(
        string toolName,
        string recordScope,
        CancellationToken cancellationToken) =>
        RequireScopeAsync(AutomationToolScopes.RequiredScope(toolName, recordScope), cancellationToken);

    /// <summary>
    /// The gate itself. The two content routes, which are not tools, call it
    /// with their endpoint's scope.
    /// </summary>
    public async Task<AutomationActorContext> RequireScopeAsync(
        string requiredScope,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new McpException("The automation request context is unavailable.");
        var principal = httpContext.User;
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.GetClaim(OpenIddictConstants.Claims.Subject);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(subject))
        {
            await DenyAsync(
                httpContext,
                SecurityEventType.Token,
                "anonymous",
                actor: null,
                "automation_token_rejected",
                cancellationToken);
            throw new McpException("The automation authorization is not valid.");
        }

        // A token that names a member of staff is never read as the
        // Automation Actor, whatever else it carries (ADR-0092).
        return DesktopPrincipal.IsDesktop(principal)
            ? await RequireStaffAsync(httpContext, principal, subject, requiredScope, cancellationToken)
            : await RequireAutomationAsync(httpContext, principal, subject, requiredScope, cancellationToken);
    }

    private async Task<AutomationActorContext> RequireAutomationAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        string clientId,
        string requiredScope,
        CancellationToken cancellationToken)
    {
        if (!await registry.IsEnabledAsync(clientId, cancellationToken))
        {
            await DenyAsync(
                httpContext,
                SecurityEventType.Client,
                clientId,
                ActionActor.Automation(clientId),
                "automation_client_disabled",
                cancellationToken);
            httpContext.Items[AutomationMcp.RefusalRecordedItem] = true;
            throw new McpException("The Automation client registration is disabled.");
        }

        if (!principal.HasScope(requiredScope))
        {
            await DenyAsync(
                httpContext,
                SecurityEventType.Token,
                clientId,
                ActionActor.Automation(clientId),
                "automation_scope_denied",
                cancellationToken);
            throw new McpException(
                $"The '{requiredScope}' scope is required for this tool.");
        }
        var grantId = principal.FindFirstValue(AutomationMcp.GrantIdentityClaim);
        if (string.IsNullOrWhiteSpace(grantId))
        {
            throw new McpException("The automation grant identity is missing.");
        }

        return AutomationActorContext.ForAutomation(
            clientId,
            grantId,
            HeldScopes(principal),
            httpContext.TraceIdentifier);
    }

    /// <summary>
    /// A Pegasus Desktop token acts as the member of staff it names (R1304),
    /// and only while that account could still sign in to Pegasus: the read
    /// the staff cookie makes on every request (<see cref="StaffAccountReader"/>)
    /// must show it enabled, not waiting on a password change, holding exactly
    /// one staff role, and with the security stamp the token carries, so a
    /// disable, a role change, a password reset and a forced sign-out each end
    /// the session at the next call (ADR-0092 decision 5). The role is the
    /// account's now, never one a token carries.
    /// <para>
    /// Stopping the Automation client does not stop a member of staff signed
    /// in from Pegasus Desktop (operator, 26 November 2026; R1307): the kill
    /// switch is the Automation client's and is not read here. A sign-in ends
    /// when the account is disabled, its password is reset, an Administrator
    /// signs the person out, or after 8 hours.
    /// </para>
    /// </summary>
    private async Task<AutomationActorContext> RequireStaffAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        string subject,
        string requiredScope,
        CancellationToken cancellationToken)
    {
        var stamp = principal.GetClaim(AutomationMcp.StaffStampClaim);
        var staffId = options.DesktopSignInEnabled && Guid.TryParse(subject, out var parsed)
            ? parsed
            : Guid.Empty;
        var account = staffId == Guid.Empty
            ? null
            : await StaffAccountReader.ReadAsync(httpContext.RequestServices, staffId, cancellationToken);
        if (account is not { IsEnabled: true, MustChangePassword: false, Role: { } role }
            || string.IsNullOrEmpty(stamp)
            || !StaffPrincipalValidator.StampMatches(account.SecurityStamp, stamp)
            || string.IsNullOrWhiteSpace(principal.FindFirstValue(AutomationMcp.GrantIdentityClaim)))
        {
            await DenyDesktopAsync(httpContext, subject, cancellationToken);
            throw new McpException("The automation authorization is not valid.");
        }

        if (!principal.HasScope(requiredScope))
        {
            await DenyDesktopAsync(httpContext, subject, cancellationToken);
            throw new McpException(
                $"The '{requiredScope}' scope is required for this tool.");
        }

        return AutomationActorContext.ForStaff(
            staffId,
            role,
            HeldScopes(principal),
            httpContext.TraceIdentifier);
    }

    private static string[] HeldScopes(ClaimsPrincipal principal) =>
        [.. AutomationMcp.Scopes.Where(principal.HasScope)];

    /// <summary>
    /// Records a refused Pegasus Desktop call under the member of staff the
    /// token names. The reason code is not an <c>automation_</c> one, so the
    /// refusal does not count as Automation activity, and the transport
    /// denial is not recorded again.
    /// </summary>
    private async Task DenyDesktopAsync(
        HttpContext httpContext,
        string subject,
        CancellationToken cancellationToken)
    {
        var securityEvent = new SecurityEvent(
            Guid.NewGuid(),
            SecurityEventType.Token,
            SecurityEventOutcome.Denied,
            subject,
            timeProvider.GetUtcNow(),
            httpContext.TraceIdentifier,
            AutomationMcp.DesktopAccessDenied);
        await securityEvents.AppendAsync(
            Guid.TryParse(subject, out _)
                ? securityEvent.By(ActorKind.Staff, subject)
                : securityEvent,
            cancellationToken);
        httpContext.Items[AutomationMcp.RefusalRecordedItem] = true;
    }

    /// <summary>
    /// Records a refused automation request. <paramref name="actor"/> is the
    /// client that presented an identity; a request that carried none is
    /// anonymous, which no actor kind represents, so it is recorded
    /// unattributed and read by what it is.
    /// </summary>
    private Task DenyAsync(
        HttpContext httpContext,
        SecurityEventType type,
        string subjectId,
        ActionActor? actor,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var securityEvent = new SecurityEvent(
            Guid.NewGuid(),
            type,
            SecurityEventOutcome.Denied,
            subjectId,
            timeProvider.GetUtcNow(),
            httpContext.TraceIdentifier,
            reasonCode);
        return securityEvents.AppendAsync(
            actor is null ? securityEvent : securityEvent.By(actor),
            cancellationToken);
    }
}

/// <summary>
/// Writes one permanent action-history entry per MCP tool invocation,
/// attributed to whoever the call was made for (the Automation actor, or the
/// member of staff signed in from Pegasus Desktop, in the area
/// <see cref="AutomationActorContext.AuditArea"/> names) with the operation's
/// correlation identifier: the idempotency operation key when the tool carries one,
/// otherwise the request trace identifier. Business history keeps being
/// written by the Core use cases themselves; this is the ingress attribution
/// record that the Admin activity view consolidates.
/// </summary>
internal sealed class AutomationMcpAuditor(
    AutomationActorResolver resolver,
    IActionHistoryWriter actionHistory,
    TimeProvider timeProvider,
    ILogger<AutomationMcpAuditor> logger)
{
    /// <summary>The action-history area of a call made for the Automation Actor.</summary>
    public const string AutomationAggregateType = "automation_mcp";

    private static readonly Action<ILogger, string, string, string, Exception?> LogMechanicSucceeded =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1, nameof(RecordDenialAsync)),
            "Automation MCP {Tool} succeeded for {AggregateId} (correlation {CorrelationId}).");

    /// <summary>
    /// One tool call that carries no operation key: gates it on the tool's
    /// scope, then records it as case work around <paramref name="body"/>,
    /// whose refusals become content-safe MCP errors.
    /// </summary>
    public async Task<TResult> CallAsync<TResult>(
        string toolName,
        string aggregateId,
        Func<AutomationActorContext, Task<TResult>> body,
        CancellationToken cancellationToken)
    {
        var context = await resolver.RequireAsync(toolName, cancellationToken);
        return await RecordAsync(
            context,
            toolName,
            aggregateId,
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(context, () => body(context)),
            cancellationToken);
    }

    /// <summary>
    /// As the call above, for a tool that carries the caller's operation key:
    /// the key is validated after the gate and is the call's correlation
    /// identifier.
    /// </summary>
    public async Task<TResult> CallAsync<TResult>(
        string toolName,
        string aggregateId,
        string operationKey,
        Func<AutomationActorContext, string, Task<TResult>> body,
        CancellationToken cancellationToken)
    {
        var context = await resolver.RequireAsync(toolName, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await RecordAsync(
            context,
            toolName,
            aggregateId,
            key,
            () => AutomationMcpErrors.ExecuteAsync(context, () => body(context, key)),
            cancellationToken);
    }

    /// <summary>
    /// As the calls above, for the tool whose operation key is optional: with
    /// a key it is the keyed call, and without one the call that carries none.
    /// </summary>
    public Task<TResult> CallWithOptionalKeyAsync<TResult>(
        string toolName,
        string aggregateId,
        string? operationKey,
        Func<AutomationActorContext, string?, Task<TResult>> body,
        CancellationToken cancellationToken) =>
        operationKey is null
            ? CallAsync(toolName, aggregateId, context => body(context, null), cancellationToken)
            : CallAsync(toolName, aggregateId, operationKey, (context, key) => body(context, key), cancellationToken);

    /// <summary>
    /// The audit resource of a call about one record: its identifier, or
    /// <c>invalid</c> for the empty one the call then refuses.
    /// </summary>
    public static string Resource(Guid id) => id == Guid.Empty ? "invalid" : id.ToString("D");

    /// <summary>
    /// Records a tool call whose success is itself attributable case work.
    /// </summary>
    public async Task<TResult> RecordAsync<TResult>(
        AutomationActorContext context,
        string toolName,
        string aggregateId,
        string? operationKey,
        Func<Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            var result = await action();
            await AppendAsync(
                context,
                toolName,
                aggregateId,
                operationKey,
                "Succeeded",
                reason: null,
                cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            await AppendAsync(
                context,
                toolName,
                aggregateId,
                operationKey,
                "Failed",
                Reason(exception),
                cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Records a tool call that is lease or adapter mechanics rather than case work. The
    /// requirement classifies routine renewal, expiry, heartbeat, polling and adapter mechanics as
    /// telemetry and keeps only a deliberate recovery or a material denial in permanent history, so
    /// success is logged and refusal is still appended.
    /// </summary>
    public Task<TResult> RecordDenialAsync<TResult>(
        AutomationActorContext context,
        string toolName,
        string aggregateId,
        string? operationKey,
        Func<Task<TResult>> action,
        CancellationToken cancellationToken) =>
        RecordMechanicAsync(context, toolName, aggregateId, operationKey, logSuccess: true, action, cancellationToken);

    /// <summary>
    /// As <c>RecordDenialAsync</c>, for a call repeated for every part of one upload:
    /// its success is not logged, because its request telemetry already counts it and a
    /// 10 MiB document would otherwise log 214 lines; a refusal is still appended.
    /// </summary>
    public Task<TResult> RecordRefusalAsync<TResult>(
        AutomationActorContext context,
        string toolName,
        string aggregateId,
        string? operationKey,
        Func<Task<TResult>> action,
        CancellationToken cancellationToken) =>
        RecordMechanicAsync(context, toolName, aggregateId, operationKey, logSuccess: false, action, cancellationToken);

    private async Task<TResult> RecordMechanicAsync<TResult>(
        AutomationActorContext context,
        string toolName,
        string aggregateId,
        string? operationKey,
        bool logSuccess,
        Func<Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            var result = await action();
            if (logSuccess)
            {
                LogMechanicSucceeded(
                    logger,
                    toolName,
                    aggregateId,
                    CorrelationId(context, operationKey),
                    null);
            }

            return result;
        }
        catch (Exception exception)
        {
            await AppendAsync(
                context,
                toolName,
                aggregateId,
                operationKey,
                "Failed",
                Reason(exception),
                cancellationToken);
            throw;
        }
    }

    public static string CorrelationId(AutomationActorContext context, string? operationKey) =>
        NormalizedOperationKey(operationKey) ?? context.TraceIdentifier;

    private Task AppendAsync(
        AutomationActorContext context,
        string toolName,
        string aggregateId,
        string? operationKey,
        string outcome,
        string? reason,
        CancellationToken cancellationToken) =>
        actionHistory.AppendAsync(
            new ActionHistoryEntry(
                Guid.NewGuid(),
                context.AuditArea,
                Truncate(aggregateId, 200),
                toolName,
                context.Actor,
                timeProvider.GetUtcNow(),
                outcome,
                CorrelationId(context, operationKey),
                reason),
            cancellationToken);

    private static string? NormalizedOperationKey(string? operationKey)
    {
        var normalized = operationKey?.Trim();
        return normalized is { Length: > 0 and <= 100 } ? normalized : null;
    }

    private static string Reason(Exception exception)
    {
        var reason = $"{exception.GetType().Name}: {exception.Message}";
        return Truncate(reason, 1000);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
