namespace Pegasus.Infrastructure.Persistence;

internal sealed class EngineerNoteEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public required string RecordedByKind { get; set; }
    public required string RecordedBySubjectId { get; set; }
    public required string RecordedByRolesJson { get; set; }
    public required string Note { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
