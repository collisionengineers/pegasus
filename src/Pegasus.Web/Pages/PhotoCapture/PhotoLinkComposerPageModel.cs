using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Operations;
using Pegasus.Core.PhotoCapture;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// The one photo-link composer Send a photo link and Resend share (operator,
/// 11 November 2026; v51 L, M; R948): one channel at a time, Email, Text
/// message or Link to copy, each with its own recipient, wording and one
/// button, drawn by <c>_PhotoLinkComposer</c>. Its fields keep their names, so
/// a post is validated for the channel its button names and nothing else; the
/// other channels' boxes are posted as they stand. <c>?handler=Composer</c>
/// answers the composer as a dialog over the page it was asked from, whose
/// form posts to the page itself: a send returns there with the page's notice,
/// and a refusal or a link to copy is answered by the page.
/// </summary>
public abstract class PhotoLinkComposerPageModel : PhotoCapturePageModel
{
    public const string ByEmail = "email";
    public const string ByText = "text";
    public const string ToCopy = "copy";
    public const string StandardWording = "standard";
    public const string StandardTextWording = "standard-text";

    /// <summary>The fragment <c>?handler=Composer</c> answers: the composer as a dialog.</summary>
    public const string ComposerDialog = "_PhotoLinkComposerDialog";

    /// <summary>Where Cancel and a sent message return: the page the composer was asked from.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>The channel the composer opens on (<c>?channel=</c>); a post shows the one its button named.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Channel { get; set; }

    [BindProperty]
    public Guid? FromMailboxId { get; set; }

    [BindProperty, Display(Name = Labels.Subject), StringLength(998)]
    public string? Subject { get; set; }

    [BindProperty, Display(Name = Labels.Message), StringLength(EmailTemplates.MaximumBodyLength)]
    public string? Body { get; set; }

    /// <summary>The standard wording as the page last drew it, so a post knows whether staff edited the message.</summary>
    [BindProperty]
    public string? StandardBody { get; set; }

    /// <summary>The mobile a text goes to.</summary>
    [BindProperty, Display(Name = Labels.Mobile), StringLength(CaptureLimits.MobileLength)]
    public string? Mobile { get; set; }

    [BindProperty, Display(Name = Labels.TextMessage), StringLength(SendPhotoLinkText.MaximumLength)]
    public string? TextBody { get; set; }

    /// <summary>The text's standard wording as the page last drew it (R919).</summary>
    [BindProperty]
    public string? StandardTextBody { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    /// <summary>Which button sent the form: <see cref="ByEmail"/>, <see cref="ByText"/>, <see cref="ToCopy"/>, <see cref="StandardWording"/> or <see cref="StandardTextWording"/>.</summary>
    [BindProperty]
    public string? Submit { get; set; }

    /// <summary>Whether staff edited the message: "Edited for this one only." (R806).</summary>
    public bool BodyEdited => Body is not null && StandardBody is not null
        && !string.Equals(Normalized(Body), Normalized(StandardBody), StringComparison.Ordinal);

    /// <summary>Whether staff edited the text (R806, R919).</summary>
    public bool TextBodyEdited => TextBody is not null && StandardTextBody is not null
        && !string.Equals(Normalized(TextBody), Normalized(StandardTextBody), StringComparison.Ordinal);

    public CaseMailSenderOptions Senders { get; protected set; } = CaseMailSenderOptions.None;

    /// <summary>Whether the link can go by e-mail here.</summary>
    public abstract bool CanEmail { get; }

    /// <summary>Whether the link can go by text here: a text provider is configured (R918).</summary>
    public abstract bool CanText { get; }

    /// <summary>The composer's heading: Send a photo link, or the act Resend was opened as.</summary>
    public abstract string ComposerTitle { get; }

    /// <summary>The eyebrow's Case reference; null for a capture with no Case.</summary>
    public abstract string? EyebrowReference { get; }

    /// <summary>The eyebrow's registration, as staff read it.</summary>
    public abstract string EyebrowRegistration { get; }

    /// <summary>The eyebrow's Principal; null when the Case names none.</summary>
    public abstract string? EyebrowPrincipal { get; }

    /// <summary>The page the composer's form posts to.</summary>
    public abstract string ComposerPage { get; }

    /// <summary>The route the composer's form posts to.</summary>
    public abstract IDictionary<string, string> ComposerRoute { get; }

    /// <summary>Whether the composer is drawn as the dialog (<c>?handler=Composer</c>) rather than the page.</summary>
    public bool InDialog { get; protected set; }

    /// <summary>Where Cancel and a sent message go: the page it was opened from, else the page's own default.</summary>
    public string BackHref => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : DefaultBackHref;

    /// <summary>Whether the composer was opened from a page it returns to.</summary>
    protected bool HasReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl);

    protected abstract string DefaultBackHref { get; }

    /// <summary>
    /// The channel shown: the one the posted button named, else the one asked
    /// for, else the first the page offers (Email, Text message, Link to copy).
    /// </summary>
    public string ActiveChannel
    {
        get
        {
            var asked = Submit switch
            {
                ByEmail or StandardWording => ByEmail,
                ByText or StandardTextWording => ByText,
                ToCopy => ToCopy,
                _ => Channel
            };
            return asked switch
            {
                ByEmail when CanEmail => ByEmail,
                ByText when CanText => ByText,
                ToCopy => ToCopy,
                _ => CanEmail ? ByEmail : CanText ? ByText : ToCopy
            };
        }
    }

    /// <summary>
    /// Leaves today's sent notice for the page the composer returns to: the
    /// Photo capture pages show their own Notice, every other page (the
    /// Case) the layout's one-time confirmation.
    /// </summary>
    protected void LeaveSentNotice(string notice)
    {
        var photoCapturePage = BackHref.StartsWith("/PhotoCapture", StringComparison.OrdinalIgnoreCase);
        TempData[photoCapturePage ? "Notice" : "Confirmation"] = notice;
    }

    /// <summary>
    /// A post is checked for the channel its button names (v51 L; R948): the
    /// boxes only another channel sends ride along unchecked.
    /// </summary>
    protected virtual void SetAsideOtherChannels()
    {
        if (Submit is not (ByEmail or StandardWording))
        {
            ModelState.Remove(nameof(FromMailboxId));
            ModelState.Remove(nameof(Subject));
            ModelState.Remove(nameof(Body));
            ModelState.Remove(nameof(StandardBody));
        }

        if (Submit is not (ByText or StandardTextWording))
        {
            ModelState.Remove(nameof(TextBody));
            ModelState.Remove(nameof(StandardTextBody));
        }
    }

    protected static string Normalized(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
}
