using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Web.Presentation;

public static partial class OperatorLabels
{
    /// <summary>
    /// The AI job ledger's words on the Work Centre's AI jobs pane, Administration
    /// AI jobs and the message page.
    /// </summary>
    /// <remarks>
    /// The kind and state wordings are FRD-27 &#167; AI Job List's own; the Core
    /// enum names are the writer's spelling of them. This is the only map of
    /// either in the Web layer &#8212; nothing named these states before.
    ///
    /// <c>Shared/_StatusChip</c> already owns tones for Completed, Failed,
    /// Cancelled and settled terminal labels. <see cref="StateToneOverride"/>
    /// supplies only the three AI-specific labels that partial does not know.
    /// </remarks>
    public static class AiJobs
    {
        // An AI Proposal (CONTEXT.md) is named by its reserved term, not as an estimate (v36 item C, 9 October 2026).
        public const string AiProposalReady = "AI Proposal ready";
        public const string PanelTitle = "AI Job List";
        public const string SendUnidentified = "Send Unidentified to AI";
        public const string CompleteJob = "Complete job";
        public const string ReviewEstimate = "Review AI Proposal";
        // A Draft ready Query response opens the reply composer on its draft (FRD-27).
        public const string OpenQuery = QueryResponseJobs.ReplyWithDraft;
        public const string Review = "Review";

        public const string Stop = "Stop";
        public const string None = "No AI jobs are recorded.";

        // The Administration list's columns, pager and Stop outcomes.
        public const string JobColumn = "Job";
        public const string SubjectColumn = "Subject";
        public const string CreatedColumn = "Created";
        public const string StateColumn = "State";
        public const string ActionColumn = "Action";
        public const string Pages = "AI job pages";
        public const string Previous = "Previous";
        public const string Next = "Next";
        public const string Stopped = "The AI job was stopped.";

        /// <summary>A Stop Core refused for its input or a job it cannot find.</summary>
        public const string NotStopped = "The AI job could not be stopped.";

        /// <summary>A Stop that met a newer version or a job already settled.</summary>
        public const string ChangedBeforeStop = "The AI job changed before it could be stopped.";

        public static string Page(int page) =>
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Page {page}");

        /// <summary>The list's head: "2 active · 1 failed · Send to AI on".</summary>
        public static string Counts(Pegasus.Core.AiWork.AiJobCounts counts, bool sendToAiEnabled) =>
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{counts.Active} active · {counts.Failed} failed · {SendToAiState(sendToAiEnabled)}");

        /// <summary>The Estimate kind's reserved term (CONTEXT.md; v41 A28).</summary>
        public const string AiProposal = "AI Proposal";

        /// <summary>The Send to AI switch beside the job counts: "Send to AI on" or "Send to AI off" (v41 A28).</summary>
        public static string SendToAiState(bool enabled) =>
            $"{AutomationAdmin.SendToAi} {(enabled ? "on" : "off")}";

        /// <summary>
        /// The instruction a row prints. A Query response stores the message
        /// id it answers as its instruction, which no operator reads, so that
        /// kind prints its fixed sentence instead.
        /// </summary>
        public static string Instruction(Pegasus.Core.AiWork.AiJobRecord job) =>
            job.Kind == Pegasus.Core.AiWork.AiJobKind.QueryResponse
                ? QueryResponseJobs.Instruction
                : job.Instruction;

        /// <summary>
        /// A queue pass names no record: its Core subject reference is the
        /// internal token <c>unidentified-queue</c>, which no operator reads.
        /// </summary>
        public const string QueueRecord = "Unidentified queue";

        public static string Kind(Pegasus.Core.AiWork.AiJobKind kind) => kind switch
        {
            Pegasus.Core.AiWork.AiJobKind.Estimate => AiProposal,
            Pegasus.Core.AiWork.AiJobKind.UnidentifiedResolution => "Unidentified resolution",
            Pegasus.Core.AiWork.AiJobKind.QueryResponse => "Query response",
            Pegasus.Core.AiWork.AiJobKind.UnidentifiedQueuePass => "Unidentified-queue pass",
            Pegasus.Core.AiWork.AiJobKind.MarketResearch => "Market research",
            _ => Humanise(kind.ToString())
        };

