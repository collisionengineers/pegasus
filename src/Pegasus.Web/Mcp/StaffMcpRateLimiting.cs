using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Mcp;

internal sealed class StaffMcpToolRateLimiter : IDisposable
{
    public const int ReadPermitLimit = 60;
    public const int MutationPermitLimit = 20;

    private readonly PartitionedRateLimiter<(string Subject, string Client)> reads =
        CreateLimiter(ReadPermitLimit);
    private readonly PartitionedRateLimiter<(string Subject, string Client)> mutations =
        CreateLimiter(MutationPermitLimit);

    public ValueTask<RateLimitLease> AcquireAsync(
        string subject,
        string client,
        bool readOnly,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(client);
        return (readOnly ? reads : mutations).AcquireAsync(
            (subject, client),
            permitCount: 1,
            cancellationToken);
    }

    public void Dispose()
    {
        reads.Dispose();
        mutations.Dispose();
    }

    private static PartitionedRateLimiter<(string Subject, string Client)> CreateLimiter(
        int permitLimit) =>
        PartitionedRateLimiter.Create<
            (string Subject, string Client),
            (string Subject, string Client)>(partition =>
            RateLimitPartition.GetFixedWindowLimiter(
                partition,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = permitLimit,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1)
                }));
}

internal sealed class StaffMcpToolRateLimitMiddleware(RequestDelegate next)
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 16
    };

    public async Task InvokeAsync(
        HttpContext context,
        StaffMcpToolRateLimiter limiter,
        ISecurityEventWriter securityEvents,
        TimeProvider timeProvider)
    {
        if (!HttpMethods.IsPost(context.Request.Method)
            || !context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase)
            || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var descriptor = await TryGetCalledToolAsync(
            context.Request,
            context.RequestAborted);
        if (descriptor is null)
        {
            await next(context);
            return;
        }

        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.GetClaim(OpenIddictConstants.Claims.Subject);
        var presenters = context.User.GetPresenters();
        var client = presenters.Length == 1 ? presenters[0] : null;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(client))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var lease = await limiter.AcquireAsync(
            subject,
            client,
            descriptor.Hints.ReadOnly,
            context.RequestAborted);
        if (lease.IsAcquired)
        {
            await next(context);
            return;
        }

        await securityEvents.AppendAsync(
            new SecurityEvent(
                Guid.NewGuid(),
                SecurityEventType.RateLimited,
                SecurityEventOutcome.Denied,
                $"{subject}|{client}",
                timeProvider.GetUtcNow(),
                context.TraceIdentifier,
                descriptor.Hints.ReadOnly
                    ? "mcp_read_rate_limited"
                    : "mcp_mutation_rate_limited"),
            context.RequestAborted);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = "60";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(
            new { error = "rate_limited" },
            cancellationToken: context.RequestAborted);
    }

    private static async Task<AlphaMcpToolDescriptor?> TryGetCalledToolAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        try
        {
            using var document = await JsonDocument.ParseAsync(
                request.Body,
                JsonOptions,
                cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("method", out var method)
                || !string.Equals(method.GetString(), "tools/call", StringComparison.Ordinal)
                || !root.TryGetProperty("params", out var parameters)
                || parameters.ValueKind != JsonValueKind.Object
                || !parameters.TryGetProperty("name", out var name)
                || name.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return AlphaMcpToolManifest.TryGet(name.GetString()!, out var descriptor)
                ? descriptor
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
