using System.Globalization;
using Pegasus.Core;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.Web.Presentation;

public static partial class OperatorLabels
{
    /// <summary>
    /// The v26 Work Centre's words (Work Centre D2, D4, D8, D9, P1–P6): the
    /// kind chips, the due-day groups and their empty states, the relative due
    /// and received text, the Office/Mine switch and the New cases arrival chip.
    /// </summary>
    public static class WorkCentre
    {
        /// <summary>The Office queues (FRD-15), each opening its Cases tab; Triages last.</summary>
        public const string NotReadyQueue = "Not ready";
        public const string ReviewQueue = "Review";
        public const string HeldQueue = "Held";
        public const string UnidentifiedQueue = "Unidentified";
        public const string Triages = "Triages";
        public const string Eyebrow = "Office-wide work";
        public const string Title = "Work Centre";
        public const string NeedsAttention = "Needs attention";
        public const string Office = "Office";
        public const string Mine = "Mine";
        /// <summary>Find within Needs attention (v30 WB): the field's label and its placeholder.</summary>
        public const string FindInNeedsAttention = "Find in Needs attention";
        public const string FindInWorkCentre = "Filter Work Centre items";
        public const string FindWorkCentrePlaceholder = "Filter by Case, VRM, claimant or action…";
        public const string FindPlaceholder = "Find in this work";
        public const string ClearFilters = "Clear filters";
        public const string NothingNeedsAttention = "Nothing needs attention";
        public const string NoMatches = "No work matches these filters.";
        /// <summary>The one line an entirely empty Work Centre shows (v30 WG).</summary>
        public const string NoWorkToShow = "No work to show.";
        public const string OfficeQueueTotals = "Office queue totals";
        public const string OfficeQueues = "Office queues";
        public const string AllCases = "All Cases";
        public const string Sections = "Work Centre sections";
        public const string NextAction = "Next action";
        public const string RecordDetail = "Record / detail";
        public const string Owner = "Owner";
        public const string Due = "Due";
        public const string Received = "Received";
        public const string AssignEngineer = "Assign Engineer";

        /// <summary>
        /// Take (#1213, v41 W8 and K9, signed off 19 October 2026): an
        /// Unassigned Case assigned to the person taking it, in one step.
        /// </summary>
        public const string Take = "Take";
        public const string Assign = "Assign";
        public const string Reassign = "Reassign";
        public const string ChooseEngineer = "Choose an Engineer";
        public const string Engineer = "Engineer";
        public const string NotRecorded = "Not recorded";
        public const string Unavailable = "Work Centre is unavailable. Refresh to run the live queues again.";
        public const string NewCases = "New cases";
        public const string SinceYouLastLooked = "Since you last looked";
        public const string NoNewCases = "No Case was created in the last 7 days";
        public const string NewCasesUnavailable = "New cases are unavailable.";
        public const string SystemActions = "System Actions";
        public const string NoSystemActions = "No system action in the last 7 days";
        public const string SystemActionsUnavailable = "System actions are unavailable.";
        public const string AiActions = "AI Actions";
        public const string NoAiActions = "No AI action in the last 7 days";
        public const string AiActionsUnavailable = "AI actions are unavailable.";
        public const string NoReasonRecorded = "No reason recorded";
        public const string OpenCase = "Open Case";
        public const string Previous = "Previous";
        public const string Next = "Next";

        /// <summary>The Activity panel (v32 item I): the figures head the columns, Today and This week the rows.</summary>
        public const string Activity = "Activity";
        public const string Today = "Today";
        public const string ThisWeek = "This week";
        public const string SentToEngineer = "Sent to Engineer";
        public const string ReportsSent = "Reports sent";
        public const string EmailsReceived = "E-mails received";
        public const string ActivityUnavailable = "Activity is unavailable.";

        /// <summary>The ledgers' column heads (v32 A).</summary>
        public const string ActionColumn = "Action";
        public const string CaseColumn = "Case";
        public const string SystemActionColumn = "System action";
        public const string MatchedDetailColumn = "Matched detail & signal";
        public const string WhenColumn = "When";
        public const string AiActionColumn = "AI action";
        public const string SummaryColumn = "Summary / output";
        public const string SignalLabel = "Signal";
        public const string DetailColumn = "Detail";
        public const string ArrivalColumn = "Arrival";
        public const string JobColumn = "Job";
        public const string StateColumn = "State";
        public const string InstructionColumn = "Instruction";
        public const string RecordColumn = "Record";
        public const string StartedColumn = "Started";
        public const string NoteColumn = "Note";

        public static string Updated(DateTimeOffset value) => $"Updated {OfficeClock(value)}";

