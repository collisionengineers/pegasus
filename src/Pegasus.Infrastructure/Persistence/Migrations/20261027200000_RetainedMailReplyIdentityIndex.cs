using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Reply-header association (FRD-22 v2) looks an inbound message's
/// In-Reply-To and References identities up across every approved mailbox,
/// a lookup the per-mailbox Message-ID index cannot seek, so each inbound
/// message scanned it inside its association transaction. The index answers
/// that lookup with a seek. Additive: one non-unique filtered index, no data
/// change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261027200000_RetainedMailReplyIdentityIndex")]
public partial class RetainedMailReplyIdentityIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_RetainedMailboxMessages_FolderScope_CanonicalInternetMessageIdentity",
            table: "RetainedMailboxMessages",
            columns: ["FolderScope", "CanonicalInternetMessageIdentity"],
            filter: "[CanonicalInternetMessageIdentity] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RetainedMailboxMessages_FolderScope_CanonicalInternetMessageIdentity",
            table: "RetainedMailboxMessages");
    }
}
