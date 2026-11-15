using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Contacts are Principals, Claim Sources and Repairers, and none holds a
/// contact person (operator, 13 November 2026; R1047 to R1051). Nothing read
/// a Storage or Third Party Engineer contact, so both types go with their
/// Principal links. A Storage contact that holds an address and a postcode
/// first becomes an inspection location with the same name, address, postcode,
/// active state and Principal links, so a Case of a linked Principal can
/// choose the yard under Inspect at. The Case fields no page or tool could
/// set, the storage business and the inspection contact, go too. Non-additive
/// and forward only: the contact persons, the Third Party Engineer contacts
/// and the Storage contacts' telephone and e-mail are not kept.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261113140000_ContactsThreeTypes")]
public partial class ContactsThreeTypes : Migration
{
    /// <summary>The source of an inspection location that was a Storage contact; its record id is that contact's.</summary>
    internal const string YardSource = "contact";

    private const string Table = "Organizations";
    private const string RoleConstraint = "CK_Organizations_Role";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RemoveTypesSql);

        migrationBuilder.DropCheckConstraint(
            name: RoleConstraint,
            table: Table);

        migrationBuilder.AddCheckConstraint(
            name: RoleConstraint,
            table: Table,
            sql: "[Role] IN ('principal', 'claim_source', 'repairer')");

        migrationBuilder.DropColumn(
            name: "ContactPerson",
            table: Table);
    }

    /// <summary>
    /// The yards' move and the two types' removal, held apart so a test can
    /// run it over contacts it seeded. It stops before any write where a
    /// photo link or capture names a contact it would delete.
    /// </summary>
    internal const string RemoveTypesSql =
        $"""
        IF EXISTS (
            SELECT 1 FROM [dbo].[Organizations] AS [Contact]
            WHERE [Contact].[Role] IN (N'storage', N'third_party_engineer')
                AND (EXISTS (SELECT 1 FROM [dbo].[StandingPhotoLinks] AS [Link] WHERE [Link].[OrganizationId] = [Contact].[Id])
                    OR EXISTS (SELECT 1 FROM [dbo].[PhotoCaptures] AS [Capture] WHERE [Capture].[OrganizationId] = [Contact].[Id])))
            THROW 50000, N'A Storage or Third Party Engineer contact holds a photo link or capture; reconcile the contacts before migrating.', 1;

        WITH [Yard] AS (
            SELECT [Id], [Name], [Active],
                LTRIM(RTRIM([Address])) AS [Address],
                UPPER(LTRIM(RTRIM([Postcode]))) AS [Postcode]
            FROM [dbo].[Organizations]
            WHERE [Role] = N'storage'
                AND NULLIF(LTRIM(RTRIM([Address])), N'') IS NOT NULL
                AND NULLIF(LTRIM(RTRIM([Postcode])), N'') IS NOT NULL)
        INSERT INTO [dbo].[InspectionLocations] ([Id], [Name], [Address], [Postcode], [Source], [SourceRecordId], [ReviewedAtUtc], [Active], [Version])
        SELECT [Yard].[Id], [Yard].[Name],
            CASE WHEN RIGHT(UPPER([Yard].[Address]), LEN([Yard].[Postcode])) = [Yard].[Postcode]
                THEN [Yard].[Address]
                ELSE LEFT([Yard].[Address] + N', ' + [Yard].[Postcode], 1000) END,
            [Yard].[Postcode], N'{YardSource}', LOWER(CONVERT(nvarchar(36), [Yard].[Id])),
            TODATETIMEOFFSET(SYSUTCDATETIME(), 0), [Yard].[Active], 0
        FROM [Yard]
        WHERE NOT EXISTS (
            SELECT 1 FROM [dbo].[InspectionLocations] AS [Held]
            WHERE [Held].[Id] = [Yard].[Id]
                OR ([Held].[Source] = N'{YardSource}' AND [Held].[SourceRecordId] = LOWER(CONVERT(nvarchar(36), [Yard].[Id]))));

        INSERT INTO [dbo].[InspectionLocationPrincipalLinks] ([PrincipalId], [InspectionLocationId])
        SELECT [Link].[PrincipalId], [Link].[OrganizationId]
        FROM [dbo].[ContactPrincipalLinks] AS [Link]
        JOIN [dbo].[Organizations] AS [Contact]
            ON [Contact].[Id] = [Link].[OrganizationId] AND [Contact].[Role] = N'storage'
        JOIN [dbo].[InspectionLocations] AS [Location]
            ON [Location].[Id] = [Link].[OrganizationId] AND [Location].[Source] = N'{YardSource}'
        WHERE NOT EXISTS (
            SELECT 1 FROM [dbo].[InspectionLocationPrincipalLinks] AS [Held]
            WHERE [Held].[PrincipalId] = [Link].[PrincipalId] AND [Held].[InspectionLocationId] = [Link].[OrganizationId]);

        DELETE [Link]
        FROM [dbo].[ContactPrincipalLinks] AS [Link]
        JOIN [dbo].[Organizations] AS [Contact] ON [Contact].[Id] = [Link].[OrganizationId]
        WHERE [Contact].[Role] IN (N'storage', N'third_party_engineer');

        DELETE FROM [dbo].[Organizations] WHERE [Role] IN (N'storage', N'third_party_engineer');

        DELETE FROM [dbo].[CaseDataFields]
        WHERE [FieldName] IN (
            N'storage_business_id', N'storage_business_version', N'storage_business_name',
            N'storage_business_contact_name', N'storage_business_contact_telephone', N'storage_business_contact_email',
            N'inspection_contact_name', N'inspection_contact_telephone', N'inspection_contact_email');
        """;

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Contacts are Principals, Claim Sources and Repairers; the removed contacts and contact persons are not kept.");
}
