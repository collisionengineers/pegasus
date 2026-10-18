using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The verified bytes of one managed version, for a caller that wants the
/// bytes themselves — an archive entry, a rendered report image — and holds
/// one version at a time.
/// </summary>
internal static class ManagedVersionBytes
{
    /// <summary>
    /// Where the content cache is composed the version is read through it, so
    /// a repeat read does not ask Box again; otherwise it is the store's
    /// single read. Either way it is verified against its custody hash and
    /// length before it is returned.
    /// </summary>
    internal static async ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        IDocumentContentStore contentStore,
        IReadCachedDocumentVersions? cachedVersions,
        ManagedDocumentContentRead read,
        CancellationToken cancellationToken)
    {
        if (cachedVersions is not null)
        {
            return await cachedVersions.ReadVersionAsync(read, cancellationToken).ConfigureAwait(false);
        }

        await using var content = await contentStore.OpenReadVersionAsync(
            read.Address, read.ExpectedSha256, read.ExpectedLength, cancellationToken).ConfigureAwait(false);
        var bytes = GC.AllocateUninitializedArray<byte>(checked((int)read.ExpectedLength));
        await content.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }
}
