namespace Pegasus.Infrastructure.Persistence;

/// <summary>Associates a non-Principal contact with a policy-owning Principal.</summary>
internal sealed class ContactPrincipalLinkEntity
{
    public Guid PrincipalId { get; set; }
    public PrincipalEntity Principal { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = null!;
}