        public static string Items(int count) => count == 1 ? "1 item" : string.Create(CultureInfo.InvariantCulture, $"{count} items");

        public static string Rows(int count) => count == 1 ? "1 row" : string.Create(CultureInfo.InvariantCulture, $"{count} rows");

        public static string ThisWeekCount(int count) =>
            string.Create(CultureInfo.InvariantCulture, $"This week: {count}");

        public static string NewCasesMeta(int count) => $"Last 7 days · {Rows(count)}";

        public static string TakenBy(string name) => $"Taken by {name}";

        public static string AiJobsMeta(int draftReady, int failed) =>
            string.Create(CultureInfo.InvariantCulture, $"{draftReady} draft ready · {failed} failed");

        public static string AttentionPaging(int page, int pages) =>
            string.Create(CultureInfo.InvariantCulture, $"Page {page} of {pages} · earliest due first");

        public static string NewCasesPaging(int page, int pages) =>
            string.Create(CultureInfo.InvariantCulture, $"Page {page} of {pages} · newest first");

        /// <summary>A Taken job's note (v32 item K): the time its turn ends, in the office clock.</summary>
        public static string TakenUntil(DateTimeOffset value) => $"Taken until {OfficeClock(value)}";

        public static string StartedBy(string name) => $"Started by {name}";

        /// <summary>The Received column: the age alone, the column heading carrying the word.</summary>
        public static string? ReceivedAge(DateTimeOffset? received, DateTimeOffset now)
        {
            if (received is not { } instant)
            {
                return null;
            }

            var days = LondonCalendar.DateAt(now).DayNumber - LondonCalendar.DateAt(instant).DayNumber;
            return days <= 0 ? "Today" : string.Create(CultureInfo.InvariantCulture, $"{days} d ago");
        }

        /// <summary>
        /// Early vehicle images paired with their Case (FRD-19, operator,
        /// 28 September 2026): the chip, the row's kind and its bold line.
        /// </summary>
        public const string VehicleImagesPaired = "Vehicle images paired";

        /// <summary>
        /// A Case with open tasks (operator, 6 October 2026): the row's kind
        /// and its action.
        /// </summary>
        public const string OpenTasks = "Open tasks";

        /// <summary>A Query row's action (operator, 27 October 2026).</summary>
        public const string OpenQuery = "Open query";

        public const string All = "All";
        public const string AutoMatched = "Auto-matched";
        public const string ImagesPaired = "Images paired";
        public const string RulesAndQueries = "Rules & queries";
        public const string EstimateDone = "Estimate done";
        public const string DraftToQuery = "Draft to a query";
        public const string Failed = "Failed";

        public static string SystemActionKindLabel(SystemActionKind kind) => kind switch
        {
            SystemActionKind.AutoMatchedMail => "Auto-matched e-mail",
            SystemActionKind.VehicleImagesPaired => VehicleImagesPaired,
            SystemActionKind.QueryOpenedAutomatically => "Query opened automatically",
            SystemActionKind.CancellationMatched => "Cancellation matched",
            _ => Humanise(kind.ToString())
        };

        public static string AiActionLabel(AiJobKind kind, AiJobState state) => state == AiJobState.Failed
            ? Failed
            : kind switch
            {
                AiJobKind.Estimate => EstimateDone,
                AiJobKind.QueryResponse => DraftToQuery,
                _ => AiJobs.Kind(kind)
            };

        /// <summary>
        /// A With Engineer Case its Engineer holds (#1213, v41 W8): the row's
        /// kind, its kind chip and its bold line, the stage's own word.
        /// </summary>
        public static readonly string WithEngineer = CaseStage(CaseLifecycleState.ReportPreparation);

        /// <summary>The kind filter chip (P3), in the mockup's order.</summary>
        public static string KindChip(NeedsAttentionKind kind) => kind switch
        {
            Pegasus.Core.Operations.NeedsAttentionKind.CaseChase => "Case",
            Pegasus.Core.Operations.NeedsAttentionKind.HeldDecision => "Held",
            Pegasus.Core.Operations.NeedsAttentionKind.ReviewCase => "Review",
            Pegasus.Core.Operations.NeedsAttentionKind.UnassignedEngineer => "Unassigned",
            Pegasus.Core.Operations.NeedsAttentionKind.VehicleImagesPaired => VehicleImagesPaired,
            Pegasus.Core.Operations.NeedsAttentionKind.Unidentified => "Unidentified",
            Pegasus.Core.Operations.NeedsAttentionKind.Triage => "Triage",
            Pegasus.Core.Operations.NeedsAttentionKind.OpenTasks => "Tasks",
            Pegasus.Core.Operations.NeedsAttentionKind.Query => "Query",
            Pegasus.Core.Operations.NeedsAttentionKind.AiDraft => "AI draft",
            Pegasus.Core.Operations.NeedsAttentionKind.WithEngineer => WithEngineer,
            _ => Humanise(kind.ToString())
        };

