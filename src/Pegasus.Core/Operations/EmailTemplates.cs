using System.Text.RegularExpressions;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

/// <summary>
/// The staff message whose body an Administrator may edit (FRD-17 E-mail
/// templates). Each purpose has its own placeholder set and built-in body.
/// </summary>
public enum EmailTemplatePurpose { TriageOutcomeReply, CaseReportDelivery, CaseChaser }

/// <summary>
/// One purpose's body as it stands: the saved text, or the built-in body at
/// version 0 with no change recorded.
/// </summary>
public sealed record EmailTemplate(
    EmailTemplatePurpose Purpose,
    string Body,
    long Version,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy);

public sealed record UpdateEmailTemplateRequest(
    EmailTemplatePurpose Purpose,
    string Body,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey);

/// <summary>
/// The one owner of template policy: each purpose's placeholders and built-in
/// body, what a saved body may contain, and how a body renders.
/// </summary>
/// <remarks>
/// A placeholder with no value renders nothing, and a line whose placeholders
/// are all empty is left out, so a fact that was not recorded does not appear.
/// The subject is never templated: a reply keeps "Re: {original subject}",
/// and a chaser opens with the registration and claimant.
/// </remarks>
public static partial class EmailTemplates
{
    public const int MaximumBodyLength = 5000;

    public const string Registration = "registration";
    public const string Roadworthiness = "roadworthiness";
    public const string RepairOutcome = "repair outcome";
    public const string FindingReason = "reason";
    public const string CaseReference = "case reference";
    public const string Outcome = "outcome";
    public const string PrincipalName = "principal name";
    public const string SupersededReportDate = "superseded report date";
    public const string OutstandingMaterial = "outstanding material";
    public const string Claimant = "claimant";

    private static readonly string[] TriageOutcomeReplyPlaceholders =
        [Registration, Roadworthiness, RepairOutcome, FindingReason];

    /// <summary>
    /// The Triage outcome reply's built-in body, one dimension per line.
    /// <c>{reason}</c> is available but not used: the finding reason is
    /// internal wording.
    /// </summary>
    private const string TriageOutcomeReplyDefault =
        "Thank you for your triage request for {registration}.\n"
        + "\n"
        + "Roadworthiness: {roadworthiness}\n"
        + "Repair outcome: {repair outcome}\n"
        + "\n"
        + "Kind regards\n"
        + "Collision Engineers";

    private static readonly string[] CaseReportDeliveryPlaceholders =
        [CaseReference, Registration, Outcome, PrincipalName, SupersededReportDate];

    /// <summary>
    /// The Case report delivery's built-in body. The "supersedes" line names
    /// the date of the report sent before, so on a first send it has no value
    /// and is left out. <c>{principal name}</c> is available but not used.
    /// </summary>
    private const string CaseReportDeliveryDefault =
        "Please find attached our report.\n"
        + "\n"
        + "Our reference: {case reference}\n"
        + "Registration: {registration}\n"
        + "Outcome: {outcome}\n"
        + "\n"
        + "This report supersedes our report dated {superseded report date}.\n"
        + "\n"
        + "Kind regards\n"
        + "Collision Engineers";

    /// <summary>
    /// The Case chaser carries no Case/PO reference: it is internal and means
    /// nothing to the party chased (operator, 5 October 2026).
    /// </summary>
    private static readonly string[] CaseChaserPlaceholders =
        [Registration, OutstandingMaterial, PrincipalName, Claimant];

    /// <summary>
    /// The Case chaser's built-in body: the one sentence the due-chaser sweep
    /// has always written, without the reference. <c>{principal name}</c> and
    /// <c>{claimant}</c> are available but not used.
    /// </summary>
    private const string CaseChaserDefault =
        "Please provide the outstanding material for {registration}: {outstanding material}.\n"
        + "\n"
        + "Kind regards\n"
        + "Collision Engineers";

    public static IReadOnlyList<string> Placeholders(EmailTemplatePurpose purpose) => purpose switch
    {
        EmailTemplatePurpose.TriageOutcomeReply => TriageOutcomeReplyPlaceholders,
        EmailTemplatePurpose.CaseReportDelivery => CaseReportDeliveryPlaceholders,
        EmailTemplatePurpose.CaseChaser => CaseChaserPlaceholders,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };

    public static string DefaultBody(EmailTemplatePurpose purpose) => purpose switch
    {
        EmailTemplatePurpose.TriageOutcomeReply => TriageOutcomeReplyDefault,
        EmailTemplatePurpose.CaseReportDelivery => CaseReportDeliveryDefault,
        EmailTemplatePurpose.CaseChaser => CaseChaserDefault,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };

