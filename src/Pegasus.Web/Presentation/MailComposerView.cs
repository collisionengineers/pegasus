using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Presentation;

/// <summary>A mailbox a composer's From offers.</summary>
public sealed record MailComposerSender(Guid Id, string Address);

/// <summary>An address the browser's own picker offers on every recipient box.</summary>
public sealed record MailComposerAddress(string Address, string? Source);

/// <summary>
/// One send-e-mail dialogue (operator, 27 November 2026; R1329, R1330): the
/// Send report composer's frame, fields and buttons, drawn once by
/// <c>Pages/Shared/_MailComposer.cshtml</c> (the frame) and
/// <c>_MailComposeForm.cshtml</c> (the form) for every staff send: Send
/// report, the e-mail dialogue's Reply, Reply all, Forward and new message,
/// Send chaser, the Triage reply and chaser, and a photo link's e-mail. Each
/// composer keeps its own posted names, handler and send rules; this says
/// only what the one dialogue draws. A slot is a Razor template its caller
/// writes, for what only that composer has.
/// </summary>
public sealed class MailComposerView
{
    private static readonly IReadOnlyDictionary<string, string?> None = new Dictionary<string, string?>();

    // ---- the frame --------------------------------------------------------

    /// <summary>The backdrop's id and <c>data-dialog</c> in a dialogue; the section's id on a page.</summary>
    public required string Id { get; init; }

    /// <summary>A dialogue over the page that opened it; otherwise a page of its own (the no-script route).</summary>
    public bool InDialog { get; init; } = true;

    /// <summary>
    /// The dialogue draws its own backdrop; false when it is fetched into one
    /// its page already holds (a photo link's), when only the card is drawn.
    /// </summary>
    public bool OwnBackdrop { get; init; } = true;

    /// <summary>The dialogue opens as the page loads (a refused send returns to it).</summary>
    public bool OpenOnLoad { get; init; }

    /// <summary>Further attributes on the backdrop, such as <c>data-dialog-carry</c> or <c>data-mail-message</c>.</summary>
    public IReadOnlyDictionary<string, string?> BackdropAttributes { get; init; } = None;

    /// <summary>The line above the title: what the send is about.</summary>
    public string? Eyebrow { get; init; }

    /// <summary>The eyebrow as markup, where it holds more than words; it stands in for <see cref="Eyebrow"/>.</summary>
    public Func<object?, object>? EyebrowContent { get; init; }

    public required string Title { get; init; }

    /// <summary>Where the page's close and Cancel go; a dialogue closes in place.</summary>
    public string? BackHref { get; init; }

    /// <summary>What the body shows before the form: a send's status, a notice, the reasons a send was refused.</summary>
    public Func<object?, object>? Status { get; init; }

    /// <summary>The form is drawn; false once a send is under way or done, when only <see cref="Status"/> shows.</summary>
    public bool OffersForm { get; init; } = true;

    // ---- the form ---------------------------------------------------------

    public required string Action { get; init; }

    /// <summary>The form's id; a control outside it names the form by this.</summary>
    public string? FormId { get; init; }

    public IReadOnlyDictionary<string, string?> FormAttributes { get; init; } = None;

    /// <summary>A further class on the form, for a composer's own rules (a photo link's channels).</summary>
    public string? FormClass { get; init; }

    public IReadOnlyList<KeyValuePair<string, string?>> Hidden { get; init; } = [];

    /// <summary>First in the fields: what a composer asks before its addresses.</summary>
    public Func<object?, object>? BeforeFields { get; init; }

    /// <summary>
    /// Attributes on each e-mail field's holder (the addresses, Subject and
    /// Message), for a composer whose e-mail fields show only on one channel.
    /// </summary>
    public IReadOnlyDictionary<string, string?> EmailFieldAttributes { get; init; } = None;

    // ---- From, To, Cc -----------------------------------------------------

    public string FromName { get; init; } = "FromMailboxId";

    public IReadOnlyList<MailComposerSender> FromChoices { get; init; } = [];

    public Guid? FromSelected { get; init; }

    /// <summary>A From that is not a choice (a forward leaves the mailbox that holds the original; R655).</summary>
    public string? FixedFrom { get; init; }

    public IReadOnlyDictionary<string, string?> FromAttributes { get; init; } = None;

    public string ToName { get; init; } = "To";

    public IReadOnlyList<string> To { get; init; } = [];

    /// <summary>One To address shown and not changed (a photo link's resend goes to the link's own address).</summary>
    public bool ToReadOnly { get; init; }

    /// <summary>The Cc boxes' posted name; null when the send has no Cc (a photo link, R915).</summary>
    public string? CcName { get; init; } = "Cc";

    public IReadOnlyList<string> Cc { get; init; } = [];

    /// <summary>Add To recipient and Add Cc recipient are offered; a send to one address offers neither.</summary>
    public bool OffersMoreRecipients { get; init; } = true;

