using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Reports;

namespace Pegasus.IntegrationTests.Reports;

public sealed class AssessmentReportRendererTests
{
    [Fact]
    public void NoSignatoryResourceIsEmbedded()
    {
        var assembly = typeof(PlaywrightAssessmentReportRenderer).Assembly;
        Assert.DoesNotContain(
            assembly.GetManifestResourceNames(),
            name => name.Contains("brand.signatures", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheRendererPublishesItsEngineVersionWithoutRendering()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();

        var engineVersion = scope.ServiceProvider
            .GetRequiredService<IAssessmentReportRenderer>().EngineVersion;

        Assert.Contains("Playwright", engineVersion, StringComparison.Ordinal);
        Assert.Contains("Chromium", engineVersion, StringComparison.Ordinal);
    }

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure((_, options) =>
            options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=renderer;Trusted_Connection=True"));
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
