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

internal sealed record ProviderSubmissionFileResultResponse(
    int Ordinal,
    string FileName,
    QueuedIntakeStatusKind Status,
    IntakeDecision? Decision,
    IntakeAllocationFailureKind? AllocationFailure,
    string? FailureCode,
    string? CaseReference);

internal sealed record ProviderSubmissionResultResponse(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? ProviderReference,
    QueuedIntakeStatusKind Status,
    string? CaseReference,
    IReadOnlyList<ProviderSubmissionFileResultResponse> Files);

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
            .WithMetadata(new RequestSizeLimitAttribute(IntakeEnvelopeLimits.MaximumBatchContentLength));
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
        if (request.ContentLength > IntakeEnvelopeLimits.MaximumBatchContentLength)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge, "The submission exceeds the envelope limit.");
        }
        if (!request.HasFormContentType)
        {
            return Problem(StatusCodes.Status415UnsupportedMediaType, "The submission must be multipart/form-data.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var files = new List<ProviderSubmissionFile>(form.Files.Count);
        foreach (var file in form.Files)
        {
            if (file.Length > IntakeEnvelopeLimits.MaximumContentLength)
            {
                return Problem(StatusCodes.Status413PayloadTooLarge, "A file exceeds the per-file limit.");
            }

            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, cancellationToken);
            files.Add(new(files.Count, file.FileName, file.ContentType, buffer.ToArray()));
        }

        try
        {
            var receipt = await submit.ExecuteAsync(
                new(
                    credential,
                    request.Headers[ProviderApi.IdempotencyKeyHeader].ToString(),
                    form[ProviderApi.ProviderReferenceField].ToString(),
                    files,
                    context.TraceIdentifier),
                cancellationToken);
            var body = new ProviderSubmissionReceiptResponse(
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
                body,
                ResponseJson,
                statusCode: receipt.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created);
        }
        catch (ProviderSubmissionException exception)
        {
            if (exception.Error == ProviderSubmissionError.CredentialPaused)
            {
                await securityEvents.AppendAsync(
                    new SecurityEvent(
                        Guid.NewGuid(),
                        SecurityEventType.Client,
                        SecurityEventOutcome.Denied,
                        credential.KeyId,
                        timeProvider.GetUtcNow(),
                        context.TraceIdentifier,
                        "provider_credential_paused"),
                    cancellationToken);
            }

            return exception.Error switch
            {
                ProviderSubmissionError.CredentialPaused =>
                    Problem(StatusCodes.Status403Forbidden, "The provider credential is paused; submissions are refused until it is resumed."),
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
                result.CaseReference,
                result.Files
                    .Select(file => new ProviderSubmissionFileResultResponse(
                        file.Ordinal,
                        file.FileName,
                        file.Status,
                        file.Decision,
                        file.AllocationFailure,
                        file.FailureCode,
                        file.CaseReference))
                    .ToArray()),
            ResponseJson);
    }

    private static IResult Problem(int statusCode, string title) =>
        Results.Problem(statusCode: statusCode, title: title);
}
