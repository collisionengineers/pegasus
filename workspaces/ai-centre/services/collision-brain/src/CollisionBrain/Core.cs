using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using System.Text.Json.Serialization;
using AngleSharp;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
namespace CollisionBrain;

public enum DocumentStatus { Pending, Processing, Ready, Failed, Deleting, Deleted }
public enum Role { Reader, Contributor, Admin }
public sealed record Principal(string Subject, ImmutableHashSet<Role> Roles)
{
    public bool Has(Role role) => Roles.Contains(role) || Roles.Contains(Role.Admin);
}
public sealed record DocumentMetadata(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("filename")] string? Filename,
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes);
public sealed record EmbeddingDescriptor(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("dimensions")] int Dimensions,
    [property: JsonPropertyName("version")] string Version);
public sealed record DocumentRecord(string Id, DocumentStatus Status, string ContentHash, string SourceObjectKey, DocumentMetadata Metadata, EmbeddingDescriptor? Embedding, int ChunkCount, int TextLength, string? Error, string CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record DocumentSummary(string Id, DocumentStatus Status, string ContentHash, DocumentMetadata Metadata, EmbeddingDescriptor? Embedding, int ChunkCount, int TextLength, string? Error, string CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ChunkRecord(string Id, string DocumentId, int Index, string Text, string Citation, float[] Embedding);
public sealed record IngestionJob(string Id, string DocumentId, int Attempts, DateTimeOffset CreatedAt);
public sealed record Tombstone(string DocumentId, string ContentHash, string RemovedBy, DateTimeOffset RemovedAt, int ChunkCount);
public sealed record LookupFilters(string? Source = null, IReadOnlyList<string>? Tags = null, IReadOnlyList<string>? DocumentIds = null);
public sealed record CreateDocumentInput(string Id, string ContentHash, string SourceObjectKey, DocumentMetadata Metadata, string CreatedBy);
public sealed record ReadyDocumentInput(string DocumentId, IReadOnlyList<ChunkRecord> Chunks, EmbeddingDescriptor Embedding, int TextLength);
public sealed record SearchInput(string Query, float[] QueryEmbedding, LookupFilters Filters, int Limit);
public sealed record LookupMatch(string DocumentId, string ChunkId, string Title, string? Source, int ChunkIndex, string Excerpt, double Score, string Citation, IReadOnlyList<string> Tags);
public sealed record StoredObject(byte[] Body, string Filename, string ContentType, DateTimeOffset CreatedAt);

public abstract class AppError(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
public sealed class ValidationError(string message) : AppError(400, "validation_error", message);
public sealed class UnauthorizedError(string message = "Authentication required") : AppError(401, "unauthorized", message);
public sealed class ForbiddenError(string message = "Insufficient role") : AppError(403, "forbidden", message);
public sealed class NotFoundError(string message) : AppError(404, "not_found", message);
public sealed class ConflictError(string message) : AppError(409, "conflict", message);

public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => DateTimeOffset.Parse(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
}

public interface IDocumentRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<(DocumentRecord Document, IngestionJob Job)> CreateDocumentWithJobAsync(CreateDocumentInput input, CancellationToken ct = default);
    Task<DocumentRecord?> GetDocumentAsync(string id, CancellationToken ct = default);
    Task<DocumentRecord?> FindByContentHashAsync(string hash, CancellationToken ct = default);
    Task<(IReadOnlyList<DocumentSummary> Documents, string? NextCursor)> ListDocumentsAsync(string? cursor, int limit, DocumentStatus? status, CancellationToken ct = default);
    Task<IngestionJob?> ClaimNextJobAsync(CancellationToken ct = default);
    Task MarkProcessingAsync(string id, CancellationToken ct = default);
    Task MarkReadyAsync(ReadyDocumentInput input, CancellationToken ct = default);
    Task MarkFailedAsync(string id, string error, CancellationToken ct = default);
    Task MarkDeletingAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<LookupMatch>> SearchAsync(SearchInput input, CancellationToken ct = default);
    Task<Tombstone> PurgeDocumentAsync(string id, string removedBy, CancellationToken ct = default);
}
public interface IObjectStore
{
    Task PutAsync(string key, StoredObject value, CancellationToken ct = default);
    Task<StoredObject> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<int> DeleteExpiredAsync(string prefix, DateTimeOffset before, CancellationToken ct = default);
}
public interface IEmbeddingProvider
{
    EmbeddingDescriptor Descriptor { get; }
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
public interface IAuthProvider
{
    Task<Principal> AuthenticateAsync(string? authorization, CancellationToken ct = default);
}

public static class TextAlgorithms
{
    private static readonly Regex Words = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    public static string Normalize(string value) => Regex.Replace(value.Replace("\0", "").Replace("\r\n", "\n").Replace('\r', '\n'), @"[ \t]+\n", "\n").Trim();
    public static string[] Tokens(string value) => Words.Matches(Normalize(value).ToLowerInvariant()).Select(m => m.Value).Where(x => x.Length > 1).ToArray();
    public static double Lexical(string query, string text)
    {
        var q = Tokens(query).ToHashSet(StringComparer.Ordinal);
        if (q.Count == 0) return 0;
        var t = Tokens(text).ToHashSet(StringComparer.Ordinal);
        return q.Count(x => t.Contains(x)) / (double)q.Count;
    }
    public static double Cosine(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count != b.Count || a.Count == 0) return 0;
        double dot = 0, aa = 0, bb = 0;
        for (var i = 0; i < a.Count; i++) { dot += a[i] * b[i]; aa += a[i] * a[i]; bb += b[i] * b[i]; }
        return aa == 0 || bb == 0 ? 0 : dot / Math.Sqrt(aa * bb);
    }
    public static IReadOnlyList<(int Index, string Text)> Chunk(string value, int max = 2000, int overlap = 250)
    {
        var text = Normalize(value); if (text.Length == 0) return [];
        overlap = Math.Min(overlap, max / 2);
        var parts = Regex.Split(text, "\\n{2,}"); var chunks = new List<(int Index, string Text)>(); var current = "";
        void Push() { var c = Normalize(current); if (c.Length == 0) return; chunks.Add((chunks.Count, c)); current = c[^Math.Min(overlap, c.Length)..]; }
        foreach (var p in parts)
        {
            if (p.Length > max)
            {
                if (current.Length > 0) Push();
                for (var start = 0; start < p.Length; start += max - overlap) { var segment = Normalize(p[start..Math.Min(p.Length, start + max)]); chunks.Add((chunks.Count, segment)); if (start + max >= p.Length) break; }
                current = ""; continue;
            }
            var joined = current.Length == 0 ? p : current + "\n\n" + p;
            if (joined.Length > max && current.Length > 0) Push();
            current = current.Length == 0 ? p : current + "\n\n" + p;
        }
        if (current.Length > 0 && (chunks.Count == 0 || chunks[^1].Text != Normalize(current))) chunks.Add((chunks.Count, Normalize(current)));
        return chunks;
    }
}

public sealed class LocalHashEmbeddingProvider(int dimensions) : IEmbeddingProvider
{
    public EmbeddingDescriptor Descriptor { get; } = new("local", "feature-hash", dimensions, "1");
    private static uint Fnv(string value) { unchecked { uint hash = 0x811c9dc5; foreach (var c in value) { hash ^= c; hash *= 0x01000193; } return hash; } }
    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var result = new List<float[]>();
        foreach (var text in texts)
        {
            var vector = new float[dimensions]; var tokens = TextAlgorithms.Tokens(text);
            for (var i = 0; i < tokens.Length; i++)
            {
                var u = Fnv(tokens[i]); vector[u % (uint)dimensions] += (u & 1) != 0 ? 1 : -1;
                if (i + 1 < tokens.Length) { var b = Fnv(tokens[i] + ":" + tokens[i + 1]); vector[b % (uint)dimensions] += (b & 1) != 0 ? .5f : -.5f; }
            }
            var norm = Math.Sqrt(vector.Sum(x => x * x)); if (norm > 0) for (var i = 0; i < vector.Length; i++) vector[i] = (float)(vector[i] / norm);
            result.Add(vector);
        }
        return Task.FromResult<IReadOnlyList<float[]>>(result);
    }
}

public sealed class InMemoryObjectStore : IObjectStore
{
    private readonly ConcurrentDictionary<string, StoredObject> _objects = new(StringComparer.Ordinal);
    public Task PutAsync(string key, StoredObject value, CancellationToken ct = default) { _objects[key] = value with { Body = value.Body.ToArray() }; return Task.CompletedTask; }
    public Task<StoredObject> GetAsync(string key, CancellationToken ct = default) => _objects.TryGetValue(key, out var value) ? Task.FromResult(value with { Body = value.Body.ToArray() }) : throw new NotFoundError($"Object {key} was not found");
    public Task DeleteAsync(string key, CancellationToken ct = default) { _objects.TryRemove(key, out _); return Task.CompletedTask; }
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(_objects.ContainsKey(key));
    public Task<int> DeleteExpiredAsync(string prefix, DateTimeOffset before, CancellationToken ct = default) { var n = 0; foreach (var p in _objects.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal) && x.Value.CreatedAt < before).Select(x => x.Key)) if (_objects.TryRemove(p, out _)) n++; return Task.FromResult(n); }
}

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private sealed record JobState(IngestionJob Job, string State);
    private readonly object _gate = new();
    private readonly Dictionary<string, DocumentRecord> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ChunkRecord>> _chunks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JobState> _jobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Tombstone> _tombstones = new(StringComparer.Ordinal);
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    private static DocumentSummary Summary(DocumentRecord d) => new(d.Id, d.Status, d.ContentHash, d.Metadata with { Tags = d.Metadata.Tags.ToArray() }, d.Embedding, d.ChunkCount, d.TextLength, d.Error, d.CreatedBy, d.CreatedAt, d.UpdatedAt);
    public Task<(DocumentRecord Document, IngestionJob Job)> CreateDocumentWithJobAsync(CreateDocumentInput input, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_documents.Values.Any(x => x.ContentHash == input.ContentHash) || _tombstones.Values.Any(x => x.ContentHash == input.ContentHash)) throw new ConflictError($"Content already exists as document {input.Id}");
            var now = DateTimeOffset.UtcNow; var d = new DocumentRecord(input.Id, DocumentStatus.Pending, input.ContentHash, input.SourceObjectKey, input.Metadata, null, 0, 0, null, input.CreatedBy, now, now); var j = new IngestionJob(Guid.NewGuid().ToString(), d.Id, 0, now); _documents.Add(d.Id, d); _jobs.Add(j.Id, new JobState(j, "queued")); return Task.FromResult((d, j));
        }
    }
    public Task<DocumentRecord?> GetDocumentAsync(string id, CancellationToken ct = default) { lock (_gate) return Task.FromResult(_documents.TryGetValue(id, out var d) ? d : null); }
    public Task<DocumentRecord?> FindByContentHashAsync(string hash, CancellationToken ct = default) { lock (_gate) return Task.FromResult(_documents.Values.FirstOrDefault(x => x.ContentHash == hash)); }
    public Task<(IReadOnlyList<DocumentSummary> Documents, string? NextCursor)> ListDocumentsAsync(string? cursor, int limit, DocumentStatus? status, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var values = _documents.Values.Select(Summary).Concat(_tombstones.Values.Select(t => new DocumentSummary(t.DocumentId, DocumentStatus.Deleted, t.ContentHash, new("[removed]", null, [], null, "application/x-removed", 0), null, t.ChunkCount, 0, null, t.RemovedBy, t.RemovedAt, t.RemovedAt))).Where(x => status is null || x.Status == status).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToList();
            if (cursor is not null) { var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Replace('-', '+').Replace('_', '/') + new string('=', (4 - cursor.Length % 4) % 4))); var split = decoded.LastIndexOf('|'); if (split < 1) throw new ValidationError("Invalid cursor"); var at = DateTimeOffset.Parse(decoded[..split]); var id = decoded[(split + 1)..]; values = values.Where(x => x.CreatedAt < at || x.CreatedAt == at && string.CompareOrdinal(x.Id, id) < 0).ToList(); }
            var page = values.Take(limit).ToArray(); string? next = values.Count > page.Length && page.Length > 0 ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{page[^1].CreatedAt:O}|{page[^1].Id}")).TrimEnd('=').Replace('+', '-').Replace('/', '_') : null; return Task.FromResult(((IReadOnlyList<DocumentSummary>)page, next));
        }
    }
    public Task<IngestionJob?> ClaimNextJobAsync(CancellationToken ct = default) { lock (_gate) { var pair = _jobs.Where(x => x.Value.State == "queued").OrderBy(x => x.Value.Job.CreatedAt).FirstOrDefault(); if (pair.Key is null) return Task.FromResult<IngestionJob?>(null); var j = pair.Value.Job with { Attempts = pair.Value.Job.Attempts + 1 }; _jobs[pair.Key] = new JobState(j, "processing"); return Task.FromResult<IngestionJob?>(j); } }
    private DocumentRecord Required(string id) => _documents.TryGetValue(id, out var d) ? d : throw new NotFoundError($"Document {id} was not found");
    public Task MarkProcessingAsync(string id, CancellationToken ct = default) { lock (_gate) { var d = Required(id); _documents[id] = d with { Status = DocumentStatus.Processing, Error = null, UpdatedAt = DateTimeOffset.UtcNow }; return Task.CompletedTask; } }
    public Task MarkReadyAsync(ReadyDocumentInput input, CancellationToken ct = default) { lock (_gate) { var d = Required(input.DocumentId); _documents[d.Id] = d with { Status = DocumentStatus.Ready, Embedding = input.Embedding, ChunkCount = input.Chunks.Count, TextLength = input.TextLength, Error = null, UpdatedAt = DateTimeOffset.UtcNow }; _chunks[d.Id] = input.Chunks.ToList(); Finish(d.Id, "completed"); return Task.CompletedTask; } }
    public Task MarkFailedAsync(string id, string error, CancellationToken ct = default) { lock (_gate) { var d = Required(id); _documents[id] = d with { Status = DocumentStatus.Failed, Error = error[..Math.Min(error.Length, 1000)], UpdatedAt = DateTimeOffset.UtcNow }; Finish(id, "failed"); return Task.CompletedTask; } }
    public Task MarkDeletingAsync(string id, CancellationToken ct = default) { lock (_gate) { var d = Required(id); _documents[id] = d with { Status = DocumentStatus.Deleting, UpdatedAt = DateTimeOffset.UtcNow }; return Task.CompletedTask; } }
    public Task<IReadOnlyList<LookupMatch>> SearchAsync(SearchInput input, CancellationToken ct = default) { lock (_gate) { var matches = new List<LookupMatch>(); foreach (var d in _documents.Values.Where(x => x.Status == DocumentStatus.Ready)) { if (input.Filters.Source is not null && d.Metadata.Source != input.Filters.Source) continue; if (input.Filters.DocumentIds is not null && !input.Filters.DocumentIds.Contains(d.Id)) continue; if (input.Filters.Tags is not null && !input.Filters.Tags.All(d.Metadata.Tags.Contains)) continue; foreach (var c in _chunks.GetValueOrDefault(d.Id, []) ) { var score = ((TextAlgorithms.Cosine(input.QueryEmbedding, c.Embedding) + 1) / 2) * .65 + TextAlgorithms.Lexical(input.Query, c.Text) * .35; matches.Add(new(d.Id, c.Id, d.Metadata.Title, d.Metadata.Source, c.Index, c.Text[..Math.Min(800, c.Text.Length)], score, c.Citation, d.Metadata.Tags)); } } return Task.FromResult<IReadOnlyList<LookupMatch>>(matches.OrderByDescending(x => x.Score).Take(input.Limit).ToArray()); } }
    public Task<Tombstone> PurgeDocumentAsync(string id, string removedBy, CancellationToken ct = default) { lock (_gate) { var d = Required(id); var t = new Tombstone(id, d.ContentHash, removedBy, DateTimeOffset.UtcNow, d.ChunkCount); _documents.Remove(id); _chunks.Remove(id); foreach (var j in _jobs.Where(x => x.Value.Job.DocumentId == id).Select(x => x.Key).ToArray()) _jobs.Remove(j); _tombstones[id] = t; return Task.FromResult(t); } }
    private void Finish(string id, string state) { foreach (var key in _jobs.Where(x => x.Value.Job.DocumentId == id && x.Value.State == "processing").Select(x => x.Key).ToArray()) _jobs[key] = _jobs[key] with { State = state }; }
}

