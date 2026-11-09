using System.Globalization;
using Pegasus.Core.PhotoCapture;
using CaptureRecord = Pegasus.Core.PhotoCapture.PhotoCapture;

namespace Pegasus.Web.Presentation;

public static partial class OperatorLabels
{
    /// <summary>
    /// Guided photo capture's staff words (FRD-28; R773, R774): the Photo
    /// capture page and its two tabs, the Send, link and Contacts surfaces and
    /// the Case's Files → Images story. The wording is the handover
    /// prototype's (5 November 2026) where it has one.
    /// </summary>
    public static class PhotoCapture
    {
        public const string Title = "Photo capture";

        // The two tabs.
        public const string ComingIn = "Coming in";
        public const string LinksWeSent = "Links we sent";
        public const string ComingInKey = "coming-in";
        public const string LinksWeSentKey = "sent";

        // Columns.
        public const string Vehicle = "Vehicle";
        public const string From = "From";
        public const string Customer = "Customer";
        public const string Case = "Case";
        public const string Status = "Status";
        public const string Photos = "Photos";
        public const string Received = "Received";
        public const string SentBy = "Sent by";
        public const string NextReminder = "Next reminder";
        public const string Action = "Action";

        // Filters.
        public const string All = "All";
        public const string Search = "Search";
        public const string SearchPlaceholder = "Search by reg, name, case or company";
        public const string Apply = "Apply";
        public const string Clear = "Clear";
        public const string NothingHere = "No photo links";

        // How the photos came (FRD-28 Standing photo links).
        public const string TheirStaffTookThem = "Their staff took them";
        public const string SentOnToTheirCustomer = "Sent on to their customer";
        public const string CustomerFromOurWebsite = "Customer, from our website";
        public const string Website = "Website";

        public static string Source(PhotoCaptureOrigin origin) => origin switch
        {
            PhotoCaptureOrigin.StandingStaff => TheirStaffTookThem,
            PhotoCaptureOrigin.StandingForwarded => SentOnToTheirCustomer,
            PhotoCaptureOrigin.Website => CustomerFromOurWebsite,
            PhotoCaptureOrigin.CaseLink => LinksWeSent,
            _ => throw new InvalidOperationException($"Unknown photo capture origin '{(int)origin}'.")
        };

        /// <summary>The Coming in origins, in the order the From filter offers them.</summary>
        public static IReadOnlyList<PhotoCaptureOrigin> ComingInOrigins { get; } =
            [PhotoCaptureOrigin.StandingStaff, PhotoCaptureOrigin.StandingForwarded, PhotoCaptureOrigin.Website];

        public static string OriginKey(PhotoCaptureOrigin origin) => origin switch
        {
            PhotoCaptureOrigin.StandingStaff => "staff",
            PhotoCaptureOrigin.StandingForwarded => "forwarded",
            PhotoCaptureOrigin.Website => "website",
            PhotoCaptureOrigin.CaseLink => "case",
            _ => throw new InvalidOperationException($"Unknown photo capture origin '{(int)origin}'.")
        };

        public static PhotoCaptureOrigin? OriginFromKey(string? key) => key switch
        {
            "staff" => PhotoCaptureOrigin.StandingStaff,
            "forwarded" => PhotoCaptureOrigin.StandingForwarded,
            "website" => PhotoCaptureOrigin.Website,
            _ => null
        };

        // Statuses (FRD-28 Statuses and reminders).
        public static string State(PhotoCaptureState state) => state switch
        {
            PhotoCaptureState.SentNotOpened => "Sent, not opened",
            PhotoCaptureState.Opened => "Opened",
            PhotoCaptureState.PhotosComingIn => "Photos coming in",
            PhotoCaptureState.SomethingMissing => "Something missing",
            PhotoCaptureState.NeedsCall => "Needs a phone call",
            PhotoCaptureState.Complete => "Complete",
            PhotoCaptureState.Expired => "Expired",
            PhotoCaptureState.Cancelled => "Cancelled",
            _ => throw new InvalidOperationException($"Unknown photo capture state '{(int)state}'.")
        };

        /// <summary>The chip tone: amber incomplete, blue active, green complete, red needs a person, neutral closed.</summary>
        public static string StateTone(PhotoCaptureState state) => state switch
        {
            PhotoCaptureState.SentNotOpened => "neutral",
            PhotoCaptureState.Opened => "blue",
            PhotoCaptureState.PhotosComingIn => "amber",
            PhotoCaptureState.SomethingMissing => "amber",
            PhotoCaptureState.NeedsCall => "red",
            PhotoCaptureState.Complete => "green",
            PhotoCaptureState.Expired or PhotoCaptureState.Cancelled => "neutral",
            _ => "neutral"
        };

