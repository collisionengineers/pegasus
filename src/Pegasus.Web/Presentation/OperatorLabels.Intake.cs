using System.Globalization;
using Pegasus.Core;
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
    /// The Upload surfaces' words (v30 Upload E, "Inspection studio",
    /// 25 September 2026): choosing files, the review of a stored upload and
    /// its one Case decision.
    /// </summary>
    public static class Upload
    {
        public const string Title = "Upload";
        public const string ChooseHeading = "Choose the files for this upload";
        public const string DropHeading = "Drop files here";
        public const string AcceptedKinds = "Images, documents, emails and video";
        public const string AcceptedExtensions = "JPG, JPEG, PNG, PDF, DOC, DOCX, EML, MSG, MP4 or MOV";
        public const string Choose = "Choose files";
        public const string Clear = "Clear";
        public const string SelectedFiles = "Selected files";
        public const string FilesInUpload = "Files in this upload";
        public const string NamesAndOutcomes = "File names and outcomes";
        public const string NewUpload = "New upload";
        public const string Open = "Open";
        public const string OpenOriginal = "Open original";
        public const string Previous = "Previous";
        public const string Next = "Next";
        public const string CheckFiles = "Check the selected files";
        public const string Remove = "Remove";

        // A destination declared before the upload: Add evidence on a Case
        // page opened Upload for that Case (FRD-18). The card alone says
        // where the files go; no sentence follows it.
        public const string AddingTo = "Adding to";
        public const string DestinationUnavailable = "This Case is not available for evidence. Files you upload here will need a destination after upload.";

        // The decision panel.
        public const string PendingEyebrow = "Files received";
        public const string PendingTitle = "Processing your files";
        public const string ChooseEyebrow = "Choose a destination";
        public const string ChooseTitle = "Which Case do these files belong to?";
        public const string NoMatchEyebrow = "No suggested match";
        public const string NoMatchTitle = "Find the right Case";
        public const string Review = "Review and add to Case";
        public const string FindCase = "Find a Case";
        public const string FindAnotherCase = "Find another Case";
        public const string SearchLabel = "Search by Case/PO, registration or claimant";
        public const string SearchPlaceholder = "Case/PO, registration or claimant";
        public const string Search = "Search";
        public const string RetrySearch = "Retry search";
        public const string SearchUnavailable = "Case search is unavailable.";
        public const string SearchUnavailableSentence = "Your upload is retained. Try again in a moment.";
        public const string VehicleImages = "Vehicle images";
        public const string RegisteredAutomatically = "Registered automatically";
        public const string Proposal = "Review new Case proposal";
        public const string LeaveUndecided = "Leave undecided";
        public const string Discard = "Discard upload";
        public const string DiscardTitle = "Discard this upload?";
        public const string DiscardAcknowledgement = "I understand that every file in this upload will be discarded.";
        public const string DiscardMissing = "Select the confirmation before discarding.";
        public const string Cancel = "Cancel";
        public const string ConfirmSentence = "Confirm the exact destination for this upload.";
        public const string ReviewRequiredEyebrow = "Review required";
        public const string CompleteEyebrow = "Upload complete";
        public const string AttachedTitle = "Added to Case";
        public const string ClosedEyebrow = "Upload closed";
        public const string DiscardedTitle = "Upload discarded";
        public const string DiscardedSentence = "The source files and processing record are retained.";
        public const string StartAnother = "Start another upload";
        public const string AlreadyReceived = "Already received";
        public const string UnreadableTitle = "The files could not be read";
        public const string UnreadableSentence = "The originals are retained in Unidentified. Open a file to inspect it, or review the item in Unidentified.";
        public const string OpenUnidentified = "Open Unidentified item";
        public const string IncompleteTitle = "This upload is incomplete";
        public const string IncompleteSentence = "One file is unavailable. Refresh the status before choosing a Case; nothing has been added.";
        public const string MixedOutcomesTitle = "Review each file";
        public const string MixedOutcomesSentence = "The files in this upload reached different outcomes. Each file's outcome is listed with it.";

        // File states.
        public const string StateReceived = "Received";
        public const string StateProcessing = "Processing";
        public const string StateReady = "Ready";
        public const string StateCouldNotBeRead = "Could not be read";
        public const string StateAdded = "Added to Case";
        public const string StateAddedUnreadable = "Added · unreadable";
        public const string StateDiscarded = "Discarded";
        public const string StateFailed = "Not processed";

        public static string Files(int count) => count == 1 ? "1 file" : string.Create(CultureInfo.InvariantCulture, $"{count} files");

        public static string UploadFiles(int count) => $"Upload {Files(count)}";

        public static string AddFilesTitle(int count) => $"Add {Files(count)} to this Case?";

        public static string ConfirmAdd(string reference) => $"Confirm and add to {reference}";

        public static string OpenCase(string reference) => $"Open {reference}";

        /// <summary>The way back from a declared upload to the Case it is for (v42 Upload E).</summary>
        public static string BackToCase(string reference) => $"Back to {reference}";

        /// <summary>The declared-upload wording, reused wherever filing resumes after the request.</summary>
        public const string AppearUnderFiles = "They appear under Files once processed.";

        public static string AttachedSentence(int count) => $"{Files(count)} added to the confirmed destination. {AppearUnderFiles}";

        public static string PendingSentence(int count) => $"All {Files(count)} are stored. The outcome will appear here when processing finishes.";

        public static string DiscardSentence(int count) => $"Discard all {Files(count)} in this upload. The source files and processing record will be retained.";

        public static string OriginalFiles(int count) => count == 1 ? "1 original file" : string.Create(CultureInfo.InvariantCulture, $"{count} original files");

        public static string CouldNotBeReadTitle(int count) => count == 1 ? "1 file could not be read" : string.Create(CultureInfo.InvariantCulture, $"{count} files could not be read");

        public static string CouldNotBeReadSentence(int total) => total == 1
            ? "The original is retained. Check the marked file before adding this upload."
            : string.Create(CultureInfo.InvariantCulture, $"All {total} originals are retained. Check the marked file before adding this upload.");

        public static string CouldNotBeReadNote(int count) => count == 1
            ? "1 file could not be read; its original will be included."
            : string.Create(CultureInfo.InvariantCulture, $"{count} files could not be read; their originals will be included.");

        public static string NoSearchMatches(string term) => $"No Cases or Triage items match “{term}”. Try another reference.";

        /// <summary>The Case page's notice after Add evidence: the files are stored and on their way to Files.</summary>
        public static string ReceivedForCase(int count, string reference) =>
            $"{Files(count)} received for {reference}. {AppearUnderFiles}";

        public static string AlreadyReceivedForCase(string reference) => $"Already received for {reference}.";

        /// <summary>What Pegasus pulled out of a document, on the review's inspector.</summary>
        public static string PhotographsFound(int count) => count == 1
            ? "1 photograph found in this file"
            : string.Create(CultureInfo.InvariantCulture, $"{count} photographs found in this file");


        /// <summary>"Received today, 09:41" or "Received 24 Sep, 09:41", in the office's zone.</summary>
        public static string ReceivedAt(DateTimeOffset value, DateTimeOffset now)
        {
            var day = LondonCalendar.DateAt(value);
            var today = LondonCalendar.DateAt(now);
            var when = day == today ? "today" : day.ToString("d MMM", CultureInfo.InvariantCulture);
            return $"Received {when}, {OfficeClock(value)}";
        }

        /// <summary>
        /// The file's kind in the operator's words, from its media type. This
        /// stays its own table because the upload review names formats ("JPEG
        /// image", "PDF document", "Email") where Core's one file-kind table
        /// gives the broad kind and the Unidentified wording.
        /// </summary>
        public static string Kind(string? mediaType, string fileName) => (mediaType ?? string.Empty).ToLowerInvariant() switch
        {
            "image/jpeg" => "JPEG image",
            "image/png" => "PNG image",
            var image when image.StartsWith("image/", StringComparison.Ordinal) => "Image",
            "application/pdf" => "PDF document",
            "application/msword" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "Word document",
            "message/rfc822" or "application/vnd.ms-outlook" => "Email",
            "video/mp4" or "video/quicktime" => "Video",
            _ => Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant() is { Length: > 0 } extension ? extension + " file" : "File"
        };

        public static string ImageIntakeState(Pegasus.Core.ImageIntake.ImageInitiatedCaseState state) => state switch
        {
            Pegasus.Core.ImageIntake.ImageInitiatedCaseState.AwaitingInstruction => "Awaiting instruction",
            Pegasus.Core.ImageIntake.ImageInitiatedCaseState.MergedIntoInstructionCase => "Merged into a Case",
            Pegasus.Core.ImageIntake.ImageInitiatedCaseState.StaffClosed => "Closed",
            _ => Humanise(state.ToString())
        };

    }

    /// <summary>
    /// What the DVLA/MOT lookup did, for the Vehicle panel's own line.
    /// </summary>
    /// <remarks>
    /// A lookup that answers fills the empty vehicle fields, so it is visible
    /// in the values themselves. A not-found, failed or throttled outcome
    /// fills nothing and once rendered nowhere, which made a lookup that
    /// answered "no such vehicle" look exactly like one that never ran. The
    /// line states which it was.
    /// </remarks>
    public static class VehicleLookup
    {
        /// <summary>No lookup has recorded an answer for this case yet.</summary>
        public const string NotYetLookedUp = "Not yet looked up";

        /// <summary>
        /// The one line under the vehicle facts: when the latest lookup ran
        /// and what it returned, or that none has.
        /// </summary>
        public static string Outcome(VehicleLookupObservation? observation)
        {
            if (observation is null)
            {
                return NotYetLookedUp;
            }

            var at = OfficeTime(observation.Provenance.RetrievedAtUtc);
            return observation.Outcome switch
            {
                VehicleLookupOutcome.Current
                    or VehicleLookupOutcome.Stale
                    or VehicleLookupOutcome.Partial =>
                    $"Looked up {at} · {Evidence(observation.Outcome)}",
                VehicleLookupOutcome.NotFound =>
                    $"Not found for {observation.Registration} ({at})",
                _ => $"Failed: {FailureReason(observation.Failure?.Code)} ({at})"
            };
        }

        /// <summary>
        /// How current the evidence is, in the freshness words the rest of the
        /// product uses.
        /// </summary>
        private static string Evidence(VehicleLookupOutcome outcome) => outcome switch
        {
            VehicleLookupOutcome.Stale => Freshness.Label("stale"),
            VehicleLookupOutcome.Partial => Freshness.Label("partial"),
            _ => Freshness.Current
        };

        /// <summary>
        /// A provider failure code as words, with both provider names spelled
        /// the way the operator knows them.
        /// </summary>
        public static string FailureReason(string? failureCode) =>
            Humanise(failureCode)
                .Replace("Dvla", "DVLA", StringComparison.Ordinal)
                .Replace("dvla", "DVLA", StringComparison.Ordinal)
                .Replace("Dvsa", "DVSA", StringComparison.Ordinal)
                .Replace("dvsa", "DVSA", StringComparison.Ordinal);
    }

    /// <summary>Administration › Logs › Intake log (13 September): the outcome words and what a receipt became.</summary>
    public static class IntakeLog
    {
        public const string Logs = "Logs";
        public const string ActionLogsTab = "Action logs";
        public const string IntakeLogTab = "Intake log";
        public const string FailedFiles = "Failed files";
        public const string OldestFileWaiting = "Oldest file waiting";
        public const string Reevaluate = "Re-evaluate with current policy";
        public const string RetryAllocation = "Retry allocation";
        public const string RetryOcr = "Retry OCR";
        public const string OpenFile = "Open file";
        public const string OpenMessage = "Open message";

        /// <summary>The Action logs period error (R438): the Management Reports sentence.</summary>
        public const string PeriodInvalid = "Choose a valid date range.";

        /// <summary>A drawer action posted without a valid operation key or version.</summary>
        public const string Expired = EmailTemplates.Expired;

        public const string ReasonRequired = "Give a reason for this action.";

        /// <summary>
        /// A reason over Core's 500-character limit. Its own words wait for the
        /// operator (R437); until then it reads as a missing reason.
        /// </summary>
        public const string ReasonTooLong = ReasonRequired;

        /// <summary>
        /// A stale or refused drawer action. The store's refusals (already being
        /// evaluated, OCR no longer retryable, a closed Unidentified item, no
        /// evaluation work) read this until the operator gives their words (R437).
        /// </summary>
        public const string ItemChanged = "The received item changed. Reload it before trying again.";

        /// <summary>Re-evaluate refused because the retained file no longer matches what was received.</summary>
        public static string RetainedFileMismatch => IntakeFailure("artifact_integrity_failure");

        public static string Outcome(IntakeLogOutcome outcome) => outcome switch
        {
            IntakeLogOutcome.CaseCreated => "Case created",
            IntakeLogOutcome.Unidentified => "Unidentified",
            IntakeLogOutcome.NoCaseFound => Inbox.NoCaseFound,
            IntakeLogOutcome.Triage => "Triage",
            IntakeLogOutcome.VehicleImages => "Vehicle images",
            IntakeLogOutcome.CouldNotBeRead => "Could not be read",
            IntakeLogOutcome.ProcessingFailed => "Processing failed",
            IntakeLogOutcome.Closed => "Closed",
            IntakeLogOutcome.AllocationFailed => "Allocation failed",
            IntakeLogOutcome.OcrFailed => "OCR failed",
            _ => Humanise(outcome.ToString())
        };

        /// <summary>The page a produced record opens.</summary>
        public static string BecameHref(IntakeLogBecame became) => became.Kind switch
        {
            IntakeLogBecameKind.Case => $"/Cases/{became.Id:D}",
            IntakeLogBecameKind.Unidentified => $"/Unidentified/{became.Id:D}",
            IntakeLogBecameKind.Triage => $"/Cases/{became.Id:D}",
            IntakeLogBecameKind.ImageIntake => $"/VehicleImages/{became.Id:D}",
            _ => "/"
        };

        public static string Source(IntakeLogSource source) =>
            string.Join(" · ", new[] { SourceChannel(source.Channel), source.Address, source.Detail }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        /// <summary>"2 processing · 1 allocation", or null when neither ran more than once.</summary>
        public static string? Attempts(IntakeLogRow row) =>
            row.ProcessingAttempts > 1 || row.AllocationAttempts > 1
                ? $"{row.ProcessingAttempts} processing · {row.AllocationAttempts} allocation"
                : null;
    }

    /// <summary>Crop and tag on a pre-Case image (image record, Triage, Unidentified; v26).</summary>
    public static class PreCaseImages
    {
        public const string Crop = "Crop";
        public const string Cropped = "Cropped";
        public const string DrawHint = "Drag across the image to frame it";
        public const string Apply = "Apply";
        public const string Clear = "Clear";
        public const string Cancel = "Cancel";
        public const string Tag = "Tag";
        public const string AddTag = "Add a tag";
        public const string CropSaved = "The crop was saved.";
        public const string CropCleared = "The crop was cleared.";
        public const string TagAdded = "The tag was added.";
        public const string TagRemoved = "The tag was removed.";
    }

    /// <summary>Inbox and the message record: three scopes, a Type select, Dismiss and Restore.</summary>
    public static class Inbox
    {
        public const string All = "All";
        public const string AllPrincipals = "All principals";
        public const string Type = "Type";
        public const string Correspondence = "Correspondence";
        public const string NewInstructionInspection = "New instruction · Inspection";
        public const string NewInstructionAudit = "New instruction · Audit";
        public const string NewInstructionInspectionAndAudit = "New instruction · Inspection and Audit";
        public const string ChangeCase = "Change case";

        /// <summary>The message record's cog menu (v42 G) and the foot line naming who decided its type and when.</summary>
        public const string MessageSettings = "Message settings";
        public const string Decided = "Decided";
        public const string DismissedScope = "Dismissed";

        /// <summary>Received mail with no linked Case: its scope and its outcome chip (operator, 13 October 2026).</summary>
        public const string NoCaseFound = "No Case Found";
        public const string Dismiss = "Dismiss";
        public const string Restore = "Restore";
        public const string DismissedNotice = "Dismissed";
        public const string RestoredNotice = "Restored";

        /// <summary>The message dialog's head buttons (v41 I6), as drawn.</summary>
        public const string PreviousMessage = "Previous message";
        public const string NextMessage = "Next message";

        /// <summary>
        /// The line above the list after a bulk post that could not change some
        /// ticked messages (operator, 22 October 2026, R180): the number not
        /// changed; those rows stay ticked so they can be retried.
        /// </summary>
        public static string NotChanged(int count) => $"{count} not changed";

        // A classification correction's refusals, on the message record and for ticked rows alike.
        public const string ChooseClassification = "Choose a valid classification.";
        public const string ChooseCaseType = "Choose a valid case type.";
        public const string CorrectionReasonRequired = "Explain why this classification is being corrected.";

        // The correction's words: the bulk dialog's fields and the record's
        // Classification chips, which apply a correction at once.
        public const string Classification = "Type";
        public const string ChooseClassificationOption = "Choose classification";
        public const string ChooseCaseTypeOption = "Choose case type";
        public const string CorrectionReason = "Reason for correction";
        public const string SaveCorrection = "Save correction";

        // The record's Case row (v41 I9): the Case / PO picker and its buttons.
        public const string CasePo = "Case / PO";
        public const string CasePoHint = "Search by Case / PO, registration, claimant, claim number, or Principal.";
        public const string LinkToCase = "Link to Case";
        public const string ChangeLinkedCase = "Change linked Case";
        public const string Unlink = "Unlink";
        public const string OpenFile = "Open file";
        public const string OpenFullMessage = "Open full message";

        // The Case cell naming the Case/PO or the Triage's t. reference.
        public const string Case = "Case";
        public const string NoCase = "None";
        public const string OpenTriage = "Open Triage";

        /// <summary>
        /// The chip for a message on a Triage Case or asking for one. It is the
        /// Triage destination's own word, not a new one.
        /// </summary>
        public const string Triage = "Triage";

        /// <summary>"1 attachment", "3 attachments": a list row's count.</summary>
        public static string AttachmentCount(int count) => Counted(count, "attachment");

        /// <summary>
        /// The ledger row's Type chip tone (v42 H): navy for a New instruction,
        /// amber for a Triage request, neutral for a Cancellation.
        /// </summary>
        public static string CategoryTone(Pegasus.Core.Intake.MailCategory category) => category switch
        {
            Pegasus.Core.Intake.MailCategory.NewInstruction => "navy",
            Pegasus.Core.Intake.MailCategory.TriageRequest => "amber",
            _ => "neutral"
        };

        private static string Counted(int count, string noun) =>
            $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
    }

    /// <summary>Upload: one upload is one group with one decision (Upload, 13 September).</summary>
    public static class UploadDecision
    {
        public const string Attached = "Attached to a Case";
        public const string VehicleImages = "Registered as vehicle images";
        public const string Unidentified = "Unidentified";
    }

    /// <summary>
    /// Register images: the staff registration that makes Unidentified image
    /// material an Image-initiated Case, the same act on the Unidentified record
    /// and on the upload review (operator, 28 October 2026, R463).
    /// </summary>
    public static class RegisterImages
    {
        public const string Action = "Register images";
        public const string VehicleRegistration = "Vehicle registration";
        public const string Reason = "Reason";
        public const string Cancel = "Cancel";

        public static string Registered(string reference) => $"Registered as vehicle images {reference}.";
    }
}
