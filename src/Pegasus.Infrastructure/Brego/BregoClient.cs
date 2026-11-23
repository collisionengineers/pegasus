using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Brego's platform API as the Brego web application calls it (ADR-0084):
/// authorizer discovery, AWS Cognito token renewal from the stored refresh
/// token (whose ID token names the tenant organisation and slug and the
/// Cognito username), browser device-approval verification, BFF session
/// exchange, VRM vehicle lookup, point-in-time valuation, derivative option
/// configuration, and PDF valuation report rendering. Cognito tokens and
/// session cookies travel only in request bodies and headers, never in a URL
/// or log.
/// </summary>
internal sealed class BregoClient(HttpClient http)
{
    internal static readonly Uri LoginBaseUri = new("https://www.brego.ai/");
    internal static readonly Uri AuthorizerUri = new(LoginBaseUri, "login/api/getAuthorizer");
    internal static readonly Uri DeviceChallengeUri = new(LoginBaseUri, "login/api/brego/users/me/device-approval/challenge");
    internal static readonly Uri SessionsUri = new(LoginBaseUri, "login/api/brego/v1/sessions");
    internal static readonly Uri AssetsBaseUri = new("https://assets.brego.io/");

    /// <summary>
    /// Pre-approved browser device identifier for Collision Engineers' dedicated
    /// Brego valuation account, avoiding interactive email OTP challenges.
    /// </summary>
    internal const string DefaultDeviceId = "c1e1ff9f-5081-4eb6-a94f-2567cfd88e8b";

    /// <summary>How long one JSON API or asset call may take.</summary>
    internal static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long option-configuration loading or Headless Chrome PDF rendering may take.</summary>
    internal static readonly TimeSpan PdfCallTimeout = TimeSpan.FromSeconds(120);

    private const string JsonMediaType = "application/json";
    private const string CognitoJsonMediaType = "application/x-amz-json-1.1";
    private const string GetTokensFromRefreshTokenTarget = "AWSCognitoIdentityProviderService.GetTokensFromRefreshToken";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/156.0.0.0";

