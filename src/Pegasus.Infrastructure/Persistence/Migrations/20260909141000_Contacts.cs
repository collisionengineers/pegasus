using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Replaces the separate ClaimSources directory with roles on the canonical
/// Organization identity. A same-name conflict is deliberately a migration
/// stop: choosing one directory record by name would be an unapproved merge.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260909141000_Contacts")]
public partial class Contacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM dbo.ClaimSources AS source
                INNER JOIN dbo.Organizations AS organization
                    ON organization.NormalizedName = UPPER(LTRIM(RTRIM(source.Name))))
                THROW 51000, N'Claim Source and Organization names conflict. Resolve the directory identities before applying Contacts.', 1;
            IF EXISTS (
                SELECT 1
                FROM dbo.ClaimSources AS source
                INNER JOIN dbo.Organizations AS organization ON organization.Id = source.Id)
                THROW 51000, N'Claim Source and Organization identifiers conflict. Resolve the directory identities before applying Contacts.', 1;
            IF EXISTS (SELECT 1 FROM dbo.ClaimSources WHERE DATALENGTH(Name) > 600)
                THROW 51001, N'Claim Source name exceeds the 300-character Contact limit. Correct it before applying Contacts.', 1;
            IF EXISTS (SELECT 1 FROM dbo.ClaimSources WHERE DATALENGTH(Contact) > 400)
                THROW 51002, N'Claim Source contact exceeds the 200-character Contact limit. Correct it before applying Contacts.', 1;
            IF EXISTS (SELECT 1 FROM dbo.ClaimSources WHERE DATALENGTH(Email) > 640)
                THROW 51003, N'Claim Source email exceeds the 320-character Contact limit. Correct it before applying Contacts.', 1;
            IF EXISTS (SELECT 1 FROM dbo.ClaimSources WHERE DATALENGTH(Telephone) > 100)
                THROW 51004, N'Claim Source telephone exceeds the 50-character Contact limit. Correct it before applying Contacts.', 1;
            IF EXISTS (SELECT 1 FROM dbo.ClaimSources WHERE DATALENGTH(LTRIM(RTRIM(Notes))) > 8000)
                THROW 51005, N'Claim Source notes exceed the 4000-character guidance limit. Correct them before applying Contacts.', 1;
            """);

        migrationBuilder.AddColumn<string>(name: "ContactPerson", table: "Organizations", type: "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Email", table: "Organizations", type: "nvarchar(320)", maxLength: 320, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Telephone", table: "Organizations", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Address", table: "Organizations", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "GuidanceTemplate", table: "Organizations", type: "nvarchar(4000)", maxLength: 4000, nullable: true);
        migrationBuilder.AddColumn<long>(name: "GuidanceTemplateVersion", table: "Organizations", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<bool>(name: "Active", table: "Organizations", type: "bit", nullable: false, defaultValue: true);

        migrationBuilder.CreateTable(
            name: "ContactRoles",
            columns: table => new { OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false) },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContactRoles", item => new { item.OrganizationId, item.Role });
                table.CheckConstraint("CK_ContactRoles_Role", "[Role] IN ('principal', 'claim_source', 'repairer', 'storage', 'third_party_engineer')");
                table.ForeignKey("FK_ContactRoles_Organizations_OrganizationId", item => item.OrganizationId, principalTable: "Organizations", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ContactPrincipalLinks",
            columns: table => new { PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false) },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContactPrincipalLinks", item => new { item.PrincipalId, item.OrganizationId, item.Role });
                table.CheckConstraint("CK_ContactPrincipalLinks_Role", "[Role] IN ('claim_source', 'repairer', 'storage', 'third_party_engineer')");
                table.ForeignKey("FK_ContactPrincipalLinks_Organizations_OrganizationId", item => item.OrganizationId, principalTable: "Organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ContactPrincipalLinks_Principals_PrincipalId", item => item.PrincipalId, principalTable: "Principals", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_ContactPrincipalLinks_OrganizationId_Role", table: "ContactPrincipalLinks", columns: new[] { "OrganizationId", "Role" });
        migrationBuilder.Sql(
            """
            INSERT INTO dbo.ContactRoles (OrganizationId, Role)
            SELECT DISTINCT OrganizationId, N'principal'
            FROM dbo.Principals;

            INSERT INTO dbo.Organizations (Id, Name, ContactPerson, Email, Telephone, Address, GuidanceTemplate, GuidanceTemplateVersion, Active, Version)
            SELECT Id, Name, Contact, Email, Telephone, NULL,
                   NULLIF(LTRIM(RTRIM(Notes)), N''),
                   CASE WHEN NULLIF(LTRIM(RTRIM(Notes)), N'') IS NULL THEN 0 ELSE 1 END,
                   Active, Version
            FROM dbo.ClaimSources;

            INSERT INTO dbo.ContactRoles (OrganizationId, Role)
            SELECT Id, N'claim_source'
            FROM dbo.ClaimSources;
            """);
        migrationBuilder.DropTable(name: "ClaimSources");
        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[ContactRoles] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[ContactPrincipalLinks] TO [pegasus_web_runtime_role];
                DENY UPDATE ON OBJECT::[dbo].[ContactRoles] TO [pegasus_web_runtime_role];
                DENY UPDATE ON OBJECT::[dbo].[ContactPrincipalLinks] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                DENY DELETE ON OBJECT::[dbo].[ContactRoles] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[ContactPrincipalLinks] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Contacts replaces the unreleased Claim Source directory and cannot be downgraded safely.");
}
