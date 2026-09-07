using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Pegasus.IntegrationTests;

internal sealed class TestUiResponseCaptureStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<TestUiResponseCaptureMiddleware>();
        next(app);
    };
}

internal sealed class TestUiResponseCaptureMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var captureDirectory = Environment.GetEnvironmentVariable("PEGASUS_TEST_UI_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(captureDirectory))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
            buffer.Position = 0;
            if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            {
                using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var html = await reader.ReadToEndAsync(context.RequestAborted);
                // A password-reset response is deliberately shown once to
                // its administrator; it must never become a retained fixture.
                if (!html.Contains("id=\"temporary-password-title\"", StringComparison.Ordinal))
                {
                    await CaptureAsync(captureDirectory, context, html, context.RequestAborted);
                }
                buffer.Position = 0;
            }
            else if (context.Response.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            {
                await CaptureAssetAsync(captureDirectory, context, buffer.ToArray(), context.RequestAborted);
                buffer.Position = 0;
            }

            await buffer.CopyToAsync(originalBody, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static async Task CaptureAssetAsync(
        string captureDirectory,
        HttpContext context,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var request = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request))).ToLowerInvariant();
        var metadata = new CapturedAsset(
            context.Request.PathBase + context.Request.Path,
            context.Request.QueryString.Value ?? string.Empty,
            context.Response.ContentType!);
        await WriteOnceAsync(Path.Combine(captureDirectory, "assets", hash), async itemDirectory =>
        {
            await File.WriteAllTextAsync(
                Path.Combine(itemDirectory, "asset.json"),
                JsonSerializer.Serialize(metadata, JsonOptions),
                cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(itemDirectory, "response.bin"), content, cancellationToken);
        });
    }

    /// <summary>
    /// Writes one capture into a private staging directory and moves it into
    /// place. Two test classes rendering an identical response hash to the
    /// same directory; the second arrival is the same bytes, so it is dropped
    /// instead of racing the first on the same file names.
    /// </summary>
    private static async Task WriteOnceAsync(string itemDirectory, Func<string, Task> write)
    {
        if (Directory.Exists(itemDirectory))
        {
            return;
        }

        var staging = $"{itemDirectory}.{Guid.NewGuid():N}";
        Directory.CreateDirectory(staging);
        await write(staging);
        try
        {
            Directory.Move(staging, itemDirectory);
        }
        catch (IOException) when (Directory.Exists(itemDirectory))
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static async Task CaptureAsync(
        string captureDirectory,
        HttpContext context,
        string html,
        CancellationToken cancellationToken)
    {
        var request = $"{context.Request.Method} {context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request + "\n" + html))).ToLowerInvariant();
        var metadata = new CapturedResponse(
            context.Request.Method,
            context.Request.PathBase + context.Request.Path,
            context.Request.QueryString.Value ?? string.Empty);
        await WriteOnceAsync(Path.Combine(captureDirectory, hash), async itemDirectory =>
        {
            await File.WriteAllTextAsync(
                Path.Combine(itemDirectory, "response.json"),
                JsonSerializer.Serialize(metadata, JsonOptions),
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(itemDirectory, "response.html"),
                html,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        });
    }

    private sealed record CapturedResponse(string Method, string Path, string Query);
    private sealed record CapturedAsset(string Path, string Query, string ContentType);
}