        public static string State(Pegasus.Core.AiWork.AiJobState state) => state switch
        {
            Pegasus.Core.AiWork.AiJobState.Queued => "Queued",
            Pegasus.Core.AiWork.AiJobState.Taken => "Taken",
            Pegasus.Core.AiWork.AiJobState.DraftReady => "Draft ready",
            Pegasus.Core.AiWork.AiJobState.Completed => "Completed",
            Pegasus.Core.AiWork.AiJobState.Failed => "Failed",
            Pegasus.Core.AiWork.AiJobState.Cancelled => "Cancelled",
            Pegasus.Core.AiWork.AiJobState.Expired => "Expired",
            _ => Humanise(state.ToString())
        };

        /// <summary>
        /// The explicit chip tone only for AI state labels not owned by
        /// Shared/_StatusChip. A null lets the shared partial apply its single
        /// tone vocabulary.
        /// </summary>
        public static string? StateToneOverride(Pegasus.Core.AiWork.AiJobState state) => state switch
        {
            Pegasus.Core.AiWork.AiJobState.Queued or Pegasus.Core.AiWork.AiJobState.DraftReady => "amber",
            Pegasus.Core.AiWork.AiJobState.Taken => "navy",
            _ => null
        };
    }

    /// <summary>The principal-submission API's operator vocabulary — one list.</summary>
    public static class PrincipalSubmissionApi
    {
        public const string Source = "Principal API";
    }

    /// <summary>
    /// Staff correspondence's own state (S12, C08): the operator sees only
    /// whether it is on its way, delivered, or needs attention — the internal
    /// attempt-stage vocabulary (draft creation, attaching, sending) is
    /// writer detail, not a distinction the operator acts on differently.
    /// Neither Submitted nor Unknown offers a resend: the Worker's Sent poll
    /// settles them, and a resend from an unknown outcome could double-send a
    /// message that already reached Outlook.
    /// </summary>
    public static class StaffMail
    {
        public const string Reply = "Reply";
        public const string ReplyAll = "Reply all";
        public const string Forward = "Forward";
        /// <summary>The reply composer's tick that marks the replied message's open query done on observed Sent.</summary>
        public const string MarkQueryDone = "Mark query done";

        public static string State(Pegasus.Core.Operations.StaffMailState state) => state switch
        {
            Pegasus.Core.Operations.StaffMailState.Prepared => "Preparing",
            Pegasus.Core.Operations.StaffMailState.DraftCreating => "Creating draft",
            Pegasus.Core.Operations.StaffMailState.DraftReady => "Draft ready",
            Pegasus.Core.Operations.StaffMailState.Sending => "Sending",
            Pegasus.Core.Operations.StaffMailState.Submitted => "Submitted",
            Pegasus.Core.Operations.StaffMailState.Sent => "Sent",
            Pegasus.Core.Operations.StaffMailState.Failed => "Failed",
            Pegasus.Core.Operations.StaffMailState.Cancelled => "Cancelled",
            Pegasus.Core.Operations.StaffMailState.Unknown => "Unknown",
            _ => throw new InvalidOperationException(
                $"Unknown staff mail state '{(int)state}'.")
        };

        /// <summary>
        /// A refused send's reason in operator words (FRD-21: a send Graph
        /// refuses shows as a failure on the composer). The failure code is
        /// the engine's; nothing here is sent.
        /// </summary>
        public static string Failure(string failureCode)
        {
            var reason = failureCode switch
            {
                "graph_rejected_403" =>
                    "The mailbox refused the message. Pegasus is not permitted to write to or send from this mailbox.",
                "staff_send_content_invalid" =>
                    "The message could not be prepared for sending. It may be larger than the mailbox allows.",
                "staff_send_authorization_lost" =>
                    "Your authorisation or the mailbox's send setting changed before the message was sent.",
                _ when failureCode.StartsWith("graph_rejected_", StringComparison.Ordinal) =>
                    $"The mail service refused the message (code {failureCode["graph_rejected_".Length..]}).",
                _ => $"The message was refused ({failureCode})."
            };
            return reason + " Nothing was sent.";
        }
    }

