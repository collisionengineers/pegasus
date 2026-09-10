using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260910100000_ManualUploadGroupDiscard")]
public partial class ManualUploadGroupDiscard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DiscardedAtUtc",
            table: "IntakeSubmissionGroups",
            type: "datetimeoffset",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DiscardedByActorKind",
            table: "IntakeSubmissionGroups",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DiscardedByActorSubjectId",
            table: "IntakeSubmissionGroups",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DiscardOperationKey",
            table: "IntakeSubmissionGroups",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DiscardRequestFingerprint",
            table: "IntakeSubmissionGroups",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "Version",
            table: "IntakeSubmissionGroups",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "IntakeSubmissionGroupHistory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                BeforeVersion = table.Column<long>(type: "bigint", nullable: false),
                AfterVersion = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntakeSubmissionGroupHistory", x => x.Id);
                table.ForeignKey(
                    name: "FK_IntakeSubmissionGroupHistory_IntakeSubmissionGroups_GroupId",
                    column: x => x.GroupId,
                    principalTable: "IntakeSubmissionGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IntakeSubmissionGroupHistory_GroupId_OccurredAtUtc",
            table: "IntakeSubmissionGroupHistory",
            columns: new[] { "GroupId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_IntakeSubmissionGroupHistory_OperationKey",
            table: "IntakeSubmissionGroupHistory",
            column: "OperationKey",
            unique: true);

        if (string.Equals(ActiveProvider, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
        {
            migrationBuilder.Sql(
                "GRANT UPDATE ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_web_runtime_role];");
            migrationBuilder.Sql(
                "GRANT INSERT ON OBJECT::[dbo].[IntakeSubmissionGroupHistory] TO [pegasus_web_runtime_role];");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Manual upload discard is an irreversible retained-history decision.");
}
