using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729184000_DueChaserSweep")]
public sealed class DueChaserSweep : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // no-runtime-grant: CaseDueChasers - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        migrationBuilder.DropIndex(
            name: "IX_CaseDueWork_State_NextChaseAtUtc",
            table: "CaseDueWork");
        migrationBuilder.AddColumn<long>(
            name: "NextChaseAtUtcTicks",
            table: "CaseDueWork",
            nullable: true);
        migrationBuilder.Sql(
            """
            UPDATE [CaseDueWork]
            SET [NextChaseAtUtcTicks] =
                DATEDIFF_BIG(
                    SECOND,
                    CAST('0001-01-01T00:00:00+00:00' AS datetimeoffset),
                    SWITCHOFFSET([NextChaseAtUtc], '+00:00')) * 10000000
                + DATEPART(NANOSECOND, SWITCHOFFSET([NextChaseAtUtc], '+00:00')) / 100
            WHERE [NextChaseAtUtc] IS NOT NULL;
            """);
        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseDueWork_NextChaseOrdering",
            table: "CaseDueWork",
            sql: "([NextChaseAtUtc] IS NULL AND [NextChaseAtUtcTicks] IS NULL) OR ([NextChaseAtUtc] IS NOT NULL AND [NextChaseAtUtcTicks] IS NOT NULL)");
        migrationBuilder.CreateIndex(
            name: "IX_CaseDueWork_State_NextChaseAtUtcTicks",
            table: "CaseDueWork",
            columns: new[] { "State", "NextChaseAtUtcTicks" });

        migrationBuilder.CreateTable(
            name: "CaseDueChasers",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                ScheduledAtUtc = table.Column<DateTimeOffset>(nullable: false),
                GeneratedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                NextChaseAtUtc = table.Column<DateTimeOffset>(nullable: false),
                CopyableText = table.Column<string>(maxLength: 2000, nullable: false),
                RequestLinkReference = table.Column<Guid>(nullable: true),
                RequestLinkPurpose = table.Column<string>(maxLength: 100, nullable: true),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                RequestHash = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                BeforeDueWorkVersion = table.Column<long>(nullable: false),
                AfterDueWorkVersion = table.Column<long>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseDueChasers", item => item.Id);
                table.CheckConstraint(
                    "CK_CaseDueChasers_Versions",
                    "[BeforeDueWorkVersion] >= 0 AND [AfterDueWorkVersion] = [BeforeDueWorkVersion] + 1");
                table.CheckConstraint(
                    "CK_CaseDueChasers_RequestLink",
                    "([RequestLinkReference] IS NULL AND [RequestLinkPurpose] IS NULL) OR ([RequestLinkReference] IS NOT NULL AND [RequestLinkPurpose] = 'missing-material-upload')");
                table.ForeignKey(
                    name: "FK_CaseDueChasers_CaseDueWork_CaseId",
                    column: item => item.CaseId,
                    principalTable: "CaseDueWork",
                    principalColumn: "CaseId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CaseDueChasers_RequestUploadLinks_RequestLinkReference",
                    column: item => item.RequestLinkReference,
                    principalTable: "RequestUploadLinks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CaseDueChasers_CaseId_GeneratedAtUtc",
            table: "CaseDueChasers",
            columns: new[] { "CaseId", "GeneratedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_CaseDueChasers_CaseId_ScheduledAtUtc",
            table: "CaseDueChasers",
            columns: new[] { "CaseId", "ScheduledAtUtc" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_CaseDueChasers_OperationKey",
            table: "CaseDueChasers",
            column: "OperationKey",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_CaseDueChasers_RequestLinkReference",
            table: "CaseDueChasers",
            column: "RequestLinkReference");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CaseDueChasers");
        migrationBuilder.DropIndex(
            name: "IX_CaseDueWork_State_NextChaseAtUtcTicks",
            table: "CaseDueWork");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseDueWork_NextChaseOrdering",
            table: "CaseDueWork");
        migrationBuilder.DropColumn(
            name: "NextChaseAtUtcTicks",
            table: "CaseDueWork");
        migrationBuilder.CreateIndex(
            name: "IX_CaseDueWork_State_NextChaseAtUtc",
            table: "CaseDueWork",
            columns: new[] { "State", "NextChaseAtUtc" });
    }
}