        public static string StateKey(PhotoCaptureState state) => state switch
        {
            PhotoCaptureState.SentNotOpened => "not-opened",
            PhotoCaptureState.Opened => "opened",
            PhotoCaptureState.PhotosComingIn => "coming-in",
            PhotoCaptureState.SomethingMissing => "missing",
            PhotoCaptureState.NeedsCall => "call",
            PhotoCaptureState.Complete => "complete",
            PhotoCaptureState.Expired => "expired",
            PhotoCaptureState.Cancelled => "cancelled",
            _ => throw new InvalidOperationException($"Unknown photo capture state '{(int)state}'.")
        };

        public static PhotoCaptureState? StateFromKey(string? key) =>
            Enum.GetValues<PhotoCaptureState>().Cast<PhotoCaptureState?>()
                .FirstOrDefault(state => string.Equals(StateKey(state!.Value), key, StringComparison.Ordinal));

        public static string StateIcon(PhotoCaptureState state) => state switch
        {
            PhotoCaptureState.SentNotOpened => "icon-mail",
            PhotoCaptureState.Opened => "icon-mail-open",
            PhotoCaptureState.PhotosComingIn => "icon-images",
            PhotoCaptureState.SomethingMissing => "icon-alert-triangle",
            PhotoCaptureState.NeedsCall => "icon-bell",
            PhotoCaptureState.Complete => "icon-check-circle",
            PhotoCaptureState.Expired => "icon-clock",
            PhotoCaptureState.Cancelled => "icon-x",
            _ => "icon-list"
        };

        // Cell values.
        public const string NotGiven = "Not given";
        public const string None = "None";
        public const string AwaitingInstruction = "Awaiting instruction";
        public const string MatchedAutomatically = "Matched automatically";
        public const string AlreadyInstructed = "Already instructed";

        /// <summary>"34 photos" once complete, "22 so far" while photos are still coming.</summary>
        public static string PhotoCount(int count, bool complete) =>
            complete
                ? string.Create(CultureInfo.InvariantCulture, $"{count} photos")
                : string.Create(CultureInfo.InvariantCulture, $"{count} so far");

        public static string Registration(string? normalized) =>
            normalized is { Length: > 0 } value ? PhotoCaptureText.DisplayRegistration(value) : NotGiven;

        // Actions.
        public const string SendAPhotoLink = "Send a photo link";
        public const string SendPhotoLink = "Send photo link";
        public const string Resend = "Resend";
        public const string Remind = "Remind";
        public const string LogCall = "Log call";
        public const string Extend = "Extend";
        public const string CancelLink = "Cancel link";
        public const string ViewPhotos = "View photos";
        public const string CopyLink = "Copy link";
        public const string Copy = "Copy";
        public const string MatchToACase = "Match to a case";

        // The Send page (handover § Send a photo link).
        public const string WhoItsFor = "Who it’s for";
        public const string InstructedBy = "Instructed by";
        public const string CustomerName = "Customer name";
        public const string AccidentDate = "Accident date";
        public const string Mobile = "Mobile";
        public const string Email = "Email";
        public const string LinkSettings = "Link settings";
        public const string PhotoGuidance = "Photo guidance";
        public const string Guided = "Guided";
        public const string Quick = "Quick";
        public const string LiveCameraOnly = "Live camera only";
        public const string LinkLasts = "Link lasts";
        public const string LinksLast = "Links last";
        public const string FullSizeOriginals = "Full-size originals";
        public const string HowToSendIt = "How to send it";
        public const string FromMailbox = "From";
        public const string Subject = "Subject";
        public const string Message = "Message";
        public const string SendByEmail = "Send by e-mail";
        public const string CreateLinkToCopy = "Create link to copy";
        public const string NeededToSendByEmail = "Needed to send by email";
        public const string SendStatus = "Send status";
        public const string PhotoLinkPlaceholder = "{photo link}";

        /// <summary>The photo link e-mail's subject (operator, 9 November 2026; R788).</summary>
        public static string EmailSubject(string? displayRegistration) =>
            string.IsNullOrWhiteSpace(displayRegistration)
                ? "Photos for Collision Engineers"
                : $"Photos of {displayRegistration.Trim()} for Collision Engineers";

