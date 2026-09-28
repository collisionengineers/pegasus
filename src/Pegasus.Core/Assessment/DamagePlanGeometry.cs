using System.Globalization;
using System.Text;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The plan: one top-down drawing of the vehicle the Case records — a car, a
/// van or a motorbike (operator, 28 September 2026, from the precision marker
/// pack) — and the comic burst each damage is drawn as. The Case page and the
/// report both draw from here; the page shows the shaded drawing and the
/// report the flat one, which the PDF renderer reproduces faithfully. Every
/// drawing shares one body box, so the areas, the bands and the disc a damage
/// is judged by are <see cref="DamageAreaGeometry"/>'s whatever the vehicle,
/// and a saved disc sits in the same place on each.
/// </summary>
public static class DamagePlanGeometry
{
    public const string ViewBox = "0 0 240 434";

    public const string Car = "car";
    public const string Van = "van";
    public const string Motorbike = "motorbike";

    /// <summary>
    /// The drawing a recorded Vehicle type is drawn with: a van as the van, a
    /// motorcycle or scooter as the motorbike, anything else (and nothing
    /// recorded) as the car.
    /// </summary>
    public static string ProfileFor(string? vehicleType) => vehicleType switch
    {
        "van" => Van,
        "motorcycle" or "scooter" => Motorbike,
        _ => Car
    };

    /// <summary>The shaded drawing the Case page shows, as SVG content for a <see cref="ViewBox"/> canvas.</summary>
    public static string Artwork(string profile) => Drawing(profile, flat: false);

    /// <summary>The same drawing in flat colours, as the report prints it.</summary>
    public static string FlatArtwork(string profile) => Drawing(profile, flat: true);

    /// <summary>
    /// The outline of each drawing as the union of its parts (body, tyres,
    /// mirrors; for the motorbike its frame, bars and exhaust too): where a
    /// press on the plan starts a new damage.
    /// </summary>
    public static IReadOnlyList<string> HitPaths(string profile) => profile switch
    {
        Van => VanHitPaths,
        Motorbike => MotorbikeHitPaths,
        _ => CarHitPaths
    };

    /// <summary>
    /// The body box the unit plan maps onto: x 42 to 198, y 16 to 410, the
    /// same on every drawing. Core's canonical plan has this box's shape, so
    /// a disc touches the same areas here as where Core judges them.
    /// </summary>
    public const int PlanLeft = 42;
    public const int PlanTop = 16;
    public const int PlanWidth = 156;
    public const int PlanHeight = 394;

    /// <summary>The dashed band lines shown while editing: Core's bands on this plan.</summary>
    public static string GuidesPath { get; } = FormattableString.Invariant(
        $"M{PlanLeft} {PlanY(DamageAreaGeometry.FrontBand)} H{PlanLeft + PlanWidth} M{PlanLeft} {PlanY(DamageAreaGeometry.RearBand)} H{PlanLeft + PlanWidth} M{PlanX(DamageAreaGeometry.LeftBand)} {PlanTop} V{PlanTop + PlanHeight} M{PlanX(DamageAreaGeometry.RightBand)} {PlanTop} V{PlanTop + PlanHeight}");

    /// <summary>
    /// The disc a damage is judged by, in this plan's coordinates: its drawn
    /// disc, or for a damage recorded by area the disc its areas give; null
    /// for the other areas. It is drawn as its <see cref="BurstPath"/>.
    /// </summary>
    public static DamageDisc? Disc(AssessmentImpact impact)
    {
        ArgumentNullException.ThrowIfNull(impact);
        return Disc(impact.Areas, impact.Disc);
    }

