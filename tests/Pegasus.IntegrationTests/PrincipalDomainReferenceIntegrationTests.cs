using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class PrincipalDomainReferenceIntegrationTests
{
    private const string PackagePath =
        "src/Pegasus.Infrastructure/Persistence/ReferenceData/principal-domains.v1.json";
    private const string PackageSha256 = "f6b5ad8ecdd428db4316b23e16aa7e0ffc93562aec33374c03ea68cd4f0370a3";
    private const string HistoricalWorkbookPath =
        "docs/reference/workproviders-and-repairers/initial.xlsx";
    private const string PhysicalWorkbookPath =
        "reference/workproviders-and-repairers/initial.xlsx";
    private const string PreInspectionModeMigration =
        "20260801220500_GrantWebMigrationHistoryRead";

    [Fact]
    public void PackageFileMatchesApprovedWorkbookDomainEvidence()
    {
        var packageBytes = LoadPackageBytes();
        Assert.Equal(PackageSha256, Convert.ToHexStringLower(SHA256.HashData(packageBytes)));

        var package = DeserializePackage(packageBytes);
        Assert.Equal(HistoricalWorkbookPath, package.Source.Path);
        var workbook = ReadApprovedWorkbook(package.Source.Path, package.Source.Sheet);

        Assert.Equal(package.Source.RowCount, workbook.HighestContractRow);
        Assert.Equal(package.Source.ContentSha256, workbook.ContentSha256);
        Assert.Equal(11, package.Principals.Length);
        Assert.Equal(16, package.Principals.Sum(provider => provider.DomainSuffixes.Length));
        Assert.Equal(
            workbook.DomainEvidence.ToArray(),
            package.Principals
                .SelectMany(provider => provider.DomainSuffixes.Select(suffix =>
                    new PrincipalEvidence(provider.Code, provider.SourceRow, suffix)))
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.DomainSuffix, StringComparer.Ordinal)
                .ToArray());
    }


    [Fact]
    public void CoreAcceptedQdosRouteSetMatchesTheReferenceSnapshotExactly()
    {
        var package = DeserializePackage(LoadPackageBytes());
        var qdos = Assert.Single(package.Principals, provider => provider.Code == "QDOS");

        Assert.Equal(
            qdos.DomainSuffixes
                .Select(suffix => suffix.TrimStart('@'))
                .OrderBy(domain => domain, StringComparer.Ordinal)
                .ToArray(),
            PrincipalMailRoutePolicy.AcceptedIdentities["QDOS"]
                .OrderBy(domain => domain, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task FreshSchemaMigrationSeedsExactPackage()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();

        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.MigrateAsync();
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }

        var package = DeserializePackage(LoadPackageBytes());
        Assert.Equal(package.Principals.Length, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM PrincipalReferences"));
        Assert.Equal(
            package.Principals.Sum(provider => provider.DomainSuffixes.Length),
            await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM PrincipalDomainEvidence"));
        Assert.Equal(
            PackageSha256,
            await ScalarAsync<string>(connection,
                "SELECT PackageSha256 FROM PrincipalDomainPackages WHERE Version = 'provider-domains-v1'"));

        Assert.Equal(
            new PrincipalPackageRow(
                package.Version,
                package.SchemaVersion,
                PackageSha256,
                package.Source.Path,
                package.Source.ContentSha256,
                package.Source.Sheet,
                package.Source.RowCount),
            await PrincipalPackageRowAsync(connection, "PrincipalDomainPackages"));
        Assert.Equal(
            package.Principals
                .SelectMany(provider => provider.DomainSuffixes.Select(suffix =>
                    new PrincipalEvidence(provider.Code, provider.SourceRow, suffix)))
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.DomainSuffix, StringComparer.Ordinal)
                .ToArray(),
            (await PrincipalEvidenceRowsAsync(connection)).ToArray());
    }

    [Fact]
    public async Task ExistingPrincipalSnapshotMigratesForwardWithoutRepublishingV1()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        var package = DeserializePackage(LoadPackageBytes());

        await using var context = await database.CreateContextAsync();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreInspectionModeMigration);

        var priorSchemaPackage = await PrincipalPackageRowAsync(connection, "ProviderDomainPackages");
        Assert.Equal(ExpectedPackageRow(package), priorSchemaPackage);
        Assert.Equal(1, await ScalarAsync<long>(
            connection,
            "SELECT COUNT(*) FROM ProviderDomainPackages WHERE Version = 'provider-domains-v1'"));

        await migrator.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(priorSchemaPackage, await PrincipalPackageRowAsync(connection, "PrincipalDomainPackages"));
        Assert.Equal(1, await ScalarAsync<long>(
            connection,
            "SELECT COUNT(*) FROM PrincipalDomainPackages WHERE Version = 'provider-domains-v1'"));
        Assert.Contains(
            "20260803014608_ProviderInspectionModeSetting",
            await context.Database.GetAppliedMigrationsAsync());
    }

    private static byte[] LoadPackageBytes() =>
        File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), PackagePath.Replace('/', Path.DirectorySeparatorChar)));

    private static PackageFile DeserializePackage(ReadOnlySpan<byte> packageBytes) =>
        JsonSerializer.Deserialize<PackageFile>(packageBytes, JsonSerializerOptions.Strict)
        ?? throw new InvalidDataException("Principal-domain package deserialized to null.");

    private static WorkbookEvidence ReadApprovedWorkbook(string repositoryPath, string sheetName)
    {
        var physicalRepositoryPath = repositoryPath == HistoricalWorkbookPath
            ? PhysicalWorkbookPath
            : repositoryPath;
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            physicalRepositoryPath.Replace('/', Path.DirectorySeparatorChar));
        var sourceBytes = File.ReadAllBytes(sourcePath);
        using var document = SpreadsheetDocument.Open(sourcePath, false);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("Approved workbook has no workbook part.");
        var workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Approved workbook has no workbook.");
        var sheet = workbook.Descendants<Sheet>()
            .SingleOrDefault(item => string.Equals(item.Name?.Value, sheetName, StringComparison.Ordinal))
            ?? throw new InvalidDataException("Approved workbook sheet was not found.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(
            sheet.Id?.Value ?? throw new InvalidDataException("Approved workbook sheet has no relationship."));
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        var evidence = ImmutableArray.CreateBuilder<PrincipalEvidence>();
        var highestContractRow = 0;
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidDataException("Approved workbook sheet has no worksheet.");
        foreach (var row in worksheet.Descendants<Row>())
        {
            var rowNumber = checked((int)(row.RowIndex?.Value
                ?? throw new InvalidDataException("Approved workbook row has no index.")));
            var code = CellText(row, 'A', sharedStrings).Trim();
            var observations = CellText(row, 'E', sharedStrings).Trim();
            if (code.Length == 0 && observations.Length == 0)
            {
                continue;
            }
            if (code.Length == 0 || observations.Length == 0)
            {
                throw new InvalidDataException($"Approved workbook row {rowNumber} is incomplete.");
            }

            highestContractRow = Math.Max(highestContractRow, rowNumber);
            foreach (var observation in observations.Split(';'))
            {
                var address = observation.Trim();
                var separator = address.LastIndexOf('@');
                if (separator <= 0 || separator == address.Length - 1)
                {
                    throw new InvalidDataException($"Approved workbook row {rowNumber} has invalid domain evidence.");
                }

                var suffix = string.Concat("@", address.AsSpan(separator + 1)).ToLowerInvariant();
                evidence.Add(new PrincipalEvidence(code, rowNumber, suffix));
            }
        }

        return new WorkbookEvidence(
            Convert.ToHexStringLower(SHA256.HashData(sourceBytes)),
            highestContractRow,
            evidence.Distinct().OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.DomainSuffix, StringComparer.Ordinal).ToImmutableArray());
    }

    private static string CellText(Row row, char column, SharedStringTable? sharedStrings)
    {
        var cell = row.Elements<Cell>().SingleOrDefault(item =>
            item.CellReference?.Value is { Length: > 0 } reference && reference[0] == column);
        if (cell is null)
        {
            return string.Empty;
        }

        var value = cell.CellValue?.InnerText ?? cell.InnerText;
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (sharedStrings is null ||
                !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                throw new InvalidDataException("Approved workbook shared-string reference is invalid.");
            }
            return sharedStrings.ElementAt(index).InnerText;
        }

        return value;
    }



    private static async Task<PrincipalPackageRow> PrincipalPackageRowAsync(
        SqlConnection connection,
        string packageTable)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT Version, SchemaVersion, PackageSha256, SourcePath,
                   SourceContentSha256, SourceSheet, SourceRowCount
            FROM {packageTable}
            """;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected the principal-domain package row.");
        }
        var package = new PrincipalPackageRow(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6));
        if (await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected exactly one principal-domain package row.");
        }
        return package;
    }

    private static PrincipalPackageRow ExpectedPackageRow(PackageFile package) =>
        new(
            package.Version,
            package.SchemaVersion,
            PackageSha256,
            package.Source.Path,
            package.Source.ContentSha256,
            package.Source.Sheet,
            package.Source.RowCount);

    private static async Task<ImmutableArray<PrincipalEvidence>> PrincipalEvidenceRowsAsync(
        SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT reference.Code, reference.SourceRow, evidence.DomainSuffix
            FROM PrincipalReferences AS reference
            INNER JOIN PrincipalDomainEvidence AS evidence
                ON evidence.Version = reference.Version AND evidence.Code = reference.Code
            ORDER BY reference.Code, evidence.DomainSuffix
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var evidence = ImmutableArray.CreateBuilder<PrincipalEvidence>();
        while (await reader.ReadAsync())
        {
            evidence.Add(new PrincipalEvidence(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2)));
        }
        return evidence.ToImmutable();
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(
            await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a scalar value."),
            typeof(T),
            CultureInfo.InvariantCulture);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }



    private sealed record PrincipalEvidence(string Code, int SourceRow, string DomainSuffix);

    private sealed record PrincipalPackageRow(
        string Version,
        int SchemaVersion,
        string PackageSha256,
        string SourcePath,
        string SourceContentSha256,
        string SourceSheet,
        int SourceRowCount);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record PackageFile(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("source")] PackageSource Source,
        [property: JsonPropertyName("providers")] ImmutableArray<PackagePrincipal> Principals);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record PackageSource(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("contentSha256")] string ContentSha256,
        [property: JsonPropertyName("sheet")] string Sheet,
        [property: JsonPropertyName("rowCount")] int RowCount);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record PackagePrincipal(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("sourceRow")] int SourceRow,
        [property: JsonPropertyName("domainSuffixes")] ImmutableArray<string> DomainSuffixes);

    private sealed record WorkbookEvidence(
        string ContentSha256,
        int HighestContractRow,
        ImmutableArray<PrincipalEvidence> DomainEvidence);
}
