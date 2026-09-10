using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Replaces the one-way third-party vehicle flag with image tags: a shared
/// vocabulary (<c>ImageTags</c>) and a join carrying who applied which tag to
/// which occurrence (<c>DocumentOccurrenceTags</c>).
///
/// Every recorded confirmation becomes a Third party tag row keeping its
/// original moment, actor and operation key, so the EVA exclusion the flag
/// carried survives the change with its provenance. The three flag columns and
/// their index then go: nothing reads them afterwards, and leaving a dead
/// second answer to "is this the other vehicle?" is what the tags replace.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260910120000_CaseImageTags")]
public partial class CaseImageTags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.CreateTable(
            name: "ImageTags",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                NormalizedName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Colour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreateOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ImageTags", item => item.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ImageTags_NormalizedName",
            table: "ImageTags",
            column: "NormalizedName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ImageTags_CreateOperationKey",
            table: "ImageTags",
            column: "CreateOperationKey",
            unique: true);

        migrationBuilder.CreateTable(
            name: "DocumentOccurrenceTags",
            columns: table => new
            {
                OccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AppliedByKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                AppliedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocumentOccurrenceTags", item => new { item.OccurrenceId, item.TagId });
                table.ForeignKey(
                    "FK_DocumentOccurrenceTags_DocumentOccurrences_OccurrenceId",
                    item => item.OccurrenceId,
                    principalTable: "DocumentOccurrences",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_DocumentOccurrenceTags_ImageTags_TagId",
                    item => item.TagId,
                    principalTable: "ImageTags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentOccurrenceTags_TagId",
            table: "DocumentOccurrenceTags",
            column: "TagId");

        // The built-in vocabulary. Fixed identifiers, so the conversion below
        // and Core's EVA exclusion name the same row without a lookup by name.
        migrationBuilder.Sql(
            """
            DECLARE @seededAt datetimeoffset = SYSDATETIMEOFFSET();
            INSERT dbo.ImageTags (Id, Name, NormalizedName, Colour, IsBuiltIn, CreatedAtUtc, CreatedBy, CreateOperationKey, Version)
            VALUES
                (N'00000000-0000-4000-8000-0000000017a1', N'Overview', N'OVERVIEW', N'Blue', 1, @seededAt, N'system', N'image-tag-seed:overview', 1),
                (N'00000000-0000-4000-8000-0000000017a2', N'Close-up', N'CLOSE-UP', N'Green', 1, @seededAt, N'system', N'image-tag-seed:close-up', 1),
                (N'00000000-0000-4000-8000-0000000017a3', N'Third party', N'THIRD PARTY', N'Amber', 1, @seededAt, N'system', N'image-tag-seed:third-party', 1),
                (N'00000000-0000-4000-8000-0000000017a4', N'Reflection', N'REFLECTION', N'Navy', 1, @seededAt, N'system', N'image-tag-seed:reflection', 1);
            """);

        // Every recorded confirmation becomes a Third party tag. The actor is
        // read back from the audited action the confirmation wrote, so the row
        // names the person who decided it rather than this migration.
        migrationBuilder.Sql(
            """
            INSERT dbo.DocumentOccurrenceTags (OccurrenceId, TagId, AppliedByKind, AppliedBySubjectId, AppliedAtUtc, OperationKey)
            SELECT
                occurrence.Id,
                N'00000000-0000-4000-8000-0000000017a3',
                ISNULL(history.ActorKind, N'Staff'),
                ISNULL(history.ActorSubjectId, N'unknown'),
                occurrence.ThirdPartyVehicleConfirmedAtUtc,
                LEFT(ISNULL(
                        occurrence.ThirdPartyVehicleConfirmationOperationKey,
                        CONCAT(N'third-party-vehicle:', CONVERT(nvarchar(36), occurrence.Id))), 100)
            FROM dbo.DocumentOccurrences AS occurrence
            OUTER APPLY (
                SELECT TOP (1) entry.ActorKind, entry.ActorSubjectId
                FROM dbo.ActionHistory AS entry
                WHERE entry.CorrelationId = occurrence.ThirdPartyVehicleConfirmationOperationKey
                  AND entry.EventKind = N'third_party_vehicle_evidence_confirmed'
                ORDER BY entry.OccurredAtUtc DESC) AS history
            WHERE occurrence.ThirdPartyVehicleConfirmedAtUtc IS NOT NULL;
            """);

        migrationBuilder.DropIndex(
            name: "IX_DocumentOccurrences_CaseId_ThirdPartyVehicleConfirmedAtUtc",
            table: "DocumentOccurrences");

        migrationBuilder.DropColumn(name: "ThirdPartyVehicleConfirmedAtUtc", table: "DocumentOccurrences");
        migrationBuilder.DropColumn(name: "ThirdPartyVehicleConfirmationReason", table: "DocumentOccurrences");
        migrationBuilder.DropColumn(name: "ThirdPartyVehicleConfirmationOperationKey", table: "DocumentOccurrences");

        // The runtime roles: Web reads the vocabulary and adds to it, and puts
        // tags on and takes them off; the Worker only reads, because the EVA
        // export asks which images wear Third party.
        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageTags] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_web_runtime_role];
                DENY UPDATE ON OBJECT::[dbo].[ImageTags] TO [pegasus_web_runtime_role];
                DENY UPDATE ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[ImageTags] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT ON OBJECT::[dbo].[ImageTags] TO [pegasus_worker_runtime_role];
                GRANT SELECT ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_worker_runtime_role];
                DENY UPDATE ON OBJECT::[dbo].[ImageTags] TO [pegasus_worker_runtime_role];
                DENY UPDATE ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[ImageTags] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Case image tags replace the unreleased third-party vehicle flag and cannot be downgraded safely.");
}
