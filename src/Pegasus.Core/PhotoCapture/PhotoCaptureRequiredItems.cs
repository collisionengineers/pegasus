namespace Pegasus.Core.PhotoCapture;

/// <summary>One thing a capture is missing, in the words the HQ lists show.</summary>
public sealed record MissingCaptureItem(string Code, string Label);

/// <summary>
/// What a capture must hold before it is complete: the app's non-optional
/// steps (FRD-28). Eight overviews; for each damaged area an overview, a
/// closer shot and at least one close-up; the number plate; the dashboard
/// unless the sender said they have no keys (R799);
/// the VIN only where a bodyshop's own staff took the photos, on their own
/// standing link or a quick link sent from a Case for a Bodyshop (R808) not
/// handed on to their customer (R874),
/// because the app asks them for it and marks it optional for everyone else. Extras, the
/// scene, the other vehicle, paperwork, video and the note are never required.
/// </summary>
public static class PhotoCaptureRequiredItems
{
    public static IReadOnlyList<MissingCaptureItem> Evaluate(
        PhotoCapture capture,
        IReadOnlyList<PhotoCaptureItem> items,
        bool vinRequired)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(items);
        var missing = new List<MissingCaptureItem>();

        var overviews = items
            .Where(item => item.Kind == PhotoCaptureItemKind.Overview)
            .Select(item => item.Position)
            .Distinct()
            .Count(position => position is >= 1 and <= PhotoCaptureStory.OverviewCount);
        if (overviews < PhotoCaptureStory.OverviewCount)
        {
            missing.Add(new(
                "overview",
                overviews == 0 ? "No overview photos" : $"Overview {overviews} of {PhotoCaptureStory.OverviewCount}"));
        }

        // The areas are the app's own grouping of the marked zones
        // (PhotoCaptureAreas), so a run of touching zones asks for one set of
        // stages, as the app does, not one per zone.
        var areas = PhotoCaptureAreas.Derive(capture.Details.MarkedAreas);
        foreach (var area in areas)
        {
            var stages = items
                .Where(item => item.Kind == PhotoCaptureItemKind.Damage
                    && item.Stage is not null
                    && PhotoCaptureAreas.Find(areas, item.Area) == area)
                .Select(item => item.Stage!.Value)
                .ToHashSet();
            if (!stages.Contains(PhotoCaptureStage.Overview))
            {
                missing.Add(new($"damage:{area.Key}:overview", $"{area.Label}: overview"));
            }
            if (!stages.Contains(PhotoCaptureStage.Closer))
            {
                missing.Add(new($"damage:{area.Key}:closer", $"{area.Label}: closer"));
            }
            if (!stages.Contains(PhotoCaptureStage.CloseUp))
            {
                missing.Add(new($"damage:{area.Key}:close-up", $"{area.Label}: close-up"));
            }
        }

        if (!items.Any(item => item.Kind == PhotoCaptureItemKind.NumberPlate))
        {
            missing.Add(new("plate", "No number plate photo"));
        }

        // A sender with no keys skips the dashboard (R799).
        if (!capture.Details.NoKeys && !items.Any(item => item.Kind == PhotoCaptureItemKind.Dashboard))
        {
            missing.Add(new("dashboard", "No dashboard photo"));
        }

        if (vinRequired && !items.Any(item => item.Kind == PhotoCaptureItemKind.Vin))
        {
            missing.Add(new("vin", "No VIN photo"));
        }

        return missing;
    }

    /// <summary>
    /// The VIN is asked of a bodyshop's own staff: on their standing link
    /// (whether it is a repairer's is read with the capture), or on a quick
    /// link sent from a Case for a Bodyshop (R808) that they have not handed
    /// on to their customer: once a Handoff token exists for it
    /// (<paramref name="handedOn"/>), a customer takes the photos and is not
    /// asked for a VIN (operator, 10 November 2026; R874).
    /// </summary>
    public static bool VinRequired(PhotoCapture capture, bool standingLinkIsRepairer, bool handedOn)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return capture.Origin switch
        {
            PhotoCaptureOrigin.StandingStaff => standingLinkIsRepairer,
            PhotoCaptureOrigin.CaseLink => capture.Mode == PhotoCaptureMode.Quick
                && capture.QuickLinkFor == PhotoCaptureQuickLinkFor.Bodyshop
                && !handedOn,
            _ => false
        };
    }
}