    /// <summary>Administration → Contacts: the one type each contact has.</summary>
    public static class Contacts
    {
        public static string Type(ContactRole role) => role switch
        {
            ContactRole.Principal => "Principal",
            ContactRole.ClaimSource => "Claim Source",
            ContactRole.Repairer => "Repairer",
            ContactRole.Storage => "Storage",
            ContactRole.ThirdPartyEngineer => "Third Party Engineer",
            _ => role.ToString()
        };

        /// <summary>The heading of a type's panel on the Contacts list (FRD-17's words).</summary>
        public static string Plural(ContactRole role) => role switch
        {
            ContactRole.Principal => "Principals",
            ContactRole.ClaimSource => "Claim Sources",
            ContactRole.Repairer => "Repairers",
            ContactRole.Storage => "Storage",
            ContactRole.ThirdPartyEngineer => "Third Party Engineers",
            _ => role.ToString()
        };

        /// <summary>A refused contact save, worded once for the list's New dialog and the contact page.</summary>
        public static string ErrorMessage(ContactDirectoryError error) => error switch
        {
            ContactDirectoryError.DuplicateOrganizationName => "A contact with that name already exists.",
            ContactDirectoryError.DuplicatePrincipalCode => "That principal code already exists.",
            ContactDirectoryError.InvalidPrincipalAssociation => "Check the selected linked principals.",
            ContactDirectoryError.StaleVersion => "The contact changed after you opened it. Reload it and try again.",
            ContactDirectoryError.OperationConflict => "This form was already used for a different change.",
            _ => "The contact could not be saved."
        };

        /// <summary>The list's search box: it matches the start of a name (operator, 29 October 2026, R535).</summary>
        public const string SearchLabel = "Name";
        /// <summary>The list with no contact to show, and Check existing contacts with no match (R537).</summary>
        public const string NoneFound = "No contacts found.";

        /// <summary>An empty organisation name (operator, 29 October 2026, R539).</summary>
        public const string NameRequired = "An organisation name is required.";

        // The form's own field words, as an over-long field's refusal names them (R539).
        public const string OrganisationNameField = "organisation name";
        public const string ContactPersonField = "contact person";
        public const string EmailField = "email";
        public const string PhoneField = "phone";
        public const string AddressField = "address";
        public const string PostcodeField = "postcode";
        public const string NotesOnEveryCaseField = "notes on every Case";

        public const string Saved = "The contact was saved.";
        public const string ChooseType = "Choose a contact type.";
        public const string PhoneMustBeDigits = "Phone must be digits.";
        public const string DetailsNotAccepted = "The contact details were not accepted.";
        /// <summary>The list's New dialog with an expired form.</summary>
        public const string NewFormExpired = "The form has expired. Start again.";
        /// <summary>The contact page's Save with an expired form.</summary>
        public const string EditFormExpired = "The form has expired. Open the contact again.";
        /// <summary>The list's New dialog with an unknown inspection mode.</summary>
        public const string SelectInspectionType = "Select an inspection type.";
        /// <summary>The contact page's Save with an unknown inspection mode.</summary>
        public const string SelectInspectionMode = "Select an inspection mode.";
    }