    /// <summary>The datalist id every recipient box names; null for none.</summary>
    public string? AddressBookId { get; init; }

    public IReadOnlyList<MailComposerAddress> AddressBook { get; init; } = [];

    /// <summary>Before the address grid: a composer's heading for its recipients.</summary>
    public Func<object?, object>? BeforeAddresses { get; init; }

    /// <summary>The address grid's fourth cell, beside Cc (the e-mail dialogue's Case / PO).</summary>
    public Func<object?, object>? AddressCell { get; init; }

    /// <summary>After the address grid: a hint about who the send copies.</summary>
    public Func<object?, object>? AfterAddresses { get; init; }

    // ---- Subject and Message ----------------------------------------------

    public string SubjectName { get; init; } = "Subject";

    public string? Subject { get; init; }

    public int SubjectMaxLength { get; init; } = 500;

    public IReadOnlyDictionary<string, string?> SubjectAttributes { get; init; } = None;

    /// <summary>Before Message: a line about where its wording came from.</summary>
    public Func<object?, object>? BeforeMessage { get; init; }

    public string BodyName { get; init; } = "Body";

    public string? Body { get; init; }

    public int BodyRows { get; init; } = 9;

    public int BodyMaxLength { get; init; } = EmailTemplates.MaximumBodyLength;

    public IReadOnlyDictionary<string, string?> BodyAttributes { get; init; } = None;

    /// <summary>Further attributes on Message's holder.</summary>
    public IReadOnlyDictionary<string, string?> BodyFieldAttributes { get; init; } = None;

    /// <summary>Inside Message's holder, after the box: a hint and its reset.</summary>
    public Func<object?, object>? BodyExtra { get; init; }

    /// <summary>After Message: further fields and ticks.</summary>
    public Func<object?, object>? AfterMessage { get; init; }

    // ---- attachments and the foot -----------------------------------------

    public string AttachmentName { get; init; } = "SelectedAttachments";

    public IReadOnlyList<StaffMailAttachmentOption> Attachments { get; init; } = [];

    public IReadOnlyCollection<string> SelectedAttachments { get; init; } = [];

    /// <summary>The Files rows when a composer draws its own (Send report: rename and view).</summary>
    public Func<object?, object>? AttachmentRows { get; init; }

    public string AttachmentsLegend { get; init; } = Labels.Attachments;

    public IReadOnlyDictionary<string, string?> AttachmentsAttributes { get; init; } = None;

    /// <summary>After the attachments: warnings, holds and the Delete case correspondence tick.</summary>
    public Func<object?, object>? AfterAttachments { get; init; }

    public string SendLabel { get; init; } = Labels.Send;

    /// <summary>Send is drawn; false while something the send needs is missing.</summary>
    public bool OffersSend { get; init; } = true;

    /// <summary>Further attributes on Cancel (the e-mail dialogue's Cancel goes back to the message).</summary>
    public IReadOnlyDictionary<string, string?> CancelAttributes { get; init; } = None;

    /// <summary>The send buttons when a composer has more than one (a photo link's three channels).</summary>
    public Func<object?, object>? SendButtons { get; init; }

    /// <summary>
    /// From, To, Subject and Message are required in the browser; false where
    /// they apply to one channel only, and the server checks the channel sent.
    /// </summary>
    public bool FieldsRequired { get; init; } = true;

    /// <summary>The ids the form draws, under the composer's own id.</summary>
    public string Part(string name) => $"{Id}-{name}";

    public bool HasAttachments => AttachmentRows is not null || Attachments.Count > 0;

    /// <summary>
    /// <paramref name="attributes"/> written into a tag, each value encoded;
    /// a null value is a bare attribute.
    /// </summary>
    public static IHtmlContent Write(IReadOnlyDictionary<string, string?> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        var text = new System.Text.StringBuilder();
        foreach (var (name, value) in attributes)
        {
            text.Append(' ').Append(name);
            if (value is not null)
            {
                text.Append("=\"").Append(HtmlEncoder.Default.Encode(value)).Append('"');
            }
        }
        return new HtmlString(text.ToString());
    }

    /// <summary>The words every composer shares.</summary>
    public static class Labels
    {
        public const string From = "From";
        public const string To = "To";
        public const string Cc = "Cc";
        public const string Subject = "Subject";
        public const string Message = "Message";
        public const string Attachments = "Attachments";
        public const string AddTo = CaseWorkspaceLabels.Report.AddTo;
        public const string AddCc = CaseWorkspaceLabels.Report.AddCc;
        public const string RemoveRecipient = CaseWorkspaceLabels.ReportDelivery.RemoveRecipient;
        public const string Recipient = "Recipient";
        public const string CopyRecipient = "Copy recipient";
        public const string Cancel = "Cancel";
        public const string Send = "Send";
        public const string CloseDialog = "Close dialog";
    }
}
