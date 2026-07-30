using System.Collections.Immutable;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace CollisionBrain;

public sealed class Settings
{
    public string NodeEnv { get; init; } = Env("NODE_ENV", "development");
    public string RepositoryDriver { get; init; } = Env("REPOSITORY_DRIVER", "postgres");
    public string ObjectStoreDriver { get; init; } = Env("OBJECT_STORE_DRIVER", "filesystem");
    public string DatabaseUrl { get; init; } = Env("DATABASE_URL", "postgres://rag:rag@localhost:5432/rag");
    public string ObjectStorePath { get; init; } = Env("OBJECT_STORE_PATH", ".data/objects");
    public string? S3Endpoint { get; init; } = NullableEnv("S3_ENDPOINT");
    public string S3Region { get; init; } = Env("S3_REGION", "us-east-1");
    public string? S3Bucket { get; init; } = NullableEnv("S3_BUCKET");
    public string? S3AccessKeyId { get; init; } = NullableEnv("S3_ACCESS_KEY_ID");
    public string? S3SecretAccessKey { get; init; } = NullableEnv("S3_SECRET_ACCESS_KEY");
    public bool S3ForcePathStyle { get; init; } = bool.TryParse(Env("S3_FORCE_PATH_STYLE", "false"), out var forcePathStyle) && forcePathStyle;
    public int Port { get; init; } = int.TryParse(Env("PORT", "3000"), out var p) ? p : 3000;
    public int EmbeddingDimensions { get; init; } = int.TryParse(Env("EMBEDDING_DIMENSIONS", "384"), out var d) ? d : 384;
    public string AuthMode { get; init; } = Env("AUTH_MODE", "none");
    public string SharedSecret { get; init; } = Env("MCP_SHARED_SECRET", "development-only");
    public string? OidcIssuer { get; init; } = NullableEnv("OIDC_ISSUER");
    public string? OidcAudience { get; init; } = NullableEnv("OIDC_AUDIENCE");
    public string? OidcJwksUrl { get; init; } = NullableEnv("OIDC_JWKS_URL");
    public string UploadTokenSecret { get; init; } = Env("UPLOAD_TOKEN_SECRET", "local-development-upload-secret");
    public int UploadTtlSeconds { get; init; } = int.TryParse(Env("UPLOAD_TTL_SECONDS", "900"), out var ttl) ? ttl : 900;
    public int MaxUploadBytes { get; init; } = int.TryParse(Env("MAX_UPLOAD_BYTES", "26214400"), out var m) ? m : 26214400;
    public int WorkerPollMs { get; init; } = int.TryParse(Env("WORKER_POLL_MS", "1000"), out var w) ? w : 1000;
    public string StdioUrl { get; init; } = Env("RAG_HTTP_URL", "http://localhost:3000/mcp");
    public string? StdioToken { get; init; } = Environment.GetEnvironmentVariable("RAG_HTTP_BEARER_TOKEN");
    static string Env(string key, string fallback) => Environment.GetEnvironmentVariable(key) ?? fallback;
    static string? NullableEnv(string key) => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)) ? null : Environment.GetEnvironmentVariable(key);
    public void Validate()
    {
        if (NodeEnv == "production" && AuthMode == "none") throw new ValidationError("AUTH_MODE=none is not permitted in production");
        if (NodeEnv == "production" && string.IsNullOrWhiteSpace(UploadTokenSecret)) throw new ValidationError("UPLOAD_TOKEN_SECRET is required in production");
        if (EmbeddingDimensions != 384 && RepositoryDriver.Equals("postgres", StringComparison.OrdinalIgnoreCase)) throw new ValidationError("PostgreSQL requires EMBEDDING_DIMENSIONS=384");
        if (AuthMode == "shared-secret" && SharedSecret.Length < 16) throw new ValidationError("MCP_SHARED_SECRET must be at least 16 characters");
        if (!new[] { "memory", "postgres" }.Contains(RepositoryDriver, StringComparer.OrdinalIgnoreCase)) throw new ValidationError("Unsupported repository driver");
        if (!new[] { "memory", "filesystem", "s3" }.Contains(ObjectStoreDriver, StringComparer.OrdinalIgnoreCase)) throw new ValidationError("Unsupported object store driver");
        if (ObjectStoreDriver.Equals("s3", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(S3Bucket)) throw new ValidationError("S3_BUCKET is required when OBJECT_STORE_DRIVER=s3");
        if (AuthMode.Equals("oidc", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(OidcIssuer) || string.IsNullOrWhiteSpace(OidcAudience) || string.IsNullOrWhiteSpace(OidcJwksUrl))) throw new ValidationError("OIDC_ISSUER, OIDC_AUDIENCE, and OIDC_JWKS_URL are required for OIDC authentication");
        if (!new[] { "none", "shared-secret", "oidc" }.Contains(AuthMode, StringComparer.OrdinalIgnoreCase)) throw new ValidationError("Unsupported authentication mode");
    }
}

