using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>Identity and OpenIddict composition shared by persistence fixtures.</summary>
internal static class IdentityPersistenceTestServices
{
    public static void Configure(IServiceCollection services)
    {
        services
            .AddIdentity<PegasusIdentityUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Lockout.AllowedForNewUsers = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<PegasusDbContext>()
            .AddDefaultTokenProviders();
        // The staff-identity surfaces are composed by the Web host beside its
        // Identity; a persistence test that exercises them composes the same.
        services.AddPegasusStaffIdentity();
        services.AddOpenIddict()
            .AddCore(options => options
                .UseEntityFrameworkCore()
                .UseDbContext<PegasusDbContext>());
    }
}
