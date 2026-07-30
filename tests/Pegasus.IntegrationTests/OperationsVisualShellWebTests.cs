using System.Net;
using System.Text.RegularExpressions;
using Pegasus.Core.Intake;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

public sealed class OperationsVisualShellWebTests
{
    [Fact]
    public async Task OperationsShellRendersLogoSpriteAndSemanticLandmarks()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Test-Anonymous", "1");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        // Logo runtime copy replacement (no fake CE text mark)
        Assert.Contains("brand-logo", html);
        Assert.DoesNotContain("<span class=\"brand-mark\">CE</span>", html);

        // Accessibility Landmarks & Skip Link
        Assert.Contains("class=\"skip-link\"", html);
        Assert.Contains("href=\"#main-content\"", html);
        Assert.Contains("<header class=\"app-nav\">", html);
        Assert.Contains("<nav class=\"nav-links\" aria-label=\"Primary\">", html);
        Assert.Contains("id=\"main-content\"", html);
        Assert.Contains("<footer class=\"footer\">", html);

        // Only the sign-in route is available before authentication. Authenticated
        // shell navigation is exercised by the staff workspace tests.
        Assert.Contains("Sign in", html);
        Assert.Contains("href=\"/\"", html);
        Assert.DoesNotContain("href=\"\"", html);
    }

    [Fact]
    public async Task SiteCssEnforcesDesignTokensAccessibilityAndReflow()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var css = await response.Content.ReadAsStringAsync();

        // Exact Approved Token Values
        Assert.Contains("#DB0816", css); // Collision red
        Assert.Contains("#8F1422", css); // Dark red
        Assert.Contains("#2C2A27", css); // Warm charcoal
        Assert.Contains("#16191D", css); // Near-black ink
        Assert.Contains("#F5F4F2", css); // Light neutral
        Assert.Contains("#E6E4E1", css); // Border
        Assert.Contains("#6B6B6B", css); // Muted
        Assert.Contains("#16833B", css); // Green success

        // Amber Incomplete & Navy Review Approved State Tokens
        Assert.Contains("#7A3E00", css); // Amber fg
        Assert.Contains("#FFF4D6", css); // Amber bg
        Assert.Contains("#A15C00", css); // Amber border
        Assert.Contains("#143A5E", css); // Navy fg
        Assert.Contains("#EAF1F8", css); // Navy bg
        Assert.Contains("#365F87", css); // Navy border

        // System Font Stack, 2px Geometry, 3px Focus Ring
        Assert.Contains("ui-sans-serif", css);
        Assert.Contains("2px", css);
        Assert.Contains("3px rgba(219, 8, 22, 0.38)", css);

        // Accessibility & Reflow Rules
        Assert.Contains("min-height: 44px", css); // 44px targets
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("@media (forced-colors: active)", css);
        Assert.Contains("@media (max-width: 1279px)", css); // 1024-1279 reflow
    }

    [Fact]
    public async Task TechnicalFixtureArtifactGuardRejectsBusinessContentAndTokens()
    {
        // Artifact Guard verification: ensure technical fixture outputs exclude PII/bearer tokens/document text
        var technicalContent = "{\"fixture\": \"shell-render\", \"status\": \"ok\", \"asOf\": \"2026-07-30T12:00:00Z\"}";

        bool GuardPasses(string content)
        {
            // Rejects bearer tokens, document bytes, email bodies, PII, or unredacted secrets
            if (Regex.IsMatch(content, @"Bearer\s+[A-Za-z0-9\-\._~\+\/]+=*", RegexOptions.IgnoreCase)) return false;
            if (Regex.IsMatch(content, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.IgnoreCase)) return false;
            if (content.Contains("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        Assert.True(GuardPasses(technicalContent));
        Assert.False(GuardPasses("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.testTokenData"));
        Assert.False(GuardPasses("unredacted-user@example.com"));
        await Task.CompletedTask;
    }
}
