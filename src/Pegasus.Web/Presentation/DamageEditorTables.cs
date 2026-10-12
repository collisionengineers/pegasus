using System.Text.Json;
using Pegasus.Core.Assessment;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The Damage editor's vocabulary and plan as the JSON the browser reads. Both
/// depend only on constants, so each is serialised once, not on every render.
/// </summary>
public static class DamageEditorTables
{
    // The vocabulary and the plan the editor works with, handed to script once
    // so the browser holds no second copy of the areas, the bands, the disc
    // limits or the severity table.
    public static readonly string AreaTable = JsonSerializer.Serialize(new
    {
        plan = AssessmentVocabulary.DamagePlanAreas,
        other = AssessmentVocabulary.DamageOtherAreas,
        names = AssessmentVocabulary.DamageAreas,
        centres = DamageAreaGeometry.Centres.ToDictionary(
            entry => entry.Key,
            entry => new { x = entry.Value.X, y = entry.Value.Y }),
        box = new { x = DamagePlanGeometry.PlanLeft, y = DamagePlanGeometry.PlanTop, w = DamagePlanGeometry.PlanWidth, h = DamagePlanGeometry.PlanHeight },
        bands = new { front = DamageAreaGeometry.FrontBand, rear = DamageAreaGeometry.RearBand, left = DamageAreaGeometry.LeftBand, right = DamageAreaGeometry.RightBand },
        radius = DamageAreaGeometry.BaseRadius,
        margin = DamageAreaGeometry.Margin,
        minRadius = DamageAreaGeometry.MinRadius,
        maxRadius = DamageAreaGeometry.MaxRadius,
        burst = new
        {
            points = DamageBurst.Points,
            depth = DamageBurst.SpikeDepth,
            rotation = DamageBurst.Rotation,
            stretchX = DamageBurst.StretchX,
            stretchY = DamageBurst.StretchY,
            jitter = DamageBurst.Jitter,
            fill = DamageBurst.Fill,
            opacity = DamageBurst.FillOpacity,
            line = DamageBurst.Line,
            lineWidth = DamageBurst.LineWidth
        }
    });

    public static readonly string SeverityTable = JsonSerializer.Serialize(AssessmentVocabulary.DamageSeverities
        .OrderBy(entry => entry.Value.Rank)
        .Select(entry => new { k = entry.Key, n = entry.Value.Display }));
}
