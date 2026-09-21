using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The firm's own unroadworthy reason wordings (v28 P15, ruled 20
    /// September 2026). One row per Principal and wording; Web reads and
    /// appends, and nothing deletes one. Additive.
    /// </summary>
    public partial class UnroadworthyReasonBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnroadworthyReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnroadworthyReasons", x => x.Id);
                    table.CheckConstraint("CK_UnroadworthyReasons_Text", "[Text] <> ''");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnroadworthyReasons_PrincipalCode_Text",
                table: "UnroadworthyReasons",
                columns: new[] { "PrincipalCode", "Text" },
                unique: true);

            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[UnroadworthyReasons] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[UnroadworthyReasons] TO [pegasus_web_runtime_role];
                END;
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                BEGIN
                    DENY DELETE ON OBJECT::[dbo].[UnroadworthyReasons] TO [pegasus_worker_runtime_role];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnroadworthyReasons");
        }
    }
}
