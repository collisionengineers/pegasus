using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CollisionBrain;

public sealed record LookupFilterInput(string? source = null, string[]? tags = null, string[]? document_ids = null);

[McpServerToolType]
public sealed class RagTools(RuntimeContext context, IHttpContextAccessor httpContextAccessor)
{
    private async Task<Principal> PrincipalAsync(CancellationToken ct) => await context.Auth.AuthenticateAsync(httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault(), ct);
    [McpServerTool(Name = "lookup", Title = "Look up knowledge", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Retrieve ranked source passages with stable citations. This read-only tool does not generate an answer and is safe for client-controlled automatic invocation.")]

    public async Task<Dictionary<string, object?>> Lookup(string query, int limit = 8, LookupFilterInput? filters = null, CancellationToken cancellationToken = default) => await context.Rag.LookupAsync(await PrincipalAsync(cancellationToken), query, limit, new LookupFilters(filters?.source, filters?.tags, filters?.document_ids), cancellationToken);
    [McpServerTool(Name = "write", Title = "Add knowledge", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true), Description("Queue pasted text or a securely staged file for asynchronous extraction, chunking, embedding, and indexing.")]
    public async Task<Dictionary<string, object?>> Write(string title, string? text = null, string? upload_ref = null, string? source = null, string[]? tags = null, CancellationToken cancellationToken = default) => await context.Rag.WriteAsync(await PrincipalAsync(cancellationToken), title, text, upload_ref, source, tags, cancellationToken);

    [McpServerTool(Name = "view_all", Title = "View all documents", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Return a paginated document registry with processing status and metadata, without returning complete document bodies.")]
    public async Task<Dictionary<string, object?>> ViewAll(string? cursor = null, int limit = 50, string? status = null, CancellationToken cancellationToken = default) => await context.Rag.ViewAllAsync(await PrincipalAsync(cancellationToken), cursor, limit, status is null ? null : Enum.TryParse<DocumentStatus>(status, true, out var parsed) ? parsed : throw new ValidationError("Invalid document status"), cancellationToken);

    [McpServerTool(Name = "remove", Title = "Remove knowledge", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true), Description("Permanently purge a document's source and searchable chunks while retaining a content-free audit tombstone.")]
    public async Task<Dictionary<string, object?>> Remove(string document_id, bool confirm, CancellationToken cancellationToken = default) => await context.Rag.RemoveAsync(await PrincipalAsync(cancellationToken), document_id, confirm, cancellationToken);
}
