using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729190000_CaseEditLeaseReplay")]
public sealed class CaseEditLeaseReplay : Migration
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        RequireSqlServer();
        migrationBuilder.AddColumn<string>(
            name: "ResultJson",
            table: "CaseWorkflowEvents",
            type: "nvarchar(max)",
            nullable: true);


        migrationBuilder.AddColumn<string>(
            name: "EditLeaseRequestHash",
            table: "CaseWorkflows",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "EditLeaseToken",
            table: "CaseWorkflows",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "CaseEditLeaseOperations",
            columns: table => new
            {
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OperationKey = table.Column<string>(
                    type: "nvarchar(100)",
                    maxLength: 100,
                    nullable: false),
                OperationKind = table.Column<string>(
                    type: "nvarchar(40)",
                    maxLength: 40,
                    nullable: false),
                RequestHash = table.Column<string>(
                    type: "nchar(64)",
                    fixedLength: true,
                    maxLength: 64,
                    nullable: false),
                ActorKind = table.Column<string>(
                    type: "nvarchar(40)",
                    maxLength: 40,
                    nullable: false),
                ActorSubjectId = table.Column<string>(
                    type: "nvarchar(200)",
                    maxLength: 200,
                    nullable: false),
                ActorRolesJson = table.Column<string>(
                    type: "nvarchar(500)",
                    maxLength: 500,
                    nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false),
                ResultVersion = table.Column<long>(
                    type: "bigint",
                    nullable: false),
                ResultExpiresAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: true),
                ResultTokenHash = table.Column<string>(
                    type: "nchar(64)",
                    fixedLength: true,
                    maxLength: 64,
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_CaseEditLeaseOperations",
                    item => new { item.CaseId, item.OperationKey });
                table.CheckConstraint(
                    "CK_CaseEditLeaseOperations_ResultVersion",
                    "[ResultVersion] >= 0");
                table.ForeignKey(
                    name: "FK_CaseEditLeaseOperations_CaseWorkflows_CaseId",
                    column: item => item.CaseId,
                    principalTable: "CaseWorkflows",
                    principalColumn: "CaseId",
                    onDelete: ReferentialAction.Restrict);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RequireSqlServer();
        migrationBuilder.DropColumn(
            name: "ResultJson",
            table: "CaseWorkflowEvents");


        migrationBuilder.DropTable(name: "CaseEditLeaseOperations");

        migrationBuilder.DropColumn(
            name: "EditLeaseRequestHash",
            table: "CaseWorkflows");
        migrationBuilder.DropColumn(
            name: "EditLeaseToken",
            table: "CaseWorkflows");
    }

    private void RequireSqlServer()
    {
        if (!string.Equals(ActiveProvider, SqlServerProvider, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }
    }
}