    /// <summary>A Principal contact's own labels (Administration → Contacts, Principal details) — one list.</summary>
    public static class PrincipalAdministration
    {
        public const string Code = "Principal code";
        public const string ContactTab = "Contact";
        public const string ReportGeneration = "Report generation";
        public const string DefaultInspectionLocation = "Default inspection location";
        public const string InspectionMode = "Inspection mode";
        public const string AcceptedIdentities = "Accepted e-mail identities";
        /// <summary>A Principal with no accepted route (operator, 13 October 2026).</summary>
        public const string NoAcceptedRoute = "No accepted e-mail route. Instructions arrive by upload, Triage or the Principal API.";
        public const string State = "State";
        public const string ReplaceCode = "Replace code";
        public const string PrincipalApi = "Principal API";
        public const string SalvageMatrix = "Salvage matrix";
        public const string SalvageFrom = "From (£)";
        public const string SalvageTo = "To (£)";
        public const string SalvagePercentage = "Percentage paid (%)";
        public const string AddSalvageBand = "Add band";
        public const string RemoveSalvageBand = "Remove";
        public const string SaveSalvageMatrix = "Save salvage matrix";
        public const string DefaultFee = "Default fee (£)";
        public const string ReportSending = "Report sending";
        public const string SaveReportSending = "Save report sending";
        public const string SendFrom = "Send from";
        public const string SendFromNone = "Not set";
        public const string SendTo = "Send to";
        public const string SendToHint = "Empty: the original instruction sender.";
        public const string SendToOnly = "Send to only";
        public const string ReplyAll = "Reply all";
        public const string Cc = "Cc";
        public const string NeverCc = "Never cc";
        public const string Attach = "Attach";
        public const string FeeNoteSeparate = "Fee note is a separate PDF";
        public const string AttachEstimate = "Estimate";
        public const string AttachAudatex = "Audatex";
        public const string ReportImages = "Report carries vehicle images";
        public const string VehicleImagesDocument = "Vehicle images document required";
        public const string FigureBreakdown = "Figure breakdown required";
        public const string GarageFigures = "Garage figures";
        public const string Hold = "Hold";
        public const string HoldHint = "Send stays off until staff tick this as done.";
        public const string Reminders = "Reminders";
        public const string AttachmentName = "Attachment name";
        public const string AttachmentNameFirst = "First send";
        public const string AttachmentNameResend = "Re-send";
        public const string AttachmentNameHint = "Use {ref}, {reg} and {outcome}. Empty: the standard name.";
        public const string AddAddress = "Add address";
        public const string AddReminder = "Add reminder";
        public const string Rules = "Rules";
        public const string Rule = "Rule";
        public const string AddRule = "Add rule";
        public const string RemoveRule = "Remove rule";
        public const string RuleMatch = "Match";
        public const string RuleMatchAll = "All conditions";
        public const string RuleMatchAny = "Any condition";
        public const string Condition = "Condition";
        public const string NoCondition = "No condition";
        public const string ConditionValueHint = "Separate several with commas.";
        public const string ActionCcAdd = "Cc add";
        public const string ActionCcRemove = "Cc remove";
        public const string ActionRemind = "Remind";
        public const string ActionHold = "Hold";
        public const string ActionStop = "Stop";
        public const string ActionStopHint = "Send is refused; staff may override with a reason.";
        public const string ActionRateCard = "Rate card";
        /// <summary>A rule's three choices for each attach flag (operator, 16 October 2026).</summary>
        public const string AttachAsThePrincipal = "As the Principal";
        public const string AttachOverrideOn = "Attach";
        public const string AttachOverrideOff = "Do not attach";
        public const string RefPrefixLettersOnly = "A reference prefix is letters only.";

        // A Principal panel's notices and refusals on the contact page.
        public const string FormExpired = "The form has expired. Retry the operation.";
        public const string SubmittedValueInvalid = "The submitted value is not valid.";
        public const string SettingsNotAccepted = "The settings were not accepted.";
        public const string ReportSettingsUpdated = "The principal's report settings were updated.";
        public const string SalvageMatrixUpdated = "The principal's salvage matrix was updated.";
        public const string ReportSendingUpdated = "The principal's report sending rules were updated.";
        public const string AddressRequired = "An address is required.";
        public const string DefaultLocationUpdated = "The principal's default inspection location was updated.";
        public const string DefaultLocationNotAccepted = "The default location was not accepted.";
        public const string NewCodeRequired = "A new principal code is required.";
        // The form's own field words, as a refusal names them (R539).
        public const string CodeField = "principal code";
        public const string NewCodeField = "new principal code";
        public const string LocationLabelField = "location label";