public sealed class UploadTokenService(IObjectStore store, string secret, TimeSpan ttl)
{
    public sealed record Payload(string Key, string Filename, string ContentType, long SizeBytes, string ContentHash, DateTimeOffset ExpiresAt);
    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Unb64(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
    public async Task<(string UploadRef, string ContentHash, DateTimeOffset ExpiresAt)> StageAsync(byte[] body, string filename, string contentType, CancellationToken ct = default)
    {
        var hash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        var key = $"uploads/{Guid.NewGuid()}";
        var exp = DateTimeOffset.UtcNow.Add(ttl);
        await store.PutAsync(key, new StoredObject(body, filename, contentType, DateTimeOffset.UtcNow), ct);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            key,
            filename,
            contentType,
            sizeBytes = body.LongLength,
            contentHash = hash,
            expiresAt = exp.ToUnixTimeSeconds(),
        });
        var data = B64(payload);
        var sig = B64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)));
        return ($"{data}.{sig}", hash, exp);
    }
    public Payload Verify(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            throw new ValidationError("Upload reference is malformed");
        var expected = B64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(parts[0])));
        var supplied = Encoding.UTF8.GetBytes(parts[1]);
        var actual = Encoding.UTF8.GetBytes(expected);
        if (!CryptographicOperations.FixedTimeEquals(actual, supplied))
            throw new ValidationError("Upload reference signature is invalid");
        try
        {
            using var doc = JsonDocument.Parse(Unb64(parts[0]));
            var root = doc.RootElement;
            if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 1)
                throw new ValidationError("Upload reference payload is invalid");
            var exp = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("expiresAt").GetInt64());
            if (exp <= DateTimeOffset.UtcNow) throw new ValidationError("Upload reference has expired");
            return new(
                root.GetProperty("key").GetString()!,
                root.GetProperty("filename").GetString()!,
                root.GetProperty("contentType").GetString()!,
                root.GetProperty("sizeBytes").GetInt64(),
                root.GetProperty("contentHash").GetString()!,
                exp);
        }
        catch (AppError) { throw; }
        catch { throw new ValidationError("Upload reference payload is invalid"); }
    }
    public DateTimeOffset ExpirationCutoff(DateTimeOffset now) => now.Subtract(ttl);
}

