using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The denials every runtime table carries, which
/// <c>20260826151807_ApprovedMailboxStableIdentityAndSubscriptions</c> left out: the
/// Web resolves subscription callbacks read-only and the Worker never deletes a
/// subscription. Production has held these four rows since August, so here they
/// change nothing; a fresh database gains them (R483, 28 October 2026).
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261028160000_DenyApprovedMailboxSubscriptionsWrites")]
public partial class DenyApprovedMailboxSubscriptionsWrites : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer") return;
        migrationBuilder.Sql("DENY INSERT, UPDATE, DELETE ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] TO [pegasus_web_runtime_role];");
        migrationBuilder.Sql("DENY DELETE ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] TO [pegasus_worker_runtime_role];");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer") return;
        migrationBuilder.Sql("REVOKE INSERT, UPDATE, DELETE ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] FROM [pegasus_web_runtime_role];");
        migrationBuilder.Sql("REVOKE DELETE ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] FROM [pegasus_worker_runtime_role];");
    }
}
