using System.Text.Json;
using System.Text.Json.Nodes;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// Shared snake_case valuation fixtures, mirroring the shape the vehicle-valuation skill
/// emits. The payload is fully populated (every subject + advert field set) so the mapper
/// round-trip test can prove no field is dropped, and it satisfies the full policy
/// validator (3 supportive/comparable adverts, guide-supported, evidence assessment).
/// </summary>
internal static class ValuationFixtures
{
    public const string FullPayloadJson = """
    {
      "meta": { "our_ref": "PCH25309", "your_ref": "KLZ-2025-184", "date": "20/06/2026" },
      "valuation_mode": "guide_supported",
      "guide_value": "23900",
      "assessed_retail_value": "24750",
      "is_commercial_vehicle": false,
      "subject_vehicle": {
        "registration": "AB12 CDE",
        "make": "BMW",
        "model": "3 Series",
        "derivative": "320d M Sport",
        "vehicle_description": "BMW 3 Series 320d M Sport Saloon",
        "body_type": "Saloon",
        "fuel": "Diesel",
        "transmission": "Automatic",
        "engine": "1995cc",
        "first_registered": "01/03/2022",
        "mileage": "31450",
        "colour": "Mineral Grey Metallic",
        "vehicle_history": "No adverse history recorded",
        "vin": "WBA00000000000000"
      },
      "intro": "We have reviewed comparable retail adverts to establish the pre-accident retail value.",
      "market_research": "Comparable retail market evidence reviewed having regard to make, model, age, mileage and specification.",
      "vat_note": "All figures are stated inclusive of VAT where applicable.",
      "search_summary": "Six comparable adverts identified across franchise and independent retailers.",
      "evidence_assessment": { "sufficient_for_pdf": true, "basis": "Three closely comparable supportive adverts bracket the assessed figure." },
      "valuation_commentary": [
        "The subject vehicle is a well-specified 320d M Sport with mileage commensurate with its age.",
        "The comparable evidence sits within a range of approximately £22,995 to £26,750.",
        "Greatest weight is placed on the examples closest in age, mileage and specification."
      ],
      "conclusion": "It is our professional opinion that the pre-accident retail value is £24,750.00.",
      "adverts": [
        {
          "source": "Retail listing", "url": "https://example.com/advert/100231", "price": "25495",
          "make": "BMW", "model": "3 Series", "derivative_or_engine": "320d M Sport",
          "registration_year": "2022", "mileage": "28000", "fuel": "Diesel", "transmission": "Automatic",
          "body_style": "Saloon", "seller_type": "Franchise", "location": "Manchester",
          "date_accessed": "20/06/2026", "comparability_note": "Closely comparable specification and age.",
          "differences_note": "Slightly lower mileage than the subject vehicle.",
          "supports_assessed_value": true, "evidence_role": "supportive", "is_materially_comparable": true,
          "advert_id": "A-100231", "screenshot_path": "shots/100231.png",
          "report_comment": "Directly supportive of the assessed retail value.",
          "vat_status": "Inc. VAT", "admin_fee": "0", "delivery_fee": "0"
        },
        {
          "source": "Retail listing", "url": "https://example.com/advert/100244", "price": "23990",
          "make": "BMW", "model": "3 Series", "derivative_or_engine": "320d M Sport",
          "registration_year": "2021", "mileage": "34500", "fuel": "Diesel", "transmission": "Automatic",
          "body_style": "Saloon", "seller_type": "Independent", "location": "Leeds",
          "date_accessed": "20/06/2026", "comparability_note": "Comparable derivative and specification.",
          "differences_note": "One year older than the subject vehicle.",
          "supports_assessed_value": true, "evidence_role": "supportive", "is_materially_comparable": true,
          "advert_id": "A-100244", "screenshot_path": "shots/100244.png",
          "report_comment": "Supports the lower end of the range.",
          "vat_status": "Inc. VAT", "admin_fee": "0", "delivery_fee": "0"
        },
        {
          "source": "Retail listing", "url": "https://example.com/advert/100258", "price": "25250",
          "make": "BMW", "model": "3 Series", "derivative_or_engine": "320d M Sport Pro",
          "registration_year": "2022", "mileage": "30100", "fuel": "Diesel", "transmission": "Automatic",
          "body_style": "Saloon", "seller_type": "Franchise", "location": "Birmingham",
          "date_accessed": "20/06/2026", "comparability_note": "Directly comparable age and mileage.",
          "differences_note": "Higher specification trim.",
          "supports_assessed_value": true, "evidence_role": "supportive", "is_materially_comparable": true,
          "advert_id": "A-100258", "screenshot_path": "shots/100258.png",
          "report_comment": "Closely supportive of the assessed figure.",
          "vat_status": "Inc. VAT", "admin_fee": "0", "delivery_fee": "0"
        }
      ]
    }
    """;

    public static readonly string[] AdvertUrls =
    {
        "https://example.com/advert/100231",
        "https://example.com/advert/100244",
        "https://example.com/advert/100258",
    };

    /// <summary>Parse the fixture into a self-contained JsonElement (backed by a rooted JsonDocument).</summary>
    public static JsonElement Payload() => JsonSerializer.SerializeToElement(JsonNode.Parse(FullPayloadJson));

    /// <summary>Build a captures array (one per advert url) carrying the given inline PDF base64.</summary>
    public static JsonElement Captures(string pdfBase64)
    {
        var arr = new JsonArray();
        foreach (var url in AdvertUrls)
        {
            arr.Add(new JsonObject
            {
                ["url"] = url,
                ["status"] = "success",
                ["filename"] = url.Split('/').Last() + ".pdf",
                ["pdf_base64"] = pdfBase64,
            });
        }

        return JsonSerializer.SerializeToElement(arr);
    }
}
