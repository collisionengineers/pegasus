using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Staff can leave a manual upload's matched Cases undecided (operator, 11
/// October 2026): the group records who and when, and its images then register
/// as an Image-initiated Case instead of waiting. Additive; no registered
/// record changes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261011140000_ImageMatchDecline")]
public partial class ImageMatchDecline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ImageMatchDeclinedAtUtc",
            table: "IntakeSubmissionGroups",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ImageMatchDeclinedByActorSubjectId",
            table: "IntakeSubmissionGroups",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "ImageMatchDecline is forward-only: restore from an approved backup instead.");
}
