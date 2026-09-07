using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

public partial class RemoveAutomaticEvaSubmission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "EvaAutomaticSubmission",
            table: "Principals");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "EvaAutomaticSubmission",
            table: "Principals",
            type: "bit",
            nullable: false,
            defaultValue: false);
}
