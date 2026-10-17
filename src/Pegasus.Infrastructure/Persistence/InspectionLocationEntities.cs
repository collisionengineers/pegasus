namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// A reviewed inspection location from the register (R22), unique by the
/// source row it came from.
/// </summary>
internal sealed class InspectionLocationEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string Postcode { get; set; }
    public required string Source { get; set; }
    public required string SourceRecordId { get; set; }
    public DateTimeOffset ReviewedAtUtc { get; set; }
    public bool Active { get; set; } = true;
    public long Version { get; set; }
    public List<InspectionLocationPrincipalLinkEntity> PrincipalLinks { get; set; } = [];
}

/// <summary>Offers a reviewed inspection location on the linked Principal's Cases.</summary>
internal sealed class InspectionLocationPrincipalLinkEntity
{
    public Guid PrincipalId { get; set; }
    public PrincipalEntity Principal { get; set; } = null!;
    public Guid InspectionLocationId { get; set; }
    public InspectionLocationEntity InspectionLocation { get; set; } = null!;
}
