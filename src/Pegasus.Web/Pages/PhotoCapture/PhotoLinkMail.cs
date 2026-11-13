using Microsoft.Extensions.Options;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.PhotoCapture;
using Pegasus.Infrastructure.Email;
using Pegasus.Web.PhotoCapture;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// The photo link e-mail (FRD-28 The photo link e-mail; R773), composed the
/// way the Case's Send chaser composes a staff send: the Graph staff send from
/// an Outgoing approved mailbox (<see cref="ResolveCaseMailSender"/>), the
/// Case as its context at the Case's current version, and the operation key
/// the form carried. It is always a new message to the customer, never a
/// reply in the instruction e-mail's thread, which is the Principal's.
/// </summary>
public sealed class PhotoLinkMail(
    IStaffMailSend staffMailSend,
    IGetCaseHeader getCaseHeader,
    ResolveCaseMailSender senders,
    RenderEmailTemplate renderTemplate,
    IOptions<PhotoCaptureOptions> options)
{
    /// <summary>Whether a staff send is composed in this deployment at all.</summary>
    public bool Available => staffMailSend is not UnavailableStaffMailSend;

    /// <summary>The link a raw token opens, on Pegasus's own hostname (R779).</summary>
    public string CaptureUrl(CaptureLinkSecret token) =>
        PhotoLinkUrls.CaptureUrl(options.Value.PublicOrigin, token);

    /// <summary>A standing link's address (<c>/s/{slug}</c>).</summary>
    public string StandingUrl(string slug) => StandingUrl(options.Value, slug);

    /// <inheritdoc cref="StandingUrl(string)"/>
    public static string StandingUrl(PhotoCaptureOptions options, string slug)
    {
        ArgumentNullException.ThrowIfNull(options);
        return PhotoLinkUrls.StandingUrl(options.PublicOrigin, slug);
    }

    /// <summary>The mailboxes staff may send from for this Case, starting on its default sender.</summary>
    public Task<CaseMailSenderOptions> SendersAsync(Guid caseId, CancellationToken cancellationToken) =>
        Available ? senders.OptionsAsync(caseId, cancellationToken) : Task.FromResult(CaseMailSenderOptions.None);

    /// <summary>
    /// The Photo link template's values. <c>{claimant}</c> is the name the
    /// greeting uses (<see cref="PhotoCaptureText.GreetingName"/>; R855) when
    /// one is known (EmailTemplates.PhotoLink); a value not yet known is
    /// passed as its own placeholder so the composer keeps it in place.
    /// </summary>
    public static Dictionary<string, string?> Values(
        string? normalizedRegistration,
        string? customerName,
        string? principalName,
        string? link,
        DateTimeOffset? expiresAtUtc) => new(StringComparer.Ordinal)
        {
            [EmailTemplates.Registration] = normalizedRegistration is { Length: > 0 } registration
                ? PhotoCaptureText.DisplayRegistration(registration)
                : null,
            [EmailTemplates.Claimant] = PhotoCaptureText.GreetingName(customerName),
            [EmailTemplates.PhotoLink] = link,
            [EmailTemplates.LinkExpiry] = expiresAtUtc is { } expires ? OperatorLabels.OfficeDate(expires) : null,
            [EmailTemplates.PrincipalName] = principalName
        };

    /// <summary>The Photo link template rendered for these values.</summary>
    public Task<string> RenderAsync(
        ActionActor actor,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken) =>
        renderTemplate.ExecuteAsync(actor, EmailTemplatePurpose.PhotoLink, values, cancellationToken);

    /// <summary>The Photo link (text) template rendered for these values (R919).</summary>
    public Task<string> RenderTextAsync(
        ActionActor actor,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken) =>
        renderTemplate.ExecuteAsync(actor, EmailTemplatePurpose.PhotoLinkText, values, cancellationToken);

    /// <summary>
    /// A body staff edited on the Send page: its remaining placeholders take
    /// their real values, and the link is added on its own line if staff took
    /// <c>{photo link}</c> out, since the message exists to carry it.
    /// </summary>
    public static string Complete(string body, IReadOnlyDictionary<string, string?> values, string link)
    {
        ArgumentNullException.ThrowIfNull(body);
        var rendered = EmailTemplates.Render(body, values);
        return rendered.Contains(link, StringComparison.Ordinal) ? rendered : rendered.TrimEnd() + "\n\n" + link;
    }

    /// <summary>
    /// The e-mail a link that now exists goes in: the message as written
    /// (<see cref="Complete"/>), or with none written the standard wording
    /// for the link as issued (R806).
    /// </summary>
    public async Task<string> ComposeAsync(
        ActionActor actor,
        string? written,
        IReadOnlyDictionary<string, string?> values,
        string link,
        CancellationToken cancellationToken) =>
        Complete(written ?? await RenderAsync(actor, values, cancellationToken), values, link);

    /// <summary>The text a link that now exists goes in, as <see cref="ComposeAsync"/> composes the e-mail (R919).</summary>
    public async Task<string> ComposeTextAsync(
        ActionActor actor,
        string? written,
        IReadOnlyDictionary<string, string?> values,
        string link,
        CancellationToken cancellationToken) =>
        Complete(written ?? await RenderTextAsync(actor, values, cancellationToken), values, link);

    /// <summary>
    /// Sends one photo link e-mail on the Case. The From is the mailbox staff
    /// chose, else the Case's default sender; none that can send refuses with
    /// <see cref="CaseMailSenderRequiredException"/>.
    /// </summary>
    public async Task<StaffMailOperation> SendAsync(
        ActionActor actor,
        Guid caseId,
        Guid? fromMailboxId,
        string to,
        string subject,
        string body,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var header = await getCaseHeader.ExecuteAsync(new(caseId, actor), cancellationToken)
            ?? throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.CaseUnavailable, IssuePhotoLink.CaseCannotTakeALink);
        var choices = await senders.OptionsAsync(caseId, cancellationToken);
        var sender = choices.Sender(fromMailboxId) ?? throw new CaseMailSenderRequiredException();
        return await staffMailSend.SendAsync(
            new(
                actor,
                sender.Mailbox.Id,
                sender.Mailbox.Generation,
                StaffMailPurpose.PhotoLink,
                caseId,
                header.Workflow.Version,
                StaffMailComposeMode.New,
                OriginalMessage: null,
                [new StaffMailRecipient(to.Trim(), DisplayName: null)],
                [],
                subject.Trim(),
                body.Trim(),
                [],
                operationKey),
            cancellationToken);
    }

    /// <summary>The subject (R788): "Photos of AB12 CDE for Collision Engineers", or "Photos for Collision Engineers" with no registration.</summary>
    public static string Subject(string? normalizedRegistration) =>
        OperatorLabels.PhotoCapture.EmailSubject(
            normalizedRegistration is { Length: > 0 } registration
                ? PhotoCaptureText.DisplayRegistration(registration)
                : null);
}