        /// <summary>
        /// A code the principal code rule refuses: ASCII letters and digits only
        /// (<c>OrganizationAdministrationPolicy.NormalizePrincipalCode</c>; R539).
        /// </summary>
        public static string CodeLettersAndDigitsOnly(string field) => $"The {field} must use letters and digits only.";
        public const string Replaced = "The predecessor was disabled and its linked successor was created.";
        public const string ReplacementNotAccepted = "The replacement details were not accepted.";
        public const string CredentialUpdated = "The principal credential was updated.";
        public const string CredentialNotAccepted = "The API key change was not accepted.";
        /// <summary>A stale Principal change whose contact moved on as well.</summary>
        public const string ContactChanged = "This contact changed. Reload it before making further changes.";

        /// <summary>A refused API key change.</summary>
        public static string CredentialErrorMessage(PrincipalCredentialError error) => error switch
        {
            PrincipalCredentialError.StaleVersion => "The API key changed. Retry from the current settings.",
            PrincipalCredentialError.OperationConflict => "The form was already used for a different operation.",
            PrincipalCredentialError.PrincipalInactive => "The principal is disabled.",
            _ => CredentialNotAccepted
        };

        /// <summary>A refused Principal change.</summary>
        public static string ErrorMessage(OrganizationAdministrationError error) => error switch
        {
            OrganizationAdministrationError.PrincipalNotFound => "The principal no longer exists.",
            OrganizationAdministrationError.PrincipalInactive => "The principal is disabled. Change the settings on its successor instead.",
            OrganizationAdministrationError.PrincipalAlreadyReplaced => "The predecessor already has a linked successor.",
            OrganizationAdministrationError.DuplicatePrincipalCode => "That normalized successor code already exists.",
            OrganizationAdministrationError.StaleVersion => "The principal changed after this page was loaded. Review the current settings and retry.",
            OrganizationAdministrationError.OperationConflict => "The form was already used for a different operation. Retry from the current page.",
            _ => "The settings change was not accepted."
        };

        /// <summary>A report sending condition as the rule row names it.</summary>
        public static string ConditionKind(Pegasus.Core.Reports.ReportSendingConditionKind kind) => kind switch
        {
            Pegasus.Core.Reports.ReportSendingConditionKind.ClaimSource => "Claim Source",
            Pegasus.Core.Reports.ReportSendingConditionKind.Repairer => "Repairer",
            Pegasus.Core.Reports.ReportSendingConditionKind.ImagesFrom => "Images from",
            Pegasus.Core.Reports.ReportSendingConditionKind.Mentions => "Instruction mentions",
            Pegasus.Core.Reports.ReportSendingConditionKind.SenderNot => "Sender is not",
            Pegasus.Core.Reports.ReportSendingConditionKind.Outcome => "Outcome",
            Pegasus.Core.Reports.ReportSendingConditionKind.BodyshopMentions => "Bodyshop mentions",
            Pegasus.Core.Reports.ReportSendingConditionKind.CaseType => "Case type",
            Pegasus.Core.Reports.ReportSendingConditionKind.RefPrefix => "Reference prefix",
            Pegasus.Core.Reports.ReportSendingConditionKind.PreviousJob => "Previous job",
            _ => kind.ToString()
        };

        /// <summary>A salvage category as the Case names it: "Cat S".</summary>
        public static string SalvageCategory(string code) => $"Cat {code}";
    }

    /// <summary>The Mail settings area labels and status values — one list.</summary>
    public static class MailSettings
    {
        public const string ApprovedMailboxes = "Approved mailboxes";
        public const string Mailbox = "Mailbox";
        public const string State = "State";
        public const string Subscription = "Subscription";
        public const string AddMailbox = "Add mailbox";
        public const string SaveMailbox = "Save mailbox";
        public const string ApprovedAddress = "Approved address";
        public const string RouteScope = "Used for";
        public const string NoApprovedMailboxes = "No approved mailboxes";
        public const string NotActivated = "Not activated";
        public const string NoSubscription = "None.";
        public const string NotPolled = "Not polled";
        public const string NotYetPolled = "Not yet polled";
        public const string NoCompletedPoll = "No completed poll yet";
        public const string NoError = "None";
        public const string LastChecked = "Last checked";
        public const string Settings = "Settings";
        public const string MailboxSettings = "Mailbox settings";
        public const string CloseDialog = "Close dialog";
        public const string Cancel = "Cancel";
        public const string AccessAndPolling = "Access and polling";
        public const string Activation = "Activation";
        public const string LastSuccess = "Last success";
        public const string Freshness = "Freshness";
        public const string LastErrorTerm = "Last error";
        public const string Fresh = "Fresh";
        public const string Stale = "Stale";

