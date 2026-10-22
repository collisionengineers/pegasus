using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Pegasus.Core;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Cazana;

/// <summary>
/// Cazana's Companion Report of one Companion lookup (ADR-0079), built as
/// Companion builds it: the lookup's timeline, comparable listings and stock
/// image are read over the same signed-in session, Companion's own templates
/// are rendered with Companion's own mapping, and Cazana's PDF service turns
/// the page into the PDF. Collision Engineers' account shows no private
/// value, so the view carries none; Companion's quirks are kept as they print.
/// </summary>
internal sealed class CazanaCompanionReport(
    CazanaCompanionClient client,
    string jwt,
    string uuid,
    JsonElement response,
    TimeProvider timeProvider) : IGuideValuationReport
{
    private const string NotAvailable = "N/A";
    private const string Logo = "https://fuel-economy-pdf-images.s3.eu-west-2.amazonaws.com/cazana-logotype-dark.svg";
    private const string LogoClass = "w-auto h-10";
    private const string PlaceholderLogo = "https://fuel-economy-pdf-images.s3.eu-west-2.amazonaws.com/cazana-logotype-white.svg";
    private const string PlaceholderBackground = "bg-tertiary";
    private const string PrimaryColour = "#2cdcdb";
    private const string SecondaryColour = "#211527";
    private const string PriceColour = "#581c87";

    private static readonly CultureInfo EnGb = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>The filters of a lookup that applied none: seven empty strings, the same in every report.</summary>
    private static readonly FrozenDictionary<string, object?> SimilarListingFilters = new Dictionary<string, object?>
    {
        ["listingTypes"] = "",
        ["sellerTypes"] = "",
        ["registrations"] = "",
        ["mileages"] = "",
        ["colours"] = "",
        ["distance"] = "",
        ["options"] = "",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly Lazy<Page> Pages = new(LoadPage);

    public string Identity => "cazana-companion:" + uuid;

    /// <summary>
    /// The timeline, comparable listings and stock image do not depend on each
    /// other, so they are asked for together and the page waits for the slowest.
    /// The timeline answer is read in place while the page renders and disposed
    /// before the PDF service is called, whichever way the reads end.
    /// </summary>
    public async Task<byte[]> FetchPdfAsync(CancellationToken cancellationToken)
    {
        var vrm = CazanaJson.String(response, "valuation", "searchMethod", "identifier");
        var timeline = client.TimelineAsync(jwt, uuid, cancellationToken);
        var similar = client.SimilarListingsAsync(jwt, response, cancellationToken);
        var image = client.ImageAsync(jwt, vrm ?? "", cancellationToken);
        StringBuilder html;
        try
        {
            await Task.WhenAll(timeline, similar, image);
            var timelineAnswer = await timeline;
            var timelineElement = CazanaJson.Get(timelineAnswer?.RootElement, "response");
            html = RenderHtml(response, vrm, timelineElement, await similar, await image, timeProvider.GetUtcNow());
        }
        finally
        {
            if (timeline.IsCompletedSuccessfully)
            {
                (await timeline)?.Dispose();
            }
        }

        return await client.CreatePdfAsync(jwt, html, cancellationToken);
    }

    /// <summary>
    /// Companion's <c>getCompanionPDF</c> page: the report around its sections,
    /// each section rendered in place into the page with the one view. The
    /// page stays the builder it was written into, so it is encoded once for
    /// the PDF service and never copied into a string.
    /// </summary>
    internal static StringBuilder RenderHtml(
        JsonElement response,
        string? vrm,
        JsonElement? timeline,
        SimilarListings similar,
        string? image,
        DateTimeOffset now)
    {
        var page = Pages.Value;
        return CazanaMustache.Build(page.Template, View(response, vrm, timeline, similar, image, now), page.Partials);
    }

    private static Dictionary<string, object?> View(
        JsonElement response,
        string? vrm,
        JsonElement? timeline,
        SimilarListings similar,
        string? image,
        DateTimeOffset now)
    {
        var valuation = CazanaJson.Get(response, "valuation");
        var specs = CazanaJson.Get(response, "specs");
        var valuedOn = CazanaJson.String(valuation, "date");

        // The events are read once: each is mapped as it is met, and a Sale is noted on the way.
        List<object?>? carHistory = null;
        var containsSalesEvents = false;
        if (timeline is not null)
        {
            carHistory = [];
            foreach (var item in CazanaJson.Items(timeline, "events"))
            {
                if (TimelineEvent(item, out var sale) is { } entry)
                {
                    carHistory.Add(entry);
                }

                containsSalesEvents |= sale;
            }
        }

        var view = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["vrm"] = vrm,
            ["vehicleDescription"] = NonEmpty(CazanaJson.String(valuation, "description")) ?? NotAvailable,
            ["mileage"] = Number(CazanaJson.Get(valuation, "mileage", "value")),
            ["valuationDate"] = DayMonthYear(valuedOn),
            ["condition"] = Condition(CazanaJson.Get(valuation, "namaGrade")),
            ["marketValue"] = Number(CazanaJson.Get(valuation, "retail")),
            ["tradeValue"] = Number(CazanaJson.Get(valuation, "remarket")),
            // Companion reads the valuation's OCN, so a lookup without one prints "£N/A".
            ["originalValue"] = Number(CazanaJson.Get(valuation, "ocn", "onTheRoadPrice")),
            ["firstRegistration"] = Ordinal(
                CazanaJson.String(timeline, "firstRegistration") ?? CazanaJson.String(specs, "firstRegistration")),
            ["type"] = Scalar(CazanaJson.Get(specs, "type")),
            ["manufacturer"] = Scalar(CazanaJson.Get(specs, "make")),
            ["model"] = Scalar(CazanaJson.Get(specs, "model")),
            ["trim"] = Scalar(CazanaJson.Get(specs, "trim")),
            ["year"] = Scalar(CazanaJson.Get(specs, "year")),
            ["bodyType"] = Scalar(CazanaJson.Get(specs, "bodyStyle")),
            ["transmission"] = Scalar(CazanaJson.Get(specs, "transmission")),
            ["doors"] = Scalar(CazanaJson.Get(specs, "doors")),
            ["fuelType"] = Scalar(CazanaJson.Get(specs, "fuelType")),
            ["engineSize"] = Scalar(CazanaJson.Get(specs, "engineSizeCc")),
            ["valves"] = Scalar(CazanaJson.Get(specs, "numberValves")),
            ["cylinders"] = Scalar(CazanaJson.Get(specs, "numberCylinders")),
            ["gears"] = Scalar(CazanaJson.Get(specs, "gears")),
            ["engineNumber"] = Scalar(CazanaJson.Get(specs, "engineNumber")),
            ["powerOutputKw"] = Scalar(CazanaJson.Get(specs, "powerOutputKw")),
            ["powerOutputBhp"] = Scalar(CazanaJson.Get(specs, "powerOutputBhp")),
            ["currentColour"] = Capitalise(CazanaJson.String(specs, "currentColour")),
            ["vehicleSeats"] = Scalar(CazanaJson.Get(specs, "seats")),
            ["containsSalesEvents"] = containsSalesEvents,
            ["isHistoricalValuationDate"] = !IsToday(valuedOn, now),
            ["sortBy"] = "Newest",
            ["listings"] = similar.Listings.Select(SimilarListing).ToList<object?>(),
            ["similarListingFilters"] = SimilarListingFilters,
            ["totalSimilarListings"] = Scalar(similar.Total),
            ["customerReference"] = null,
            ["placeholderLogo"] = PlaceholderLogo,
            ["placeholderBgClass"] = PlaceholderBackground,
            ["primaryColor"] = PrimaryColour,
            ["secondaryColor"] = SecondaryColour,
            ["priceColor"] = PriceColour,
        };
        if (image is not null)
        {
            view["vehicleImage"] = image;
        }

        if (carHistory is not null)
        {
            view["carHistory"] = carHistory;
        }

        var listing = CazanaJson.Get(response, "listing");
        var adText = CazanaJson.String(listing, "adText");
        if (NonEmpty(adText) is not null)
        {
            view["listing"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["listinglastSeen"] = MonthsAgo(CazanaJson.String(listing, "soldDate"), now),
                ["listingOriginalPrice"] = Number(CazanaJson.Get(listing, "listingOriginalPrice")),
                ["listinglastPrice"] = Number(CazanaJson.Get(listing, "listingPrice")),
                ["listingDaysonMarket"] = Scalar(CazanaJson.Get(listing, "daysOnSale")),
                ["listingDateListed"] = DayMonthYear(CazanaJson.String(listing, "firstSeen")),
                ["listingMileage"] = Number(CazanaJson.Get(listing, "mileage")),
                ["listingSeller"] = Scalar(CazanaJson.Get(listing, "dealerName")),
                ["listingAdText"] = adText,
            };
        }

        return view;
    }

    private static Dictionary<string, object?> SimilarListing(JsonElement listing) => new(StringComparer.Ordinal)
    {
        ["listingVrm"] = Scalar(CazanaJson.Get(listing, "vrm")),
        ["listingYear"] = Scalar(CazanaJson.Get(listing, "year")),
        ["listingColour"] = Scalar(CazanaJson.Get(listing, "colour")),
        ["listingSeller"] = Scalar(CazanaJson.Get(listing, "dealer", "type")),
        ["listingMileage"] = Scalar(CazanaJson.Get(listing, "mileage", "display")),
        ["listingPrice"] = Scalar(CazanaJson.Get(listing, "price", "display")),
    };

    /// <summary>
    /// One timeline entry as Companion maps it; a type Companion does not map is
    /// dropped. <paramref name="sale"/> says the entry is a Sale.
    /// </summary>
    private static Dictionary<string, object?>? TimelineEvent(JsonElement item, out bool sale)
    {
        sale = false;

        // The type is compared where it lies in the answer, as bytes: no string per event.
        if (CazanaJson.Get(item, "type") is not { ValueKind: JsonValueKind.String } type)
        {
            return null;
        }

        if (type.ValueEquals("Mot"u8))
        {
            var passed = CazanaJson.Get(item, "passed") is { ValueKind: JsonValueKind.True };
            return new(StringComparer.Ordinal)
            {
                ["motDate"] = Ordinal(CazanaJson.String(item, "date")),
                ["motStatus"] = passed ? "Passed" : "Failed",
                ["motStatusColour"] = passed
                    ? "fa-solid fa-circle-check text-green-600"
                    : "fa-solid fa-circle-xmark text-red-600",
                ["motMileage"] = Number(CazanaJson.Get(item, "odometer", "value")),
                ["motComments"] = CazanaJson.Items(item, "rfcAndComments")
                    .Select(comment => CazanaJson.String(comment, "text"))
                    .ToList<object?>(),
            };
        }

        if (type.ValueEquals("Sale"u8))
        {
            sale = true;
            return new(StringComparer.Ordinal)
            {
                ["advertisementDateRange"] =
                    $"{Ordinal(CazanaJson.String(item, "date"))} - {Ordinal(CazanaJson.String(item, "lastSeen"))}",
                ["saleListings"] = CazanaJson.Items(item, "listings").Select(SaleListing).ToList<object?>(),
            };
        }

        if (type.ValueEquals("PlateChange"u8))
        {
            return new(StringComparer.Ordinal)
            {
                ["plateChangeDate"] = Ordinal(CazanaJson.String(item, "date")),
                ["plateChangePrevious"] = CazanaJson.String(item, "previousVRM"),
                ["plateChangeCurrent"] = CazanaJson.String(item, "currentVRM"),
            };
        }

        return type.ValueEquals("KeeperChange"u8)
            ? new(StringComparer.Ordinal) { ["keeperChangeDate"] = Ordinal(CazanaJson.String(item, "date")) }
            : null;
    }

    private static Dictionary<string, object?> SaleListing(JsonElement listing)
    {
        var advert = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["advertisementDate"] = Ordinal(CazanaJson.String(listing, "firstSeen")),
            ["advertisementPrice"] = Number(CazanaJson.Get(listing, "price")),
            ["advertisementMileage"] = Number(CazanaJson.Get(listing, "mileage")),
            ["advertisementAdText"] = CazanaJson.String(listing, "adText"),
        };
        if (CazanaJson.Items(listing, "images").FirstOrDefault() is { ValueKind: JsonValueKind.String } image)
        {
            advert["advertisementImage"] = image.GetString();
        }

        return advert;
    }

    /// <summary>Companion's number: grouped in thousands, a numeric string as it stands, else N/A.</summary>
    private static string Number(JsonElement? value) => value switch
    {
        { ValueKind: JsonValueKind.Number } number when number.TryGetDecimal(out var figure) =>
            figure.ToString("#,##0.###", EnGb),
        { ValueKind: JsonValueKind.String } text when text.GetString() is { } literal && decimal.TryParse(
            literal, NumberStyles.Number, CultureInfo.InvariantCulture, out _) => literal,
        _ => NotAvailable,
    };

    /// <summary>A JSON value as the view holds it: text, whole number, decimal, flag or nothing.</summary>
    private static object? Scalar(JsonElement? value) => value switch
    {
        { ValueKind: JsonValueKind.String } text => text.GetString(),
        { ValueKind: JsonValueKind.Number } number when number.TryGetInt64(out var whole) => whole,
        { ValueKind: JsonValueKind.Number } number when number.TryGetDecimal(out var figure) => figure,
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        _ => null,
    };

    /// <summary>"2026-10-21" or "2026-10-21T…" as "21/10/2026".</summary>
    private static string DayMonthYear(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return NotAvailable;
        }

        // The date part is read in place as its first three '-' segments: one string, no arrays.
        var date = value.AsSpan();
        var time = date.IndexOf('T');
        if (time >= 0)
        {
            date = date[..time];
        }

        var firstDash = date.IndexOf('-');
        if (firstDash < 0)
        {
            return NotAvailable;
        }

        var afterYear = date[(firstDash + 1)..];
        var secondDash = afterYear.IndexOf('-');
        if (secondDash < 0)
        {
            return NotAvailable;
        }

        var afterMonth = afterYear[(secondDash + 1)..];
        var thirdDash = afterMonth.IndexOf('-');
        var day = thirdDash < 0 ? afterMonth : afterMonth[..thirdDash];
        return string.Create(CultureInfo.InvariantCulture, $"{day}/{afterYear[..secondDash]}/{date[..firstDash]}");
    }

    /// <summary>A date as "7th May 2026".</summary>
    private static string Ordinal(string? value)
    {
        if (!TryDate(value, out var date))
        {
            return NotAvailable;
        }

        var suffix = date.Day is >= 11 and <= 13
            ? "th"
            : (date.Day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };
        // The month name is written straight into the result in British English; the day and
        // year are plain digits in every culture.
        return string.Create(EnGb, $"{date.Day}{suffix} {date:MMMM} {date.Year}");
    }

    /// <summary>Whether a "yyyy-MM-dd" date text is the London calendar day of <paramref name="now"/>, compared without building today's text.</summary>
    private static bool IsToday(string? valuedOn, DateTimeOffset now)
    {
        Span<char> today = stackalloc char[10];
        return LondonCalendar.DateAt(now).TryFormat(today, out var written, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            && today[..written].SequenceEqual(valuedOn.AsSpan());
    }

    private static string MonthsAgo(string? value, DateTimeOffset now)
    {
        if (!TryDate(value, out var date))
        {
            return NotAvailable;
        }

        var today = LondonCalendar.DateAt(now);
        return (12 * (today.Year - date.Year) + today.Month - date.Month).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The calendar date a Cazana timestamp names: an instant is read on the
    /// London calendar, a bare date or local time as it is written.
    /// </summary>
    private static bool TryDate(string? value, out DateOnly date)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            date = default;
            return false;
        }

        date = DateOnly.FromDateTime(HasOffset(value!) ? LondonCalendar.LocalAt(parsed).DateTime : parsed.DateTime);
        return true;
    }

    private static bool HasOffset(string value)
    {
        var text = value.AsSpan();
        var time = text.IndexOfAny('T', ' ');
        if (time < 0)
        {
            return false;
        }

        var tail = text[time..].TrimEnd();
        return (tail.Length > 0 && tail[^1] is 'Z' or 'z') || tail.IndexOfAny('+', '-') >= 0;
    }

    private static string Condition(JsonElement? namaGrade) => namaGrade switch
    {
        null or { ValueKind: JsonValueKind.Null } => NotAvailable,
        { ValueKind: JsonValueKind.Number } number when number.TryGetDecimal(out var grade) => grade switch
        {
            0 => NotAvailable,
            4 => "Poor",
            _ => "Good",
        },
        _ => "Good",
    };

    private static string Capitalise(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return NotAvailable;
        }

        // Already capitalised: the same string, nothing built.
        return char.ToUpperInvariant(value[0]) == value[0]
            ? value
            : string.Create(
                value.Length,
                value,
                static (characters, source) =>
                {
                    source.AsSpan().CopyTo(characters);
                    characters[0] = char.ToUpperInvariant(characters[0]);
                });
    }

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// The page and its seven sections parsed once, and the header rendered
    /// once: it is made only of constants, so every report carries the same bytes.
    /// </summary>
    private static Page LoadPage()
    {
        var templates = LoadTemplates();
        var header = CazanaMustache.Render(
            templates["header"],
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Companion Report",
                ["mainLogo"] = Logo,
                ["logoClass"] = LogoClass,
            });
        var partials = new Dictionary<string, CazanaMustache.Template>(StringComparer.Ordinal)
        {
            ["headerSection"] = CazanaMustache.Literal(header),
            ["marketOverviewSection"] = templates["market-overview"],
            ["vehicleInformationCardSection"] = templates["vehicle-information-card"],
            ["vehicleOverviewCardSection"] = templates["vehicle-overview-card"],
            ["vehicleSpecificationColumnSection"] = templates["vehicle-specification"],
            ["vehicleTimelineColumnSection"] = templates["vehicle-timeline"],
            ["VehicleAdvertFullPageSection"] = templates["vehicle-advert-full-details"],
            ["recentlistingColumnSection"] = templates["recent-listing"],
        };
        return new Page(templates["companion-report"], partials);
    }

    private static Dictionary<string, CazanaMustache.Template> LoadTemplates()
    {
        string[] names =
        [
            "companion-report", "header", "market-overview", "vehicle-information-card", "vehicle-overview-card",
            "vehicle-specification", "vehicle-timeline", "vehicle-advert-full-details", "recent-listing",
        ];
        var assembly = typeof(CazanaCompanionReport).Assembly;
        var templates = new Dictionary<string, CazanaMustache.Template>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var resource = $"Pegasus.Infrastructure.Cazana.CompanionReport.{name}.mustache";
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"The Companion Report template {resource} is not embedded.");
            using var reader = new StreamReader(stream);
            templates[name] = CazanaMustache.Parse(reader.ReadToEnd());
        }

        return templates;
    }

    private sealed record Page(
        CazanaMustache.Template Template,
        IReadOnlyDictionary<string, CazanaMustache.Template> Partials);
}
