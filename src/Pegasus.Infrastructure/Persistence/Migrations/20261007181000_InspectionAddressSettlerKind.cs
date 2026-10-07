using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Automation actor settles an inspection address as staff do (ADR-0064,
/// operator, 7 October 2026), so a settled address records its settler's
/// actor kind and subject rather than a staff identifier alone. Each settled
/// address is an <c>ext18-address-resolution/v1/</c> signal in its receipt's
/// <c>EvidenceJson</c>: base64url of the resolution's JSON, whose
/// <c>"staffId":"…"</c> becomes <c>"resolvedByKind":"Staff","resolvedBy":"…"</c>
/// under the <c>v2/</c> prefix. Every v1 resolution was a member of staff's.
/// The resolution JSON is ASCII (System.Text.Json escapes anything else), so
/// its bytes convert without a code page. No schema or grant changes.
/// An older release reads a v2 signal as no resolution: an item settled but
/// not yet a Case would ask for its address again there.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007181000_InspectionAddressSettlerKind")]
public partial class InspectionAddressSettlerKind : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql(
            """
            DECLARE @v1 nvarchar(40) = N'ext18-address-resolution/v1/';
            DECLARE @v2 nvarchar(40) = N'ext18-address-resolution/v2/';
            DECLARE @signals TABLE (
                [Id] int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                [ReceiptId] uniqueidentifier NOT NULL,
                [OldSignal] nvarchar(max) NOT NULL,
                [NewSignal] nvarchar(max) NULL);

            INSERT INTO @signals ([ReceiptId], [OldSignal])
            SELECT [r].[Id], [e].[signal]
            FROM (
                SELECT [Id], [EvidenceJson]
                FROM [dbo].[IntakeReceipts]
                WHERE CHARINDEX(@v1, [EvidenceJson]) > 0
                  AND ISJSON([EvidenceJson]) = 1) AS [r]
            CROSS APPLY OPENJSON([r].[EvidenceJson], N'$.data')
                WITH ([signal] nvarchar(max) N'$.signal') AS [e]
            WHERE LEFT([e].[signal], LEN(@v1)) = @v1 COLLATE Latin1_General_100_BIN2;

            -- base64url payload -> standard base64 -> bytes -> ASCII JSON,
            -- the settler renamed, then back the same way under v2.
            WITH [decoded] AS (
                SELECT [s].[Id],
                       CAST(CAST(N'' AS xml).value(
                           'xs:base64Binary(sql:column("p.Padded"))', 'varbinary(max)') AS varchar(max)) AS [Json]
                FROM @signals AS [s]
                CROSS APPLY (
                    SELECT REPLACE(REPLACE(SUBSTRING([s].[OldSignal], LEN(@v1) + 1, LEN([s].[OldSignal])), '-', '+'), '_', '/')
                        AS [Standard]) AS [b]
                CROSS APPLY (
                    SELECT CAST([b].[Standard] + REPLICATE('=', (4 - LEN([b].[Standard]) % 4) % 4) AS varchar(max))
                        AS [Padded]) AS [p]),
            [rewritten] AS (
                SELECT [d].[Id],
                       CAST(REPLACE(
                           [d].[Json] COLLATE Latin1_General_100_BIN2,
                           '"staffId":"',
                           '"resolvedByKind":"Staff","resolvedBy":"') AS varbinary(max)) AS [Bytes]
                FROM [decoded] AS [d]
                WHERE CHARINDEX('"staffId":"', [d].[Json] COLLATE Latin1_General_100_BIN2) > 0),
            [encoded] AS (
                SELECT [w].[Id],
                       CAST(N'' AS xml).value('xs:base64Binary(sql:column("w.Bytes"))', 'varchar(max)') AS [Base64]
                FROM [rewritten] AS [w])
            UPDATE [s]
            SET [NewSignal] = @v2 + REPLACE(REPLACE(REPLACE([n].[Base64], '+', '-'), '/', '_'), '=', '')
            FROM @signals AS [s]
            INNER JOIN [encoded] AS [n] ON [n].[Id] = [s].[Id];

            IF EXISTS (SELECT 1 FROM @signals WHERE [NewSignal] IS NULL)
                THROW 51000, 'An inspection-address resolution names no staff settler and cannot be rewritten.', 1;

            -- One signal at a time: a receipt can hold more than one resolution.
            DECLARE @id int = (SELECT MIN([Id]) FROM @signals);
            WHILE @id IS NOT NULL
            BEGIN
                UPDATE [r]
                SET [EvidenceJson] = REPLACE(
                    [r].[EvidenceJson] COLLATE Latin1_General_100_BIN2, [s].[OldSignal], [s].[NewSignal])
                FROM [dbo].[IntakeReceipts] AS [r]
                INNER JOIN @signals AS [s] ON [s].[ReceiptId] = [r].[Id]
                WHERE [s].[Id] = @id;
                SET @id = (SELECT MIN([Id]) FROM @signals WHERE [Id] > @id);
            END;

            IF EXISTS (
                SELECT 1 FROM [dbo].[IntakeReceipts]
                WHERE CHARINDEX(@v1, [EvidenceJson] COLLATE Latin1_General_100_BIN2) > 0)
                THROW 51000, 'A v1 inspection-address resolution remains after the rewrite.', 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "InspectionAddressSettlerKind is forward-only: an Automation settler has no staff identifier to restore.");
}