        public static string MailboxState(ApprovedMailboxState state) =>
            Humanise(state.ToString());

        /// <summary>The mailbox count the panel head shows under the title.</summary>
        public static string MailboxCount(int count) =>
            count == 1 ? "1 mailbox" : $"{count} mailboxes";

        /// <summary>The Last checked cell: the last completed poll's time, or why there is none.</summary>
        public static string PollStatus(
            ApprovedMailbox mailbox,
            ApprovedMailboxPollStatus? status)
        {
            if (status is null)
            {
                if (mailbox.State != ApprovedMailboxState.Approved
                    || !mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.Incoming))
                {
                    return NotPolled;
                }

                // Polling starts from the recorded identity and activation the
                // address check produced. Without them the estate never offers this
                // mailbox, so "not yet polled" would promise a poll that cannot
                // happen; the stored evidence says only that it is not activated.
                return mailbox.IdentityIsBound && mailbox.ActivatedAtUtc is not null
                    ? NotYetPolled
                    : NotActivated;
            }

            return status.LastCompletedAtUtc is { } lastCompletedAtUtc
                ? OfficeTime(lastCompletedAtUtc)
                : NoCompletedPoll;
        }

        /// <summary>The Settings dialog's Last error, in words rather than the stored failure code.</summary>
        public static string LastError(string? failureCode) => failureCode switch
        {
            null => NoError,
            "mailbox_access_denied" =>
                "The tenant has not granted this application access to this mailbox.",
            "mailbox_not_approved" =>
                "The last attempt stopped because this mailbox was no longer approved.",
            var code => Humanise(code)
        };

