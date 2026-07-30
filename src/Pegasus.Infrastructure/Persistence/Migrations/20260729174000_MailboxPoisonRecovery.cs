using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729174000_MailboxPoisonRecovery")]
public partial class MailboxPoisonRecovery : Migration
{
    private static readonly string[] MailboxOccurrenceColumns = ["MailboxId", "OccurrenceKey"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApprovedInboxPoisonMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                MailboxId = table.Column<string>(maxLength: 100, nullable: false),
                OccurrenceKey = table.Column<string>(maxLength: 64, nullable: false),
                ImmutableMessageId = table.Column<string>(nullable: false),
                FileName = table.Column<string>(nullable: false),
                SourceLength = table.Column<long>(nullable: true),
                SourceHash = table.Column<string>(maxLength: 64, nullable: true),
                OriginalSourceHash = table.Column<string>(maxLength: 64, nullable: true),
                EvidenceMarker = table.Column<string>(maxLength: 50, nullable: true),
                StorageKey = table.Column<string>(maxLength: 200, nullable: true),
                ReceivedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                FailureCode = table.Column<string>(maxLength: 100, nullable: false),
                CursorAfterMessage = table.Column<string>(nullable: false),
                QuarantinedAtUtc = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovedInboxPoisonMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovedInboxPoisonMessages_ApprovedInboxPollStates_MailboxId",
                    column: x => x.MailboxId,
                    principalTable: "ApprovedInboxPollStates",
                    principalColumn: "MailboxId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedInboxPoisonMessages_MailboxId_OccurrenceKey",
            table: "ApprovedInboxPoisonMessages",
            columns: MailboxOccurrenceColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedInboxPoisonMessages_QuarantinedAtUtc",
            table: "ApprovedInboxPoisonMessages",
            column: "QuarantinedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ApprovedInboxPoisonMessages");
    }
}
