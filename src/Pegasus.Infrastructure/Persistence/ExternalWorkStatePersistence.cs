namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The persisted <see cref="ExternalWorkItemEntity.State"/> words. Stores
/// compare and assign these constants instead of repeating the literals.
///
/// Not yet the vocabulary's only reader: the remaining Infrastructure stores
/// on the same table still spell the words out, and folding them onto this
/// class is PLAT-056.
/// </summary>
internal static class ExternalWorkStatePersistence
{
    public const string Pending = "pending";
    public const string Dispatching = "dispatching";
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
