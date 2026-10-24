using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The local staging adapter: each object written once under
/// <c>uploads/</c>, a repeated part compared by its bytes, listed in name
/// order from a continuation and deleted only when old enough.
/// </summary>
public sealed class FileSystemDocumentUploadStagingTests : IDisposable
{
    private static readonly Guid UploadId = new("0b1c2d3e-4f50-6172-8394-a5b6c7d8e9f0");
    private readonly string root = Path.Combine(Path.GetTempPath(), $"pegasus-upload-staging-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TheManifestAndEachPartAreWrittenOnceAndReadBack()
    {
        var staging = new FileSystemDocumentUploadStaging(root);
        var manifest = new DocumentUploadManifest(
            UploadId, "pegasus-automation", 10, new string('A', 64), new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        byte[] part = [1, 2, 3];
        byte[] other = [4, 5, 6];

        Assert.True(await staging.CreateManifestAsync(manifest, default));
        Assert.False(await staging.CreateManifestAsync(manifest with { TotalBytes = 11 }, default));
        Assert.Equal(manifest, await staging.ReadManifestAsync(UploadId, default));
        Assert.Null(await staging.ReadManifestAsync(Guid.NewGuid(), default));

        Assert.Equal(DocumentUploadPartWrite.Created, await staging.CreatePartAsync(UploadId, 2, part, Sha256Of(part), default));
        Assert.Equal(DocumentUploadPartWrite.AlreadyStored, await staging.CreatePartAsync(UploadId, 2, part, Sha256Of(part), default));
        Assert.Equal(DocumentUploadPartWrite.Conflict, await staging.CreatePartAsync(UploadId, 2, other, Sha256Of(other), default));
        Assert.Equal(DocumentUploadPartWrite.Created, await staging.CreatePartAsync(UploadId, 10, other, Sha256Of(other), default));

        Assert.Equal([2, 10], await staging.ListPartNumbersAsync(UploadId, default));
        Assert.Equal(part, (await staging.ReadPartAsync(UploadId, 2, default))!.Value.ToArray());
        Assert.Null(await staging.ReadPartAsync(UploadId, 3, default));
        Assert.True(File.Exists(Path.Combine(root, "uploads", UploadId.ToString("D"), "0002")));
    }

    [Fact]
    public async Task TheListingPagesInNameOrderAndADeleteKeepsAYoungerObject()
    {
        var staging = new FileSystemDocumentUploadStaging(root);
        foreach (var partNumber in new[] { 1, 2, 3 })
        {
            byte[] part = [(byte)partNumber];
            await staging.CreatePartAsync(UploadId, partNumber, part, Sha256Of(part), default);
        }

        var prefix = $"uploads/{UploadId:D}/";
        var first = await staging.ListAsync(2, null, default);
        var second = await staging.ListAsync(2, first.NextContinuation, default);

        Assert.Equal(new[] { prefix + "0001", prefix + "0002" }, first.Items.Select(item => item.Name));
        Assert.Equal(prefix + "0002", first.NextContinuation);
        Assert.Equal(prefix + "0003", Assert.Single(second.Items).Name);
        Assert.Null(second.NextContinuation);

        var written = first.Items[0].CreatedAtUtc;
        Assert.False(await staging.DeleteAsync(prefix + "0001", written.AddMinutes(-1), default));
        Assert.True(await staging.DeleteAsync(prefix + "0001", written.AddMinutes(1), default));
        Assert.Null(await staging.ReadPartAsync(UploadId, 1, default));
        await Assert.ThrowsAsync<ArgumentException>(() => staging.DeleteAsync("staging/../secret", written, default));

        await staging.DeleteAsync(prefix + "0002", written.AddMinutes(1), default);
        await staging.DeleteAsync(prefix + "0003", written.AddMinutes(1), default);
        Assert.False(Directory.Exists(Path.Combine(root, "uploads", UploadId.ToString("D"))));
    }

    private static string Sha256Of(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
