using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImageIntakeGroupExpectedMemberCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaulted to 1 rather than 0: this table has no deployed rows
            // (it was introduced by the still-unreleased GroupedIntakeSubmission
            // migration), but 0 would violate the check constraint added right
            // below if any ever existed.
            migrationBuilder.AddColumn<int>(
                name: "ExpectedMemberCount",
                table: "IntakeSubmissionGroups",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_IntakeSubmissionGroups_ExpectedMemberCount",
                table: "IntakeSubmissionGroups",
                sql: "[ExpectedMemberCount] >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_IntakeSubmissionGroups_ExpectedMemberCount",
                table: "IntakeSubmissionGroups");

            migrationBuilder.DropColumn(
                name: "ExpectedMemberCount",
                table: "IntakeSubmissionGroups");
        }
    }
}
