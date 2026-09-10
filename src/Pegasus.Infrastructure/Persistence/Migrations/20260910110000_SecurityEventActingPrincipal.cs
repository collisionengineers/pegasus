using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A security event's <c>SubjectId</c> is what the event is about — the account
/// disabled, the client refused — so the Action logs view had nothing to name
/// the operator who acted, and every Security-area row read as an unknown user.
/// These two additive columns carry the acting principal alongside the subject.
/// Rows written before them stay null and are labelled by what they are rather
/// than backfilled with a guess.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260910110000_SecurityEventActingPrincipal")]
public partial class SecurityEventActingPrincipal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActorKind",
            table: "SecurityEvents",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActorSubjectId",
            table: "SecurityEvents",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ActorSubjectId", table: "SecurityEvents");
        migrationBuilder.DropColumn(name: "ActorKind", table: "SecurityEvents");
    }
}
