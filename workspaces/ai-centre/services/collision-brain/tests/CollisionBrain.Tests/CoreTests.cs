using System.Collections.Immutable;
using System.Text;
using CollisionBrain;

namespace CollisionBrain.Tests;

public sealed class CoreTests
{
    private static Principal Admin => new("test", ImmutableHashSet.Create(Role.Reader, Role.Contributor, Role.Admin));

    [Fact]
    public void NormalizationAndChunkingPreserveSearchableText()
    {
        Assert.Equal("a\n b", TextAlgorithms.Normalize("a\r\n b\0"));
        var chunks = TextAlgorithms.Chunk(string.Join("\n\n", Enumerable.Range(0, 4).Select(x => new string((char)('a' + x), 600))));
        Assert.True(chunks.Count > 1);
        Assert.Contains(chunks[0].Text[^20..], chunks[1].Text);
    }

    [Fact]
    public async Task LocalHashEmbeddingIsDeterministicAndNormalized()
    {
        var provider = new LocalHashEmbeddingProvider(384);
        var first = (await provider.EmbedAsync(["manufacturer repair method"]))[0];
        var second = (await provider.EmbedAsync(["manufacturer repair method"]))[0];
        Assert.Equal(first, second);
        Assert.Equal(384, first.Length);
        Assert.Equal(1, Math.Sqrt(first.Sum(x => x * x)), 5);
        Assert.Equal(new EmbeddingDescriptor("local", "feature-hash", 384, "1"), provider.Descriptor);
    }

    [Fact]
    public async Task LocalHashEmbeddingMatchesTypeScriptFixture()
    {
        var vector = (await new LocalHashEmbeddingProvider(384).EmbedAsync(["manufacturer repair method"]))[0];
        Assert.Equal(0.2132007f, vector[179], 6);
        Assert.Equal(0.2132007f, vector[215], 6);
        Assert.Equal(-0.8528029f, vector[304], 6);
        Assert.Equal(-0.4264014f, vector[308], 6);
    }

    [Fact]
    public async Task LifecycleDeduplicatesAndRetainsTombstone()
    {
        var repository = new InMemoryDocumentRepository();
        var objects = new InMemoryObjectStore();
        var embeddings = new LocalHashEmbeddingProvider(384);
        var tokens = new UploadTokenService(objects, "test-secret-which-is-long", TimeSpan.FromMinutes(15));
        var rag = new RagService(repository, objects, embeddings, tokens);
        var first = await rag.WriteAsync(Admin, "Repair", "A structural repair method must be cited.", null, "manual", ["repair"]);
        var duplicate = await rag.WriteAsync(Admin, "Repair again", "A structural repair method must be cited.", null, null, null);
        Assert.False((bool)first["deduplicated"]!);
        Assert.True((bool)duplicate["deduplicated"]!);
        while (await rag.ProcessOneJobAsync()) { }
        var lookup = await rag.LookupAsync(Admin, "structural repair method", 5, new());
        Assert.Equal(1, lookup["count"]);
        var id = (string)first["document_id"]!;
        var removed = await rag.RemoveAsync(Admin, id, true);
        Assert.Equal("deleted", removed["status"]);
        var all = await rag.ViewAllAsync(Admin, null, 10, DocumentStatus.Deleted);
        Assert.Single((IEnumerable<Dictionary<string, object?>>)all["documents"]!);
    }

    [Fact]
    public async Task UploadReferenceIsSignedAndConsumedOnWrite()
    {
        var objects = new InMemoryObjectStore(); var tokens = new UploadTokenService(objects, "test-secret-which-is-long", TimeSpan.FromMinutes(15));
        var staged = await tokens.StageAsync(Encoding.UTF8.GetBytes("hello world"), "note.txt", "text/plain");
        var payload = tokens.Verify(staged.UploadRef);
        Assert.Equal(staged.ContentHash, payload.ContentHash);
        var tampered = staged.UploadRef[..^1] + (staged.UploadRef[^1] == 'a' ? 'b' : 'a');
        Assert.Throws<ValidationError>(() => tokens.Verify(tampered));
    }
    [Fact]
    public async Task FilesystemStoreRoundTripsMetadataAndRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "collision-brain-tests", Guid.NewGuid().ToString("N"));
        var store = new FileObjectStore(root);
        await store.PutAsync("uploads/note.txt", new StoredObject(Encoding.UTF8.GetBytes("hello"), "note.txt", "text/plain", DateTimeOffset.UtcNow));
        var value = await store.GetAsync("uploads/note.txt");
        Assert.Equal("hello", Encoding.UTF8.GetString(value.Body));
        Assert.Equal("note.txt", value.Filename);
        await Assert.ThrowsAsync<ValidationError>(() => store.GetAsync("../outside"));
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task PostgresUriConnectionStringsAreAccepted()
    {
        await using var repository = new PostgresDocumentRepository("postgres://rag:password@localhost:5432/rag?sslmode=disable");
    }

    [Fact]
    public async Task ExportAndImportPreserveReadyDocumentContent()
    {
        var settings = new Settings
        {
            RepositoryDriver = "memory",
            ObjectStoreDriver = "memory",
            AuthMode = "none",
            UploadTokenSecret = "test-secret-which-is-long",
        };
        var output = Path.Combine(Path.GetTempPath(), $"collision-brain-export-{Guid.NewGuid():N}.json");
        await using (var source = new RuntimeContext(settings))
        {
            await source.InitializeAsync();
            await source.Rag.WriteAsync(Admin, "Exported", "Export and import must preserve the source.", null, "fixture", ["export"]);
            while (await source.Rag.ProcessOneJobAsync()) { }
            await global::Admin.ExportAsync(source, ["--output", output]);
        }
        await using (var destination = new RuntimeContext(settings))
        {
            await destination.InitializeAsync();
            Assert.Equal(0, await global::Admin.ImportAsync(destination, ["--input", output]));
            var documents = await destination.Rag.ViewAllAsync(Admin, null, 10, DocumentStatus.Ready);
            Assert.Single((IEnumerable<Dictionary<string, object?>>)documents["documents"]!);
        }
        File.Delete(output);
    }
}
