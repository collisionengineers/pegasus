using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

internal static class TestMailboxId
{
    public static Guid From(string value)
    {
        if (string.Equals(value, "instructions", StringComparison.Ordinal))
        {
            return Guid.Parse("49f47eb9-c5b0-464f-b8f0-8c90ba061728");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }

    public static async Task<Guid> EnsureApprovedAsync(
        PegasusDbContext context,
        string graphId,
        string address,
        DateTimeOffset activatedAtUtc)
    {
        var id = From(graphId);
        var mailbox = await context.ApprovedMailboxes.SingleOrDefaultAsync(item => item.Id == id);
        if (mailbox is null)
        {
            context.ApprovedMailboxes.Add(new()
            {
                Id = id,
                Address = address,
                AllowInboundIntake = true,
                State = "Approved",
                MailboxIdentity = graphId,
                InboxFolderIdentity = "inbox",
                ActivatedAtUtc = activatedAtUtc,
                Version = 1
            });
        }
        else
        {
            mailbox.MailboxIdentity = graphId;
            mailbox.InboxFolderIdentity = "inbox";
            mailbox.ActivatedAtUtc = activatedAtUtc;
        }
        return id;
    }
}