public sealed class NoneAuthProvider : IAuthProvider
{
    public Task<Principal> AuthenticateAsync(string? authorization, CancellationToken ct = default) => Task.FromResult(new Principal("local-development", ImmutableHashSet.Create(Role.Reader, Role.Contributor, Role.Admin)));
}
public sealed class SharedSecretAuthProvider(string secret) : IAuthProvider
{
    public Task<Principal> AuthenticateAsync(string? authorization, CancellationToken ct = default)
    {
        if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedError();
        var supplied = authorization[7..]; var a = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(supplied)); var b = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)); if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b)) throw new UnauthorizedError();
        return Task.FromResult(new Principal("shared-secret", ImmutableHashSet.Create(Role.Reader, Role.Contributor, Role.Admin)));
    }
}

public sealed class OidcAuthProvider(string issuer, string audience, string jwksUrl) : IAuthProvider
{
    private static readonly HttpClient client = new();
    private static readonly JsonWebTokenHandler handler = new();
    private IReadOnlyCollection<SecurityKey>? signingKeys;

    public async Task<Principal> AuthenticateAsync(string? authorization, CancellationToken ct = default)
    {
        if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedError();
        var token = authorization[7..];
        try
        {
            signingKeys ??= (await client.GetFromJsonAsync<JsonWebKeySet>(jwksUrl, ct))?.GetSigningKeys().ToArray()
                ?? throw new UnauthorizedError();
            var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(1),
            });
            if (!result.IsValid) throw new UnauthorizedError();
            var jwt = handler.ReadJsonWebToken(token);
            if (string.IsNullOrWhiteSpace(jwt.Subject)) throw new UnauthorizedError();
            var roles = jwt.Claims
                .Where(x => x.Type is "roles" or "role")
                .Select(x => x.Value)
                .Where(x => Enum.TryParse<Role>(x, true, out _))
                .Select(x => Enum.Parse<Role>(x, true))
                .ToHashSet();
            if (roles.Count == 0) roles.Add(Role.Reader);
            return new Principal(jwt.Subject, roles.ToImmutableHashSet());
        }
        catch (UnauthorizedError) { throw; }
        catch { throw new UnauthorizedError(); }
    }
}