        /// <summary>An Open tasks row's bold line: the first open task, and how many more the Case has.</summary>
        public static string OpenTasksTitle(string firstTask, int moreCount) =>
            moreCount > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{firstTask} (+{moreCount} more)")
                : firstTask;

        /// <summary>The due-day group heading (P1) with the whole-list count.</summary>
        public static string Group(NeedsAttentionPriority priority, int count) => priority switch
        {
            Pegasus.Core.Operations.NeedsAttentionPriority.Overdue => string.Create(CultureInfo.InvariantCulture, $"Overdue ({count})"),
            Pegasus.Core.Operations.NeedsAttentionPriority.Today => string.Create(CultureInfo.InvariantCulture, $"Due today ({count})"),
            _ => string.Create(CultureInfo.InvariantCulture, $"Later ({count})")
        };

        /// <summary>
        /// The relative due text (D2, decided 13 September): "2 days overdue",
        /// "Due today", "Due tomorrow", "Due Fri", "Due 24 Sep". Core's due
        /// instants are the midnight that ends the due day, so the day named is
        /// the Europe/London date just before the instant.
        /// </summary>
        public static string DueText(DateTimeOffset? due, DateTimeOffset now)
        {
            if (due is not { } instant)
            {
                return "No due date";
            }

            var dueDay = LondonCalendar.DateAt(instant.AddTicks(-1));
            var today = LondonCalendar.DateAt(now);
            var days = today.DayNumber - dueDay.DayNumber;
            if (instant <= now)
            {
                return days switch
                {
                    <= 0 => "Overdue",
                    1 => "1 day overdue",
                    _ => string.Create(CultureInfo.InvariantCulture, $"{days} days overdue")
                };
            }

            return -days switch
            {
                <= 0 => "Due today",
                1 => "Due tomorrow",
                < 7 => $"Due {dueDay.ToString("ddd", CultureInfo.InvariantCulture)}",
                _ => $"Due {dueDay.ToString("d MMM", CultureInfo.InvariantCulture)}"
            };
        }

        /// <summary>The received age (P6): "Received today", "Received 3 d ago".</summary>
        public static string? ReceivedText(DateTimeOffset? received, DateTimeOffset now)
        {
            if (received is not { } instant)
            {
                return null;
            }

            var days = LondonCalendar.DateAt(now).DayNumber - LondonCalendar.DateAt(instant).DayNumber;
            return days <= 0
                ? "Received today"
                : string.Create(CultureInfo.InvariantCulture, $"Received {days} d ago");
        }

        public static string Arrival(CaseArrival arrival) => arrival switch
        {
            CaseArrival.Manual => "Manual",
            CaseArrival.Email => "E-mail",
            CaseArrival.PrincipalApi => "Principal API",
            CaseArrival.Automation => "Automation",
            _ => Humanise(arrival.ToString())
        };

        public static string ArrivalTone(CaseArrival arrival) => arrival switch
        {
            CaseArrival.Automation => "blue",
            CaseArrival.PrincipalApi => "navy",
            _ => "neutral"
        };

        /// <summary>The Triage kind's row title: a Triage without a finding.</summary>
        public static string TriageTitle(string? state) => state switch
        {
            nameof(Pegasus.Core.Triage.TriageState.AwaitingInformation) => "Awaiting information",
            _ => "Finding required"
        };

        public static string AiDraftTitle(string kind) =>
            Enum.TryParse<Pegasus.Core.AiWork.AiJobKind>(kind, out var parsed)
                ? $"{AiJobs.Kind(parsed)} draft ready"
                : $"{Humanise(kind)} draft ready";

        public static string AiDraftAction(string? action) => action switch
        {
            nameof(Pegasus.Core.AiWork.AiDraftAction.ReviewEstimate) => AiJobs.ReviewEstimate,
            nameof(Pegasus.Core.AiWork.AiDraftAction.OpenQuery) => AiJobs.OpenQuery,
            _ => AiJobs.Review
        };

        public const string Assigned = "The Case was assigned.";
        public const string AssignRefused = "The Case was not assigned because it changed, someone is editing it, or the action is not permitted.";
        public const string JobCompleted = "The AI job was completed.";
        public const string JobRefused = "The AI job changed before it could be completed. Refresh and try again.";

        /// <summary>Every row's Dismiss (FRD-15); the New cases icon button names its Case.</summary>
        public const string Dismiss = "Dismiss";

        public static string DismissRow(string reference) => $"{Dismiss} {reference}";
    }
}
