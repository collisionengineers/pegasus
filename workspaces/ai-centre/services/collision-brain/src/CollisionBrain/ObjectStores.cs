using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;

namespace CollisionBrain;

public sealed class FileObjectStore : IObjectStore
{
    private readonly string _root;
    public FileObjectStore(string root) { _root = Path.GetFullPath(root); Directory.CreateDirectory(_root); }
    private string PathFor(string key)
    {
        if (key.Contains('\0') || key.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(key)) throw new ValidationError("Invalid object key");
        var path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar))); if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ValidationError("Invalid object key"); return path;
    }
    public async Task PutAsync(string key, StoredObject value, CancellationToken ct = default) { var path = PathFor(key); Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temp = path + ".tmp-" + Guid.NewGuid().ToString("N"); await File.WriteAllBytesAsync(temp, value.Body, ct); File.Move(temp, path, true); await File.WriteAllTextAsync(path + ".metadata.json", JsonSerializer.Serialize(value with { Body = [] }), ct); }
    public async Task<StoredObject> GetAsync(string key, CancellationToken ct = default) { var path = PathFor(key); if (!File.Exists(path)) throw new NotFoundError($"Object {key} was not found"); var metadata = JsonSerializer.Deserialize<StoredObject>(await File.ReadAllTextAsync(path + ".metadata.json", ct)) ?? new([], Path.GetFileName(path), "application/octet-stream", File.GetCreationTimeUtc(path)); return metadata with { Body = await File.ReadAllBytesAsync(path, ct) }; }
    public Task DeleteAsync(string key, CancellationToken ct = default) { var path = PathFor(key); if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".metadata.json")) File.Delete(path + ".metadata.json"); return Task.CompletedTask; }
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(File.Exists(PathFor(key)));
    public Task<int> DeleteExpiredAsync(string prefix, DateTimeOffset before, CancellationToken ct = default) { var count = 0; var basePath = PathFor(prefix); if (!Directory.Exists(basePath)) return Task.FromResult(0); foreach (var path in Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".metadata.json", StringComparison.Ordinal) && File.GetCreationTimeUtc(x) < before.UtcDateTime).ToArray()) { File.Delete(path); if (File.Exists(path + ".metadata.json")) File.Delete(path + ".metadata.json"); count++; } return Task.FromResult(count); }
}

public sealed class S3ObjectStore(IAmazonS3 client, string bucket, string prefix = "") : IObjectStore
{
    private string Key(string key) => string.IsNullOrEmpty(prefix) ? key : prefix.TrimEnd('/') + "/" + key;
    public async Task PutAsync(string key, StoredObject value, CancellationToken ct = default) { await using var stream = new MemoryStream(value.Body, writable: false); await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = Key(key), InputStream = stream, ContentType = value.ContentType, Metadata = { ["filename"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value.Filename)), ["created-at"] = value.CreatedAt.ToString("O") } }, ct); }
    public async Task<StoredObject> GetAsync(string key, CancellationToken ct = default) { try { using var response = await client.GetObjectAsync(bucket, Key(key), ct); await using var ms = new MemoryStream(); await response.ResponseStream.CopyToAsync(ms, ct); return new(ms.ToArray(), response.Metadata["filename"] is { } f ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(f)) : Path.GetFileName(key), response.Headers.ContentType ?? "application/octet-stream", response.LastModified.GetValueOrDefault(DateTime.UtcNow).ToUniversalTime()); } catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { throw new NotFoundError($"Object {key} was not found"); } }
    public async Task DeleteAsync(string key, CancellationToken ct = default) => await client.DeleteObjectAsync(bucket, Key(key), ct);
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default) { try { await client.GetObjectMetadataAsync(bucket, Key(key), ct); return true; } catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return false; } }
    public async Task<int> DeleteExpiredAsync(string prefix, DateTimeOffset before, CancellationToken ct = default) { var count = 0; string? token = null; do { var page = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, Prefix = Key(prefix), ContinuationToken = token }, ct); foreach (var item in page.S3Objects.Where(x => x.LastModified < before)) { await client.DeleteObjectAsync(bucket, item.Key, ct); count++; } token = page.IsTruncated == true ? page.NextContinuationToken : null; } while (token is not null); return count; }
}
