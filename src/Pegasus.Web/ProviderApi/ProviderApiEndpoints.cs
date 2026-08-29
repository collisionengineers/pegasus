using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Web.ProviderApi;

internal sealed record ProviderSubmissionFileResponse(
    int Ordinal,
    string FileName,
    string Sha256,
    bool Duplicate);

internal sealed record ProviderSubmissionReceiptResponse(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? ProviderReference,
    bool Replayed,
    IReadOnlyList<ProviderSubmissionFileResponse> Files);

internal sealed record ProviderSubmissionResultResponse(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? ProviderReference,
    QueuedIntakeStatusKind Status,
    IntakeDecision? Decision,
    IntakeAllocationFailureKind? AllocationFailure,
    string? FailureCode,
    string? CaseReference);

/// <summary>
/// Composition for the configuration-gated Provider API. Nothing here is
/// registered unless <c>Features:ProviderApi</c> enabled it at startup; the
/// application otherwise exposes no such surface and answers 404.
/// </summary>
public static class ProviderApiEndpoints
{
    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static IServiceCollection AddPegasusProviderApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ProviderApiAuthenticationHandler>(
                ProviderApi.AuthenticationScheme,
                displayName: "Pegasus Provider API",
                _ => { });
        services.AddAuthorizationBuilder()
            .AddPolicy(ProviderApi.EndpointPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(ProviderApi.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ProviderApi.KeyIdClaim);
            });
        return services;
    }

    /// <summary>
    /// Maps the bearer-only provider surface. The endpoint policy
    /// authenticates exclusively with the provider scheme, so a staff cookie
    /// never reaches a handler; antiforgery is disabled because there is no
    /// cookie to forge.
    /// </summary>
    public static void MapPegasusProviderApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup(ProviderApi.SubmissionsPath)
            .RequireAuthorization(ProviderApi.EndpointPolicy)
            .RequireRateLimiting(ProviderApi.RateLimitPolicy)
            .DisableAntiforgery();
        group.MapPost(string.Empty, SubmitAsync)
            .WithMetadata(new RequestSizeLimitAttribute(
                IntakeEnvelopeLimits.MaximumProviderApiRequestLength));
        group.MapGet("/{id:guid}", GetAsync);
    }

    private static async Task<IResult> SubmitAsync(
        HttpContext context,
        ClaimsPrincipal user,
        ISubmitProviderInstruction submit,
        ISecurityEventWriter securityEvents,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var credential = ProviderApiAuthenticationHandler.ReadCredential(user);
        if (credential is null)
        {
            return Problem(StatusCodes.Status401Unauthorized, "The provider credential is missing or not valid.");
        }

        var request = context.Request;
        if (request.ContentLength > IntakeEnvelopeLimits.MaximumProviderApiRequestLength)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "The submission exceeds the envelope limit.");
        }
        if (!IsJson(request.ContentType))
        {
            return Problem(StatusCodes.Status415UnsupportedMediaType, "The submission must be application/json.");
        }

        try
        {
            ProviderSubmissionPolicy.RequireMaySubmit(credential);

            // The body is retained exactly as it arrived, so the case's origin is
            // the provider's own instruction rather than a rendering of it. It is
            // read once, bounded, and both parsed and retained from the same bytes.
            var body = await ReadBodyAsync(request, cancellationToken);
            if (body is null)
            {
                return Problem(StatusCodes.Status413PayloadTooLarge, "The submission exceeds the envelope limit.");
            }

            var (instruction, files) = ProviderInstructionJson.Parse(body);
            var receipt = await submit.ExecuteAsync(
                new(
                    credential,
                    request.Headers[ProviderApi.IdempotencyKeyHeader].ToString(),
                    instruction,
                    files,
                    body,
                    context.TraceIdentifier),
                cancellationToken);
            var responseBody = new ProviderSubmissionReceiptResponse(
                receipt.SubmissionId,
                receipt.ReceivedAtUtc,
                receipt.ProviderReference,
                receipt.Replayed,
                receipt.Files
                    .Select(file => new ProviderSubmissionFileResponse(
                        file.Ordinal, file.FileName, file.Sha256, file.IsDuplicate))
                    .ToArray());
            if (!receipt.Replayed)
            {
                context.Response.Headers.Location = $"{ProviderApi.SubmissionsPath}/{receipt.SubmissionId:D}";
            }

            return Results.Json(
                responseBody,
                ResponseJson,
                statusCode: receipt.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created);
        }
        catch (ProviderInstructionValidationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: exception.Message,
                extensions: new Dictionary<string, object?> { ["field"] = exception.Field });
        }
        catch (ProviderSubmissionException exception)
        {
            if (exception.Error is ProviderSubmissionError.CredentialPaused
                or ProviderSubmissionError.PrincipalMismatch)
            {
                await securityEvents.AppendAsync(
                    new SecurityEvent(
                        Guid.NewGuid(),
                        SecurityEventType.Client,
                        SecurityEventOutcome.Denied,
                        credential.KeyId,
                        timeProvider.GetUtcNow(),
                        context.TraceIdentifier,
                        exception.Error == ProviderSubmissionError.CredentialPaused
                            ? "provider_credential_paused"
                            : "provider_principal_mismatch"),
                    cancellationToken);
            }

            return exception.Error switch
            {
                ProviderSubmissionError.CredentialPaused =>
                    Problem(StatusCodes.Status403Forbidden, "The provider credential is paused; submissions are refused until it is resumed."),
                ProviderSubmissionError.PrincipalMismatch =>
                    Problem(StatusCodes.Status403Forbidden, "The submission names a principal other than the authenticated one."),
                ProviderSubmissionError.EnvelopeExceeded =>
                    Problem(StatusCodes.Status413PayloadTooLarge, "The submission exceeds the envelope limit."),
                ProviderSubmissionError.IdempotencyKeyConflict =>
                    Problem(StatusCodes.Status409Conflict, "The idempotency key was already used with a different submission."),
                _ => Problem(StatusCodes.Status409Conflict, "The submission conflicted with a concurrent request; retry.")
            };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            return Problem(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (StaffAuthorizationException)
        {
            return Problem(StatusCodes.Status403Forbidden, "The provider credential may not perform this operation.");
        }
        catch (IntakeArtifactRetentionException)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "The submission could not be retained; retry with the same idempotency key.");
        }
    }

    private static bool IsJson(string? contentType) =>
        contentType is not null
        && contentType.StartsWith(ProviderInstructionPolicy.SourceMediaType, StringComparison.OrdinalIgnoreCase);

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
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > IntakeEnvelopeLimits.MaximumProviderApiRequestLength)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal user,
        IGetProviderSubmissionResult getResult,
        CancellationToken cancellationToken)
    {
        var credential = ProviderApiAuthenticationHandler.ReadCredential(user);
        if (credential is null)
        {
            return Problem(StatusCodes.Status401Unauthorized, "The provider credential is missing or not valid.");
        }

        var result = await getResult.ExecuteAsync(credential, id, cancellationToken);
        if (result is null)
        {
            return Problem(StatusCodes.Status404NotFound, "The submission was not found.");
        }

        return Results.Json(
            new ProviderSubmissionResultResponse(
                result.SubmissionId,
                result.ReceivedAtUtc,
                result.ProviderReference,
                result.Status,
                result.Decision,
                result.AllocationFailure,
                result.FailureCode,
                result.CaseReference),
            ResponseJson);
    }

    private static IResult Problem(int statusCode, string title) =>
        Results.Problem(statusCode: statusCode, title: title);
}