        public static string Days(int days) => string.Create(CultureInfo.InvariantCulture, $"{days} days");

        public static string Mode(PhotoCaptureMode mode) => mode switch
        {
            PhotoCaptureMode.Guided => Guided,
            PhotoCaptureMode.Quick => Quick,
            _ => throw new InvalidOperationException($"Unknown photo capture mode '{(int)mode}'.")
        };

        // The link's detail page.
        public const string PhotoLink = "Photo link";
        public const string Created = "Created";
        public const string Expires = "Expires";
        public const string SomethingMissing = "Something missing";
        public const string History = "History";
        public const string Opened = "Opened";
        public const string FirstPhoto = "First photo";
        public const string LastPhoto = "Last photo";
        public const string Sent = "Sent";
        public const string Reminded = "Reminded";
        public const string CallLoggedAt = "Call logged";
        public const string Filed = "Filed";
        public const string Cancelled = "Cancelled";

        // The Contacts tab (R774) and the website link (R797).
        public const string PhotoLinksTab = "Photo links";
        public const string TheirStandingLink = "Their standing link";
        public const string PauseTheirLink = "Pause their link";
        public const string ResumeTheirLink = "Resume their link";
        public const string ReplaceLink = "Replace link";
        // Retire (R796), in the words R805 lists.
        public const string RetireLink = "Retire link";
        public const string RetireLinkConfirmation = "Retire this link? Its address stops working for good and cannot be resumed.";
        public const string DownloadQr = "Download QR";
        public const string SavedAddresses = "Saved addresses";
        public const string DefaultsForTheirLinks = "Defaults for their links";
        public const string CreateTheirLink = "Create their link";
        public const string WebsiteLink = "Website link";
        public const string CreateTheWebsiteLink = "Create the website link";
        public const string Paused = "Paused";
        public const string Active = "Active";
        public const string Saved = "Saved";

        // The Case's Files → Images story (FRD-16).
        public const string WhatTheyToldUs = "What they told us";
        public const string CustomersNote = "Customer’s note";
        public const string OtherImages = "Other images";

        // Confirmations.
        public static string LinkSentTo(string? name) => $"Link sent to {(string.IsNullOrWhiteSpace(name) ? "the customer" : name.Trim())} by e-mail";
        public const string LinkCopied = "Link copied";
        public const string LinkCancelled = "Link cancelled";
        public const string LinkExtended = "Link extended";
        public const string CallLogged = "Call logged";

        /// <summary>
        /// One item's own words: its story file name without the story
        /// position and the extension (<see cref="PhotoCaptureStory.FileName"/>),
        /// so "02-03 Front driver corner closer.jpg" reads "Front driver corner closer".
        /// </summary>
        public static string ItemLabel(PhotoCaptureItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            var name = Path.GetFileNameWithoutExtension(item.FileName);
            return name.Length > 6 && name[2] == '-' && name[5] == ' ' ? name[6..] : name;
        }

        /// <summary>What the sender typed, each with the label the capture app's own summary gives it; blank ones are left out.</summary>
        public static IReadOnlyList<(string Label, string Value)> Details(CaptureRecord capture)
        {
            ArgumentNullException.ThrowIfNull(capture);
            var details = capture.Details;
            var facts = new List<(string Label, string Value)>();
            void Add(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    facts.Add((label, value.Trim()));
                }
            }

            Add("Claim handled by", details.Handler);
            var location = string.Join(
                ", ",
                new[] { details.InspectionLocationChoice, details.InspectionLocation }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
            Add("Inspection location", location);
            Add("Vehicle role", details.VehicleRole);
            Add("Your view on the vehicle", details.RepairView);
            Add("Roadworthy", details.Roadworthy);
            Add("Preferred outcome", details.Outcome);
            Add("In hire / going into hire", details.InHire);
            Add("Mileage", details.Mileage is { } miles ? miles.ToString("N0", CultureInfo.InvariantCulture) : null);
            Add("Have any airbags gone off?", details.Airbags);
            Add("Is the steering wheel harder to turn than before?", details.Steering);
            Add(
                "Damaged areas",
                string.Join(", ", details.MarkedAreas.Where(PhotoCaptureStory.IsZone).Select(PhotoCaptureStory.ZoneLabel)));
            return facts;
        }
    }
}
