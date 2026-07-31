using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729180000_AdministrationPolicies")]
public sealed class AdministrationPolicies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApprovedMailboxes",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Address = table.Column<string>(maxLength: 320, nullable: false),
                AllowInboundIntake = table.Column<bool>(nullable: false),
                AllowSentEvidence = table.Column<bool>(nullable: false),
                State = table.Column<string>(maxLength: 40, nullable: false),
                Version = table.Column<int>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovedMailboxes", item => item.Id);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowConfigurations",
            columns: table => new
            {
                Id = table.Column<string>(maxLength: 100, nullable: false),
                RequireCompleteInstructionsBeforeEngineerAssignment = table.Column<bool>(nullable: false),
                RequireCompleteImagesBeforeEngineerAssignment = table.Column<bool>(nullable: false),
                RequireStaffInstructionReviewBeforeEngineerAssignment = table.Column<bool>(nullable: false),
                RequireStaffImageReviewBeforeEngineerAssignment = table.Column<bool>(nullable: false),
                Version = table.Column<int>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowConfigurations", item => item.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedMailboxes_Address",
            table: "ApprovedMailboxes",
            column: "Address",
            unique: true);

        migrationBuilder.Sql(
            """
            INSERT INTO [ApprovedMailboxes]
                ([Id], [Address], [AllowInboundIntake], [AllowSentEvidence], [State], [Version])
            VALUES
                ('49f47eb9-c5b0-464f-b8f0-8c90ba061728',
                 N'instructions@collisionengineers.co.uk',
                 CAST(1 AS bit),
                 CAST(0 AS bit),
                 N'Approved',
                 1);
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO [WorkflowConfigurations]
                ([Id],
                 [RequireCompleteInstructionsBeforeEngineerAssignment],
                 [RequireCompleteImagesBeforeEngineerAssignment],
                 [RequireStaffInstructionReviewBeforeEngineerAssignment],
                 [RequireStaffImageReviewBeforeEngineerAssignment],
                 [Version])
            VALUES
                (N'case-workflow',
                 CAST(1 AS bit),
                 CAST(1 AS bit),
                 CAST(1 AS bit),
                 CAST(1 AS bit),
                 1);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ApprovedMailboxes");
        migrationBuilder.DropTable(name: "WorkflowConfigurations");
    }
}