public sealed class RuntimeContext : IAsyncDisposable
{
    public Settings Settings { get; }
    public IDocumentRepository Repository { get; }
    public IObjectStore Objects { get; }
    public IEmbeddingProvider Embeddings { get; }
    public IAuthProvider Auth { get; }
    public UploadTokenService Uploads { get; private set; } = null!;
    public RagService Rag { get; private set; } = null!;
    public RuntimeContext(Settings settings)
    {
        Settings = settings;
        Repository = settings.RepositoryDriver.Equals("postgres", StringComparison.OrdinalIgnoreCase) ? new PostgresDocumentRepository(settings.DatabaseUrl) : new InMemoryDocumentRepository();
        Objects = CreateObjectStore(settings);
        Embeddings = new LocalHashEmbeddingProvider(settings.EmbeddingDimensions);
        Auth = settings.AuthMode.Equals("shared-secret", StringComparison.OrdinalIgnoreCase)
            ? new SharedSecretAuthProvider(settings.SharedSecret)
            : settings.AuthMode.Equals("oidc", StringComparison.OrdinalIgnoreCase)
                ? new OidcAuthProvider(settings.OidcIssuer!, settings.OidcAudience!, settings.OidcJwksUrl!)
                : new NoneAuthProvider();
    }
    private static IObjectStore CreateObjectStore(Settings settings)
    {
        if (settings.ObjectStoreDriver.Equals("memory", StringComparison.OrdinalIgnoreCase)) return new InMemoryObjectStore();
        if (settings.ObjectStoreDriver.Equals("filesystem", StringComparison.OrdinalIgnoreCase)) return new FileObjectStore(settings.ObjectStorePath);
        if (!settings.ObjectStoreDriver.Equals("s3", StringComparison.OrdinalIgnoreCase)) throw new ValidationError("Unsupported object store driver");
        var config = new AmazonS3Config
        {
            ForcePathStyle = settings.S3ForcePathStyle,
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(settings.S3Region),
        };
        if (!string.IsNullOrWhiteSpace(settings.S3Endpoint)) config.ServiceURL = settings.S3Endpoint;
        IAmazonS3 client = settings.S3AccessKeyId is not null && settings.S3SecretAccessKey is not null
            ? new AmazonS3Client(new BasicAWSCredentials(settings.S3AccessKeyId, settings.S3SecretAccessKey), config)
            : new AmazonS3Client(config);
        return new S3ObjectStore(client, settings.S3Bucket!);
    }
    public async Task InitializeAsync(CancellationToken ct = default) { Uploads = new UploadTokenService(Objects, Settings.UploadTokenSecret, TimeSpan.FromSeconds(Settings.UploadTtlSeconds)); Rag = new RagService(Repository, Objects, Embeddings, Uploads); await Repository.InitializeAsync(ct); }
    public ValueTask DisposeAsync() => Repository.DisposeAsync();
}

