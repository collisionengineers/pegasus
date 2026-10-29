using System.Globalization;
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
    /// <summary>The primary navigation and the shell's section labels — one list.</summary>
    public static class Nav
    {
        public const string Work = "Work";
        public const string Manage = "Manage";
        public const string WorkCentre = "Work Centre";
        public const string Inbox = "Inbox";
        public const string Upload = "Upload";
        public const string Cases = "Cases";
        public const string Search = "Search";
        public const string Administration = "Administration";
    }

    /// <summary>The administration areas (§1.12) — one list.</summary>
    public static class Admin
    {
        public const string Accounts = "Staff accounts & roles";
        public const string Configuration = "Workflow configuration";
        public const string Mail = "Mail settings";
        public const string Automation = "Automation & AI";

        // C08 shell administration areas start
        public const string AiJobs = "AI jobs";
        public const string Reports = "Management Reports";
        public const string Health = "Service health";
        public const string ValuationPresets = "Valuation presets";
        // C08 shell administration areas end
        public const string ReleaseNotes = "Release notes";
        public const string ProblemReports = "Problem reports";
    }

    /// <summary>Release notes (FRD-12 What's new; FRD-17): the labels, statuses and acknowledgements.</summary>
    public static class ReleaseNotes
    {
        public const string New = "New release note";
        public const string Draft = "Draft";
        public const string None = "No release notes";
        public const string SaveDraft = "Save draft";
        public const string Publish = "Publish";
        public const string Saved = "Release note saved.";
        public const string Published = "Release note published.";
        public const string Conflict = "The release note changed before this edit was saved. Reload and try again.";
    }

    /// <summary>Report a problem (FRD-12; FRD-17): the dialog, the outcomes and the Administrator's list.</summary>
    public static class ProblemReports
    {
        public const string WhatHappened = "What happened";
        public const string Send = "Send";
        public const string Retry = "Retry";
        public const string None = "No problem reports";
        public const string Kept = "The report was kept but could not be sent.";
        public const string Sent = "Sent";
        public const string NotSent = "Not sent";
        public const string DeliveryUnknown = "Delivery unknown";
        public const string Unknown = DeliveryUnknown + ". An Administrator must check GitHub for the report ID before retrying.";

        // The Administrator's list: its headings, the Delivery unknown row's
        // actions and their outcomes (R533: the page's words, moved unchanged).
        public const string When = "When";
        public const string Who = "Who";
        public const string Page = "Page";
        public const string Case = "Case";
        public const string Status = "Status";
        public const string Issue = "Issue";
        public const string Actions = "Actions";
        public const string MatchingIssueNumber = "Matching issue number";
        public const string ConfirmIssue = "Confirm issue";
        public const string ConfirmNoIssue = "Confirm no issue";
        public const string IssueNumberRequired = "A valid issue number is required.";
        public const string CanBeRetried = "The report can now be retried.";
        public const string StatusChanged = "The report status changed. Reload and check it again.";

        public static string Reported(int issueNumber) =>
            string.Create(CultureInfo.InvariantCulture, $"Reported as #{issueNumber}.");

        /// <summary>
        /// The list's Page cell: the page a report was raised on, by the first
        /// segment of its route (<c>/Cases/…</c> reads Cases), so no record
        /// identifier shows (v41 A25). A route with no segment shows itself.
        /// </summary>
        public static string PageName(string route)
        {
            var path = route.AsSpan();
            var query = path.IndexOf('?');
            if (query >= 0)
            {
                path = path[..query];
            }

            path = path.Trim('/');
            var segment = path.IndexOf('/');
            var name = segment >= 0 ? path[..segment] : path;
            return name.IsEmpty ? route : name.ToString();
        }
    }

    /// <summary>The freshness words the shell and every page header share.</summary>
    public static class Freshness
    {
        public const string Current = "Current";
        public const string Never = "Never updated";

        public static string Label(string? status) => status switch
        {
            "loading" => "Refreshing",
            "stale" => "Stale",
            "partial" => "Partial",
            "unavailable" => "Unavailable",
            "failed" => "Failed",
            _ => Current
        };
    }

    /// <summary>
    /// What an action button says while its request runs (site.js
    /// <c>pegasusBusy</c>). A button names its word in
    /// <c>data-busy-label</c>; one without it says <see cref="Working"/>,
    /// which the layouts render as the page default. Every word here is a
    /// capitalised "-ing" phrase ending in an ellipsis: after five seconds the
    /// script prefixes <see cref="Still"/> and lowercases the first letter
    /// ("Still saving…"), which the architecture test pins.
    /// </summary>
    public static class Busy
    {
        /// <summary>The word a press that is taking a while gains in front of its busy word.</summary>
        public const string Still = "Still";

        public const string Working = "Working…";
        public const string Adding = "Adding…";
        public const string Logging = "Logging…";
        public const string Resolving = "Resolving…";
        public const string Applying = "Applying…";
        public const string Archiving = "Archiving…";
        public const string Assigning = "Assigning…";
        public const string Authorising = "Authorising…";
        public const string Changing = "Changing…";
        public const string Checking = "Checking…";
        public const string Clearing = "Clearing…";
        public const string Closing = "Closing…";
        public const string Completing = "Completing…";
        public const string Confirming = "Confirming…";
        public const string Creating = "Creating…";
        public const string Deleting = "Deleting…";
        public const string Disabling = "Disabling…";
        public const string Discarding = "Discarding…";
        public const string Dismissing = "Dismissing…";
        public const string Downloading = "Downloading…";
        public const string Drafting = "Drafting…";
        public const string Duplicating = "Duplicating…";
        public const string Enabling = "Enabling…";
        public const string Exporting = "Exporting…";
        public const string Finishing = "Finishing…";
        public const string Generating = "Generating…";
        public const string Importing = "Importing…";
        public const string Linking = "Linking…";
        public const string Loading = "Loading…";
        public const string LookingUp = "Looking up…";
        public const string Marking = "Marking…";
        public const string Opening = "Opening…";
        public const string Pausing = "Pausing…";
        public const string Placing = "Placing on hold…";
        public const string Publishing = "Publishing…";
        public const string Recording = "Recording…";
        public const string Refusing = "Refusing…";
        public const string Registering = "Registering…";
        public const string Releasing = "Releasing…";
        public const string Removing = "Removing…";
        public const string Reopening = "Reopening…";
        public const string Replacing = "Replacing…";
        public const string Resetting = "Resetting…";
        public const string Restoring = "Restoring…";
        public const string Resuming = "Resuming…";
        public const string Retrying = "Retrying…";
        public const string Returning = "Returning…";
        public const string Revoking = "Revoking…";
        public const string Saving = "Saving…";
        public const string Searching = "Searching…";
        public const string Sending = "Sending…";
        public const string SigningIn = "Signing in…";
        public const string SigningOut = "Signing out…";
        public const string Starting = "Starting…";
        public const string Stopping = "Stopping…";
        public const string Tagging = "Tagging…";
        public const string Unlinking = "Unlinking…";
        public const string Uploading = "Uploading…";
        public const string Recovering = "Recovering…";

        /// <summary>What a fetched download says when the file did not arrive and the server gave no reason.</summary>
        public const string DownloadFailed = "The file could not be downloaded. Try again.";

        /// <summary>
        /// What a button answered in place says beside its tick for a moment
        /// after its action succeeded (<c>data-busy-done</c>). A button without
        /// one shows the tick beside its own label. A press that reloads the
        /// page names none: the page's notice is its result.
        /// </summary>
        public static class Done
        {
            public const string Saved = "Saved";
            public const string Generated = "Generated";
            public const string Downloaded = "Downloaded";
            public const string Exported = "Exported";
            public const string Valued = "Valuation received";
        }
    }

    /// <summary>The shell's own words (v26 shell): the rail foot and the bell.</summary>
    public static class Shell
    {
        public const string Collapse = "Collapse";
        public const string CollapseNavigation = "Collapse navigation";
        public const string ExpandNavigation = "Expand navigation";
        public const string NewCase = "New case";
        public const string Notifications = "Notifications";
        public const string NotificationsUnavailable = "Notifications unavailable.";
        public const string NoNotifications = "No notifications";
        public const string MarkAllRead = "Mark all read";
        public const string Unread = "Unread";
        public const string AccessDenied = "Access denied";
        public const string AccessDeniedSentence = "Your account does not have access to this page.";
        public const string AdministrationDenied = "Administration is available to Administrators only.";
        public const string WhatsNew = "What’s new";
        public const string GotIt = "Got it";
        public const string ReportProblem = "Report a problem";
        public const string ReleaseNotes = "Release notes";

        /// <summary>"Notifications · 3 unread", the bell's accessible name while anything is unread.</summary>
        public static string BellLabel(int unread) =>
            unread > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{Notifications} · {unread} unread")
                : Notifications;
    }
}
