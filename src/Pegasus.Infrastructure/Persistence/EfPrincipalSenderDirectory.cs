using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The active Principal Contacts that hold an e-mail, read on each call so a
/// directory edit applies from the next one. The standing Private customer
/// record is not a sender's Principal: a one-off is created on its own route
/// (R581).
/// </summary>
public sealed class EfPrincipalSenderDirectory(IDbContextFactory<PegasusDbContext> contextFactory)
    : IPrincipalSenderDirectory
{
    public async Task<PrincipalSenderContacts> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var contacts = await context.Principals.AsNoTracking()
            .Where(principal => principal.IsActive
                && principal.ReferenceScheme != PrincipalReferenceSchemes.Registration
                && principal.Organization.Active
                && !string.IsNullOrWhiteSpace(principal.Organization.Email))
            .Select(principal => new { principal.Organization.Email, principal.Code })
            .ToArrayAsync(cancellationToken);
        return new(contacts.Select(contact => (contact.Email, contact.Code)));
    }
}
