using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A receipt whose bytes Box custody has not confirmed yet is still being
/// filed: the Source and Asset routes tell an authenticated operator to wait,
/// rather than presenting it as missing or failing the request.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class IntakePendingCustodyWebTests
{
    private static readonly Guid ReceiptId = Guid.Parse("7b4d2e63-0b31-4d22-8b30-5b32dae29d11");
    private static readonly Guid AssetId = Guid.Parse("7b4d2e63-0b31-4d22-8b30-5b32dae29d12");

    [Theory]
    [InlineData("Source")]
    [InlineData("Asset")]
    public async Task ASourceOrAssetStillReachingCustodyIsReportedAsUnavailable(string route)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                var pending = new PendingCustody();
                Substitute<IDownloadIntakeSource>(services, pending);
                Substitute<IDownloadIntakeAsset>(services, pending);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var path = route == "Source"
            ? $"/Received/{ReceiptId:D}/Source"
            : $"/Received/{ReceiptId:D}/Asset/{AssetId:D}";
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("durable storage is confirmed", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static void Substitute<T>(IServiceCollection services, T instance)
        where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(instance);
    }

    /// <summary>What the content store says while the receipt's Box ids are still absent.</summary>
    private sealed class PendingCustody : IDownloadIntakeSource, IDownloadIntakeAsset
    {
        public Task<IntakeSourceDownload?> ExecuteAsync(
            DownloadIntakeSourceQuery query,
            CancellationToken cancellationToken = default) =>
            throw new IntakeCustodyUnavailableException("Durable Box custody has not been confirmed.");

        public Task<IntakeSourceDownload?> ExecuteAsync(
            DownloadIntakeAssetQuery query,
            CancellationToken cancellationToken = default) =>
            throw new IntakeCustodyUnavailableException("Durable Box custody has not been confirmed.");
    }
}
