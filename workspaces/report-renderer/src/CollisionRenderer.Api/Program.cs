using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CollisionRenderer.Core;

var builder = WebApplication.CreateBuilder(args);

// One renderer for the process — it owns a reused headless-Chromium instance.
builder.Services.AddSingleton<IDocumentRenderer>(_ => CollisionRendererFactory.CreateRenderer());
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

// Cap batch size so a single request cannot enqueue an unbounded number of Chromium renders.
const int MaxBatchItems = 100;

// Optional bearer auth. Supports CR_API_TOKEN for compatibility, CR_API_TOKENS
// for rotation windows, and CR_API_TOKEN_SHA256 / CR_API_TOKEN_SHA256S for
// deployments that should not expose raw token values to the process after load.
var auth = ApiAuthOptions.FromEnvironment();
app.Use(async (ctx, next) =>
{
    if (auth.Enabled && !ctx.Request.Path.StartsWithSegments("/healthz"))
    {
        if (!auth.IsAuthorised(ctx.Request.Headers.Authorization.ToString()))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }
    }

    await next();
});

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/v1/templates", () => Results.Ok(
    CollisionRendererFactory.Catalog.List().Select(t => new { t.Id, t.Name, t.Description })));


app.MapGet("/v1/authoring-templates", () => Results.Ok(
    CollisionRendererFactory.AuthoringCatalog.List()));

app.MapGet("/v1/authoring-templates/{id}/form", (string id) =>
{
    return CollisionRendererFactory.AuthoringCatalog.TryGet(id, out _)
        ? Results.Ok(CollisionRendererFactory.AuthoringCatalog.GetForm(id))
        : Results.NotFound(new { error = $"unknown authoring template '{id}'" });
});

app.MapGet("/v1/authoring-templates/{id}/blank", (string id) =>
{
    return CollisionRendererFactory.AuthoringCatalog.TryGet(id, out _)
        ? Results.Text(CollisionRendererFactory.AuthoringCatalog.GetBlankJson(id), "application/json")
        : Results.NotFound(new { error = $"unknown authoring template '{id}'" });
});

app.MapPost("/v1/validate", (RenderApiRequest req) =>
{
    if (!CollisionRendererFactory.Catalog.TryGet(req.TemplateId, out var d))
    {
        return Results.NotFound(new { error = $"unknown template '{req.TemplateId}'" });
    }

    try
    {
        var model = JsonSerializer.Deserialize(req.Data.GetRawText(), d!.ModelType, CrJson.Options)!;
        var v = new PayloadValidator().Validate(req.TemplateId, model, allowLocalFilePaths: false);
        return Results.Ok(new { ok = v.Ok, errors = v.Errors, warnings = v.Warnings });
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { ok = false, errors = new[] { ex.Message } });
    }
});

