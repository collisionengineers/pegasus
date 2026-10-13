using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// One type per contact (operator ruling, 13 October 2026). The type moves from the
/// <c>ContactRoles</c> rows onto <c>Organizations.Role</c>; a contact that also held
/// Principal keeps Principal only. A type a contact loses takes its Case links
/// (the id and version fields; the Case keeps its own name copy) and its Principal
/// links with it. The RJS rules that named ACSP and CS as Claim sources match the
/// v5 SOP's own text for them instead. <c>ContactRoles</c> and the unused legacy
/// <c>OrganizationRoles</c> are dropped, and a Principal link no longer carries a
/// role. A name is now unique within one type rather than across the directory.
/// Destructive and forward-only: it refuses to run while any contact holds
/// two non-Principal types or no type, or while report sending rules still name a
/// contact by a type it loses, so the operator chooses first.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261013090000_ContactSingleType")]
public partial class ContactSingleType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Role", table: "Organizations", type: "nvarchar(40)", maxLength: 40, nullable: true);

        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM dbo.ContactRoles r
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Principals p WHERE p.OrganizationId = r.OrganizationId)
                GROUP BY r.OrganizationId HAVING COUNT(*) > 1 AND SUM(CASE WHEN r.Role = N'principal' THEN 1 ELSE 0 END) = 0)
                THROW 50000, N'A contact holds more than one non-Principal type. Choose one before this migration.', 1;
            """);
        migrationBuilder.Sql("""
            UPDATE o SET Role = CASE
                WHEN EXISTS (SELECT 1 FROM dbo.Principals p WHERE p.OrganizationId = o.Id)
                    OR EXISTS (SELECT 1 FROM dbo.ContactRoles r WHERE r.OrganizationId = o.Id AND r.Role = N'principal')
                    THEN N'principal'
                ELSE (SELECT MIN(r.Role) FROM dbo.ContactRoles r WHERE r.OrganizationId = o.Id) END
            FROM dbo.Organizations o;
            IF EXISTS (SELECT 1 FROM dbo.Organizations WHERE Role IS NULL)
                THROW 50000, N'A contact has no type. Give it one before this migration.', 1;
            """);

        // The types each contact loses, then the Case and Principal links they held.
        migrationBuilder.Sql("""
            CREATE TABLE #Lost (OrganizationId uniqueidentifier NOT NULL, Role nvarchar(40) NOT NULL);
            INSERT #Lost (OrganizationId, Role)
            SELECT r.OrganizationId, r.Role FROM dbo.ContactRoles r
            JOIN dbo.Organizations o ON o.Id = r.OrganizationId
            WHERE r.Role <> o.Role;

            DELETE v FROM dbo.CaseDataFields v
            JOIN dbo.CaseDataFields id ON id.WorkId = v.WorkId
            JOIN #Lost l ON TRY_CONVERT(uniqueidentifier, id.Value) = l.OrganizationId
            WHERE (l.Role = N'claim_source' AND id.FieldName = N'claim_source_id' AND v.FieldName IN (N'claim_source_id', N'claim_source_version'))
               OR (l.Role = N'repairer' AND id.FieldName = N'repairer_id' AND v.FieldName IN (N'repairer_id', N'repairer_version'))
               OR (l.Role = N'storage' AND id.FieldName = N'storage_business_id' AND v.FieldName IN (N'storage_business_id', N'storage_business_version'));

            DELETE k FROM dbo.ContactPrincipalLinks k
            JOIN dbo.Organizations o ON o.Id = k.OrganizationId
            WHERE k.Role <> o.Role;

            DECLARE @cs nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrganizationId)) FROM dbo.Principals WHERE Code = N'CS');
            DECLARE @acsp nvarchar(36) = (SELECT LOWER(CONVERT(nvarchar(36), OrganizationId)) FROM dbo.Principals WHERE Code = N'ACSP');
            IF @cs IS NOT NULL
                UPDATE dbo.Principals SET ReportSendingRulesJson = REPLACE(ReportSendingRulesJson,
                    N'{"kind":"ClaimSource","values":["' + @cs + N'"]}',
                    N'{"kind":"Mentions","values":["Claim Specialist","Claims Specialist","claimsspecialists.co.uk"]}');
            IF @acsp IS NOT NULL
                UPDATE dbo.Principals SET ReportSendingRulesJson = REPLACE(ReportSendingRulesJson,
                    N'{"kind":"ClaimSource","values":["' + @acsp + N'"]}',
                    N'{"kind":"Mentions","values":["Accident Specialist"]}');

            IF EXISTS (
                SELECT 1 FROM dbo.Principals p
                JOIN #Lost l ON l.Role IN (N'claim_source', N'repairer')
                    AND p.ReportSendingRulesJson LIKE N'%' + LOWER(CONVERT(nvarchar(36), l.OrganizationId)) + N'%')
                THROW 50000, N'Report sending rules still name a contact by a type it loses. Edit those rules before this migration.', 1;
            DROP TABLE #Lost;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Role", table: "Organizations", type: "nvarchar(40)", maxLength: 40, nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(40)", oldMaxLength: 40, oldNullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "CK_Organizations_Role", table: "Organizations",
            sql: "[Role] IN ('principal', 'claim_source', 'repairer', 'storage', 'third_party_engineer')");
        // A name is unique within one type: a Repairer and a Storage contact may share it.
        migrationBuilder.DropIndex(name: "IX_Organizations_NormalizedName", table: "Organizations");
        // Raw SQL: EF would add an IS NOT NULL filter, which SQL Server refuses on the
        // computed NormalizedName (20260729187000_OrganizationPrincipalAdministration does the same).
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX [IX_Organizations_NormalizedName_Role] ON [dbo].[Organizations] ([NormalizedName], [Role]);");

        migrationBuilder.DropPrimaryKey(name: "PK_ContactPrincipalLinks", table: "ContactPrincipalLinks");
        migrationBuilder.DropIndex(name: "IX_ContactPrincipalLinks_OrganizationId_Role", table: "ContactPrincipalLinks");
        migrationBuilder.DropCheckConstraint(name: "CK_ContactPrincipalLinks_Role", table: "ContactPrincipalLinks");
        migrationBuilder.DropColumn(name: "Role", table: "ContactPrincipalLinks");
        migrationBuilder.AddPrimaryKey(
            name: "PK_ContactPrincipalLinks", table: "ContactPrincipalLinks", columns: new[] { "PrincipalId", "OrganizationId" });
        migrationBuilder.CreateIndex(
            name: "IX_ContactPrincipalLinks_OrganizationId", table: "ContactPrincipalLinks", column: "OrganizationId");

        migrationBuilder.DropTable(name: "ContactRoles");
        migrationBuilder.DropTable(name: "OrganizationRoles");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "ContactSingleType is forward-only: it drops ContactRoles, OrganizationRoles and ContactPrincipalLinks.Role. Restore from an approved backup instead.");
}
