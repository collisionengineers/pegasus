using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(PegasusDbContext))]
[Migration("20260909140000_EditScopeOwnership")]
public partial class EditScopeOwnership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EditScopes",
            columns: table => new
            {
                ScopeKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HolderKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Holder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                TokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                Generation = table.Column<long>(type: "bigint", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EditScopes", item => new { item.ScopeKind, item.RecordId });
                table.CheckConstraint("CK_EditScopes_ExpectedVersion", "[ExpectedVersion] >= 0");
                table.CheckConstraint("CK_EditScopes_Generation", "[Generation] > 0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_EditScopes_ExpiresAtUtc",
            table: "EditScopes",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_EditScopes_HolderKind_Holder",
            table: "EditScopes",
            columns: new[] { "HolderKind", "Holder" });
        migrationBuilder.Sql("""
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[EditScopes] TO [pegasus_web_runtime_role];
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "EditScopes");
}