    /// <summary>
    /// The same disc from a damage's areas and the disc drawn for it, which is
    /// how a report carries a damage.
    /// </summary>
    public static DamageDisc? Disc(IReadOnlyList<string> areas, DamageDisc? drawn)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var disc = DamageAreaGeometry.RenderDisc(areas, PlanWidth, PlanHeight, drawn);
        return disc is null ? null : disc with { CentreX = disc.CentreX + PlanLeft, CentreY = disc.CentreY + PlanTop };
    }

    /// <summary>
    /// The comic burst a damage is drawn as: one yellow star, outlined in
    /// black, whose spikes reach about the disc's edge. Every damage has the
    /// same burst, whatever its severity, and it carries no number; the disc
    /// alone decides the areas. The Case page's script draws the same path
    /// from <see cref="DamageBurst"/>.
    /// </summary>
    public static string BurstPath(DamageDisc disc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        var path = new StringBuilder("M");
        var vertices = DamageBurst.Points * 2;
        for (var index = 0; index < vertices; index++)
        {
            var theta = (DamageBurst.Rotation - 90) * Math.PI / 180 + index * Math.PI / DamageBurst.Points;
            var reach = index % 2 == 1 ? disc.Radius * (1 - DamageBurst.SpikeDepth) : disc.Radius;
            var wave = 1 + DamageBurst.Jitter
                * (Math.Sin(theta * 3.7 + DamageBurst.Points * .31) + Math.Cos(theta * 2.1 + DamageBurst.Rotation * .07)) * 0.35;
            if (index > 0)
            {
                path.Append(" L");
            }
            path.Append(Format(disc.CentreX + Math.Cos(theta) * reach * wave * DamageBurst.StretchX))
                .Append(' ')
                .Append(Format(disc.CentreY + Math.Sin(theta) * reach * wave * DamageBurst.StretchY));
        }
        return path.Append(" Z").ToString();
    }

    // The script's rounding (Math.round to a tenth), so the page and the
    // server write the same path.
    private static string Format(double value) =>
        (Math.Floor(value * 10 + 0.5) / 10).ToString("0.#", CultureInfo.InvariantCulture);

    private static double PlanX(double fraction) => Math.Round(PlanLeft + fraction * PlanWidth, 1);

    private static double PlanY(double fraction) => Math.Round(PlanTop + fraction * PlanHeight, 1);

    private static readonly Dictionary<string, string> Drawings = new(StringComparer.Ordinal);

    private static string Drawing(string profile, bool flat)
    {
        var name = profile is Van or Motorbike ? profile : Car;
        var resource = $"Pegasus.Core.Assessment.VehiclePlans.{name}{(flat ? ".flat" : string.Empty)}.svg";
        lock (Drawings)
        {
            if (!Drawings.TryGetValue(resource, out var markup))
            {
                using var stream = typeof(DamagePlanGeometry).Assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"The vehicle drawing {resource} is not embedded.");
                using var reader = new StreamReader(stream);
                markup = reader.ReadToEnd();
                Drawings[resource] = markup;
            }
            return markup;
        }
    }

    private static readonly string[] CarHitPaths =
    [
        "M38 91 Q38 87 42 87 H48 Q52 87 52 91 V132 Q52 136 48 136 H42 Q38 136 38 132 Z",
        "M188 91 Q188 87 192 87 H198 Q202 87 202 91 V132 Q202 136 198 136 H192 Q188 136 188 132 Z",
        "M38 317 Q38 313 42 313 H48 Q52 313 52 317 V358 Q52 362 48 362 H42 Q38 362 38 358 Z",
        "M188 317 Q188 313 192 313 H198 Q202 313 202 317 V358 Q202 362 198 362 H192 Q188 362 188 358 Z",
        "M50 144 L34 149 Q29 151 30 158 Q31 162 37 161 L50 157 Z",
        "M190 144 L206 149 Q211 151 210 158 Q209 162 203 161 L190 157 Z",
        "M82 21 Q120 11 158 21 C181 26 191 40 194 62 Q198 87 195 115 L193 154 L194 295 Q198 326 196 351 Q194 379 183 395 Q176 404 159 407 Q120 413 81 407 Q64 404 57 395 Q46 379 44 351 Q42 326 46 295 L47 154 L45 115 Q42 87 46 62 C49 40 59 26 82 21 Z"
    ];

    private static readonly string[] VanHitPaths =
    [
        "M36 67 H51 V120 H36 Z",
        "M189 67 H204 V120 H189 Z",
        "M36 320 H51 V373 H36 Z",
        "M189 320 H204 V373 H189 Z",
        "M47 100 L30 103 Q26 104 26 109 L27 120 Q29 124 34 122 L47 117 Z",
        "M193 100 L210 103 Q214 104 214 109 L213 120 Q211 124 206 122 L193 117 Z",
        "M70 19 Q120 12 170 19 Q192 23 196 44 L198 82 L197 375 Q197 399 181 407 Q120 413 59 407 Q43 399 43 375 L42 82 L44 44 Q48 23 70 19 Z"
    ];

    private static readonly string[] MotorbikeHitPaths =
    [
        "M120 18 Q108.0 18 108.0 35 V99 Q108.0 116 120 116 Q132.0 116 132.0 99 V35 Q132.0 18 120 18 Z",
        "M120 307 Q104.5 307 104.5 324 V393 Q104.5 410 120 410 Q135.5 410 135.5 393 V324 Q135.5 307 120 307 Z",
        "M103 92 L109 94 L111 143 L104 149 Z",
        "M137 92 L131 94 L129 143 L136 149 Z",
        "M96 149 L144 149 L152 224 L145 314 L132 341 L108 341 L95 314 L88 224 Z",
        "M91 204 L87 238 L94 263 L99 260 L96 230 Z",
        "M149 204 L153 238 L146 263 L141 260 L144 230 Z",
        "M120 38 Q133 38 134 56 L135 88 Q120 97 105 88 L106 56 Q107 38 120 38 Z",
        "M120 85 Q141 86 147 110 L144 140 Q120 153 96 140 L93 110 Q99 86 120 85 Z",
        "M109 130 L91 121 L69 127 L50 120 L45 126 L67 137 L91 131 L106 140 Z",
        "M131 130 L149 121 L171 127 L190 120 L195 126 L173 137 L149 131 L134 140 Z",
        "M49 118 L65 125 L60 136 L43 129 Z",
        "M191 118 L175 125 L180 136 L197 129 Z",
        "M41 93 Q35 90 32 96 L30 103 Q32 108 38 109 L53 111 L58 101 Z",
        "M199 93 Q205 90 208 96 L210 103 Q208 108 202 109 L187 111 L182 101 Z",
        "M120 157 Q143 158 148 177 Q154 205 140 231 Q120 243 100 231 Q86 205 92 177 Q97 158 120 157 Z",
        "M102 237 Q120 243 138 237 L140 274 L133 302 Q120 309 107 302 L100 274 Z",
        "M107 304 Q120 309 133 304 L143 326 L140 347 Q120 359 100 347 L97 326 Z",
        "M84 252 H98 V257 H84 Z",
        "M156 252 H142 V257 H156 Z",
        "M84 298 H98 V303 H84 Z",
        "M156 298 H142 V303 H156 Z",
        "M145 279 Q150 275 155 280 L160 333 Q161 343 155 346 L145 341 L142 289 Z",
        "M111 358 H129 V366 H111 Z"
    ];
}

/// <summary>
/// The one comic burst (the pack's comic-single recipe, in a single yellow):
/// its shape and its paint, read by the Case page and the report alike.
/// </summary>
public static class DamageBurst
{
    public const int Points = 10;
    public const double SpikeDepth = 0.46;
    public const double Rotation = 3;
    public const double StretchX = 1.18;
    public const double StretchY = 0.9;
    public const double Jitter = 0.14;

    public const string Fill = "#ffeb69";
    public const double FillOpacity = 0.96;
    public const string Line = "#1d1d1d";
    public const double LineWidth = 3.2;
}
