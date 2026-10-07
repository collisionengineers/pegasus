using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;

namespace Pegasus.Web.PrincipalApi;

internal sealed record PrincipalSubmissionReceiptResponse(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? PrincipalReference,
    bool Replayed);

internal sealed record PrincipalSubmissionResultResponse(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? PrincipalReference,
    QueuedIntakeStatusKind Status,
    IntakeDecision? Decision,
    IntakeAllocationFailureKind? AllocationFailure,
    string? FailureCode,
    string? CaseReference);

/// <summary>
/// Composition for the configuration-gated Principal API. Nothing here is
/// registered unless <c>Features:PrincipalApi</c> enabled it at startup; the
/// application otherwise exposes no such surface and answers 404.
/// </summary>
public static class PrincipalApiEndpoints
{
    public static IServiceCollection AddPegasusPrincipalApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, PrincipalApiAuthenticationHandler>(
                PrincipalApi.AuthenticationScheme,
                displayName: "Pegasus Principal API",
                _ => { });
        services.AddAuthorizationBuilder()
            .AddPolicy(PrincipalApi.EndpointPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(PrincipalApi.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });
        return services;
    }

    /// <summary>
    /// Maps the bearer-only principal surface. The endpoint policy
    /// authenticates exclusively with the principal scheme, so a staff cookie
    /// never reaches a handler; antiforgery is disabled because there is no
    /// cookie to forge.
    /// </summary>
    public static void MapPegasusPrincipalApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup(PrincipalApi.SubmissionsPath)
            .RequireAuthorization(PrincipalApi.EndpointPolicy)
            .RequireRateLimiting(PrincipalApi.RateLimitPolicy)
            .DisableAntiforgery();
        group.MapPost(string.Empty, SubmitAsync)
            .WithMetadata(new RequestSizeLimitAttribute(
                IntakeEnvelopeLimits.MaximumPrincipalApiRequestLength));
        group.MapGet("/{id:guid}", GetAsync);
    }

    private static async Task<IResult> SubmitAsync(
        HttpContext context,
        ClaimsPrincipal user,
        ISubmitPrincipalInstruction submit,
        ISecurityEventWriter securityEvents,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var credential = PrincipalApiAuthenticationHandler.ReadCredential(user);
        var request = context.Request;
        if (!request.HasJsonContentType())
        {
            return Problem(StatusCodes.Status415UnsupportedMediaType, "The submission must be application/json.");
        }
        if (!credential.MaySubmit)
        {
            await securityEvents.AppendAsync(
                new SecurityEvent(
                    Guid.NewGuid(),
                    SecurityEventType.Client,
                    SecurityEventOutcome.Denied,
                    credential.KeyId,
                    timeProvider.GetUtcNow(),
                    context.TraceIdentifier,
                    "principal_credential_paused")
                    // The credential is authenticated here, so the acting
                    // Principal is known even though the subject names the
                    // key that was presented (FRD-09).
                    .By(ActionActor.Principal(credential.PrincipalId)),
                cancellationToken);
            return Problem(StatusCodes.Status403Forbidden, "The principal credential is paused; submissions are refused until it is resumed.");
        }

        string idempotencyKey;
        try
        {
            idempotencyKey = PrincipalSubmissionPolicy.NormalizeIdempotencyKey(
                request.Headers[PrincipalApi.IdempotencyKeyHeader].ToString());
        }
        catch (ArgumentException exception)
        {
            return Problem(StatusCodes.Status400BadRequest, exception.Message);
        }

        // The body is retained exactly as it arrived, so the case's origin is
        // the principal's own instruction rather than a rendering of it. It is
        // read once, bounded, and both parsed and retained from the same bytes.
        var body = await ReadBodyAsync(request, cancellationToken);
        if (body is null)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "The submission exceeds the envelope limit.");
        }

        try
        {
            var receipt = await submit.ExecuteAsync(
                new(credential, idempotencyKey, body, context.TraceIdentifier),
                cancellationToken);
            if (!receipt.Replayed)
            {
                context.Response.Headers.Location = $"{PrincipalApi.SubmissionsPath}/{receipt.SubmissionId:D}";
            }

            return Results.Json(
                new PrincipalSubmissionReceiptResponse(
                    receipt.SubmissionId,
                    receipt.ReceivedAtUtc,
                    receipt.PrincipalReference,
                    receipt.Replayed),
                PrincipalInstructionJson.Options,
                statusCode: receipt.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created);
        }
        catch (PrincipalInstructionValidationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: exception.Message,
                extensions: new Dictionary<string, object?> { ["field"] = exception.Field });
        }
        catch (PrincipalSubmissionException exception)
        {
            return exception.Error switch
            {
                PrincipalSubmissionError.EnvelopeExceeded =>
                    Problem(StatusCodes.Status413PayloadTooLarge, "The submission exceeds the envelope limit."),
                _ => Problem(StatusCodes.Status409Conflict, "The idempotency key was already used with a different submission.")
            };
        }
        catch (IntakeArtifactRetentionException)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "The submission could not be retained; retry with the same idempotency key.");
        }
    }

    /// <summary>
    /// The whole body, or null when it runs past the envelope bound. The bound
    /// is enforced while reading rather than trusted from Content-Length, which
    /// a caller controls and a chunked request omits.
    /// </summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        try
        {
            while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > IntakeEnvelopeLimits.MaximumPrincipalApiRequestLength)
                {
                    return null;
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // The server's own request size limit is the same bound, and it
            // can stop the read first.
            return null;
        }

        return buffer.ToArray();
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal user,
        IGetPrincipalSubmissionResult getResult,
        CancellationToken cancellationToken)
    {
        var result = await getResult.ExecuteAsync(
            PrincipalApiAuthenticationHandler.ReadCredential(user),
            id,
            cancellationToken);
        if (result is null)
        {
            return Problem(StatusCodes.Status404NotFound, "The submission was not found.");
        }

        return Results.Json(
            new PrincipalSubmissionResultResponse(
                result.SubmissionId,
                result.ReceivedAtUtc,
                result.PrincipalReference,
                result.Status,
                result.Decision,
                result.AllocationFailure,
                result.FailureCode,
                result.CaseReference),
            PrincipalInstructionJson.Options);
    }

    private static IResult Problem(int statusCode, string title) =>
        Results.Problem(statusCode: statusCode, title: title);
}
