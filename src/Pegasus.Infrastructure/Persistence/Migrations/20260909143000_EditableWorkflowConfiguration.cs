using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260909143000_EditableWorkflowConfiguration")]
public sealed class EditableWorkflowConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("RequireInstructions", "WorkflowConfigurations", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>("RequireImages", "WorkflowConfigurations", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<int>("ChaseIntervalDays", "WorkflowConfigurations", nullable: false, defaultValue: 7);
        migrationBuilder.AddCheckConstraint("CK_WorkflowConfigurations_ChaseIntervalDays", "WorkflowConfigurations", "[ChaseIntervalDays] BETWEEN 1 AND 365");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_WorkflowConfigurations_ChaseIntervalDays", "WorkflowConfigurations");
        migrationBuilder.DropColumn("ChaseIntervalDays", "WorkflowConfigurations");
        migrationBuilder.DropColumn("RequireImages", "WorkflowConfigurations");
        migrationBuilder.DropColumn("RequireInstructions", "WorkflowConfigurations");
    }
}
