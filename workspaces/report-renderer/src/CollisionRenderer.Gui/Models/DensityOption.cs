using CollisionRenderer.Core;

namespace CollisionRenderer.Gui.Models;

/// <summary>
/// A named density preset shown in the density selector. Mirrors the CLI's
/// auto | normal | compact | ultra mapping onto <see cref="RenderOptions"/>.
/// </summary>
public sealed class DensityOption
{
    public DensityOption(string label, string detail, DensityFit fit, Density density)
    {
        Label = label;
        Detail = detail;
        Fit = fit;
        Density = density;
    }

    public string Label { get; }

    public string Detail { get; }

    public DensityFit Fit { get; }

    public Density Density { get; }

    public RenderOptions ToOptions() => new() { Fit = Fit, Density = Density };

    /// <summary>The four presets exposed by the CLI, in the same order, Auto first.</summary>
    public static IReadOnlyList<DensityOption> All { get; } = new[]
    {
        new DensityOption("Auto", "Best fit chosen automatically", DensityFit.Auto, Density.Normal),
        new DensityOption("Normal", "Standard spacing", DensityFit.Fixed, Density.Normal),
        new DensityOption("Compact", "Tighter spacing", DensityFit.Fixed, Density.Compact),
        new DensityOption("Ultra", "Maximum content per page", DensityFit.Fixed, Density.UltraCompact),
    };
}