    /// <summary>
    /// Renews the account's 24-hour Cognito tokens from the stored Cognito
    /// refresh token (<c>GetTokensFromRefreshToken</c>) and either pairs them
    /// with an existing 1-year <c>brego-session</c> cookie or, when no
    /// <c>brego-session</c> cookie is supplied, completes browser
    /// device-approval verification and exchanges the tokens at
    /// <c>POST /login/api/brego/v1/sessions</c> for a new 1-year
    /// <c>brego-session</c> cookie. The account signs in to Brego by e-mailed
    /// magic link only, so it is linked once by
    /// <c>scripts/Connect-BregoAccount.ps1</c>, which stores the first refresh
    /// token (ADR-0084). The session carries forward the refresh token Cognito
    /// answers with, or the one supplied when Cognito does not rotate it.
    /// </summary>
    public async Task<BregoSession> SignInAsync(
        string email,
        string refreshToken,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        string? existingBregoSessionCookie = null)
    {
        string region;
        string clientId;
        using (var authorizers = await ExchangeJsonAsync(
            "authorizer",
            JsonRequest(HttpMethod.Post, AuthorizerUri, new { email, application = (string?)null }),
            CallTimeout,
            cancellationToken))
        {
            var first = authorizers.RootElement is { ValueKind: JsonValueKind.Array } array && array.GetArrayLength() > 0
                ? array[0]
                : (JsonElement?)null;
            var authorizer = BregoJson.String(first, "authorizer") ?? "";
            clientId = BregoJson.String(first, "authorizerId") ?? "";
            var separator = authorizer.IndexOf('_');
            if (string.IsNullOrWhiteSpace(clientId)
                || separator <= 0
                || separator == authorizer.Length - 1)
            {
                throw new BregoFailure("authorizer", null, "authorizer=absent");
            }

            region = authorizer[..separator];
            if (!IsSafeHostLabel(region))
            {
                throw new BregoFailure("authorizer", null, "authorizer=absent");
            }
        }

        var cognitoUri = new Uri($"https://cognito-idp.{region}.amazonaws.com/");
        string accessToken;
        string idToken;
        int expiresIn;
        using (var refreshed = await ExchangeJsonAsync(
            "cognito_refresh",
            CognitoRequest(
                cognitoUri,
                GetTokensFromRefreshTokenTarget,
                new
                {
                    ClientId = clientId,
                    RefreshToken = refreshToken,
                }),
            CallTimeout,
            cancellationToken))
        {
            var result = BregoJson.Get(refreshed.RootElement, "AuthenticationResult");
            accessToken = BregoJson.String(result, "AccessToken") ?? "";
            idToken = BregoJson.String(result, "IdToken") ?? "";
            expiresIn = BregoJson.Int32(result, "ExpiresIn") is { } ttl && ttl > 0 ? ttl : 86400;
            if (string.IsNullOrWhiteSpace(accessToken)
                || string.IsNullOrWhiteSpace(idToken))
            {
                throw new BregoFailure("cognito_refresh", null, "tokens=absent");
            }

            // The pool does not rotate refresh tokens today; if it starts
            // to, the rotated token replaces the stored one.
            if (BregoJson.String(result, "RefreshToken") is { } rotated && !string.IsNullOrWhiteSpace(rotated))
            {
                refreshToken = rotated;
            }
        }

        // Brego's authorizer answer stopped naming the tenant in November
        // 2026; the ID token Cognito issues names it instead, and names the
        // Cognito username the session cookies are keyed by.
        var (organizationId, slug, username) = ReadIdTokenClaims(idToken);
        if (string.IsNullOrWhiteSpace(organizationId) || !IsSafeHostLabel(slug))
        {
            throw new BregoFailure("cognito_refresh", null, "tenant=absent");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new BregoFailure("cognito_refresh", null, "username=absent");
        }

        var expiresAt = timeProvider.GetUtcNow().AddSeconds(Math.Max(60, expiresIn - 300));
        if (!string.IsNullOrWhiteSpace(existingBregoSessionCookie))
        {
            var reusedCookie = existingBregoSessionCookie.Trim();
            var reusedCookieHeader =
                $"CognitoIdentityServiceProvider.{clientId}.LastAuthUser={username}; " +
                $"CognitoIdentityServiceProvider.{clientId}.{username}.accessToken={accessToken}; " +
                $"CognitoIdentityServiceProvider.{clientId}.{username}.idToken={idToken}; " +
                $"CognitoIdentityServiceProvider.{clientId}.{username}.refreshToken={refreshToken}; " +
                $"brego-session={reusedCookie}";
            return new BregoSession(
                email,
                refreshToken,
                slug,
                organizationId,
                reusedCookie,
                reusedCookieHeader,
                expiresAt,
                ReusedSessionCookie: true);
        }

        using (var challenge = await ExchangeJsonAsync(
            "device_approval",
            BootstrapRequest(DeviceChallengeUri, accessToken, new { deviceId = DefaultDeviceId }),
            CallTimeout,
            cancellationToken))
        {
            if (string.Equals(
                BregoJson.String(challenge.RootElement, "status"),
                "challenge_required",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new BregoFailure("device_approval", null, "device=challenge_required");
            }
        }

        var (sessionDoc, bregoSessionCookie) = await ExchangeSessionAsync(
            BootstrapRequest(
                SessionsUri,
                accessToken,
                new
                {
                    accessToken,
                    idToken,
                    refreshToken,
                    client = slug,
                    deviceId = DefaultDeviceId,
                }),
            cancellationToken);
        using (sessionDoc)
        {
            if (string.IsNullOrWhiteSpace(bregoSessionCookie))
            {
                throw new BregoFailure("session", null, "cookie=absent");
            }

            var resolvedOrgId = BregoJson.String(sessionDoc.RootElement, "organizationId") is { Length: > 0 } orgId
                ? orgId
                : organizationId;
            var cookieHeader =
                $"CognitoIdentityServiceProvider.{clientId}.LastAuthUser={username}; " +
                $"CognitoIdentityServiceProvider.{clientId}.{username}.accessToken={accessToken}; " +
                $"CognitoIdentityServiceProvider.{clientId}.{username}.idToken={idToken}; " +
                $"CognitoIdentityServiceProvider.{clientId}.{username}.refreshToken={refreshToken}; " +
                $"brego-session={bregoSessionCookie}";
            return new BregoSession(
                email,
                refreshToken,
                slug,
                resolvedOrgId,
                bregoSessionCookie,
                cookieHeader,
                expiresAt,
                ReusedSessionCookie: false);
        }
    }

    /// <summary>Looks up the vehicle specification and national mileage metadata by VRM.</summary>
    public async Task<BregoVehicle> LookupAsync(
        BregoSession session,
        string vrm,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            $"https://{session.Slug}.brego.ai/platform/api/brego/vehicles/vrm/{Uri.EscapeDataString(vrm.ToLowerInvariant())}?countryCode=gb&expand=sources&expand=metadata");
        using var answer = await ExchangeJsonAsync(
            "lookup",
            PlatformRequest(HttpMethod.Get, uri, session.CookieHeader),
            CallTimeout,
            cancellationToken);
        var root = answer.RootElement;
        var derivativeId = BregoJson.String(root, "derivativeId");
        if (string.IsNullOrWhiteSpace(derivativeId))
        {
            throw new BregoFailure("lookup", null, "derivative=absent");
        }

        var resolvedVrm = BregoJson.String(root, "vrm") is { Length: > 0 } plate ? plate : vrm;
        var vin = BregoJson.String(root, "vin");
        var manufacturerUrl = BregoJson.String(root, "manufacturerUrl");
        var rangeUrl = BregoJson.String(root, "rangeUrl");
        var year = BregoJson.Int32(root, "year");
        var color = BregoJson.String(root, "color");
        var rawRegistrationDate = BregoJson.String(root, "registrationDate");
        var registrationDate = rawRegistrationDate is { Length: >= 10 } ? rawRegistrationDate[..10] : null;

        var previousOwners = 0;
        foreach (var change in BregoJson.Items(root, "previousOwnerChanges"))
        {
            if (BregoJson.Int32(change, "previousOwnerCount") is { } count && count > previousOwners)
            {
                previousOwners = count;
            }
        }

        if (previousOwners <= 0)
        {
            previousOwners = 1;
        }

        var averageMonthlyMileage = 621;
        foreach (var meta in BregoJson.Items(root, "metadata"))
        {
            if (string.Equals(BregoJson.String(meta, "region"), "national", StringComparison.OrdinalIgnoreCase)
                && BregoJson.Int32(meta, "averageMileageMonthly") is { } monthly
                && monthly > 0)
            {
                averageMonthlyMileage = monthly;
                break;
            }
        }

        return new BregoVehicle(
            resolvedVrm,
            vin,
            derivativeId,
            manufacturerUrl,
            rangeUrl,
            year,
            color,
            registrationDate,
            previousOwners,
            averageMonthlyMileage,
            averageMonthlyMileage * 12);
    }

