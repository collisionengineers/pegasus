using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CollisionSpike.Core.ReferenceData;
using CollisionSpike.Infrastructure;
using CollisionSpike.Infrastructure.Persistence;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CollisionSpike.IntegrationTests;

public sealed class ProviderDomainReferenceIntegrationTests
{
    private const string PackageResourceName =
        "CollisionSpike.Infrastructure.Persistence.ReferenceData.provider-domains.v1.json";
    private const string PackageVersion = "provider-domains-v1";
    private const string PackageSha256 = "f6b5ad8ecdd428db4316b23e16aa7e0ffc93562aec33374c03ea68cd4f0370a3";

    [Fact]
    public void EmbeddedPackageMatchesApprovedWorkbookDomainEvidence()
    {
        var packageBytes = LoadEmbeddedPackageBytes();
        var requested = ExactPackageVersion();
        var validation = ReferenceDataPolicy.Validate(requested, packageBytes);
        Assert.True(validation.IsValid, string.Join(", ", validation.Issues.Select(issue => issue.Code)));
        Assert.Equal(PackageSha256, Convert.ToHexStringLower(SHA256.HashData(packageBytes)));

        var package = DeserializePackage(packageBytes);
        var workbook = ReadApprovedWorkbook(package.Source.Path, package.Source.Sheet);

        Assert.Equal(package.Source.RowCount, workbook.HighestContractRow);
        Assert.Equal(package.Source.ContentSha256, workbook.ContentSha256);
        Assert.Equal(11, package.Providers.Length);
        Assert.Equal(16, package.Providers.Sum(provider => provider.DomainSuffixes.Length));
        Assert.Equal(
            workbook.DomainEvidence.ToArray(),
            package.Providers
                .SelectMany(provider => provider.DomainSuffixes.Select(suffix =>
                    new ProviderEvidence(provider.Code, provider.SourceRow, suffix)))
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.DomainSuffix, StringComparer.Ordinal)
                .ToArray());
    }


    [Fact]
    public async Task MigrationSeedsExactPackageAndCatalogUsesOneBoundedQuery()
    {
        var commandCounter = new ReaderCommandCounter();
        await using var connection = new SqliteConnection("Data Source=ProviderDomainCatalog;Mode=Memory;Cache=Shared");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddCollisionSpikeInfrastructure((_, options) =>
            options
                .UseSqlite(connection)
                .AddInterceptors(commandCounter)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        var contextFactory = serviceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }

        var package = DeserializePackage(LoadEmbeddedPackageBytes());
        Assert.Equal(package.Providers.Length, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM ProviderReferences"));
        Assert.Equal(
            package.Providers.Sum(provider => provider.DomainSuffixes.Length),
            await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM ProviderDomainEvidence"));
        Assert.Equal(
            PackageSha256,
            await ScalarAsync<string>(connection,
                "SELECT PackageSha256 FROM ProviderDomainPackages WHERE Version = 'provider-domains-v1'"));

        Assert.Equal(
            new ProviderPackageRow(
                package.Version,
                package.SchemaVersion,
                PackageSha256,
                package.Source.Path,
                package.Source.ContentSha256,
                package.Source.Sheet,
                package.Source.RowCount),
            await ProviderPackageRowAsync(connection));
        Assert.Equal(
            package.Providers
                .SelectMany(provider => provider.DomainSuffixes.Select(suffix =>
                    new ProviderEvidence(provider.Code, provider.SourceRow, suffix)))
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.DomainSuffix, StringComparer.Ordinal)
                .ToArray(),
            (await ProviderEvidenceRowsAsync(connection)).ToArray());

        await SeedSharedDomainFixtureAsync(connection);


        await using var scope = serviceProvider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IProviderReferenceCatalog>();

        commandCounter.Reset();
        var found = await catalog.FindCandidatesByDomainSuffixAsync(
            ExactPackageVersion(), "@qdosassist.co.uk", CancellationToken.None);
        Assert.Equal(ProviderDomainCandidateStatus.Found, found.Status);
        Assert.Equal("QDOS", Assert.Single(found.ProviderCodes));
        Assert.Equal(1, commandCounter.ExecutedReaderCommands);

        commandCounter.Reset();
        var unknown = await catalog.FindCandidatesByDomainSuffixAsync(
            ExactPackageVersion(), "@unknown.invalid", CancellationToken.None);
        Assert.Equal(ProviderDomainCandidateStatus.Unknown, unknown.Status);
        Assert.Empty(unknown.ProviderCodes);
        Assert.Equal(1, commandCounter.ExecutedReaderCommands);

        commandCounter.Reset();
        var rejected = await catalog.FindCandidatesByDomainSuffixAsync(
            ExactPackageVersion() with { PackageSha256 = new string('0', 64) },
            "@qdosassist.co.uk",
            CancellationToken.None);
        Assert.Equal(ProviderDomainCandidateStatus.PackageRejected, rejected.Status);
        Assert.Empty(rejected.ProviderCodes);
        Assert.Equal(1, commandCounter.ExecutedReaderCommands);

        commandCounter.Reset();
        var ambiguous = await catalog.FindCandidatesByDomainSuffixAsync(
            new ProviderDomainPackageVersion(1, "provider-domains-test", new string('1', 64)),
            "@shared.example",
            CancellationToken.None);
        Assert.Equal(ProviderDomainCandidateStatus.Ambiguous, ambiguous.Status);
        Assert.Collection(
            ambiguous.ProviderCodes,
            code => Assert.Equal("ALPHA", code),
            code => Assert.Equal("ZETA", code));
        Assert.Equal(1, commandCounter.ExecutedReaderCommands);

        commandCounter.Reset();
        var missing = await catalog.FindCandidatesByDomainSuffixAsync(
            ExactPackageVersion() with { Version = "provider-domains-v2" },
            "@qdosassist.co.uk",
            CancellationToken.None);
        Assert.Equal(ProviderDomainCandidateStatus.PackageNotFound, missing.Status);
        Assert.Empty(missing.ProviderCodes);
        Assert.Equal(1, commandCounter.ExecutedReaderCommands);

        commandCounter.Reset();
        var invalid = await catalog.FindCandidatesByDomainSuffixAsync(
            ExactPackageVersion(), "qdosassist.co.uk", CancellationToken.None);
        Assert.Equal(ProviderDomainCandidateStatus.InvalidSuffix, invalid.Status);
        Assert.Empty(invalid.ProviderCodes);
        Assert.Equal(0, commandCounter.ExecutedReaderCommands);

        commandCounter.Reset();
        var invalidPackage = await catalog.FindCandidatesByDomainSuffixAsync(
            ExactPackageVersion() with { SchemaVersion = 2 },
            "@qdosassist.co.uk",
            CancellationToken.None);
        Assert.Equal(ProviderDomainCandidateStatus.PackageRejected, invalidPackage.Status);
        Assert.Empty(invalidPackage.ProviderCodes);
        Assert.Equal(0, commandCounter.ExecutedReaderCommands);

        commandCounter.Reset();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await catalog.FindCandidatesByDomainSuffixAsync(
                ExactPackageVersion(), "@qdosassist.co.uk", cancellation.Token));
    }

    private static ProviderDomainPackageVersion ExactPackageVersion() =>
        new(ReferenceDataPolicy.SupportedSchemaVersion, PackageVersion, PackageSha256);

    private static byte[] LoadEmbeddedPackageBytes()
    {
        using var stream = typeof(InfrastructureAssembly).Assembly.GetManifestResourceStream(PackageResourceName)
            ?? throw new InvalidOperationException("Provider-domain package resource was not found.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static ProviderDomainPackage DeserializePackage(ReadOnlySpan<byte> packageBytes) =>
        JsonSerializer.Deserialize<ProviderDomainPackage>(packageBytes, JsonSerializerOptions.Strict)
        ?? throw new InvalidDataException("Provider-domain package deserialized to null.");

    private static WorkbookEvidence ReadApprovedWorkbook(string repositoryPath, string sheetName)
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            repositoryPath.Replace('/', Path.DirectorySeparatorChar));
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

        var evidence = ImmutableArray.CreateBuilder<ProviderEvidence>();
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
                evidence.Add(new ProviderEvidence(code, rowNumber, suffix));
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



    private static async Task SeedSharedDomainFixtureAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ProviderDomainPackages
                (Version, SchemaVersion, PackageSha256, SourcePath,
                 SourceContentSha256, SourceSheet, SourceRowCount)
            VALUES
                ('provider-domains-test', 1,
                 '1111111111111111111111111111111111111111111111111111111111111111',
                 'tests/provider-domains-test.xlsx',
                 '2222222222222222222222222222222222222222222222222222222222222222',
                 'Sheet1', 2);
            INSERT INTO ProviderReferences (Version, Code, SourceRow)
            VALUES
                ('provider-domains-test', 'ZETA', 1),
                ('provider-domains-test', 'ALPHA', 2);
            INSERT INTO ProviderDomainEvidence (Version, Code, DomainSuffix)
            VALUES
                ('provider-domains-test', 'ZETA', '@shared.example'),
                ('provider-domains-test', 'ALPHA', '@shared.example');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<ProviderPackageRow> ProviderPackageRowAsync(
        SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Version, SchemaVersion, PackageSha256, SourcePath,
                   SourceContentSha256, SourceSheet, SourceRowCount
            FROM ProviderDomainPackages
            """;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected the provider-domain package row.");
        }
        var package = new ProviderPackageRow(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6));
        if (await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected exactly one provider-domain package row.");
        }
        return package;
    }

    private static async Task<ImmutableArray<ProviderEvidence>> ProviderEvidenceRowsAsync(
        SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT reference.Code, reference.SourceRow, evidence.DomainSuffix
            FROM ProviderReferences AS reference
            INNER JOIN ProviderDomainEvidence AS evidence
                ON evidence.Version = reference.Version AND evidence.Code = reference.Code
            ORDER BY reference.Code, evidence.DomainSuffix
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var evidence = ImmutableArray.CreateBuilder<ProviderEvidence>();
        while (await reader.ReadAsync())
        {
            evidence.Add(new ProviderEvidence(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2)));
        }
        return evidence.ToImmutable();
    }

    private static async Task<T> ScalarAsync<T>(SqliteConnection connection, string sql)
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



    private sealed record ProviderEvidence(string Code, int SourceRow, string DomainSuffix);

    private sealed record ProviderPackageRow(
        string Version,
        int SchemaVersion,
        string PackageSha256,
        string SourcePath,
        string SourceContentSha256,
        string SourceSheet,
        int SourceRowCount);

    private sealed record WorkbookEvidence(
        string ContentSha256,
        int HighestContractRow,
        ImmutableArray<ProviderEvidence> DomainEvidence);

    private sealed class ReaderCommandCounter : DbCommandInterceptor
    {
        private int executedReaderCommands;

        public int ExecutedReaderCommands => Volatile.Read(ref executedReaderCommands);

        public void Reset() => Interlocked.Exchange(ref executedReaderCommands, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref executedReaderCommands);
            return ValueTask.FromResult(result);
        }
    }
}