public sealed class RagService(IDocumentRepository repository, IObjectStore objects, IEmbeddingProvider embeddings, UploadTokenService uploads)
{
    public async Task<Dictionary<string, object?>> WriteAsync(Principal principal, string title, string? text, string? uploadRef, string? source, IReadOnlyList<string>? tags, CancellationToken ct = default)
    {
        Require(principal, Role.Contributor);
        title = TextAlgorithms.Normalize(title);
        if (title.Length == 0) throw new ValidationError("title is required");
        if (title.Length > 500) throw new ValidationError("title exceeds the maximum length");
        if ((text is null) == (uploadRef is null)) throw new ValidationError("Provide exactly one of text or upload_ref");
        if (source is not null && source.Trim().Length > 1000) throw new ValidationError("source exceeds the maximum length");
        if (tags is not null && tags.Count > 20) throw new ValidationError("tags exceeds the maximum count");
        var normalizedTags = (tags ?? []).Select(x => TextAlgorithms.Normalize(x)).ToArray();
        if (normalizedTags.Any(x => x.Length == 0 || x.Length > 100)) throw new ValidationError("tag must contain 1 to 100 characters");
        normalizedTags = normalizedTags.Distinct(StringComparer.Ordinal).ToArray();

        StoredObject value;
        string? staged = null;
        if (uploadRef is not null)
        {
            var payload = uploads.Verify(uploadRef);
            staged = payload.Key;
            value = await objects.GetAsync(payload.Key, ct);
            var actualHash = Convert.ToHexString(SHA256.HashData(value.Body)).ToLowerInvariant();
            if (value.Body.LongLength != payload.SizeBytes || !payload.ContentHash.Equals(actualHash, StringComparison.Ordinal))
                throw new ValidationError("Staged upload no longer matches its signed reference");
        }
        else
        {
            var normalized = TextAlgorithms.Normalize(text!);
            if (normalized.Length == 0) throw new ValidationError("text must contain searchable content");
            if (normalized.Length > 1_000_000) throw new ValidationError("text exceeds the maximum length");
            value = new(Encoding.UTF8.GetBytes(normalized), title + ".txt", "text/plain; charset=utf-8", DateTimeOffset.UtcNow);
        }

        var hash = Convert.ToHexString(SHA256.HashData(value.Body)).ToLowerInvariant();
        var duplicate = await repository.FindByContentHashAsync(hash, ct);
        if (duplicate is not null)
        {
            if (staged is not null) await objects.DeleteAsync(staged, ct);
            return new()
            {
                ["document_id"] = duplicate.Id,
                ["content_hash"] = duplicate.ContentHash,
                ["status"] = duplicate.Status.ToString().ToLowerInvariant(),
                ["job_id"] = null,
                ["deduplicated"] = true,
            };
        }

        var id = Guid.NewGuid().ToString();
        var filename = Path.GetFileName(value.Filename);
        var key = $"documents/{id}/{filename}";
        await objects.PutAsync(key, value, ct);
        var metadata = new DocumentMetadata(
            title,
            string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
            normalizedTags,
            filename,
            value.ContentType,
            value.Body.LongLength);
        try
        {
            var created = await repository.CreateDocumentWithJobAsync(new(id, hash, key, metadata, principal.Subject), ct);
            if (staged is not null) await objects.DeleteAsync(staged, ct);
            return new()
            {
                ["document_id"] = created.Document.Id,
                ["content_hash"] = created.Document.ContentHash,
                ["status"] = "pending",
                ["job_id"] = created.Job.Id,
                ["deduplicated"] = false,
            };
        }
        catch
        {
            await objects.DeleteAsync(key, ct);
            if (staged is not null) await objects.DeleteAsync(staged, ct);
            throw;
        }
    }
    public async Task<Dictionary<string, object?>> LookupAsync(Principal principal, string query, int limit, LookupFilters filters, CancellationToken ct = default)
    {
        Require(principal, Role.Reader);
        query = TextAlgorithms.Normalize(query);
        if (query.Length == 0) throw new ValidationError("query is required");
        if (query.Length > 8000) throw new ValidationError("query exceeds the maximum length");
        if (limit is < 1 or > 25) throw new ValidationError("limit must be between 1 and 25");
        var vector = (await embeddings.EmbedAsync([query], ct))[0];
        var matches = await repository.SearchAsync(new(query, vector, filters, limit), ct);
        return new() { ["query"] = query, ["matches"] = matches.Select(MatchJson).ToArray(), ["count"] = matches.Count, ["embedding"] = embeddings.Descriptor };
    }
    public async Task<Dictionary<string, object?>> ViewAllAsync(Principal principal, string? cursor, int limit, DocumentStatus? status, CancellationToken ct = default)
    {
        Require(principal, Role.Reader);
        if (limit is < 1 or > 100) throw new ValidationError("limit must be between 1 and 100");
        var page = await repository.ListDocumentsAsync(cursor, limit, status, ct);
        return new() { ["documents"] = page.Documents.Select(SummaryJson).ToArray(), ["next_cursor"] = page.NextCursor };
    }
    public async Task<Dictionary<string, object?>> RemoveAsync(Principal principal, string id, bool confirm, CancellationToken ct = default) { Require(principal, Role.Admin); if (!confirm) throw new ValidationError("confirm must be true for permanent removal"); var d = await repository.GetDocumentAsync(id, ct) ?? throw new NotFoundError($"Document {id} was not found"); await repository.MarkDeletingAsync(id, ct); await objects.DeleteAsync(d.SourceObjectKey, ct); var t = await repository.PurgeDocumentAsync(id, principal.Subject, ct); return new() { ["document_id"] = t.DocumentId, ["status"] = "deleted", ["removed_at"] = t.RemovedAt, ["removed_chunks"] = t.ChunkCount, ["tombstone_retained"] = true }; }
    public async Task<bool> ProcessOneJobAsync(CancellationToken ct = default) { var job = await repository.ClaimNextJobAsync(ct); if (job is null) return false; var d = await repository.GetDocumentAsync(job.DocumentId, ct); if (d is null) return true; try { await repository.MarkProcessingAsync(d.Id, ct); var source = await objects.GetAsync(d.SourceObjectKey, ct); var text = Extraction.Extract(source); var chunks = TextAlgorithms.Chunk(text); if (chunks.Count == 0) throw new ValidationError("Document produced no searchable chunks"); var vectors = await embeddings.EmbedAsync(chunks.Select(x => x.Text).ToArray(), ct); await repository.MarkReadyAsync(new(d.Id, chunks.Select((x, i) => new ChunkRecord(Guid.NewGuid().ToString(), d.Id, x.Index, x.Text, $"rag://documents/{d.Id}#chunk-{x.Index + 1}", vectors[i])).ToArray(), embeddings.Descriptor, text.Length), ct); } catch (Exception ex) { if (await repository.GetDocumentAsync(d.Id, ct) is not null) await repository.MarkFailedAsync(d.Id, ex.Message, ct); } return true; }
    public Task<int> CleanupExpiredUploadsAsync(CancellationToken ct = default) => objects.DeleteExpiredAsync("uploads/", uploads.ExpirationCutoff(DateTimeOffset.UtcNow), ct);
    private static void Require(Principal p, Role role) { if (!p.Has(role)) throw new ForbiddenError(); }
    private static Dictionary<string, object?> MatchJson(LookupMatch x) => new() { ["documentId"] = x.DocumentId, ["chunkId"] = x.ChunkId, ["title"] = x.Title, ["source"] = x.Source, ["chunkIndex"] = x.ChunkIndex, ["excerpt"] = x.Excerpt, ["score"] = x.Score, ["citation"] = x.Citation, ["tags"] = x.Tags };
    private static Dictionary<string, object?> SummaryJson(DocumentSummary x) => new() { ["id"] = x.Id, ["status"] = x.Status.ToString().ToLowerInvariant(), ["contentHash"] = x.ContentHash, ["metadata"] = x.Metadata, ["embedding"] = x.Embedding, ["chunkCount"] = x.ChunkCount, ["textLength"] = x.TextLength, ["error"] = x.Error, ["createdBy"] = x.CreatedBy, ["createdAt"] = x.CreatedAt, ["updatedAt"] = x.UpdatedAt };
}

