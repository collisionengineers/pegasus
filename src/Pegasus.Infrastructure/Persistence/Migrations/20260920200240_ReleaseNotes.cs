using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Release notes (ADR-0054): a note an Administrator writes in the application
/// and publishes by their press, stamped with the build it describes, and each
/// person's acknowledgement of the newest published note (FRD-12 What's new).
///
/// Web alone writes both tables and is denied DELETE; a published note never
/// changes and an acknowledgement is never withdrawn. The Worker has no part
/// in either and is denied DELETE too.
/// </summary>
public partial class ReleaseNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.CreateTable(
            name: "ReleaseNotes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                SourceSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                PublishedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReleaseNotes", x => x.Id);
                table.CheckConstraint("CK_ReleaseNotes_Status", "[Status] IN ('Draft', 'Published')");
            });

        migrationBuilder.CreateTable(
            name: "ReleaseNoteAcknowledgements",
            columns: table => new
            {
                StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReleaseNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReleaseNoteAcknowledgements", x => new { x.StaffId, x.ReleaseNoteId });
                table.ForeignKey(
                    name: "FK_ReleaseNoteAcknowledgements_ReleaseNotes_ReleaseNoteId",
                    column: x => x.ReleaseNoteId,
                    principalTable: "ReleaseNotes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReleaseNoteAcknowledgements_ReleaseNoteId",
            table: "ReleaseNoteAcknowledgements",
            column: "ReleaseNoteId");

        migrationBuilder.CreateIndex(
            name: "IX_ReleaseNotes_Status_PublishedAtUtc",
            table: "ReleaseNotes",
            columns: new[] { "Status", "PublishedAtUtc" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_ReleaseNotes_UpdatedAtUtc",
            table: "ReleaseNotes",
            column: "UpdatedAtUtc");

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ReleaseNotes] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[ReleaseNotes] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[ReleaseNoteAcknowledgements] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[ReleaseNoteAcknowledgements] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                DENY DELETE ON OBJECT::[dbo].[ReleaseNotes] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[ReleaseNoteAcknowledgements] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReleaseNoteAcknowledgements");
        migrationBuilder.DropTable(name: "ReleaseNotes");
    }
}
