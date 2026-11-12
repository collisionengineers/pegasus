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

        // v51 Photo capture tabs, Direct links and Standing links (operator, 11 November 2026; R948).
        public const string DirectLinks = "Direct links";
        public const string StandingLinks = "Standing links";
        public const string StandingLinksKey = "standing";
        public const string Recipient = "Recipient";
        public const string Destination = "Destination";
        public const string StandingLink = "Standing link";
        public const string AllSubmissions = "All submissions";
        public const string RecentSubmissions = "Recent submissions";
        public const string QrCode = "QR code";
        public const string Defaults = "Defaults";
        public const string Manage = "Manage";
        public const string Uploading = "Uploading";
        public const string ForwardedToCustomer = "Forwarded to customer";
        public const string FilterDirectLinks = "Filter direct links";
        public const string DirectSearchPlaceholder = "Search reg, customer, case or principal";
        public const string StandingSearchPlaceholder = "Search reg, customer, case or partner";
        public const string NoDirectLinksMatch = "No direct links match this filter.";
        public const string NoDirectLinksYet = "No direct photo links sent yet.";
        public const string NoStandingLinks = "No standing links configured.";
        public const string NoStandingSubmissionsYet = "No standing-link submissions yet.";
        public const string PrivateCustomers = "Private customers";

        public static string NoSubmissionsVia(string name) => $"No submissions received via {name} yet.";

        public static string AcrossActiveLinks(int count) =>
            string.Create(CultureInfo.InvariantCulture, $"Across {count} active standing links");

        public static string ReceivedCount(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} received");

        public static string CopyLinkFor(string name) => $"Copy link for {name}";

        public static string QrCodeFor(string name) => $"QR code for {name}";

        /// <summary>The Direct links row's chase cue: the reminder due, else how many were sent.</summary>
        public static string? ChaseCue(DueCaptureReminder? due, int remindersSent) =>
            due is not null
                ? $"Reminder due · {due.Label}"
                : remindersSent switch
                {
                    <= 0 => null,
                    1 => "1 reminder sent",
                    _ => string.Create(CultureInfo.InvariantCulture, $"{remindersSent} reminders sent")
                };

        public static string Scope(PhotoCaptureScope scope) => scope switch
        {
            PhotoCaptureScope.Awaiting => "Awaiting",
            PhotoCaptureScope.NeedsChase => "Needs chase",
            PhotoCaptureScope.Complete => "Complete",
            PhotoCaptureScope.Closed => "Closed",
            _ => All
        };

        public static string ScopeKey(PhotoCaptureScope scope) => scope switch
        {
            PhotoCaptureScope.Awaiting => "awaiting",
            PhotoCaptureScope.NeedsChase => "due",
            PhotoCaptureScope.Complete => "complete",
            PhotoCaptureScope.Closed => "closed",
            _ => "all"
        };

        /// <summary>The scope a key names; Awaiting when it names none, the tab's default.</summary>
        public static PhotoCaptureScope ScopeFromKey(string? key) =>
            Enum.GetValues<PhotoCaptureScope>().FirstOrDefault(scope => ScopeKey(scope) == key, PhotoCaptureScope.Awaiting);

        /// <summary>The standing link's name in the directory: its contact's, or Website.</summary>
        public static string StandingLinkName(StandingPhotoLink link)
        {
            ArgumentNullException.ThrowIfNull(link);
            return link.Kind == StandingPhotoLinkKind.Website ? Website : link.OrganizationName ?? Website;
        }

        /// <summary>Who holds a standing link: the contact's type, or private customers for the website's.</summary>
        public static string StandingLinkKind(StandingPhotoLink link)
        {
            ArgumentNullException.ThrowIfNull(link);
            return link.Kind == StandingPhotoLinkKind.Website
                ? PrivateCustomers
                : Contacts.Type(link.IsRepairer
                    ? Pegasus.Core.Cases.ContactRole.Repairer
                    : link.PrincipalId is not null ? Pegasus.Core.Cases.ContactRole.Principal : Pegasus.Core.Cases.ContactRole.ClaimSource);
        }

        /// <summary>A standing link's defaults in brief: "Quick · 7 days".</summary>
        public static string StandingLinkDefaults(StandingPhotoLink link)
        {
            ArgumentNullException.ThrowIfNull(link);
            return $"{Mode(link.ForwardedMode)} · {Days(link.LinkLifetimeDays)}";
        }

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
        public const string Apply = "Apply";
        public const string Clear = "Clear";

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
            PhotoCaptureOrigin.CaseLink => DirectLinks,
            _ => throw new InvalidOperationException($"Unknown photo capture origin '{(int)origin}'.")
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

        // Cell values.
        public const string NotGiven = "Not given";
        public const string AwaitingInstruction = "Awaiting instruction";

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
        public const string LogCall = "Log call";
        public const string Extend = "Extend";
        public const string CancelLink = "Cancel link";
        public const string ViewPhotos = "View photos";
        public const string CopyLink = "Copy link";
        public const string Copy = "Copy";

        // The prototype's words the operator chose (R806): the Send message's
        // reset and the Case page's empty state.
        public const string EditedForThisOneOnly = "Edited for this one only.";
        public const string PutTheStandardWordingBack = "Put the standard wording back";
        public const string NoPhotosYet = "No photos yet";

        // The Send page (handover § Send a photo link).
        public const string WhoItsFor = "Who it’s for";
        public const string InstructedBy = "Instructed by";
        public const string CustomerName = "Customer name";
        public const string AccidentDate = "Accident date";
        public const string Mobile = "Mobile";
        public const string Email = "Email";
        public const string LinkSettings = "Link settings";
        public const string PhotoGuidance = "Photo guidance";

        // Who a quick link is for (R808): the prototype's own words.
        public const string QuickLinkType = "Type";
        public const string AccidentManagement = "Accident management";
        public const string Bodyshop = "Bodyshop";

        public static string QuickLinkFor(PhotoCaptureQuickLinkFor quickLinkFor) => quickLinkFor switch
        {
            PhotoCaptureQuickLinkFor.AccidentManagement => AccidentManagement,
            PhotoCaptureQuickLinkFor.Bodyshop => Bodyshop,
            _ => throw new InvalidOperationException($"Unknown quick link type '{(int)quickLinkFor}'.")
        };
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

        // The photo link text (R918 to R921); the operator signed the wording off on 12 November 2026 (R1001, R1002).
        public const string SendByText = "Send by text";
        public const string TextMessage = "Text message";
        public const string TextNotSent = "Text not sent";
        public const string CreateLinkToCopy = "Create link to copy";
        public const string NeededToSendByEmail = "Needed to send by email";
        public const string SendStatus = "Send status";
        public const string PhotoLinkPlaceholder = "{photo link}";

        // v51 composer (R948): Send a photo link and Resend show one channel at
        // a time, Email, Text message or Link to copy, with Mode and Lasts on
        // one row (operator, 11 November 2026; v51 L, M). Email and Text
        // message are the channels' own words above.
        public const string DeliveryMethod = "Delivery method";
        public const string LinkToCopy = "Link to copy";
        public const string ToEmail = "To (e-mail)";
        public const string ComposerMode = "Mode";
        public const string Lasts = "Lasts";
        public const string CaptureOptions = $"Capture options ({LiveCameraOnly}, {FullSizeOriginals})";

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
        public const string History = "History";
        public const string Opened = "Opened";
        public const string Sent = "Sent";
        public const string Reminded = "Reminded";
        public const string CallLoggedAt = "Call logged";
        public const string SentByText = "Sent by text";
        public const string Cancelled = "Cancelled";

        // The Contacts tab (R774) and the website link (R797).
        public const string PhotoLinksTab = "Photo links";
        public const string TheirStandingLink = "Their standing link";
        public const string PauseTheirLink = "Pause their link";
        public const string ResumeTheirLink = "Resume their link";
        // The Website link panel's own (operator, 10 November 2026; R867); a contact's panel keeps "their".
        public const string PauseTheLink = "Pause the link";
        public const string ResumeTheLink = "Resume the link";
        public const string ReplaceLink = "Replace link";
        // Retire (R796), in the words R805 lists.
        public const string RetireLink = "Retire link";
        public const string RetireLinkConfirmation = "Retire this link? Its address stops working for good and cannot be resumed.";
        public const string DownloadQr = "Download QR";
        public const string SavedAddresses = "Saved addresses";

        // The saved addresses card, in the prototype's words (R806).
        public const string AddAddress = "Add address";
        public const string ShownAsInspectionLocation = "Shown as “Inspection location” on their form.";
        public const string NoSavedAddresses = "No addresses yet. Their staff will type one each time until you add some.";
        public const string AddressNamePlaceholder = "Name, e.g. Back yard";
        public const string FullAddressPlaceholder = "Full address";
        public const string AddressName = "Address name";
        public const string AddressField = "Address";
        public const string Edit = "Edit";
        public const string Remove = "Remove";
        public const string DefaultsForTheirLinks = "Defaults for their links";
        public const string CreateTheirLink = "Create their link";
        public const string WebsiteLink = "Website link";
        public const string CreateTheWebsiteLink = "Create the website link";
        public const string Paused = "Paused";
        public const string Active = "Active";
        public const string Saved = "Saved";

        // The operator's words of 10 November 2026: the chip on an inactive
        // contact's link, which wins over Paused (R865); the website link's
        // address box (R869); Administration › Configuration's photo link
        // settings and the lifetimes they offer (R869).
        public const string ContactInactive = "Contact inactive";
        public const string WebsiteAddress = "Website address";
        public const string PhotoLinks = "Photo links";
        public const string LifetimesOffered = "Lifetimes offered";

        // The six refusals, each shown beside its form (operator, 10 November 2026; R868).
        public const string WebsiteAddressShape =
            "A website address is 6 to 60 lower-case letters, numbers and hyphens, and cannot start or end with a hyphen.";
        public const string WebsiteAddressInUse = "That website address is already in use.";
        public const string OfferAtLeastOneLifetime = "Offer at least one lifetime.";
        public const string StartingLifetimeNotOffered = "The lifetime links start on must be one of those offered.";
        public const string SavedAddressNeedsNameAndAddress = "A saved address needs a name and an address.";
        public const string ChooseAQuickLinkType = "Choose a Type for a Quick link.";

        // The Case's Files → Images story (FRD-16).
        public const string WhatTheyToldUs = "What they told us";
        public const string CustomersNote = "Customer’s note";

        // v51 Case images (R948): the Images bar above the Case's one image
        // grid, the photo links folded under it and the photos still filing
        // (operator, 11 November 2026; v51 H, I, J, K). Standing link and
        // Uploading are the Photo capture tabs' own, above.
        public const string DirectLink = "Direct link";
        public const string LinkDetails = "Link details";
        public const string OtherLinkDetails = "Details";
        public const string NoPhotos = "No photos";

        public static string OtherPhotoLinksOnThisCase(int count) =>
            string.Create(CultureInfo.InvariantCulture, $"Other photo links on this case ({count})");

        /// <summary>
        /// The sender's answers in short labels (v51 F, I; R948): the
        /// inspection location, Roadworthy, Outcome, In hire, Mileage, Airbags
        /// and Steering, blank ones left out and the damaged areas omitted.
        /// </summary>
        public static IReadOnlyList<(string Label, string Value)> KeyAnswers(CaptureRecord capture)
        {
            ArgumentNullException.ThrowIfNull(capture);
            var details = capture.Details;
            var answers = new List<(string Label, string Value)>();
            void Add(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    answers.Add((label, value.Trim()));
                }
            }

            Add("Location", string.IsNullOrWhiteSpace(details.InspectionLocationChoice) ? details.InspectionLocation : details.InspectionLocationChoice);
            Add("Roadworthy", details.Roadworthy);
            Add("Outcome", details.Outcome);
            Add("In hire", details.InHire);
            Add("Mileage", details.Mileage is { } miles ? miles.ToString("N0", CultureInfo.InvariantCulture) : null);
            Add("Airbags", details.Airbags);
            Add("Steering", details.Steering);
            return answers;
        }

        // Confirmations.
        public static string LinkSentTo(string? name) => $"Link sent to {(string.IsNullOrWhiteSpace(name) ? "the customer" : name.Trim())} by e-mail";
        public static string LinkTextedTo(string? name) => $"Link sent to {(string.IsNullOrWhiteSpace(name) ? "the customer" : name.Trim())} by text";
        public const string LinkCopied = "Link copied";
        public const string LinkCancelled = "Link cancelled";
        public const string LinkExtended = "Link extended";
        public const string CallLogged = "Call logged";

        /// <summary>
        /// One item's own words: its story file name without the story
        /// position and the extension (<see cref="PhotoCaptureStory.Label"/>),
        /// so "02-201 Front driver corner closer.jpg" reads "Front driver corner closer".
        /// </summary>
        public static string ItemLabel(PhotoCaptureItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return PhotoCaptureStory.Label(item.FileName);
        }

        // v51 link page and Vehicle images (R948): /PhotoCapture/{id}'s header,
        // Photos panel, Summary aside and folded History, and the Vehicle
        // images record's Summary (operator, 11 November 2026; v51 A to G, O,
        // Q, R). Recipient, Destination, Standing link, Uploading, Forwarded
        // to customer and the chase cue are the Photo capture tabs' own, above.
        public const string OpenCase = "Open case";
        public const string OpenVehicleImages = "Open Vehicle images";
        public const string MissingLead = "Missing:";
        public const string UploadingLive = "Uploading live";
        public const string FilterPhotos = "Filter photos";
        public const string Overview = "Overview";
        public const string Damage = "Damage";
        public const string VehicleId = "Vehicle ID";
        public const string Files = "Files";
        public const string Summary = "Summary";
        public const string Accident = "Accident";
        public const string Chase = "Chase";
        public const string Association = "Association";
        public const string VehicleImages = "Vehicle images";
        public const string VehicleImagesRecord = "Vehicle images record";
        public const string CloseVehicleImagesRecord = "Close Vehicle images record";
        public const string NoPhotosUploadedYet = "No photos uploaded yet.";
        public const string NoPhotosWereUploaded = "No photos were uploaded on this link.";
        public const string NoImagesRecorded = "No images recorded.";

        /// <summary>The link page's eyebrow, naming the tab the link belongs to: "Photo capture · Direct links".</summary>
        public static string LinkEyebrow(bool direct) => $"{Title} · {(direct ? DirectLinks : StandingLinks)}";

        /// <summary>A heading with its count: "Photos · 18", "History · 3".</summary>
        public static string Counted(string label, int count) =>
            string.Create(CultureInfo.InvariantCulture, $"{label} · {count}");

        /// <summary>"1 photo", "25 photos".</summary>
        public static string PhotosOf(int count) =>
            count == 1 ? "1 photo" : string.Create(CultureInfo.InvariantCulture, $"{count} photos");

        /// <summary>The Photos panel's filters after All, by the key a photo's <see cref="PhotoFilterKey"/> gives.</summary>
        public static IReadOnlyList<(string Key, string Label)> PhotoFilters { get; } =
            [("overview", Overview), ("damage", Damage), ("id", VehicleId)];

        /// <summary>The filter a photograph falls under (v51 B; R948): the number plate, VIN and dashboard are the Vehicle ID; the scene and the other vehicle are under All only.</summary>
        public static string? PhotoFilterKey(PhotoCaptureItemKind kind) => kind switch
        {
            PhotoCaptureItemKind.Overview => "overview",
            PhotoCaptureItemKind.Damage => "damage",
            PhotoCaptureItemKind.NumberPlate or PhotoCaptureItemKind.Vin or PhotoCaptureItemKind.Dashboard => "id",
            _ => null
        };

        /// <summary>
        /// The captions of a capture's photographs, in the order given (v51 B;
        /// R948): the story's own words (<see cref="PhotoCaptureStory"/>), an
        /// overview by its angle and a damage photo by its area and stage
        /// ("Front driver corner · closer"); an overview past the eight angles
        /// is numbered on from them ("Overview 9"), and any other name that
        /// repeats is numbered ("Number plate 1", "Number plate 2"). No time.
        /// </summary>
        public static IReadOnlyList<string> Captions(IReadOnlyList<PhotoCaptureItem> photographs)
        {
            ArgumentNullException.ThrowIfNull(photographs);
            var names = photographs.Select(CaptionName).ToArray();
            var totals = names
                .GroupBy(name => name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var extraOverviews = 0;
            var captions = new string[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                var item = photographs[index];
                if (item.Kind == PhotoCaptureItemKind.Overview && item.Position is < 1 or > PhotoCaptureStory.OverviewCount)
                {
                    extraOverviews++;
                    captions[index] = string.Create(
                        CultureInfo.InvariantCulture, $"{Overview} {PhotoCaptureStory.OverviewCount + extraOverviews}");
                    continue;
                }

                var name = names[index];
                if (totals[name] > 1)
                {
                    var number = seen.GetValueOrDefault(name) + 1;
                    seen[name] = number;
                    captions[index] = string.Create(CultureInfo.InvariantCulture, $"{name} {number}");
                }
                else
                {
                    captions[index] = name;
                }
            }

            return captions;
        }

        private static string CaptionName(PhotoCaptureItem item) => item.Kind switch
        {
            PhotoCaptureItemKind.Overview => item.Position is >= 1 and <= PhotoCaptureStory.OverviewCount
                ? PhotoCaptureStory.OverviewAngles[item.Position - 1]
                : Overview,
            PhotoCaptureItemKind.Damage =>
                (PhotoCaptureStory.IsZone(item.Area) ? PhotoCaptureStory.ZoneLabel(item.Area!) : Damage)
                + (item.Stage is { } stage ? " · " + PhotoCaptureStory.StageLabel(stage) : string.Empty),
            _ => ItemLabel(item)
        };

        /// <summary>
        /// The Missing bar's one line (v51 C; R948): the missing items in
        /// their own words, an area's missing stages gathered after it, so
        /// "Front driver corner: closer" and "Front driver corner: close-up"
        /// read "Front driver corner (closer, close-up)"; parts are joined by " · ".
        /// </summary>
        public static string MissingSummary(IReadOnlyList<string> missing)
        {
            ArgumentNullException.ThrowIfNull(missing);
            var parts = new List<string>();
            var stagesByArea = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var label in missing)
            {
                var colon = label.LastIndexOf(": ", StringComparison.Ordinal);
                if (colon <= 0)
                {
                    parts.Add(label);
                    continue;
                }

                var area = label[..colon];
                if (!stagesByArea.TryGetValue(area, out var stages))
                {
                    stages = [];
                    stagesByArea.Add(area, stages);
                    parts.Add(area);
                }

                stages.Add(label[(colon + 2)..]);
            }

            return string.Join(
                " · ",
                parts.Select(part => stagesByArea.TryGetValue(part, out var stages) ? $"{part} ({string.Join(", ", stages)})" : part));
        }
    }
}
