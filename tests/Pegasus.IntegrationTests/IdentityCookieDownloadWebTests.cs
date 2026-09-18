using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class IdentityCookieDownloadWebTests
{
    private static readonly Guid CaseId = Guid.Parse("0a27c520-46ce-4bb9-87f5-58a1258df001");
    private static readonly Guid OccurrenceId = Guid.Parse("0a27c520-46ce-4bb9-87f5-58a1258df002");
    private static readonly Guid DocumentId = Guid.Parse("0a27c520-46ce-4bb9-87f5-58a1258df003");
    private static readonly Guid VersionId = Guid.Parse("0a27c520-46ce-4bb9-87f5-58a1258df004");
    private const string UserName = "identity-cookie-download-user";
    private const string Password = "correct horse battery staple";
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string MediaType = "image/jpeg";
    private static readonly byte[] Thumbnail = Encoding.UTF8.GetBytes("derived thumbnail");

    [Theory]
    [InlineData(ThumbnailRevocation.Disabled)]
    [InlineData(ThumbnailRevocation.Deleted)]
    [InlineData(ThumbnailRevocation.RoleChanged)]
    [InlineData(ThumbnailRevocation.PasswordReset)]
    [InlineData(ThumbnailRevocation.ForcedLogout)]
    public async Task ACurrentThumbnailUsesPrivateCachingWithoutCookieRenewalAndRevalidatesOnlyForAnAuthorizedUser(
        ThumbnailRevocation revocation)
    {
        await using var testDatabase = await LocalDbTestDatabase.CreateAsync(migrate: false);
        var ports = new ThumbnailPorts();
        var clock = new AdjustableTimeProvider(
            new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        using var baseFactory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "Production",
                ["ConnectionStrings:Pegasus"] = testDatabase.ConnectionString,
                ["Features:LocalIntake"] = "false",
                ["Features:LocalDocumentCustody"] = "false"
            });
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IReadCaseDocumentPreview>(services, ports);
                Substitute<IReadCaseDocumentThumbnail>(services, ports);
                Substitute<ICaseAssetPreparationQueries>(services, ports);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.PostConfigure<CookieAuthenticationOptions>(
                    IdentityConstants.ApplicationScheme,
                    options => options.TimeProvider = clock);
                services.PostConfigure<SecurityStampValidatorOptions>(
                    options => options.TimeProvider = clock);
            }));
        var userId = await CreateUserAsync(factory, UserName, StaffRole.User);
        _ = await CreateUserAsync(factory, "identity-cookie-download-administrator", StaffRole.Administrator);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        await SignInAsync(client);
        clock.Advance(TimeSpan.FromSeconds(1));

        var route = CurrentThumbnailRoute();
        var expectedETag = $"\"{Sha256}-{CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, null)}\"";
        using var initial = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        Assert.Equal(Thumbnail, await initial.Content.ReadAsByteArrayAsync());
        Assert.True(initial.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromDays(7), initial.Headers.CacheControl?.MaxAge);
        Assert.Equal(expectedETag, initial.Headers.ETag?.Tag);
        Assert.False(initial.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Equal(1, ports.PreviewReads);
        Assert.Equal(1, ports.PreparationReads);
        Assert.Equal(1, ports.ThumbnailReads);

        using var revalidationRequest = new HttpRequestMessage(HttpMethod.Get, route);
        revalidationRequest.Headers.TryAddWithoutValidation("If-None-Match", expectedETag);
        using var revalidated = await client.SendAsync(revalidationRequest);
        Assert.Equal(HttpStatusCode.NotModified, revalidated.StatusCode);
        Assert.True(revalidated.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromDays(7), revalidated.Headers.CacheControl?.MaxAge);
        Assert.Equal(expectedETag, revalidated.Headers.ETag?.Tag);
        Assert.False(revalidated.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Equal(2, ports.PreviewReads);
        Assert.Equal(2, ports.PreparationReads);
        Assert.Equal(1, ports.ThumbnailReads);

        await RevokeWithAdministrationAsync(factory, userId, revocation);
        using var revokedRequest = new HttpRequestMessage(HttpMethod.Get, route);
        revokedRequest.Headers.TryAddWithoutValidation("If-None-Match", expectedETag);
        using var revoked = await client.SendAsync(revokedRequest);
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        Assert.True(revoked.Headers.CacheControl?.NoStore);
        Assert.Equal(2, ports.PreviewReads);
        Assert.Equal(2, ports.PreparationReads);
        Assert.Equal(1, ports.ThumbnailReads);
    }

    private static string CurrentThumbnailRoute() =>
        $"/Cases/{CaseId:D}/Documents/{OccurrenceId:D}/Download?versionId={VersionId:D}" +
        $"&inline=true&size={CaseDocumentThumbnails.ThumbSizeToken}&prep=0" +
        $"&renderer={CaseDocumentThumbnails.RendererIdentity}";

    private static void Substitute<T>(IServiceCollection services, T instance)
        where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(instance);
    }

    private static async Task<Guid> CreateUserAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        StaffRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        await context.Database.MigrateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = true,
            MustChangePassword = false,
            LockoutEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        Assert.True((await userManager.AddToRoleAsync(user, role.ToString())).Succeeded);
        return user.Id;
    }

    private static async Task RevokeWithAdministrationAsync(
        WebApplicationFactory<Program> factory,
        Guid staffId,
        ThumbnailRevocation revocation)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<PegasusDbContext>();
        var target = await context.Users.AsNoTracking().SingleAsync(item => item.Id == staffId);
        var administrator = await context.Users.AsNoTracking().SingleAsync(
            item => item.UserName == "identity-cookie-download-administrator");
        var actor = ActionActor.Staff(administrator.Id, [StaffRole.Administrator]);
        var operationKey = "identity-cookie-" + revocation.ToString().ToLowerInvariant();

        switch (revocation)
        {
            case ThumbnailRevocation.Disabled:
                await services.GetRequiredService<IDisableStaffAccount>().ExecuteAsync(
                    new(actor, staffId, null, operationKey, target.Version), default);
                break;

            case ThumbnailRevocation.Deleted:
                await services.GetRequiredService<IDeleteStaffAccount>().ExecuteAsync(
                    new(actor, staffId, null, operationKey, target.Version), default);
                break;

            case ThumbnailRevocation.RoleChanged:
                await services.GetRequiredService<IUpdateStaffAccountSettings>().ExecuteAsync(
                    new(
                        actor,
                        staffId,
                        StaffRole.Engineer,
                        IsSignOffEngineer: false,
                        PrintedName: null,
                        Qualifications: null,
                        Signature: null,
                        IsDefaultSignOffEngineer: false,
                        OperationKey: operationKey,
                        ExpectedVersion: target.Version),
                    default);
                break;

            case ThumbnailRevocation.PasswordReset:
                await services.GetRequiredService<IResetStaffPassword>().ExecuteAsync(
                    new(actor, staffId, null, operationKey, target.Version), default);
                break;

            case ThumbnailRevocation.ForcedLogout:
                await services.GetRequiredService<IForceStaffLogout>().ExecuteAsync(
                    new(actor, staffId, null, operationKey, target.Version), default);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(revocation), revocation, null);
        }
    }

    private static async Task SignInAsync(HttpClient client)
    {
        using var signInPage = await client.GetAsync("/Account/SignIn");
        signInPage.EnsureSuccessStatusCode();
        var signInHtml = await signInPage.Content.ReadAsStringAsync();
        using var signIn = await client.PostAsync(
            "/Account/SignIn",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ReadAntiforgeryToken(signInHtml),
                ["UserName"] = UserName,
                ["Password"] = Password,
                ["ReturnUrl"] = "/"
            }));
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
    }

    private static string ReadAntiforgeryToken(string html)
    {
        var tokenTag = AntiforgeryTagRegex().Match(html);
        Assert.True(tokenTag.Success, "The sign-in form must render an antiforgery token.");
        var tokenValue = InputValueRegex().Match(tokenTag.Value);
        Assert.True(tokenValue.Success, "The sign-in antiforgery token must have a value.");
        return WebUtility.HtmlDecode(tokenValue.Groups["value"].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputValueRegex();

    private sealed class ThumbnailPorts :
        IReadCaseDocumentPreview,
        IReadCaseDocumentThumbnail,
        ICaseAssetPreparationQueries
    {
        private int previewReads;
        private int thumbnailReads;
        private int preparationReads;

        public int PreviewReads => Volatile.Read(ref previewReads);

        public int ThumbnailReads => Volatile.Read(ref thumbnailReads);

        public int PreparationReads => Volatile.Read(ref preparationReads);

        Task<CaseDocumentPreview?> IReadCaseDocumentPreview.ExecuteAsync(
            CaseDocumentPreviewQuery query,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref previewReads);
            Assert.Equal(CaseId, query.CaseId);
            Assert.Equal(OccurrenceId, query.OccurrenceId);
            Assert.Equal(VersionId, query.VersionId);
            return Task.FromResult<CaseDocumentPreview?>(new(
                CaseId,
                OccurrenceId,
                DocumentId,
                VersionId,
                "evidence.jpg",
                MediaType,
                Thumbnail.LongLength,
                Sha256,
                DocumentCustodyStatus.Confirmed));
        }

        Task<CaseDocumentThumbnail?> IReadCaseDocumentThumbnail.OpenAsync(
            CaseDocumentThumbnailRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref thumbnailReads);
            Assert.Equal(CaseId, request.CaseId);
            Assert.Equal(DocumentId, request.DocumentId);
            Assert.Equal(VersionId, request.VersionId);
            Assert.Equal(Sha256, request.Sha256);
            return Task.FromResult<CaseDocumentThumbnail?>(new(
                new MemoryStream(Thumbnail, writable: false),
                CaseDocumentThumbnails.MediaType,
                Thumbnail.LongLength,
                Sha256));
        }

        Task<CaseAssetPreparation?> ICaseAssetPreparationQueries.GetForOccurrenceAsync(
            Guid caseId,
            Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref preparationReads);
            Assert.Equal(CaseId, caseId);
            Assert.Equal(OccurrenceId, occurrenceId);
            return Task.FromResult<CaseAssetPreparation?>(null);
        }

        Task<IReadOnlyList<CaseAssetPreparation>> ICaseAssetPreparationQueries.ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(CaseId, caseId);
            return Task.FromResult<IReadOnlyList<CaseAssetPreparation>>([]);
        }
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan value) => current = current.Add(value);
    }

    public enum ThumbnailRevocation
    {
        Disabled,
        Deleted,
        RoleChanged,
        PasswordReset,
        ForcedLogout
    }
}