public static class HttpHost
{
    public static async Task<int> RunAsync(RuntimeContext context, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls($"http://0.0.0.0:{context.Settings.Port}"); builder.Services.AddSingleton(context); builder.Services.AddHttpContextAccessor(); builder.Services.AddMcpServer(options => options.ServerInfo = new ModelContextProtocol.Protocol.Implementation { Name = "collisionengineers-rag", Version = "0.1.0" }).WithHttpTransport(options => options.Stateless = true).WithTools<RagTools>(); builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = context.Settings.MaxUploadBytes); var app = builder.Build();
        app.Use(async (http, next) => { http.Response.Headers["Access-Control-Allow-Origin"] = "*"; http.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS"; http.Response.Headers["Access-Control-Allow-Headers"] = "*"; if (http.Request.Method == "OPTIONS") { http.Response.StatusCode = 204; return; } try { await next(); } catch (Exception e) { await Error(http, e); } });
        app.MapGet("/health", () => Results.Json(new { status = "ok", service = "collisionengineers-rag", version = "0.1.0" }));
        app.MapPost("/uploads", async (HttpContext http, RuntimeContext c) => { var p = await Principal(c, http); if (!p.Has(Role.Contributor)) throw new ForbiddenError(); var form = await http.Request.ReadFormAsync(); var file = form.Files.GetFile("file") ?? throw new ValidationError("Multipart field 'file' is required"); if (file.Length > c.Settings.MaxUploadBytes) throw new ValidationError("Upload exceeds configured size limit"); var ext = Path.GetExtension(file.FileName).ToLowerInvariant(); if (!new[] { ".txt", ".md", ".markdown", ".html", ".htm", ".docx", ".pdf" }.Contains(ext)) throw new ValidationError($"Unsupported file extension for {file.FileName}"); await using var ms = new MemoryStream(); await file.CopyToAsync(ms); var staged = await c.Uploads.StageAsync(ms.ToArray(), file.FileName, file.ContentType ?? "application/octet-stream", http.RequestAborted); return Results.Json(new { upload_ref = staged.UploadRef, content_hash = staged.ContentHash, expires_at = staged.ExpiresAt }, statusCode: 201); });
        app.MapMcp("/mcp");
        app.MapPost("/internal/process-one", async (RuntimeContext c) => Results.Json(new { processed = await c.Rag.ProcessOneJobAsync() }));
        app.MapFallback(async http => { http.Response.StatusCode = 404; await Error(http, new NotFoundError("Route was not found")); });
        await app.RunAsync(cancellationToken); return 0;
    }
    private static async Task<Principal> Principal(RuntimeContext c, HttpContext h) => await c.Auth.AuthenticateAsync(h.Request.Headers.Authorization.FirstOrDefault(), h.RequestAborted);
    private static async Task<IResult> Mcp(HttpContext http, RuntimeContext c)
    {
        var principal = await Principal(c, http); using var doc = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted); var root = doc.RootElement; var id = root.TryGetProperty("id", out var idEl) ? idEl.Clone() : JsonDocument.Parse("null").RootElement; var method = root.GetProperty("method").GetString() ?? "";
        object result = method switch { "initialize" => new { protocolVersion = "2025-03-26", capabilities = new { tools = new { } }, serverInfo = new { name = "collisionengineers-rag", version = "0.1.0" } }, "notifications/initialized" => new { }, "tools/list" => new { tools = ToolSchemas() }, "tools/call" => await CallTool(root.GetProperty("params"), principal, c), _ => throw new ValidationError($"Unsupported MCP method {method}") };
        return Results.Json(new { jsonrpc = "2.0", id, result });
    }
    private static object[] ToolSchemas() => [
        new { name = "lookup", description = "Retrieve ranked source passages with stable citations. This read-only tool does not generate an answer and is safe for client-controlled automatic invocation.", inputSchema = new { type = "object", properties = new { query = new { type = "string", minLength = 1, maxLength = 8000 }, limit = new { type = "integer", minimum = 1, maximum = 25 }, filters = new { type = "object" } }, required = new[] { "query" } }, annotations = new { readOnlyHint = true, destructiveHint = false, idempotentHint = true, openWorldHint = false } },
        new { name = "write", description = "Queue pasted text or a securely staged file for asynchronous extraction, chunking, embedding, and indexing.", inputSchema = new { type = "object", properties = new { title = new { type = "string" }, text = new { type = "string" }, upload_ref = new { type = "string" }, source = new { type = "string" }, tags = new { type = "array" } }, required = new[] { "title" } }, annotations = new { readOnlyHint = false, destructiveHint = false, idempotentHint = false, openWorldHint = false } },
        new { name = "view_all", description = "Return a paginated document registry with processing status and metadata, without returning complete document bodies.", inputSchema = new { type = "object", properties = new { cursor = new { type = "string" }, limit = new { type = "integer", minimum = 1, maximum = 100 }, status = new { type = "string" } } }, annotations = new { readOnlyHint = true, destructiveHint = false, idempotentHint = true, openWorldHint = false } },
        new { name = "remove", description = "Permanently purge a document's source and searchable chunks while retaining a content-free audit tombstone.", inputSchema = new { type = "object", properties = new { document_id = new { type = "string" }, confirm = new { type = "boolean", description = "Must be true" } }, required = new[] { "document_id", "confirm" } }, annotations = new { readOnlyHint = false, destructiveHint = true, idempotentHint = false, openWorldHint = false } }
    ];
    private static async Task<object> CallTool(JsonElement p, Principal principal, RuntimeContext c)
    {
        var name = p.GetProperty("name").GetString() ?? ""; var a = p.TryGetProperty("arguments", out var args) ? args : JsonDocument.Parse("{}").RootElement; Dictionary<string, object?> value; try { value = name switch { "lookup" => await Lookup(a, principal, c), "write" => await Write(a, principal, c), "view_all" => await View(a, principal, c), "remove" => await Remove(a, principal, c), _ => throw new NotFoundError($"Tool {name} was not found") }; } catch (Exception e) { return new { isError = true, content = new[] { new { type = "text", text = e is AppError ae ? $"{ae.Code}: {ae.Message}" : "internal_error: Internal server error" } } }; } var json = JsonSerializer.Serialize(value); return new { content = new[] { new { type = "text", text = json } }, structuredContent = value, isError = false };
    }
    private static async Task<Dictionary<string, object?>> Lookup(JsonElement a, Principal p, RuntimeContext c) { var filters = new LookupFilters(a.TryGetProperty("filters", out var f) && f.TryGetProperty("source", out var s) ? s.GetString() : null, null, null); return await c.Rag.LookupAsync(p, a.GetProperty("query").GetString()!, a.TryGetProperty("limit", out var l) ? l.GetInt32() : 8, filters, CancellationToken.None); }
    private static async Task<Dictionary<string, object?>> Write(JsonElement a, Principal p, RuntimeContext c) { return await c.Rag.WriteAsync(p, a.GetProperty("title").GetString()!, a.TryGetProperty("text", out var t) ? t.GetString() : null, a.TryGetProperty("upload_ref", out var u) ? u.GetString() : null, a.TryGetProperty("source", out var s) ? s.GetString() : null, a.TryGetProperty("tags", out var tags) ? tags.EnumerateArray().Select(x => x.GetString()!).ToArray() : null); }
    private static async Task<Dictionary<string, object?>> View(JsonElement a, Principal p, RuntimeContext c) { DocumentStatus? status = null; if (a.TryGetProperty("status", out var s) && Enum.TryParse<DocumentStatus>(s.GetString(), true, out var parsed)) status = parsed; return await c.Rag.ViewAllAsync(p, a.TryGetProperty("cursor", out var cur) ? cur.GetString() : null, a.TryGetProperty("limit", out var l) ? l.GetInt32() : 50, status); }
    private static async Task<Dictionary<string, object?>> Remove(JsonElement a, Principal p, RuntimeContext c) => await c.Rag.RemoveAsync(p, a.GetProperty("document_id").GetString()!, a.TryGetProperty("confirm", out var yes) && yes.ValueKind == JsonValueKind.True);
    private static async Task Error(HttpContext http, Exception e) { http.Response.ContentType = "application/json"; var id = http.Request.Headers.TryGetValue("x-request-id", out StringValues v) ? v.FirstOrDefault() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(); if (e is AppError a) { http.Response.StatusCode = a.StatusCode; await http.Response.WriteAsJsonAsync(new { error = a.Code, message = a.Message, request_id = id }); } else { http.Response.StatusCode = 500; await http.Response.WriteAsJsonAsync(new { error = "internal_error", message = "Internal server error", request_id = id }); } }
}

