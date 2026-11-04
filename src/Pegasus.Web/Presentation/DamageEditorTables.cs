using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The Damage editor's plan and words as the JSON the browser reads. Both
/// depend only on constants, so each is serialised once, not on every render.
/// </summary>
public static class DamageEditorTables
{
    // The plan the marker editor works with, handed to script once so the
    // browser holds no second copy of the body box, the disc limits or the
    // burst.
    public static readonly string PlanTable = JsonSerializer.Serialize(new
    {
        box = new { x = DamagePlanGeometry.PlanLeft, y = DamagePlanGeometry.PlanTop, w = DamagePlanGeometry.PlanWidth, h = DamagePlanGeometry.PlanHeight },
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

    // The Incident narrative the impact rows compose while the section edits:
    // Core's Nature of Incident sentence and the words it fills in, so the
    // browser writes no wording of its own.
    public static readonly string NarrativeTable = JsonSerializer.Serialize(new
    {
        sentence = ReportWordingComposition.NatureOfIncidentPattern,
        severities = AssessmentVocabulary.DamageSeverities.Keys.ToDictionary(code => code, ReportWordingComposition.Words),
        locations = AssessmentVocabulary.DamageAreaCodes.ToDictionary(code => code, ReportWordingComposition.Words)
    });
}
