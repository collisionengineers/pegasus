using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-18/S05 + EXT-19/S13 (C06): composes the C-owned adapters —
/// <see cref="EfClaimSourceAdministration"/>, <see cref="EfOrganizationDirectory"/>,
/// <see cref="UpdatePrincipalDefaultInspectionLocation"/> and the extended
/// <see cref="InspectionAddressChoicesQueries"/> — against a plain
/// <see cref="IntakeWebApplicationFactory"/>. Stream A's registrations for
/// these in <c>DependencyInjection.cs</c> are the production path; until
/// they land, C06's own tests that need the full behaviour compose it here,
/// through the ordinary DI path a real registration would use, rather than
/// depending on the optional-resolution bridge that only lets the rest of
/// the host start without it (see the constructor remarks on
/// <see cref="InspectionAddressChoicesQueries"/>).
/// </summary>
internal static class C06AdapterRegistrations
{
    public static WebApplicationFactory<Program> WithC06Adapters(
        this IntakeWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<EfClaimSourceAdministration>();
            services.AddScoped<IClaimSourceAdministration>(
                provider => provider.GetRequiredService<EfClaimSourceAdministration>());
            services.AddScoped<IClaimSourceQueries>(
                provider => provider.GetRequiredService<EfClaimSourceAdministration>());
            services.AddScoped<IOrganizationDirectoryQueries, EfOrganizationDirectory>();
            services.AddScoped<IUpdatePrincipalDefaultInspectionLocation, UpdatePrincipalDefaultInspectionLocation>();

            services.RemoveAll<IInspectionAddressChoicesQueries>();
            services.AddScoped<InspectionAddressChoicesQueries>();
            services.AddScoped<IInspectionAddressChoicesQueries>(
                provider => provider.GetRequiredService<InspectionAddressChoicesQueries>());
            services.AddScoped<IInspectionLocationChoices>(
                provider => provider.GetRequiredService<InspectionAddressChoicesQueries>());
        }));
}
