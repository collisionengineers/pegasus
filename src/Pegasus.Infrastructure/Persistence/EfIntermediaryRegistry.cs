using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The intermediaries the Contacts directory holds: each active Claim Source
/// with an e-mail, once per active Principal it is linked to. Read on each
/// evaluation, so a directory edit applies from the next one.
/// </summary>
public sealed class EfIntermediaryRegistry(IDbContextFactory<PegasusDbContext> contextFactory)
    : IIntermediaryRegistry
{
    private const string ClaimSourceRole = "claim_source";

    public async Task<IntermediaryDomains> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var contacts = await context.Organizations.AsNoTracking()
            .Where(organization => organization.Role == ClaimSourceRole
                && organization.Active
                && !string.IsNullOrWhiteSpace(organization.Email))
            .SelectMany(
                organization => organization.PrincipalLinks.Where(link => link.Principal.IsActive),
                (organization, link) => new { organization.Email, link.Principal.Code })
            .ToArrayAsync(cancellationToken);
        return new(contacts.Select(contact => (contact.Email, contact.Code)));
    }
}