public sealed class Worker(RuntimeContext context, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var client = new HttpClient();
        var upstream = context.Settings.StdioUrl.Replace("/mcp", "/internal/process-one", StringComparison.OrdinalIgnoreCase);
        var proxyMemoryWorker = context.Settings.RepositoryDriver.Equals("memory", StringComparison.OrdinalIgnoreCase) && Environment.GetEnvironmentVariable("RAG_HTTP_URL") is not null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool processed;
                if (proxyMemoryWorker)
                {
                    using var response = await client.PostAsync(upstream, null, stoppingToken);
                    response.EnsureSuccessStatusCode();
                    using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(stoppingToken), cancellationToken: stoppingToken);
                    processed = body.RootElement.GetProperty("processed").GetBoolean();
                }
                else processed = await context.Rag.ProcessOneJobAsync(stoppingToken);
                if (!processed) await Task.Delay(context.Settings.WorkerPollMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Worker iteration failed"); await Task.Delay(context.Settings.WorkerPollMs, stoppingToken); }
        }
    }
}

public static class StdioProxy
{
    public static async Task<int> RunAsync(Settings settings, CancellationToken ct = default)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (settings.StdioToken is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.StdioToken);
        string? line;
        while ((line = await Console.In.ReadLineAsync(ct)) is not null)
        {
            try
            {
                using var content = new StringContent(line, System.Text.Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(settings.StdioUrl, content, ct);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadAsStringAsync(ct);
                var data = payload.Split('\n').FirstOrDefault(x => x.StartsWith("data: ", StringComparison.Ordinal))?.TrimStart("data: ".ToCharArray()) ?? payload;
                Console.WriteLine(data);
            }
            catch (Exception ex) { await Console.Error.WriteLineAsync(ex.Message); }
        }
        return 0;
    }
}
