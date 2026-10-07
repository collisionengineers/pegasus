using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261007181000_InspectionAddressSettlerKind rewrites each settled
/// inspection address from the staff-only v1 resolution to the v2 resolution
/// that names its settler's actor kind (ADR-0064), and the store reads the
/// rewritten row back as the member of staff who settled it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class InspectionAddressSettlerMigrationTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private const string Predecessor = "20261007160000_RemoveReportDateOverride";
    private const string ReceiptId = "52000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task AV1StaffResolutionIsRewrittenToAV2StaffSettlerAndReadsBack()
    {
        var staffId = Guid.Parse("53000000-0000-0000-0000-000000000001");
        var operationId = Guid.Parse("54000000-0000-0000-0000-000000000001");
        // System.Text.Json escapes the non-ASCII character, as the store's own
        // writer does, so the resolution's bytes are ASCII.
        var v1 = Encode(JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                state = "supplied",
                value = "1 Café Street, Exampleton EX1 1EX",
                suggestionFingerprint = string.Empty,
                staffId,
                occurredAtUtc = DateTimeOffset.Parse("2031-05-06T10:30:00+00:00", System.Globalization.CultureInfo.InvariantCulture),
                operationId,
            },
            WebJson));
        var evidenceJson =
            $$"""{"version":1,"data":[{"source":"pdf_content","strength":"weak","finding":"information","signal":"other-signal","detail":"Unrelated."},{"source":"staff_correction","strength":"strong","finding":"information","signal":"ext18-address-resolution/v1/{{v1}}","detail":"Inspection address supplied by staff; no address evidence was extracted from the source."}]}""";

        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.MigrateAsync(Predecessor);
        }
        await database.ExecuteAsync(
            $$"""
            INSERT INTO IntakeReceipts
                (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
                 ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey,
                 SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Decision,
                 DecisionReason, EvidenceJson, FieldsJson, FailureCode, FailureReason, OcrCandidatesJson, Version)
            VALUES
                ('{{ReceiptId}}', 'settled.eml', 'message/rfc822', 1,
                 'CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC', 'manual_upload',
                 'settler-migration', '2031-05-06T10:30:00+00:00', '2031-05-06T10:30:00+00:00',
                 'migration_test_reader', '1', 'migration_test_policy', 1, 'case_created', 'Ready for review',
                 N'{{evidenceJson}}', N'{"version":1,"data":[]}', NULL, NULL,
                 N'{"version":1,"data":[]}', 3);
            """);

        await database.MigrateAsync();

        var migrated = await database.ScalarAsync<string>(
            $"SELECT EvidenceJson FROM IntakeReceipts WHERE Id = '{ReceiptId}'");
        Assert.DoesNotContain("ext18-address-resolution/v1/", migrated, StringComparison.Ordinal);
        Assert.Contains("\"signal\":\"other-signal\"", migrated, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(migrated);
        var signal = document.RootElement.GetProperty("data")[1].GetProperty("signal").GetString()!;
        Assert.StartsWith("ext18-address-resolution/v2/", signal, StringComparison.Ordinal);
        using var resolution = JsonDocument.Parse(Decode(signal["ext18-address-resolution/v2/".Length..]));
        Assert.Equal("Staff", resolution.RootElement.GetProperty("resolvedByKind").GetString());
        Assert.Equal(staffId.ToString("D"), resolution.RootElement.GetProperty("resolvedBy").GetString());
        Assert.False(resolution.RootElement.TryGetProperty("staffId", out _));
        Assert.Equal("1 Café Street, Exampleton EX1 1EX", resolution.RootElement.GetProperty("value").GetString());
        Assert.Equal(operationId, resolution.RootElement.GetProperty("operationId").GetGuid());

        await using var scope = database.CreateAsyncScope();
        var store = new InspectionAddressResolutionStore(
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            TimeProvider.System);
        var snapshot = await store.GetAsync(Guid.Parse(ReceiptId), CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal(InspectionAddressResolutionState.Supplied, snapshot.State);
        Assert.Equal(ActorKind.Staff, snapshot.ResolvedByKind);
        Assert.Equal(staffId.ToString("D"), snapshot.ResolvedBy);
        Assert.Equal("1 Café Street, Exampleton EX1 1EX", snapshot.ResolvedValue);
    }

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Decode(string encoded)
    {
        var standard = encoded.Replace('-', '+').Replace('_', '/');
        standard = standard.PadRight(standard.Length + ((4 - standard.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(standard));
    }
}