// Returns an artifact descriptor with a base64 PDF (machine-to-machine).
app.MapPost("/v1/render", async (RenderApiRequest req, IDocumentRenderer renderer) =>
{
    try
    {
        var result = await renderer.RenderAsync(req.ToRenderRequest());
        return Results.Ok(new
        {
            filename = result.SuggestedFileName,
            mediaType = "application/pdf",
            bytes = result.Pdf.Length,
            sha256 = result.Sha256,
            pageCount = result.PageCount,
            density = result.Density.ToString(),
            warnings = result.Warnings,
            engineVersion = result.EngineVersion,
            base64 = Convert.ToBase64String(result.Pdf),
        });
    }
    catch (RenderValidationException ex)
    {
        return Results.BadRequest(new { error = "validation_failed", details = ex.Errors });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

// Returns the raw PDF stream (browser/download friendly).
app.MapPost("/v1/render.pdf", async (RenderApiRequest req, IDocumentRenderer renderer) =>
{
    try
    {
        var result = await renderer.RenderAsync(req.ToRenderRequest());
        return Results.File(result.Pdf, "application/pdf", result.SuggestedFileName);
    }
    catch (RenderValidationException ex)
    {
        return Results.BadRequest(new { error = "validation_failed", details = ex.Errors });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapPost("/v1/render.multipart", async (HttpRequest http, IDocumentRenderer renderer) =>
{
    if (!http.HasFormContentType)
    {
        return Results.BadRequest(new
        {
            error = "expected_multipart",
            details = new[] { "Use multipart/form-data with templateId, data JSON and file parts named by target model path." },
        });
    }

    var form = await http.ReadFormAsync();
    var templateId = First(form, "templateId");
    if (string.IsNullOrWhiteSpace(templateId))
    {
        return Results.BadRequest(new { error = "validation_failed", details = new[] { "templateId is required." } });
    }

    var data = First(form, "data") ?? "{}";
    JsonNode root;
    try
    {
        root = JsonNode.Parse(data) ?? new JsonObject();
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { error = "validation_failed", details = new[] { $"Invalid data JSON: {ex.Message}" } });
    }

    var tempFiles = new HashSet<string>(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    try
    {
        foreach (var file in form.Files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            var targetPath = ResolveMultipartTargetPath(form, file);
            EnsureAttachmentPath(targetPath);
            EnsureBoundedPath(targetPath);
            var tempPath = await SaveMultipartFileAsync(file);
            tempFiles.Add(tempPath);
            JsonPath.Set(root, targetPath, tempPath);
        }

        var result = await renderer.RenderAsync(new RenderRequest
        {
            TemplateId = templateId!,
            Json = root.ToJsonString(CrJson.Options),
            Options = RenderApiRequest.ParseOptions(First(form, "density") ?? "auto"),
            AllowLocalAttachmentPaths = false, // Cloud API: never trust client-supplied local file paths for attachments.
            TrustedLocalAttachmentPaths = tempFiles,
        });

        return Results.Ok(RenderArtifact(result));
    }
    catch (RenderValidationException ex)
    {
        return Results.BadRequest(new { error = "validation_failed", details = ex.Errors });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    finally
    {
        foreach (var tempFile in tempFiles)
        {
            try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
        }
    }
});

app.MapPost("/v1/render/batch", async (BatchRenderApiRequest req, IDocumentRenderer renderer) =>
{
    if (req?.Items is not { Count: > 0 })
    {
        return Results.BadRequest(new { error = "validation_failed", details = new[] { "items must contain at least one render request" } });
    }

    if (req.Items.Count > MaxBatchItems)
    {
        return Results.BadRequest(new { error = "validation_failed", details = new[] { $"items must not exceed {MaxBatchItems} render requests per batch" } });
    }

    var allOk = true;
    var results = new List<object>();

    for (var i = 0; i < req.Items.Count; i++)
    {
        var item = req.Items[i];
        try
        {
            var result = await renderer.RenderAsync(item.ToRenderRequest());
            results.Add(new
            {
                index = i,
                ok = true,
                templateId = item.TemplateId,
                filename = result.SuggestedFileName,
                mediaType = "application/pdf",
                bytes = result.Pdf.Length,
                sha256 = result.Sha256,
                pageCount = result.PageCount,
                density = result.Density.ToString(),
                warnings = result.Warnings,
                engineVersion = result.EngineVersion,
                base64 = Convert.ToBase64String(result.Pdf),
            });
        }
        catch (RenderValidationException ex)
        {
            allOk = false;
            results.Add(new { index = i, ok = false, templateId = item.TemplateId, errors = ex.Errors });
        }
        catch (KeyNotFoundException ex)
        {
            allOk = false;
            results.Add(new { index = i, ok = false, templateId = item.TemplateId, errors = new[] { ex.Message } });
        }
    }

    return Results.Ok(new { ok = allOk, results });
});

app.Run();

static string? First(IFormCollection form, string key) =>
    form.TryGetValue(key, out var values) && values.Count > 0 ? values[0] : null;

static object RenderArtifact(RenderResult result) => new
{
    filename = result.SuggestedFileName,
    mediaType = "application/pdf",
    bytes = result.Pdf.Length,
    sha256 = result.Sha256,
    pageCount = result.PageCount,
    density = result.Density.ToString(),
    warnings = result.Warnings,
    engineVersion = result.EngineVersion,
    base64 = Convert.ToBase64String(result.Pdf),
};

static string ResolveMultipartTargetPath(IFormCollection form, IFormFile file)
{
    if (!string.IsNullOrWhiteSpace(file.Name) &&
        !file.Name.Equals("file", StringComparison.OrdinalIgnoreCase) &&
        !file.Name.Equals("files", StringComparison.OrdinalIgnoreCase) &&
        !file.Name.Equals("attachment", StringComparison.OrdinalIgnoreCase))
    {
        return file.Name;
    }

    var specific = First(form, $"{file.Name}.targetPath");
    if (!string.IsNullOrWhiteSpace(specific))
    {
        return specific!;
    }

    var shared = First(form, "targetPath");
    if (!string.IsNullOrWhiteSpace(shared))
    {
        return shared!;
    }

    throw new RenderValidationException(new[]
    {
        "Each uploaded file must use the target model path as its form field name, for example adverts[0].screenshotPath.",
    });
}

// Uploaded files may only populate attachment fields, never arbitrary model paths, so a
// crafted field name cannot inject the temp-file path into an unrelated string field.
static void EnsureAttachmentPath(string path)
{
    var allowed = path.EndsWith(".screenshotPath", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".capturedPdfPath", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".imagePath", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".customSignaturePath", StringComparison.OrdinalIgnoreCase);
    if (!allowed)
    {
        throw new RenderValidationException(new[]
        {
            $"Upload target '{path}' is not an attachment field; allowed targets end with " +
            "screenshotPath, capturedPdfPath, imagePath or customSignaturePath.",
        });
    }
}

// Cap array indices in an upload target so a crafted field name can't drive an unbounded
// allocation as the array is grown to fit; mirrors the real model paths' bounds.
static void EnsureBoundedPath(string path)
{
    foreach (var segment in JsonPath.Parse(path))
    {
        if (segment.Index is { } index && (index < 0 || index > 4095))
        {
            throw new RenderValidationException(new[] { $"Upload target array index {index} is out of the allowed range (min 0, max 4095)." });
        }
    }
}

static async Task<string> SaveMultipartFileAsync(IFormFile file)
{
    var ext = Path.GetExtension(file.FileName);
    if (ext.Length > 12)
    {
        ext = "";
    }

    var tempPath = Path.Combine(Path.GetTempPath(), $"collisionrenderer_upload_{Guid.NewGuid():N}{ext}");
    await using var stream = File.Create(tempPath);
    await file.CopyToAsync(stream);
    return tempPath;
}

/// <summary>Render/validate request body: { templateId, data: {...}, density?: "auto|normal|compact|ultra" }.</summary>
public sealed record RenderApiRequest(string TemplateId, JsonElement Data, string? Density = "auto")
{
    public RenderRequest ToRenderRequest() => new()
    {
        TemplateId = TemplateId,
        Json = Data.GetRawText(),
        Options = ParseOptions(Density ?? "auto"),
        // Cloud API: never trust client-supplied local file paths for attachments.
        AllowLocalAttachmentPaths = false,
    };

    public static RenderOptions ParseOptions(string density) =>
        density.ToLowerInvariant() switch
        {
            "normal" => new RenderOptions { Fit = DensityFit.Fixed, Density = CollisionRenderer.Core.Density.Normal },
            "compact" => new RenderOptions { Fit = DensityFit.Fixed, Density = CollisionRenderer.Core.Density.Compact },
            "ultra" or "ultra-compact" => new RenderOptions { Fit = DensityFit.Fixed, Density = CollisionRenderer.Core.Density.UltraCompact },
            _ => new RenderOptions { Fit = DensityFit.Auto, Density = CollisionRenderer.Core.Density.Normal },
        };
}

/// <summary>Batch render request body: { items: [{ templateId, data: {...}, density? }] }.</summary>
public sealed record BatchRenderApiRequest(IReadOnlyList<RenderApiRequest> Items);

internal sealed class ApiAuthOptions
{
    private readonly List<byte[]> _tokenHashes;

    private ApiAuthOptions(List<byte[]> tokenHashes)
    {
        _tokenHashes = tokenHashes;
    }

    public bool Enabled => _tokenHashes.Count > 0;

    public static ApiAuthOptions FromEnvironment()
    {
        var hashes = new List<byte[]>();
        foreach (var token in SplitEnv("CR_API_TOKEN"))
        {
            hashes.Add(Sha256(token));
        }

        foreach (var token in SplitEnv("CR_API_TOKENS"))
        {
            hashes.Add(Sha256(token));
        }

        // A configured-but-malformed hash must fail closed: if we silently skipped it and
        // no other source supplied a hash, Enabled would be false and auth would be OFF.
        foreach (var hash in SplitEnv("CR_API_TOKEN_SHA256").Concat(SplitEnv("CR_API_TOKEN_SHA256S")))
        {
            if (!TryHex(hash, out var bytes))
            {
                throw new InvalidOperationException(
                    "CR_API_TOKEN_SHA256/CR_API_TOKEN_SHA256S values must each be a 64-character hex " +
                    "SHA-256 hash; a malformed value would otherwise silently disable API authentication.");
            }

            hashes.Add(bytes);
        }

        return new ApiAuthOptions(hashes);
    }

    public bool IsAuthorised(string authorization)
    {
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var supplied = authorization[prefix.Length..].Trim();
        if (supplied.Length == 0)
        {
            return false;
        }

        var suppliedHash = Sha256(supplied);
        return _tokenHashes.Any(expected =>
            expected.Length == suppliedHash.Length &&
            CryptographicOperations.FixedTimeEquals(expected, suppliedHash));
    }

    private static IEnumerable<string> SplitEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static byte[] Sha256(string value) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static bool TryHex(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (value.Length != 64)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Exposed so WebApplicationFactory-based integration tests can reference the entry point.</summary>
public partial class Program;
