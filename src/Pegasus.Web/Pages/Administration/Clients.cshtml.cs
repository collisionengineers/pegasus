using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ClientsModel(
    IRegisterPublicMcpClient registerPublicMcpClient,
    IRevokePublicMcpClient revokePublicMcpClient)
    : AdministrationPageModel
{
    public RegisterPublicClientInput Registration { get; private set; } =
        new() { OperationKey = NewOperationKey() };

    public RevokePublicClientInput Revocation { get; private set; } =
        new() { OperationKey = NewOperationKey() };

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostRegisterAsync(
        [Bind(Prefix = nameof(Registration))] RegisterPublicClientInput registration,
        CancellationToken cancellationToken)
    {
        Registration = registration;
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!IsOperationKeyValid(registration.OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form is no longer valid. Retry from the current page.");
        }

        var redirectUris = ParseRedirectUris(registration.RedirectUris);
        if (!ModelState.IsValid)
        {
            RefreshOperationKeys();
            return Page();
        }

        try
        {
            var result = await registerPublicMcpClient.ExecuteAsync(
                new(
                    actor,
                    new(
                        registration.ClientId,
                        registration.DisplayName,
                        redirectUris,
                        new Uri(registration.Resource, UriKind.Absolute),
                        registration.SelectedScopes),
                    registration.Reason,
                    registration.OperationKey),
                cancellationToken);
            TempData["AdministrationStatus"] = result.WasReplay
                ? "The public MCP client registration was already recorded."
                : "The public MCP client was registered without a client secret.";
            return RedirectToPage();
        }
        catch (UriFormatException)
        {
            ModelState.AddModelError(
                $"{nameof(Registration)}.{nameof(Registration.Resource)}",
                "Enter an absolute HTTPS /mcp resource URI.");
        }
        catch (AuthenticationClientAdministrationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Error switch
            {
                AuthenticationClientAdministrationError.ClientMetadataConflict =>
                    "That client ID already has different metadata.",
                AuthenticationClientAdministrationError.OperationConflict =>
                    "The form was already used for a different operation. Retry from the current page.",
                _ => "The public MCP client registration was not accepted."
            });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        RefreshOperationKeys();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(
        [Bind(Prefix = nameof(Revocation))] RevokePublicClientInput revocation,
        CancellationToken cancellationToken)
    {
        Revocation = revocation;
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!IsOperationKeyValid(revocation.OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form is no longer valid. Retry from the current page.");
        }

        if (!ModelState.IsValid)
        {
            RefreshOperationKeys();
            return Page();
        }

        try
        {
            var result = await revokePublicMcpClient.ExecuteAsync(
                new(
                    actor,
                    revocation.ClientId,
                    revocation.Reason,
                    revocation.OperationKey),
                cancellationToken);
            TempData["AdministrationStatus"] = result.WasReplay
                ? "The public MCP client revocation was already recorded."
                : "The public MCP client and its active grants were revoked.";
            return RedirectToPage();
        }
        catch (AuthenticationClientAdministrationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Error switch
            {
                AuthenticationClientAdministrationError.ClientNotFound =>
                    "The public MCP client no longer exists.",
                AuthenticationClientAdministrationError.OperationConflict =>
                    "The form was already used for a different operation. Retry from the current page.",
                _ => "The public MCP client revocation was not accepted."
            });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        RefreshOperationKeys();
        return Page();
    }

    private List<Uri> ParseRedirectUris(string value)
    {
        var values = value.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length is < 1 or > PublicMcpClientPolicy.MaximumRedirectUriCount)
        {
            ModelState.AddModelError(
                $"{nameof(Registration)}.{nameof(Registration.RedirectUris)}",
                $"Enter between 1 and {PublicMcpClientPolicy.MaximumRedirectUriCount} redirect URIs, one per line.");
            return [];
        }

        var parsed = new List<Uri>(values.Length);
        foreach (var redirectUri in values)
        {
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            {
                ModelState.AddModelError(
                    $"{nameof(Registration)}.{nameof(Registration.RedirectUris)}",
                    "Every redirect URI must be absolute.");
                return [];
            }

            parsed.Add(uri);
        }

        return parsed;
    }

    private void RefreshOperationKeys()
    {
        Registration = Registration with { OperationKey = NewOperationKey() };
        Revocation = Revocation with { OperationKey = NewOperationKey() };
        ModelState.Remove($"{nameof(Registration)}.{nameof(Registration.OperationKey)}");
        ModelState.Remove($"{nameof(Revocation)}.{nameof(Revocation.OperationKey)}");
    }

    public sealed record RegisterPublicClientInput
    {
        [Required, StringLength(PublicMcpClientPolicy.MaximumClientIdLength)]
        public string ClientId { get; init; } = string.Empty;

        [Required, StringLength(PublicMcpClientPolicy.MaximumDisplayNameLength)]
        public string DisplayName { get; init; } = string.Empty;

        [Required, StringLength(20_480)]
        public string RedirectUris { get; init; } = string.Empty;

        [Required, StringLength(2_048)]
        public string Resource { get; init; } = string.Empty;

        public string[] SelectedScopes { get; init; } = [];

        [Required, StringLength(
            StaffAccountAdministrationPolicy.MaximumReasonLength,
            MinimumLength = 1)]
        public string Reason { get; init; } = string.Empty;

        public string OperationKey { get; init; } = string.Empty;
    }

    public sealed record RevokePublicClientInput
    {
        [Required, StringLength(PublicMcpClientPolicy.MaximumClientIdLength)]
        public string ClientId { get; init; } = string.Empty;

        [Required, StringLength(
            StaffAccountAdministrationPolicy.MaximumReasonLength,
            MinimumLength = 1)]
        public string Reason { get; init; } = string.Empty;

        public string OperationKey { get; init; } = string.Empty;
    }
}