    /// <summary>Queries the point-in-time valuation and answers <c>retail.high</c> and <c>trade.high</c>.</summary>
    public async Task<BregoFigures> ValueAsync(
        BregoSession session,
        BregoVehicle vehicle,
        DateOnly valuationDate,
        long miles,
        CancellationToken cancellationToken)
    {
        var hasVin = !string.IsNullOrWhiteSpace(vehicle.Vin);
        var targetType = hasVin ? "vin" : "vrm";
        var targetId = Uri.EscapeDataString((hasVin ? vehicle.Vin! : vehicle.Vrm).ToLowerInvariant());
        var query = new StringBuilder(256);
        query.Append(
            CultureInfo.InvariantCulture,
            $"countryCode=gb&dates={valuationDate:yyyy-MM-dd}&period=month&futureMonthlyMileage={vehicle.AverageMonthlyMileage}&standardOptionsValue=0&fullOptionsValue=0&significantOptionsValue=0&excludeTax=false&excludeUserHistory=false&mileage={miles}");
        if (!string.IsNullOrWhiteSpace(vehicle.Color))
        {
            query.Append("&color=").Append(Uri.EscapeDataString(vehicle.Color));
        }

        if (vehicle.Year is { } year)
        {
            query.Append(CultureInfo.InvariantCulture, $"&year={year}");
        }

        if (vehicle.PreviousOwners > 0)
        {
            query.Append(CultureInfo.InvariantCulture, $"&previousOwners={vehicle.PreviousOwners}");
        }

        if (!string.IsNullOrWhiteSpace(vehicle.RegistrationDate))
        {
            query.Append("&registrationDate=").Append(Uri.EscapeDataString(vehicle.RegistrationDate));
        }

        var uri = new Uri(
            $"https://{session.Slug}.brego.ai/platform/api/brego/vehicles/{targetType}/{targetId}/valuations?{query}");
        using var answer = await ExchangeJsonAsync(
            "valuation",
            PlatformRequest(HttpMethod.Get, uri, session.CookieHeader),
            CallTimeout,
            cancellationToken);
        var first = BregoJson.First(answer.RootElement, "items");
        if (BregoJson.Decimal(first, "retail", "high") is not { } retail || retail <= 0
            || BregoJson.Decimal(first, "trade", "high") is not { } trade || trade <= 0)
        {
            throw new BregoFailure("valuation", null, "figures=absent");
        }

        return new BregoFigures(retail, trade);
    }