    /// <summary>The built-in body at version 0, before an Administrator saves one.</summary>
    public static EmailTemplate Default(EmailTemplatePurpose purpose) =>
        new(purpose, DefaultBody(purpose), 0, null, null);

    /// <summary>
    /// The body as it is saved: line endings made plain, trailing space
    /// trimmed. A blank body, one past <see cref="MaximumBodyLength"/>, or one
    /// naming a placeholder this purpose does not have is refused.
    /// </summary>
    public static string Validate(EmailTemplatePurpose purpose, string? body)
    {
        var placeholders = Placeholders(purpose);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A template body is required.", nameof(body));
        }

        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        if (normalized.Length > MaximumBodyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(body),
                $"A template body cannot exceed {MaximumBodyLength} characters.");
        }

        foreach (Match match in PlaceholderPattern().Matches(normalized))
        {
            var name = match.Groups["name"].Value;
            if (!placeholders.Contains(name, StringComparer.Ordinal))
            {
                throw new UnknownEmailTemplatePlaceholderException(name);
            }
        }

        return normalized;
    }

    /// <summary>
    /// Renders <paramref name="body"/> line by line. A placeholder is replaced
    /// by its value, or by nothing when the value is empty; a line whose
    /// placeholders are all empty is left out.
    /// </summary>
    public static string Render(string body, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(values);
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var named = 0;
            var filled = 0;
            var rendered = PlaceholderPattern().Replace(line, match =>
            {
                named++;
                if (!values.TryGetValue(match.Groups["name"].Value, out var value)
                    || string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                filled++;
                return value;
            });
            if (named > 0 && filled == 0)
            {
                continue;
            }

            kept.Add(rendered);
        }

        return string.Join("\n", kept);
    }

    [GeneratedRegex(@"\{(?<name>[^{}\r\n]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}

public interface IEmailTemplateStore
{
    /// <summary>The saved template, or <see langword="null"/> when none has been saved.</summary>
    Task<EmailTemplate?> GetAsync(EmailTemplatePurpose purpose, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the body at <see cref="UpdateEmailTemplateRequest.ExpectedVersion"/>
    /// (0 inserts) and records the change in the action log. A replay of the
    /// same operation returns the saved template.
    /// </summary>
    Task<EmailTemplate> UpdateAsync(UpdateEmailTemplateRequest request, CancellationToken cancellationToken);
}

/// <summary>An Administrator reads a template to edit it.</summary>
public sealed class GetEmailTemplate(IEmailTemplateStore store)
{
    private readonly IEmailTemplateStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<EmailTemplate> ExecuteAsync(
        ActionActor actor,
        EmailTemplatePurpose purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageEmailTemplates);
        return await _store.GetAsync(purpose, cancellationToken) ?? EmailTemplates.Default(purpose);
    }
}

/// <summary>An Administrator saves a template's body.</summary>
public sealed class UpdateEmailTemplate(IEmailTemplateStore store)
{
    private readonly IEmailTemplateStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<EmailTemplate> ExecuteAsync(
        UpdateEmailTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageEmailTemplates);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedVersion);
        if (string.IsNullOrWhiteSpace(request.OperationKey) || request.OperationKey.Trim().Length > 100)
        {
            throw new ArgumentException("An operation key is required.", nameof(request));
        }

        return _store.UpdateAsync(
            request with
            {
                Body = EmailTemplates.Validate(request.Purpose, request.Body),
                OperationKey = request.OperationKey.Trim()
            },
            cancellationToken);
    }
}

/// <summary>
/// Staff composing a reply open it with the purpose's current template
/// rendered from their values. Reading the text to compose a reply is
/// casework; editing it is not.
/// </summary>
public sealed class RenderEmailTemplate(IEmailTemplateStore store)
{
    private readonly IEmailTemplateStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<string> ExecuteAsync(
        ActionActor actor,
        EmailTemplatePurpose purpose,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(values);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var template = await _store.GetAsync(purpose, cancellationToken) ?? EmailTemplates.Default(purpose);
        return EmailTemplates.Render(template.Body, values);
    }
}

/// <summary>A saved body names a placeholder its purpose does not have.</summary>
public sealed class UnknownEmailTemplatePlaceholderException(string placeholder)
    : ArgumentException($"The template uses an unknown placeholder: {{{placeholder}}}.")
{
    public string Placeholder { get; } = placeholder;
}

public sealed class EmailTemplateVersionConflictException()
    : InvalidOperationException("The e-mail template changed before this request was saved.");

public sealed class EmailTemplateOperationConflictException()
    : InvalidOperationException("The operation key has already been used for another e-mail template request.");
