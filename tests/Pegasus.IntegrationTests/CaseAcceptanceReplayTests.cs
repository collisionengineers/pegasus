using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed partial class CaseAcceptanceReplayTests
{
    private const string PrincipalCode = "REPLAY";
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExactReplayReturnsOriginalAcceptanceAndEveryChangedCommandConflicts()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var acceptIntake = scope.ServiceProvider.GetRequiredService<IAcceptIntake>();
        var request = new AcceptIntakeRequest(
            receipt.Id,
            reviewedVersion,
            "staff:acceptance-review",
            "acceptance:exact-replay",
            CaseType.Audit,
            PrincipalCode,
            new(true, true, true, true),
            AuditAssessment.Repairable);

        var first = await acceptIntake.ExecuteAsync(request, CancellationToken.None);
        await SetCaseCustodyStateAsync(factory.Services, "confirmed");
        var replay = await acceptIntake.ExecuteAsync(request, CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.Identity, replay.Identity);
        Assert.Equal(first.InitialState, replay.InitialState);
        Assert.Equal(first.CustodyState, replay.CustodyState);
        Assert.Equal(first.CustodyWorkId, replay.CustodyWorkId);

        var persisted = await ReadAcceptancePersistenceAsync(factory.Services);
        Assert.Equal(reviewedVersion, persisted.ExpectedIntakeVersion);
        Assert.Matches("^[0-9a-f]{64}$", persisted.CommandFingerprint);
        Assert.Contains(PrincipalCode, persisted.CommandMaterialJson, StringComparison.Ordinal);
        Assert.Contains(request.Actor, persisted.CommandMaterialJson, StringComparison.Ordinal);

        AcceptIntakeRequest[] changedRequests =
        [
            request with { OperationKey = "acceptance:different-key" },
            request with
            {
                CaseType = CaseType.InspectionAndAudit,
                StandaloneAuditAssessment = null
            },
            request with { PrincipalCode = "OTHER" },
            request with
            {
                Completeness = request.Completeness with { ImagesComplete = false }
            },
            request with { StandaloneAuditAssessment = AuditAssessment.TotalLoss },
            request with { Actor = "staff:different-reviewer" },
            request with { ExpectedVersion = reviewedVersion + 1 }
        ];

        foreach (var changedRequest in changedRequests)
        {
            var conflict = await Assert.ThrowsAsync<CaseAcceptanceOperationConflictException>(
                () => acceptIntake.ExecuteAsync(changedRequest, CancellationToken.None));
            Assert.Equal(receipt.Id, conflict.IntakeReceiptId);
            Assert.Equal(changedRequest.OperationKey, conflict.OperationKey);
        }

        Assert.Equal(1, await CountRowsAsync(factory.Services, "Cases"));
        Assert.Equal(1, await CountRowsAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(1, await CountRowsAsync(factory.Services, "CaseSequences"));
    }

    [Fact]
    public async Task ReceiptChangedAfterReviewCannotBeAccepted()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        await AdvanceReceiptVersionAsync(factory.Services, receipt.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var acceptIntake = scope.ServiceProvider.GetRequiredService<IAcceptIntake>();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => acceptIntake.ExecuteAsync(
            new(
                receipt.Id,
                reviewedVersion,
                "staff:stale-review",
                "acceptance:stale-review",
                CaseType.Inspection,
                PrincipalCode,
                new(true, true, true, true)),
            CancellationToken.None));

        Assert.Equal(0, await CountRowsAsync(factory.Services, "Cases"));
        Assert.Equal(0, await CountRowsAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(0, await CountRowsAsync(factory.Services, "CaseSequences"));
    }

    [Fact]
    public async Task ReviewPostUsesVersionRenderedInAcceptanceFormInsteadOfReloadedVersion()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var addressStore = new MutableAddressResolutionStore();
        var acceptIntake = new VersionCheckingAcceptIntake();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInspectionAddressResolutionStore>();
                services.AddSingleton<IInspectionAddressResolutionStore>(addressStore);
                services.RemoveAll<IAcceptIntake>();
                services.AddSingleton<IAcceptIntake>(acceptIntake);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        addressStore.SetReceipt(receipt.Id, reviewedVersion);
        acceptIntake.CurrentReceiptVersion = reviewedVersion;

        using var reviewResponse = await client.GetAsync($"/Intake/Review/{receipt.Id}");
        reviewResponse.EnsureSuccessStatusCode();
        var reviewHtml = await reviewResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = InputValue(reviewHtml, "__RequestVerificationToken");
        var operationKey = InputValue(reviewHtml, "AcceptanceOperationKey");
        var renderedVersion = long.Parse(
            InputValue(reviewHtml, "ReviewedReceiptVersion"),
            CultureInfo.InvariantCulture);
        Assert.Equal(reviewedVersion, renderedVersion);

        addressStore.ReceiptVersion++;
        acceptIntake.CurrentReceiptVersion++;
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["AcceptanceOperationKey"] = operationKey,
            ["ReviewedReceiptVersion"] = renderedVersion.ToString(CultureInfo.InvariantCulture),
            ["PrincipalCode"] = PrincipalCode,
            ["CaseType"] = CaseType.Inspection.ToString(),
            ["StandaloneAuditAssessment"] = string.Empty,
            ["InstructionComplete"] = bool.TrueString,
            ["ImagesComplete"] = bool.TrueString,
            ["InstructionConfirmedByStaff"] = bool.TrueString,
            ["ImagesConfirmedByStaff"] = bool.TrueString
        });

        using var response = await client.PostAsync(
            $"/Intake/Review/{receipt.Id}?handler=Accept",
            content);
        var responseHtml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, acceptIntake.Attempts);
        var capturedRequest = Assert.IsType<AcceptIntakeRequest>(acceptIntake.LastRequest);
        Assert.Equal(renderedVersion, capturedRequest.ExpectedVersion);
        Assert.NotEqual(addressStore.ReceiptVersion, capturedRequest.ExpectedVersion);
        Assert.Contains(
            "The case could not be accepted. No reference was allocated",
            responseHtml,
            StringComparison.Ordinal);
    }

    private static async Task<IntakeReceipt> CreateReadyReceiptAsync(
        IServiceProvider services,
        string principalCode)
    {
        var token = Guid.NewGuid().ToString("N");
        var sourceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        return await store.StoreAsync(
            new(
                "acceptance-review.eml",
                "message/rfc822",
                1,
                sourceHash,
                new(IntakeSourceChannel.ManualUpload, token),
                RecordedAtUtc,
                RecordedAtUtc,
                "Acceptance replay test",
                IntakeDecision.DraftReady,
                "Ready for staff review",
                [],
                [],
                new(
                    principalCode,
                    "Replay claimant",
                    "REPLAY-001",
                    "AB12CDE",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Image Based Assessment"),
                [],
                null,
                null,
                "acceptance_test_reader",
                "1",
                "acceptance_test_policy",
                1),
            CancellationToken.None);
    }

    private static async Task SeedPrincipalAsync(IServiceProvider services, string principalCode)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Replay provider"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {RecordedAtUtc})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Principals
                (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
            VALUES
                ({principalId}, {organizationId}, {principalCode}, {lineageId}, NULL, NULL, {true}, {0L})
            """);
    }

    private static async Task<long> ReadReceiptVersionAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT Version FROM IntakeReceipts";
            return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task AdvanceReceiptVersionAsync(IServiceProvider services, Guid receiptId)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var updated = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE IntakeReceipts SET Version = Version + 1 WHERE Id = {receiptId}");
        Assert.Equal(1, updated);
    }

    private static async Task SetCaseCustodyStateAsync(
        IServiceProvider services,
        string custodyState)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var updated = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Cases SET CustodyState = {custodyState}");
        Assert.Equal(1, updated);
    }

    private static async Task<AcceptancePersistence> ReadAcceptancePersistenceAsync(
        IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "SELECT ExpectedIntakeVersion, AcceptanceCommandFingerprint, AcceptanceCommandMaterialJson FROM CaseIntakeLinks";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var result = new AcceptancePersistence(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2));
            Assert.False(await reader.ReadAsync());
            return result;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int> CountRowsAsync(IServiceProvider services, string tableName)
    {
        var allowed = tableName switch
        {
            "Cases" or "CaseIntakeLinks" or "CaseSequences" => tableName,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM [{allowed}]";
            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static string InputValue(string html, string name)
    {
        var match = InputTagRegex().Matches(html)
            .Cast<Match>()
            .FirstOrDefault(candidate => string.Equals(
                WebUtility.HtmlDecode(candidate.Groups["name"].Value),
                name,
                StringComparison.Ordinal));
        Assert.True(match is not null, $"The review form must render input '{name}'.");
        return WebUtility.HtmlDecode(match!.Groups["value"].Value);
    }

    [GeneratedRegex(
        "<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    private sealed record AcceptancePersistence(
        long ExpectedIntakeVersion,
        string CommandFingerprint,
        string CommandMaterialJson);

    private sealed class MutableAddressResolutionStore : IInspectionAddressResolutionStore
    {
        private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private Guid receiptId;

        public long ReceiptVersion { get; set; }

        public void SetReceipt(Guid value, long version)
        {
            receiptId = value;
            ReceiptVersion = version;
        }

        public Task<InspectionAddressResolutionSnapshot?> GetAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (intakeReceiptId != receiptId)
            {
                return Task.FromResult<InspectionAddressResolutionSnapshot?>(null);
            }

            InspectionAddressResolutionSnapshot snapshot = new(
                intakeReceiptId,
                ReceiptVersion,
                InspectionAddressResolutionState.Accepted,
                new(
                    new(
                        "Image Based Assessment",
                        InspectionAddressEvidenceKind.ImageBasedAssessment,
                        [],
                        new string('a', 64)),
                    []),
                "Image Based Assessment",
                StaffId,
                RecordedAtUtc);
            return Task.FromResult<InspectionAddressResolutionSnapshot?>(snapshot);
        }

        public Task<InspectionAddressResolutionSnapshot> ResolveAsync(
            InspectionAddressResolutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromException<InspectionAddressResolutionSnapshot>(
                new NotSupportedException("This focused test does not mutate address resolution."));
    }

    private sealed class VersionCheckingAcceptIntake : IAcceptIntake
    {
        public long CurrentReceiptVersion { get; set; }

        public int Attempts { get; private set; }

        public AcceptIntakeRequest? LastRequest { get; private set; }

        public Task<CaseAcceptanceOutcome> ExecuteAsync(
            AcceptIntakeRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            LastRequest = request;
            if (request.ExpectedVersion != CurrentReceiptVersion)
            {
                return Task.FromException<CaseAcceptanceOutcome>(
                    new DbUpdateConcurrencyException("The rendered intake version is stale."));
            }

            return Task.FromResult(new CaseAcceptanceOutcome(
                new(Guid.NewGuid(), request.PrincipalCode, 2031, 1, "REPLAY31001"),
                CaseInitialState.Review,
                CaseCustodyState.Pending,
                Guid.NewGuid(),
                false));
        }
    }
}