    /// <summary>Loads the derivative's factory option configuration and selects default options effective on registration date.</summary>
    public async Task<BregoOptionConfiguration> GetOptionConfigurationAsync(
        BregoSession session,
        BregoVehicle vehicle,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            $"https://{session.Slug}.brego.ai/platform/api/brego/vehicles/derivativeId/{Uri.EscapeDataString(vehicle.DerivativeId)}/option-configuration?countryCode=gb");
        using var answer = await ExchangeJsonAsync(
            "options",
            PlatformRequest(HttpMethod.Get, uri, session.CookieHeader),
            PdfCallTimeout,
            cancellationToken);
        var root = answer.RootElement;
        var optionsHash = BregoJson.String(root, "optionsHash") ?? "";
        var selected = new List<string>();
        foreach (var option in BregoJson.Items(root, "options"))
        {
            if (BregoJson.Boolean(option, "default") == true
                && BregoJson.String(option, "optionId") is { Length: > 0 } optionId
                && IsOptionEffective(option, vehicle.RegistrationDate))
            {
                selected.Add(optionId);
            }
        }

        return new BregoOptionConfiguration(optionsHash, selected);
    }

    /// <summary>Renders the Brego Valuation Report PDF via <c>POST /platform/api/pdf/{organizationId}</c>.</summary>
    public async Task<byte[]> CreateValuationPdfAsync(
        BregoSession session,
        BregoVehicle vehicle,
        long miles,
        BregoOptionConfiguration options,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            $"https://{session.Slug}.brego.ai/platform/api/pdf/{Uri.EscapeDataString(session.OrganizationId)}");
        var registrationDate = vehicle.RegistrationDate ?? "";
        var payload = new
        {
            manufacturerUrl = vehicle.ManufacturerUrl,
            rangeUrl = vehicle.RangeUrl,
            make = vehicle.ManufacturerUrl,
            model = vehicle.RangeUrl,
            reportType = "valuation",
            derivativeId = vehicle.DerivativeId,
            year = vehicle.Year,
            color = vehicle.Color,
            mileage = miles,
            newCar = false,
            previousOwners = vehicle.PreviousOwners,
            annualMileage = vehicle.AnnualMileage,
            featuredMonthNav = "high",
            vin = vehicle.Vin,
            fallbackVrm = vehicle.Vrm,
            registrationDate,
            selectedOptions = options.SelectedOptionIds,
            optionSelectionInput = new
            {
                optionConfigurationHash = options.OptionsHash,
                optionEffectiveDate = registrationDate,
                requestedOptionIds = Array.Empty<string>(),
            },
            customOptions = Array.Empty<object>(),
            variableCosts = Array.Empty<object>(),
            countryCode = "gb",
            exVatSelected = false,
            totalOptionsValue = 0,
            totalVariableCostsValue = 0,
            showPdfReportImages = true,
        };
        var bytes = await ExchangeBytesAsync(
            "pdf",
            PlatformRequest(HttpMethod.Post, uri, session.CookieHeader, payload),
            PdfCallTimeout,
            cancellationToken);
        if (bytes.Length == 0)
        {
            throw new BregoFailure("pdf", null, "pdf=absent");
        }

        return bytes;
    }

    /// <summary>
    /// Fetches a studio stock image (<c>front</c> or <c>side</c>) from <c>assets.brego.io</c>.
    /// Any error answers <see langword="null"/> so PDF filing never fails on an image fallback.
    /// </summary>
    public async Task<byte[]?> TryFetchStockImageAsync(
        BregoVehicle vehicle,
        string view,
        CancellationToken cancellationToken)
    {
        var year = vehicle.Year?.ToString(CultureInfo.InvariantCulture) ?? "";
        var color = Uri.EscapeDataString(vehicle.Color ?? "");
        var uri = new Uri(
            AssetsBaseUri,
            $"vehicle/images/stock/gb/{Uri.EscapeDataString(vehicle.DerivativeId)}/?year={year}&color={color}&view={Uri.EscapeDataString(view)}");
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        message.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(CallTimeout);
        try
        {
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, bounded.Token);
            if (!Succeeded(response.StatusCode))
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(bounded.Token);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static bool TryDecodeBase64(string text, out byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            bytes = [];
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(text);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static bool IsOptionEffective(JsonElement option, string? registrationDate)
    {
        if (string.IsNullOrWhiteSpace(registrationDate))
        {
            return true;
        }

        if (BregoJson.String(option, "dateEffectiveFrom") is { Length: >= 10 } from
            && string.CompareOrdinal(registrationDate, from[..10]) < 0)
        {
            return false;
        }

        if (BregoJson.String(option, "dateEffectiveTo") is { Length: >= 10 } to
            && string.CompareOrdinal(registrationDate, to[..10]) > 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// The tenant organisation and slug an ID token names in its
    /// <c>organizationId</c> and <c>slug</c> claims, and the Cognito username
    /// in its <c>cognito:username</c> claim; each is empty when the token does
    /// not name it. The token is read, not verified: Brego verifies it at
    /// every call it is presented to, and the slug is used only as a host
    /// label under <c>brego.ai</c>.
    /// </summary>
    internal static (string OrganizationId, string Slug, string Username) ReadIdTokenClaims(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length != 3)
        {
            return ("", "", "");
        }

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        if (!TryDecodeBase64(payload, out var bytes))
        {
            return ("", "", "");
        }

        try
        {
            using var claims = JsonDocument.Parse(bytes);
            return (
                BregoJson.String(claims.RootElement, "organizationId") ?? "",
                BregoJson.String(claims.RootElement, "slug") ?? "",
                BregoJson.String(claims.RootElement, "cognito:username") ?? "");
        }
        catch (JsonException)
        {
            return ("", "", "");
        }
    }

    private static bool IsSafeHostLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, Uri uri, object? body = null)
    {
        var message = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            message.Content = JsonContent.Create(body);
        }

        message.Headers.TryAddWithoutValidation("Accept", JsonMediaType);
        message.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        return message;
    }

    private static HttpRequestMessage CognitoRequest(Uri uri, string target, object body)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, CognitoJsonMediaType),
        };
        message.Headers.TryAddWithoutValidation("X-Amz-Target", target);
        message.Headers.TryAddWithoutValidation("Accept", JsonMediaType);
        message.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        return message;
    }

    private static HttpRequestMessage BootstrapRequest(Uri uri, string accessToken, object body)
    {
        var message = JsonRequest(HttpMethod.Post, uri, body);
        message.Headers.TryAddWithoutValidation("X-Brego-Bootstrap-Access-Token", accessToken);
        return message;
    }

    private static HttpRequestMessage PlatformRequest(HttpMethod method, Uri uri, string cookieHeader, object? body = null)
    {
        var message = JsonRequest(method, uri, body);
        message.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        return message;
    }

    private async Task<JsonDocument> ExchangeJsonAsync(
        string stage,
        HttpRequestMessage message,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using (message)
        using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            bounded.CancelAfter(timeout);
            try
            {
                using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, bounded.Token);
                if (!Succeeded(response.StatusCode))
                {
                    var detail = ErrorDetail(await response.Content.ReadAsStringAsync(bounded.Token), out var forbidden);
                    throw new BregoFailure(
                        stage,
                        response.StatusCode,
                        $"status={(int)response.StatusCode} {detail}",
                        forbidden);
                }

                await using var content = await response.Content.ReadAsStreamAsync(bounded.Token);
                return await JsonDocument.ParseAsync(content, cancellationToken: bounded.Token);
            }
            catch (JsonException)
            {
                throw new BregoFailure(stage, null, "json=invalid");
            }
            catch (IOException)
            {
                throw new BregoFailure(stage, null, "body=unreadable");
            }
            catch (HttpRequestException exception)
            {
                throw new BregoFailure(stage, exception);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new BregoFailure(stage, exception);
            }
        }
    }

    private async Task<(JsonDocument Document, string? SessionCookie)> ExchangeSessionAsync(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        using (message)
        using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            bounded.CancelAfter(CallTimeout);
            try
            {
                using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, bounded.Token);
                if (!Succeeded(response.StatusCode))
                {
                    var detail = ErrorDetail(await response.Content.ReadAsStringAsync(bounded.Token), out var forbidden);
                    throw new BregoFailure(
                        "session",
                        response.StatusCode,
                        $"status={(int)response.StatusCode} {detail}",
                        forbidden);
                }

                var sessionCookie = ExtractBregoSessionCookie(response);
                await using var content = await response.Content.ReadAsStreamAsync(bounded.Token);
                var document = await JsonDocument.ParseAsync(content, cancellationToken: bounded.Token);
                return (document, sessionCookie);
            }
            catch (JsonException)
            {
                throw new BregoFailure("session", null, "json=invalid");
            }
            catch (IOException)
            {
                throw new BregoFailure("session", null, "body=unreadable");
            }
            catch (HttpRequestException exception)
            {
                throw new BregoFailure("session", exception);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new BregoFailure("session", exception);
            }
        }
    }

    private async Task<byte[]> ExchangeBytesAsync(
        string stage,
        HttpRequestMessage message,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using (message)
        using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            bounded.CancelAfter(timeout);
            try
            {
                using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, bounded.Token);
                if (!Succeeded(response.StatusCode))
                {
                    var detail = ErrorDetail(await response.Content.ReadAsStringAsync(bounded.Token), out var forbidden);
                    throw new BregoFailure(
                        stage,
                        response.StatusCode,
                        $"status={(int)response.StatusCode} {detail}",
                        forbidden);
                }

                return await response.Content.ReadAsByteArrayAsync(bounded.Token);
            }
            catch (IOException)
            {
                throw new BregoFailure(stage, null, "body=unreadable");
            }
            catch (HttpRequestException exception)
            {
                throw new BregoFailure(stage, exception);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new BregoFailure(stage, exception);
            }
        }
    }

    private static string? ExtractBregoSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        const string prefix = "brego-session=";
        foreach (var header in values)
        {
            foreach (var segment in header.Split(';'))
            {
                var trimmed = segment.Trim();
                if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var cookieValue = trimmed[prefix.Length..];
                    if (cookieValue.Length > 0)
                    {
                        return cookieValue;
                    }
                }
            }
        }

        return null;
    }

    private static bool Succeeded(HttpStatusCode status) => (int)status is >= 200 and <= 299;

    private static string ErrorDetail(string body, out bool forbidden)
    {
        forbidden = false;
        var words = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var root = document.RootElement;
                if (root.TryGetProperty("__type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                {
                    var typeText = typeProp.GetString()!;
                    words.Add(typeText);
                    if (typeText.Contains("NotAuthorizedException", StringComparison.OrdinalIgnoreCase)
                        || typeText.Contains("UserNotFoundException", StringComparison.OrdinalIgnoreCase))
                    {
                        forbidden = true;
                    }
                }

                if (root.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String)
                {
                    words.Add(messageProp.GetString()!);
                }

                if (root.TryGetProperty("error", out var errorProp))
                {
                    if (errorProp.ValueKind == JsonValueKind.String)
                    {
                        words.Add(errorProp.GetString()!);
                    }
                    else if (errorProp.ValueKind == JsonValueKind.Object
                        && errorProp.TryGetProperty("message", out var nestedMessage)
                        && nestedMessage.ValueKind == JsonValueKind.String)
                    {
                        words.Add(nestedMessage.GetString()!);
                    }
                }
            }
        }
        catch (JsonException)
        {
            return "error=unreadable";
        }

        var detail = "error=" + string.Join("; ", words);
        return detail.Length <= 200 ? detail : detail[..200];
    }
}

/// <summary>
/// A signed-in Brego session. It carries the Cognito refresh token it was
/// renewed from (rotated or as supplied); its <c>ToString</c> prints none of
/// the tokens or cookies.
/// </summary>
internal sealed record BregoSession(
    string Email,
    string RefreshToken,
    string Slug,
    string OrganizationId,
    string BregoSessionCookie,
    string CookieHeader,
    DateTimeOffset ExpiresAtUtc,
    bool ReusedSessionCookie = false)
{
    public override string ToString() => nameof(BregoSession);
}

internal sealed record BregoVehicle(
    string Vrm,
    string? Vin,
    string DerivativeId,
    string? ManufacturerUrl,
    string? RangeUrl,
    int? Year,
    string? Color,
    string? RegistrationDate,
    int PreviousOwners,
    int AverageMonthlyMileage,
    int AnnualMileage);

internal readonly record struct BregoFigures(decimal Retail, decimal Trade);

internal sealed record BregoOptionConfiguration(
    string OptionsHash,
    IReadOnlyList<string> SelectedOptionIds);