        public static string SubscriptionStatus(ApprovedMailboxSubscription? subscription)
        {
            if (subscription is null)
            {
                return NoSubscription;
            }

            var state = $"{Humanise(subscription.LifecycleState.ToString())}.";
            var expires = $" Expires {OfficeTime(subscription.ExpiresAtUtc)}.";
            var failure = subscription.LastMaintenanceFailureCode is { } code
                ? $" Last failure: {Humanise(code)}."
                : string.Empty;
            return $"{state}{expires}{failure}";
        }
    }

    /// <summary>
    /// Administration › Valuation presets: the list, its rows and its
    /// confirmations and refusals. The area's own name lives in
    /// <see cref="Admin.ValuationPresets"/>.
    /// </summary>
    public static class ValuationPresets
    {
        public const string Presets = "Presets";
        public const string PresetLabel = "Label";
        public const string Amount = "Amount";
        public const string Save = "Save";
        public const string Enabled = "Enabled";
        public const string Remove = "Remove";
        public const string Add = "Add";
        public const string Actions = "Actions";
        public const string NoPresets = "No valuation presets are recorded.";
        public const string Created = "The valuation preset was created.";
        public const string Saved = "The valuation preset was saved.";
        public const string Disabled = "The valuation preset was disabled.";
        public const string Removed = "The valuation preset was removed.";
        public const string AlreadyRemoved = "That valuation preset was removed.";
        public const string Expired = "The form has expired. Retry the operation.";
        public const string LabelRequired = "Enter a label.";
        /// <summary>The amount's range in the operator's words (R431).</summary>
        public const string AmountRequired = "Enter an amount from £0.00 to £999,999.00.";
        public const string NotFound = "The valuation preset no longer exists.";
        public const string DuplicateLabel = "That label is already assigned to a valuation preset.";
        public const string StaleVersion = "The preset changed after this page was loaded. Reload it and try again.";
        public const string OperationConflict = "The form was already used for a different operation. Retry from the current page.";
        public const string NotAccepted = "The change was not accepted.";

        /// <summary>The preset count the panel head shows under the title.</summary>
        public static string PresetCount(int count) =>
            count == 1 ? "1 preset" : $"{count} presets";
    }

    /// <summary>
    /// The consolidated "Staff accounts &amp; roles" administration area —
    /// one list. The area's own name lives in
    /// <see cref="Admin.Accounts"/>; the three <see cref="StaffRole"/> names
    /// are already the settled operator words and go through
    /// <see cref="Humanise(string?)"/> rather than being spelled a second
    /// time here.
    /// </summary>
    public static class StaffAccounts
    {
        public const string Enabled = "Enabled";
        public const string Disabled = "Disabled";
        public const string PasswordChangeRequired = "Password change required";

        public static string State(bool isEnabled) => isEnabled ? Enabled : Disabled;

        public const string SignOffEngineer = "Sign-off Engineer";
        public const string Yes = "Yes";
        public const string No = "No";
        public const string PrintedName = "Printed name";
        public const string Qualifications = "Qualifications";
        public const string SignatureImage = "Signature image";
        public const string ReplaceSignature = "Replace signature";
        public const string DefaultSignOffEngineer = "Default sign-off Engineer";
        public const string Settings = "Settings";
        public const string Account = "Account";
        public const string Cancel = "Cancel";
        public const string SignatureMissing = "Signature missing";
        public const string QualificationsMissing = "Yes · qualifications missing";
        public const string Default = "Yes · default";
        public const string NotEligible = "Yes · not eligible";
        public const string PrintedNameRequired =
            "Enter the printed name for the Sign-off Engineer.";
        public const string SignatureInvalid =
            "Select a PNG signature image no larger than 1 MiB.";
        public const string DefaultRequiresEligible =
            "The default Sign-off Engineer must be eligible to sign off.";

        public static string SignOffState(StaffAccountSummary account)
        {
            if (!account.SignOff.IsSignOffEngineer)
            {
                return No;
            }

            if (!account.SignOff.HasSignature)
            {
                return SignatureMissing;
            }

            if (account.SignOff.Qualifications is null)
            {
                return QualificationsMissing;
            }

            if (account.SignOff.IsDefault)
            {
                // Sign-off flag and signature presence are already confirmed
                // by the earlier branches; only enabled state remains to
                // determine eligibility here.
                return account.IsEnabled ? Default : NotEligible;
            }

            return Yes;
        }
    }

    /// <summary>
    /// The Automation &amp; AI administration area's words —
    /// one list. <see cref="Admin.Automation"/> above is the area's name in the
    /// rail; these are the panels inside it, beside the AI jobs list
    /// (<see cref="AiJobs"/>).
    /// </summary>
    public static class AutomationAdmin
    {
        public const string AutomationPanel = "Automation";
        public const string AiSettingsPanel = "AI settings";
        public const string Enabled = "Enabled";
        public const string Stopped = "Stopped";
        public const string RegisteredClients = "Registered clients";
        public const string Stop = "Stop automation";
        public const string Start = "Start automation";
        public const string StartedNotice = "Automation started.";
        public const string StoppedNotice = "Automation stopped.";
        public const string Saved = "AI settings saved.";
        public const string NotAvailable = "Automation is not available.";
        public const string Unavailable = "This deployment does not have Automation or AI configuration available.";

        /// <summary>The one expiry sentence Administration forms share.</summary>
        public const string Expired = EmailTemplates.Expired;

        /// <summary>
        /// A Stop or Start whose one transaction failed and changed nothing:
        /// the sentence Administration already gives a change it did not take.
        /// </summary>
        public const string NotAccepted = CaseWorkspaceLabels.GlassCredential.NotAccepted;

        /// <summary>
        /// The one consequence sentence on the kill switch, from the design
        /// authority's necessary-copy allowance for a destructive action.
        /// </summary>
        public const string StopConsequence =
            "In-flight work remains visible and no result is discarded.";

        public const string SendToAi = "Send to AI";
        public const string Save = "Save AI settings";

        /// <summary>The state word for a switch an administrator holds.</summary>
        public static string SwitchState(bool enabled) => enabled ? Enabled : Stopped;

        public const string ClientIdentifier = "Client identifier";
        public const string GrantedScopes = "Granted scopes";

        /// <summary>
        /// The plain word for one granted Automation scope key
        /// (<c>AutomationMcp</c>'s <c>automation.*</c> constants). An
        /// unrecognised key is shown as itself, so a scope added without an
        /// update here is never silently hidden from the panel.
        /// </summary>
        public static string AutomationScope(string key) => key switch
        {
            "automation.cases" => "Cases",
            "automation.intake" => "Received files",
            "automation.documents" => "Documents",
            "automation.assessment" => "Assessment",
            "automation.mail" => "Mail",
            "automation.jobs" => "AI jobs",
            "automation.send" => "Sending",
            _ => key
        };
    }

    /// <summary>The retained post-report query's AI job words.</summary>
    public static class QueryResponseJobs
    {
        /// <summary>
        /// Deliberately not "Send query to AI", which would match the shape of
        /// "Send Unidentified to AI" and "Send to Claude". Those two send a
        /// record to be worked; this one queues a ledger row for a draft reply
        /// and sends nothing. The sibling shape would misdescribe the action,
        /// so the wording differs on purpose rather than by oversight.
        /// </summary>
        public const string Create = "Draft reply with AI";
        public const string Created = "AI reply job created.";
        public const string AutomationStopped = "Automation stopped";
        public const string AvailableInPostReportWork = "Available in post-report work";
        public const string CaseUnavailable = "Case unavailable";

        /// <summary>
        /// A Draft ready reply opens the message's composer on the draft
        /// (FRD-27): the Case's Next action, the Work Centre row, the Queries
        /// row and the message record all say the same thing.
        /// </summary>
        public const string ReplyWithDraft = "Reply with AI draft";

        /// <summary>The Work Centre and Administration instruction for this kind, whose stored instruction is the message id.</summary>
        public const string Instruction = "Draft a reply to the query";

        /// <summary>A job not yet Draft ready, in place of the start button: "AI draft · Queued".</summary>
        public static string Pending(Pegasus.Core.AiWork.AiJobState state) =>
            $"AI draft · {AiJobs.State(state)}";

        /// <summary>The composer's one line above a prefilled Message.</summary>
        public static string DraftLine(DateTimeOffset writtenAtUtc, string? client) =>
            string.IsNullOrWhiteSpace(client)
                ? $"Draft written {OfficeTime(writtenAtUtc)}. Edit before sending."
                : $"Draft written {OfficeTime(writtenAtUtc)} by {client}. Edit before sending.";

        /// <summary>
        /// A send that has gone but left the AI draft job it was composed on as
        /// it was (operator, 16 October 2026): the line beside the composer's
        /// send status and in <c>pegasus_mail_send</c>'s result, then one reason.
        /// </summary>
        public const string NotCompletedBySend = "The AI draft job was not completed by this send";
        public const string NotCompletedCancelled = "it was cancelled";
        public const string NotCompletedAlreadyCompleted = "it was already completed";
        public const string NotCompletedChanged = "it changed while the reply was being sent";

        /// <summary>"The AI draft job was not completed by this send: {reason}."</summary>
        public static string NotCompleted(Pegasus.Core.AiWork.AiJobConfirmRefusal refusal)
        {
            var reason = refusal switch
            {
                Pegasus.Core.AiWork.AiJobConfirmRefusal.Cancelled => NotCompletedCancelled,
                Pegasus.Core.AiWork.AiJobConfirmRefusal.AlreadyCompleted => NotCompletedAlreadyCompleted,
                Pegasus.Core.AiWork.AiJobConfirmRefusal.Changed => NotCompletedChanged,
                _ => throw new ArgumentOutOfRangeException(nameof(refusal))
            };
            return $"{NotCompletedBySend}: {reason}.";
        }
    }
}
