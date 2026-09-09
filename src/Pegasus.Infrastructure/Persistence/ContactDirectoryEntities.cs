namespace Pegasus.Infrastructure.Persistence;

internal sealed class ContactRoleEntity
{
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = null!;
    public required string Role { get; set; }
}

/// <summary>Associates a non-Principal organisation role with a policy-owning
/// Principal. The role is deliberately part of the key: one company can be a
/// Claim Source and third-party engineer for the same Principal.</summary>
internal sealed class ContactPrincipalLinkEntity
{
    public Guid PrincipalId { get; set; }
    public PrincipalEntity Principal { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = null!;
    public required string Role { get; set; }
}