public static class Extraction
{
    public static string Extract(StoredObject source)
    {
        var extension = Path.GetExtension(source.Filename).ToLowerInvariant();
        var text = extension switch
        {
            ".txt" or ".md" or ".markdown" => Encoding.UTF8.GetString(source.Body),
            ".html" or ".htm" => Html(source.Body),
            ".docx" => Docx(source.Body),
            ".pdf" => Pdf(source.Body),
            _ => throw new ValidationError($"Unsupported file type for {source.Filename}"),
        };
        text = TextAlgorithms.Normalize(text);
        if (text.Length == 0) throw new ValidationError($"No text could be extracted from {source.Filename}; scanned documents require deferred OCR support");
        return text;
    }

    private static string Html(byte[] body)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(request => request.Content(Encoding.UTF8.GetString(body))).GetAwaiter().GetResult();
        foreach (var node in document.QuerySelectorAll("script,style,noscript")) node.Remove();
        return document.Body?.TextContent ?? document.DocumentElement?.TextContent ?? "";
    }

    private static string Docx(byte[] body)
    {
        using var document = WordprocessingDocument.Open(new MemoryStream(body), false);
        var documentBody = document.MainDocumentPart?.Document?.Body;
        var paragraphs = documentBody is null
            ? Array.Empty<string>()
            : documentBody.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .Select(x => string.Concat(x.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text)))
                .Where(x => x.Length > 0)
                .ToArray();
        return string.Join("\n\n", paragraphs);
    }

    private static string Pdf(byte[] body)
    {
        using var document = PdfDocument.Open(new MemoryStream(body));
        return string.Join("\n\n", document.GetPages().Select(page => page.Text));
    }
}
