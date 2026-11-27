using System.Globalization;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Presentation;

public static partial class OperatorLabels
{
    /// <summary>
    /// The Administration Mail classifier page (FRD-17): its runs, their
    /// messages and its health, on the test instance beside the live
    /// classifier's (operator, 27 November 2026; R1318 to R1320). The words
    /// are the ones R1320 records; the operator may change any of them.
    /// </summary>
    public static class ClassifierRuns
    {
        public const string Health = "Health";
        public const string Runs = "Runs";
        public const string EnvironmentHeading = "Environment";
        public const string LastRun = "Last run";
        public const string OutcomeHeading = "Outcome";
        public const string RunsLast24Hours = "Runs in the last 24 hours";
        public const string FailedInARow = "Failed in a row";
        public const string SpentToday = "Spent today";
        public const string SpentThisMonth = "Spent this month";
        public const string NoRunRecorded = "No run recorded";

        public const string From = "From";
        public const string To = "To";
        public const string AllOutcomes = "All outcomes";
        public const string Which = "Runs";
        public const string AllRuns = "All runs";
        public const string RunsThatActed = "Runs that acted";
        public const string BothEnvironments = "Both";
        public const string Apply = "Apply";
        public const string Clear = "Clear";
        public const string NewestFirst = "Newest first";
        public const string OldestFirst = "Oldest first";

        public const string Started = "Started";
        public const string Mode = "Mode";
        public const string Eligible = "E-mails";
        public const string Actions = "What it did";
        public const string Cost = "Cost";
        public const string Took = "Took";
        public const string Open = "Open";
        public const string Previous = "Previous";
        public const string Next = "Next";
        public const string Write = "Write";
        public const string DryRun = "Dry run";
        public const string Nothing = "Nothing";

        public const string None = "No classifier runs are recorded yet.";
        public const string NoneMatch = "No runs match these filters.";
        public const string PeriodInvalid = "Choose a period of up to 31 days, with From on or before To.";

        public const string LiveOff = "Live runs are not shown on this instance: reading the live classifier's records is not switched on here.";
        public const string LiveRefused = "Live runs are not shown: this instance has not been given access to the live classifier's records.";
        public const string LiveUnavailable = "Live runs could not be read just now. Reload the page to try again.";

        // The run's detail.
        public const string RunTitle = "Classifier run";
        public const string Finished = "Finished";
        public const string StillRunning = "Still running";
        public const string NotFinished = "Did not finish";
        public const string Model = "Model";
        public const string Batch = "Batch";
        public const string ScanPages = "List pages read";
        public const string SpendingCap = "Spending cap";
        public const string Turns = "Turns";
        public const string AwaitingDecision = "Awaiting a decision";
        public const string Version = "Version";
        public const string Error = "Error";
        public const string Summary = "The classifier's summary";
        public const string Messages = "E-mails";
        public const string Time = "Time";
        public const string Email = "E-mail";
        public const string CategoryHeading = "Type";
        public const string Case = "Case";
        public const string Reason = "Reason";
        public const string NoSubject = "No subject";
        public const string NoLongerHeld = "No longer in the Inbox";
        public const string NoMessages = "The run dealt with no e-mail.";
        public const string ItemsCut = "Some e-mails were left out of this record because it was too long to keep.";
        public const string OpenInActionLogs = "Open in Action logs";
        public const string RunNotFound = "That run is not recorded.";
        public const string Close = "Close";

