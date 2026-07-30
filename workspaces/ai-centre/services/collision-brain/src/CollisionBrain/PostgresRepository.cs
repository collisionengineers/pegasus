using System.Text.Json;
using Npgsql;

namespace CollisionBrain;

public sealed class PostgresDocumentRepository : IDocumentRepository
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresDocumentRepository(string connectionString) => _dataSource = NpgsqlDataSource.Create(NormalizeConnectionString(connectionString));
    private static string NormalizeConnectionString(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return value;
        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        };
        return builder.ConnectionString;
    }
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using (var schema = Command(connection, null, "CREATE TABLE IF NOT EXISTS schema_migrations (name text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())")) await schema.ExecuteNonQueryAsync(ct);
        var root = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "migrations")) ? Path.Combine(AppContext.BaseDirectory, "migrations") : Path.Combine(Directory.GetCurrentDirectory(), "migrations");
        foreach (var file in Directory.EnumerateFiles(root, "*.sql").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            await using var check = Command(connection, null, "SELECT 1 FROM schema_migrations WHERE name=$1"); check.Parameters.AddWithValue(Path.GetFileName(file)); if (await check.ExecuteScalarAsync(ct) is not null) continue;
            await using var tx = await connection.BeginTransactionAsync(ct); await using var apply = Command(connection, tx, await File.ReadAllTextAsync(file, ct)); await apply.ExecuteNonQueryAsync(ct); await using var record = Command(connection, tx, "INSERT INTO schema_migrations(name) VALUES ($1)"); record.Parameters.AddWithValue(Path.GetFileName(file)); await record.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
        }
    }
    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
    public async Task<(DocumentRecord Document, IngestionJob Job)> CreateDocumentWithJobAsync(CreateDocumentInput input, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); var duplicate = await FindByContentHashAsync(input.ContentHash, ct); if (duplicate is not null) throw new ConflictError($"Content already exists as document {duplicate.Id}"); var now = DateTimeOffset.UtcNow; var document = new DocumentRecord(input.Id, DocumentStatus.Pending, input.ContentHash, input.SourceObjectKey, input.Metadata, null, 0, 0, null, input.CreatedBy, now, now);
        await using (var command = Command(connection, tx, "INSERT INTO documents(id,status,content_hash,source_object_key,metadata,created_by,created_at,updated_at) VALUES($1,'pending',$2,$3,$4::jsonb,$5,$6,$6)")) { command.Parameters.AddWithValue(Guid.Parse(input.Id)); command.Parameters.AddWithValue(input.ContentHash); command.Parameters.AddWithValue(input.SourceObjectKey); command.Parameters.AddWithValue(JsonSerializer.Serialize(input.Metadata)); command.Parameters.AddWithValue(input.CreatedBy); command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(ct); }
        var job = new IngestionJob(Guid.NewGuid().ToString(), input.Id, 0, now); await using (var command = Command(connection, tx, "INSERT INTO ingestion_jobs(id,document_id,state,created_at) VALUES($1,$2,'queued',$3)")) { command.Parameters.AddWithValue(Guid.Parse(job.Id)); command.Parameters.AddWithValue(Guid.Parse(job.DocumentId)); command.Parameters.AddWithValue(job.CreatedAt); await command.ExecuteNonQueryAsync(ct); } await tx.CommitAsync(ct); return (document, job);
    }
    public async Task<DocumentRecord?> GetDocumentAsync(string id, CancellationToken ct = default) => await ReadDocumentAsync("WHERE id=$1", [Guid.Parse(id)], ct);
    public async Task<DocumentRecord?> FindByContentHashAsync(string hash, CancellationToken ct = default) => await ReadDocumentAsync("WHERE content_hash=$1", [hash], ct);
    public async Task<(IReadOnlyList<DocumentSummary> Documents, string? NextCursor)> ListDocumentsAsync(string? cursor, int limit, DocumentStatus? status, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct); await using var command = Command(connection, null, "SELECT id,status,content_hash,metadata,embedding,chunk_count,text_length,error,created_by,created_at,updated_at FROM documents WHERE ($1::text IS NULL OR status=$1) ORDER BY created_at DESC,id DESC LIMIT $2"); command.Parameters.AddWithValue(status is null ? DBNull.Value : status.Value.ToString().ToLowerInvariant()); command.Parameters.AddWithValue(limit + 1); await using var reader = await command.ExecuteReaderAsync(ct); var all = new List<DocumentSummary>(); while (await reader.ReadAsync(ct)) all.Add(ReadSummary(reader)); var page = all.Take(limit).ToArray(); var last = page.LastOrDefault(); var next = all.Count > page.Length && last is not null ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{last.CreatedAt:O}|{last.Id}")).TrimEnd('=').Replace('+','-').Replace('/','_') : null; return (page, next);
    }
    public async Task<IngestionJob?> ClaimNextJobAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using (var reclaim = Command(connection, tx, "UPDATE ingestion_jobs SET state='queued',started_at=NULL WHERE state='processing' AND started_at < now() - interval '15 minutes' AND attempts < 3"))
            await reclaim.ExecuteNonQueryAsync(ct);
        await using var command = Command(connection, tx, "UPDATE ingestion_jobs SET state='processing',attempts=attempts+1,started_at=now() WHERE id=(SELECT id FROM ingestion_jobs WHERE state='queued' AND attempts < 3 ORDER BY created_at FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING id,document_id,attempts,created_at");
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            await tx.RollbackAsync(ct);
            return null;
        }
        var job = new IngestionJob(reader.GetGuid(0).ToString(), reader.GetGuid(1).ToString(), reader.GetInt32(2), reader.GetFieldValue<DateTimeOffset>(3));
        await reader.CloseAsync();
        await tx.CommitAsync(ct);
        return job;
    }
    public Task MarkProcessingAsync(string id, CancellationToken ct = default) => ExecuteAsync("UPDATE documents SET status='processing',error=NULL,updated_at=now() WHERE id=$1", [Guid.Parse(id)], ct);
    public async Task MarkReadyAsync(ReadyDocumentInput input, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await using (var command = Command(connection, tx, "UPDATE documents SET status='ready',embedding=$2::jsonb,chunk_count=$3,text_length=$4,error=NULL,updated_at=now() WHERE id=$1")) { command.Parameters.AddWithValue(Guid.Parse(input.DocumentId)); command.Parameters.AddWithValue(JsonSerializer.Serialize(input.Embedding)); command.Parameters.AddWithValue(input.Chunks.Count); command.Parameters.AddWithValue(input.TextLength); await command.ExecuteNonQueryAsync(ct); }
        await using (var clear = Command(connection, tx, "DELETE FROM document_chunks WHERE document_id=$1")) { clear.Parameters.AddWithValue(Guid.Parse(input.DocumentId)); await clear.ExecuteNonQueryAsync(ct); }
        foreach (var chunk in input.Chunks) { await using var command = Command(connection, tx, "INSERT INTO document_chunks(id,document_id,chunk_index,content,citation,embedding) VALUES($1,$2,$3,$4,$5,$6::vector)"); command.Parameters.AddWithValue(Guid.Parse(chunk.Id)); command.Parameters.AddWithValue(Guid.Parse(chunk.DocumentId)); command.Parameters.AddWithValue(chunk.Index); command.Parameters.AddWithValue(chunk.Text); command.Parameters.AddWithValue(chunk.Citation); command.Parameters.AddWithValue("[" + string.Join(',', chunk.Embedding.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]"); await command.ExecuteNonQueryAsync(ct); }
        await using (var job = Command(connection, tx, "UPDATE ingestion_jobs SET state='completed',completed_at=now() WHERE document_id=$1 AND state='processing'")) { job.Parameters.AddWithValue(Guid.Parse(input.DocumentId)); await job.ExecuteNonQueryAsync(ct); } await tx.CommitAsync(ct);
    }
    public async Task MarkFailedAsync(string id, string error, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using (var document = Command(connection, tx, "UPDATE documents SET status='failed',error=$2,updated_at=now() WHERE id=$1"))
        {
            document.Parameters.AddWithValue(Guid.Parse(id));
            document.Parameters.AddWithValue(error[..Math.Min(error.Length, 1000)]);
            await document.ExecuteNonQueryAsync(ct);
        }
        await using (var job = Command(connection, tx, "UPDATE ingestion_jobs SET state='failed',last_error=$2,completed_at=now() WHERE document_id=$1 AND state='processing'"))
        {
            job.Parameters.AddWithValue(Guid.Parse(id));
            job.Parameters.AddWithValue(error[..Math.Min(error.Length, 1000)]);
            await job.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
    public Task MarkDeletingAsync(string id, CancellationToken ct = default) => ExecuteAsync("UPDATE documents SET status='deleting',updated_at=now() WHERE id=$1", [Guid.Parse(id)], ct);
    public async Task<IReadOnlyList<LookupMatch>> SearchAsync(SearchInput input, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = Command(connection, null, "");
        var clauses = new List<string> { "d.status='ready'" };
        command.Parameters.AddWithValue("[" + string.Join(',', input.QueryEmbedding.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]");
        command.Parameters.AddWithValue(input.Query);
        if (input.Filters.Source is not null)
        {
            clauses.Add($"d.metadata->>'source'=${command.Parameters.Count + 1}");
            command.Parameters.AddWithValue(input.Filters.Source);
        }
        if (input.Filters.DocumentIds is not null)
        {
            clauses.Add($"d.id = ANY(${command.Parameters.Count + 1}::uuid[])");
            command.Parameters.AddWithValue(input.Filters.DocumentIds.Select(Guid.Parse).ToArray());
        }
        if (input.Filters.Tags is not null)
        {
            clauses.Add($"d.metadata->'tags' @> ${command.Parameters.Count + 1}::jsonb");
            command.Parameters.AddWithValue(JsonSerializer.Serialize(input.Filters.Tags));
        }
        var limitParameter = command.Parameters.Count + 1;
        command.CommandText = $"SELECT d.id,dc.id,d.metadata->>'title',d.metadata->>'source',dc.chunk_index,dc.content,(0.65*((1-(dc.embedding <=> $1::vector))/2)+0.35*ts_rank(dc.search_vector,plainto_tsquery('simple',$2))) AS score,dc.citation,d.metadata->'tags' FROM documents d JOIN document_chunks dc ON dc.document_id=d.id WHERE {string.Join(" AND ", clauses)} ORDER BY score DESC LIMIT ${limitParameter}";
        command.Parameters.AddWithValue(input.Limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<LookupMatch>();
        while (await reader.ReadAsync(ct))
        {
            var content = reader.GetString(5);
            result.Add(new(
                reader.GetGuid(0).ToString(),
                reader.GetGuid(1).ToString(),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4),
                content[..Math.Min(800, content.Length)],
                reader.GetDouble(6),
                reader.GetString(7),
                JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? []));
        }
        return result;
    }
    public async Task<Tombstone> PurgeDocumentAsync(string id, string removedBy, CancellationToken ct = default)
    {
        var document = await GetDocumentAsync(id, ct) ?? throw new NotFoundError($"Document {id} was not found"); var tombstone = new Tombstone(id, document.ContentHash, removedBy, DateTimeOffset.UtcNow, document.ChunkCount); await using var connection = await _dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await using (var delete = Command(connection, tx, "DELETE FROM documents WHERE id=$1")) { delete.Parameters.AddWithValue(Guid.Parse(id)); await delete.ExecuteNonQueryAsync(ct); } await using (var insert = Command(connection, tx, "INSERT INTO document_tombstones(document_id,content_hash,removed_by,removed_at,chunk_count) VALUES($1,$2,$3,$4,$5)")) { insert.Parameters.AddWithValue(Guid.Parse(id)); insert.Parameters.AddWithValue(tombstone.ContentHash); insert.Parameters.AddWithValue(tombstone.RemovedBy); insert.Parameters.AddWithValue(tombstone.RemovedAt); insert.Parameters.AddWithValue(tombstone.ChunkCount); await insert.ExecuteNonQueryAsync(ct); } await tx.CommitAsync(ct); return tombstone;
    }
    private async Task ExecuteAsync(string sql, object[] values, CancellationToken ct) { await using var connection = await _dataSource.OpenConnectionAsync(ct); await using var command = Command(connection, null, sql); foreach (var value in values) command.Parameters.AddWithValue(value); await command.ExecuteNonQueryAsync(ct); }
    private async Task<DocumentRecord?> ReadDocumentAsync(string where, object[] values, CancellationToken ct) { await using var connection = await _dataSource.OpenConnectionAsync(ct); await using var command = Command(connection, null, $"SELECT id,status,content_hash,source_object_key,metadata,embedding,chunk_count,text_length,error,created_by,created_at,updated_at FROM documents {where}"); foreach (var value in values) command.Parameters.AddWithValue(value); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadDocument(reader) : null; }
    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? tx, string sql) { var command = connection.CreateCommand(); command.CommandText = sql; command.Transaction = tx; return command; }
    private static DocumentRecord ReadDocument(NpgsqlDataReader r) => new(r.GetGuid(0).ToString(), Enum.Parse<DocumentStatus>(r.GetString(1), true), r.GetString(2), r.GetString(3), JsonSerializer.Deserialize<DocumentMetadata>(r.GetString(4))!, r.IsDBNull(5) ? null : JsonSerializer.Deserialize<EmbeddingDescriptor>(r.GetString(5)), r.GetInt32(6), r.GetInt32(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetString(9), r.GetFieldValue<DateTimeOffset>(10), r.GetFieldValue<DateTimeOffset>(11));
    private static DocumentSummary ReadSummary(NpgsqlDataReader r) => new(r.GetGuid(0).ToString(), Enum.Parse<DocumentStatus>(r.GetString(1), true), r.GetString(2), JsonSerializer.Deserialize<DocumentMetadata>(r.GetString(3))!, r.IsDBNull(4) ? null : JsonSerializer.Deserialize<EmbeddingDescriptor>(r.GetString(4)), r.GetInt32(5), r.GetInt32(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetString(8), r.GetFieldValue<DateTimeOffset>(9), r.GetFieldValue<DateTimeOffset>(10));
}
