using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729170000_MailboxRouteAudit")]
public partial class MailboxRouteAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // no-runtime-grant: ApprovedInboxPollStates - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: IntakeMailRouteDecisions - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        migrationBuilder.CreateTable(
            name: "ApprovedInboxPollStates",
            columns: table => new
            {
                MailboxId = table.Column<string>(maxLength: 100, nullable: false),
                MailboxAddress = table.Column<string>(maxLength: 320, nullable: false),
                Cursor = table.Column<string>(nullable: true),
                DueAtUtc = table.Column<DateTimeOffset>(nullable: false),
                LeaseToken = table.Column<string>(maxLength: 64, nullable: true),
                LeaseExpiresAtUtc = table.Column<DateTimeOffset>(nullable: true),
                LastCompletedAtUtc = table.Column<DateTimeOffset>(nullable: true),
                LastFailureCode = table.Column<string>(maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovedInboxPollStates", x => x.MailboxId);
            });

        migrationBuilder.CreateTable(
            name: "IntakeMailRouteDecisions",
            columns: table => new
            {
                IntakeReceiptId = table.Column<Guid>(nullable: false),
                Disposition = table.Column<string>(maxLength: 40, nullable: false),
                RouteOwnerCode = table.Column<string>(maxLength: 100, nullable: true),
                RouteKind = table.Column<string>(maxLength: 40, nullable: true),
                WorkProviderCode = table.Column<string>(maxLength: 100, nullable: true),
                PredicatesJson = table.Column<string>(nullable: false),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                PolicyKey = table.Column<string>(maxLength: 100, nullable: false),
                PolicyVersion = table.Column<int>(nullable: false),
                TransportIdentitiesJson = table.Column<string>(nullable: false),
                OriginalIdentitiesJson = table.Column<string>(nullable: false),
                EffectiveSenderAddress = table.Column<string>(maxLength: 320, nullable: true),
                EffectiveSenderSourceLabel = table.Column<string>(maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntakeMailRouteDecisions", x => x.IntakeReceiptId);
                table.ForeignKey(
                    "FK_IntakeMailRouteDecisions_IntakeReceipts_IntakeReceiptId",
                    x => x.IntakeReceiptId,
                    "IntakeReceipts",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedInboxPollStates_DueAtUtc",
            table: "ApprovedInboxPollStates",
            column: "DueAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedInboxPollStates_MailboxAddress",
            table: "ApprovedInboxPollStates",
            column: "MailboxAddress",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("IntakeMailRouteDecisions");
        migrationBuilder.DropTable("ApprovedInboxPollStates");
    }
}