        /// <summary>A run's outcome as its chip reads.</summary>
        public static string Outcome(ClassifierRunOutcome outcome) => outcome switch
        {
            ClassifierRunOutcome.Running => "Running",
            ClassifierRunOutcome.DidNotFinish => "Did not finish",
            ClassifierRunOutcome.Completed => "Completed",
            ClassifierRunOutcome.NothingToDo => "Nothing to do",
            ClassifierRunOutcome.CandidatesHidden => "Mail hidden",
            ClassifierRunOutcome.CutShort => "Cut short",
            ClassifierRunOutcome.Failed => "Failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };

        /// <summary>Why a run was cut short, under its chip in the detail.</summary>
        public static string CutShortBecause(string? rawOutcome) =>
            ClassifierRunPolicy.MetSpendingCap(rawOutcome)
                ? "It reached its spending cap."
                : "It reached its turn limit.";

        public static string Environment(ClassifierRunEnvironment environment) => environment switch
        {
            ClassifierRunEnvironment.Live => "Live",
            ClassifierRunEnvironment.Test => "Test",
            _ => throw new ArgumentOutOfRangeException(nameof(environment))
        };

        /// <summary>What the run did with one e-mail; a dry run's acts are what it would have done.</summary>
        public static string Action(ClassifierRunAction action, bool dryRun) => (action, dryRun) switch
        {
            (ClassifierRunAction.Classified, false) => "Classified",
            (ClassifierRunAction.Classified, true) => "Would classify",
            (ClassifierRunAction.Linked, false) => "Linked to Case",
            (ClassifierRunAction.Linked, true) => "Would link to Case",
            (ClassifierRunAction.CaseCreated, false) => "Created Case",
            (ClassifierRunAction.CaseCreated, true) => "Would create Case",
            (ClassifierRunAction.OneOffCase, false) => "Created one-off Case",
            (ClassifierRunAction.OneOffCase, true) => "Would create one-off Case",
            (ClassifierRunAction.TriageOpened, false) => "Opened Triage",
            (ClassifierRunAction.TriageOpened, true) => "Would open Triage",
            (ClassifierRunAction.QueryOpened, false) => "Opened query",
            (ClassifierRunAction.QueryOpened, true) => "Would open query",
            (ClassifierRunAction.Left, _) => "Left as it is",
            (ClassifierRunAction.LeftForStaff, _) => "Left for staff",
            (ClassifierRunAction.Deferred, _) => "Left until the next run",
            (ClassifierRunAction.Failed, _) => "Failed",
            (ClassifierRunAction.SetAside, _) => "Failed; set aside until it changes",
            (ClassifierRunAction.NotReached, _) => "Not reached",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        /// <summary>The list's count of one act, as "3 classified".</summary>
        public static string ActionCount(ClassifierRunAction action, int count, bool dryRun)
        {
            var word = action switch
            {
                ClassifierRunAction.Classified => "classified",
                ClassifierRunAction.Linked => "linked",
                ClassifierRunAction.CaseCreated => count == 1 ? "Case created" : "Cases created",
                ClassifierRunAction.OneOffCase => count == 1 ? "one-off Case" : "one-off Cases",
                ClassifierRunAction.TriageOpened => count == 1 ? "Triage opened" : "Triages opened",
                ClassifierRunAction.QueryOpened => count == 1 ? "query opened" : "queries opened",
                ClassifierRunAction.Left => "left as it is",
                ClassifierRunAction.LeftForStaff => "left for staff",
                ClassifierRunAction.Deferred => "left until the next run",
                ClassifierRunAction.Failed => "failed",
                ClassifierRunAction.SetAside => "set aside",
                ClassifierRunAction.NotReached => "not reached",
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
            var proposal = dryRun && ClassifierRunPolicy.IsAct(action) ? " (proposed)" : string.Empty;
            return string.Create(CultureInfo.InvariantCulture, $"{count} {word}{proposal}");
        }

        /// <summary>The mail type the run gave, as the Inbox names it.</summary>
        public static string Category(string? code) =>
            code switch
            {
                null or "" => "—",
                "correspondence" => Inbox.Correspondence,
                _ when MailCategories.TryParse(code, out var category) => MailClassification(category),
                _ => Humanise(code)
            };

        /// <summary>
        /// A list hiding mail from the classifier (#1892): how many e-mails
        /// Pegasus counts as awaiting a decision, and for how many runs.
        /// </summary>
        public static string Hidden(int awaiting, int runs) =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"Mail is waiting that the classifier cannot see: Pegasus counts {awaiting} e-mail{(awaiting == 1 ? string.Empty : "s")} awaiting a decision, and {(runs <= 1 ? "the last run" : $"the last {runs} runs")} found none to look at.");

        public static string Usd(decimal amount) =>
            string.Create(CultureInfo.InvariantCulture, $"${amount:0.00}");

        public static string Duration(int milliseconds)
        {
            var span = TimeSpan.FromMilliseconds(milliseconds);
            return span.TotalMinutes >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalMinutes} min {span.Seconds} s")
                : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(span.Seconds, milliseconds > 0 ? 1 : 0)} s");
        }

        public static string FailuresInARow(int failures, bool atLeast) =>
            atLeast
                ? string.Create(CultureInfo.InvariantCulture, $"{failures} or more")
                : failures.ToString(CultureInfo.InvariantCulture);

        public static string Page(int page) =>
            string.Create(CultureInfo.InvariantCulture, $"Page {page}");
    }
}
